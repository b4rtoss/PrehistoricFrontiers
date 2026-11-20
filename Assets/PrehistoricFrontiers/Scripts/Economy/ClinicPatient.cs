using UnityEngine;
using UnityEngine.AI;
namespace PrehistoricFrontiers
{
    [RequireComponent(typeof(RTSUnit))]
    public sealed class ClinicPatient : MonoBehaviour
    {
        public HealingClinic Clinic {get;private set;}
        public bool Inside {get;private set;}
        public bool WaitingForExit {get;private set;}
        public string Status=>Inside ? WaitingForExit ? "Лечение / Ждёт свободного выхода":"Лечение / Внутри лечебницы":"Лечение / Подходит ко входу";
        RTSUnit unit;RTSSelectionController controller;
        Vector3 approach,center,entry;
        Renderer[] renderers;Collider[] colliders;
        bool[] rendererStates,colliderStates;
        float nextRetry;
        void Awake() {unit=GetComponent<RTSUnit>();unit.Treatment=this;}
        internal void Assign(HealingClinic clinic,Vector3 destination)
        {Clinic=clinic;controller=clinic.controller;center=clinic.transform.position;approach=destination;WaitingForExit=false;nextRetry=Time.time+2;}
        void Detach() {if(Clinic) Clinic.Forget(this);Clinic=null;}
        public void Cancel()
        {
            Detach();if(!Inside) return;WaitingForExit=true;
            if(!unit.IsAlive) {Restore();Inside=false;WaitingForExit=false;return;}
            TryExit();
        }
        internal void ClinicClosed()
        {
            if(Inside) {Cancel();return;}
            Detach();
            if(unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh) unit.Agent.ResetPath();
            unit.GetComponent<UnitCombat>()?.Hold();
        }
        void Enter()
        {
            entry=transform.position;Inside=true;unit.SetSelected(false);controller.Selected.Remove(unit);
            unit.GetComponent<UnitCombat>()?.Hold();unit.Agent.ResetPath();unit.Agent.enabled=false;
            renderers=GetComponentsInChildren<Renderer>(true);rendererStates=new bool[renderers.Length];
            for(int i=0;i<renderers.Length;i++) {rendererStates[i]=renderers[i].enabled;renderers[i].enabled=false;}
            colliders=GetComponentsInChildren<Collider>(true);colliderStates=new bool[colliders.Length];
            for(int i=0;i<colliders.Length;i++) {colliderStates[i]=colliders[i].enabled;colliders[i].enabled=false;}
            Physics.SyncTransforms();
        }
        void Restore()
        {
            if(renderers!=null) for(int i=0;i<renderers.Length;i++) if(renderers[i]) renderers[i].enabled=rendererStates[i];
            if(colliders!=null) for(int i=0;i<colliders.Length;i++) if(colliders[i]) colliders[i].enabled=colliderStates[i];
            renderers=null;colliders=null;
        }
        bool Free(Vector3 point,out Vector3 result)
        {
            result=point;if(!NavMesh.SamplePosition(point,out var hit,1,unit.Agent.areaMask)) return false;
            if(Physics.CheckSphere(hit.position+Vector3.up*.6f,unit.Agent.radius+.15f,(1<<9)|(1<<10)|(1<<12)|(1<<13))) return false;
            result=hit.position;return true;
        }
        void TryExit()
        {
            if(!Inside || !unit.IsAlive) return;
            bool found=Free(entry,out var exit);
            for(int ring=0;!found && ring<6;ring++) for(int i=0;!found && i<24;i++)
            {float a=i*Mathf.PI/12;found=Free(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(3+unit.Agent.radius+ring*1.8f),out exit);}
            if(!found) return;
            unit.Agent.enabled=true;if(!unit.Agent.Warp(exit)) {unit.Agent.enabled=false;return;}
            Restore();Inside=false;WaitingForExit=false;Detach();unit.GetComponent<UnitCombat>()?.Hold();unit.Agent.ResetPath();unit.Agent.velocity=Vector3.zero;Physics.SyncTransforms();
        }
        void Update()
        {
            if(!Inside && !Clinic) return;
            if(!unit.IsAlive) {Cancel();return;}
            if(controller && controller.IsPaused) return;
            if(controller && controller.MatchOver || !Clinic || !Clinic.Health.IsAlive || !Clinic.isActiveAndEnabled)
            {if(Inside) {Detach();WaitingForExit=true;TryExit();}else Detach();return;}
            if(Inside)
            {
                var health=GetComponent<UnitHealth>();if(!WaitingForExit) health.Heal(HealingClinic.Rate*Time.deltaTime);
                if(health.CurrentHealth>=health.MaxHealth) WaitingForExit=true;
                if(WaitingForExit) TryExit();return;
            }
            if(!unit.Agent || !unit.Agent.isOnNavMesh) {Detach();return;}
            bool arrived=(transform.position-approach).sqrMagnitude<.55f*.55f;
            if(arrived)
            {
                unit.Agent.ResetPath();var combat=GetComponent<UnitCombat>();float last=Mathf.Max(GetComponent<UnitHealth>().LastHostileDamageAt,combat ? combat.LastAttackAt:-100);
                if(Time.time-last>=HealingClinic.CombatDelay) Enter();
            }
            else if(Time.time>=nextRetry) {nextRetry=Time.time+2;if(!unit.NavigateTo(approach)) Cancel();}
        }
        void OnDisable() {Detach();if(Inside) {Restore();Inside=false;WaitingForExit=false;}}
        void OnDestroy() {if(unit && unit.Treatment==this) unit.Treatment=null;}
    }
}
