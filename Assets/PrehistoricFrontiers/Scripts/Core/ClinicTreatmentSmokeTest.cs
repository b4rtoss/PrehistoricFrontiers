using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace PrehistoricFrontiers
{
    public sealed class ClinicTreatmentSmokeTest:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() {if(Array.IndexOf(Environment.GetCommandLineArgs(),"-clinicTreatmentSmoke")>=0) new GameObject("Clinic treatment regression").AddComponent<ClinicTreatmentSmokeTest>();}
        string report="";bool failed;
        void Check(bool value,string name) {var line=(value ? "PASS ":"FAIL ")+name;report+=line+"\n";failed|=!value;Debug.Log(line);File.WriteAllText("clinictreatment-test.txt",report);}
        IEnumerator Until(Func<bool> condition,float seconds) {float end=Time.time+seconds;while(!condition() && Time.time<end) yield return null;}
        IEnumerator Start()
        {
            yield return null;Time.timeScale=4;
            var c=FindAnyObjectByType<RTSSelectionController>();SmokeTestSetup.DisableEnemyProduction(c,false);c.SetAutoBattle(false);c.SetAutoScout(false);c.SetWorkerProtection(false);
            foreach(var u in c.units) u.Stop();foreach(var e in c.enemies) {e.Stop();e.Agent.Warp(new Vector3(21,0,21));}
            var worker=Array.Find(c.units,u=>u.stats.isWorker);worker.Agent.Warp(new Vector3(-6,0,1));
            var site=c.construction.Place(worker,StructureKind.Clinic,new Vector3(-6,0,6));
            Check(site && !site.GetComponent<HealingClinic>(),"unfinished clinic cannot accept treatment");
            if(!site) {Application.Quit(1);yield break;}
            yield return Until(()=>site.Complete,18);var clinic=site.GetComponent<HealingClinic>();
            Check(clinic && site.Complete,"real construction creates treatment clinic");
            if(!clinic) {Application.Quit(1);yield break;}
            worker.Stop();worker.Agent.Warp(new Vector3(20,0,-20));
            var u0=c.units[0];var u1=c.units[1];var u2=c.units[2];
            u0.Agent.Warp(new Vector3(-9.7f,0,6));u1.Agent.Warp(new Vector3(-6,0,2.3f));u2.Agent.Warp(new Vector3(-2.2f,0,6));
            var h=u0.GetComponent<UnitHealth>();h.TakeDamage(40);float hp=h.CurrentHealth;yield return new WaitForSeconds(5.5f);
            Check(h.CurrentHealth==hp,"standing near clinic no longer heals without a command");
            Check(!clinic.Request(u1) && !clinic.Request(c.enemies[0]),"healthy units and enemy units rejected");
            u1.GetComponent<UnitHealth>().TakeDamage(40);u2.GetComponent<UnitHealth>().TakeDamage(40);
            c.Clear();c.Select(u0,true);c.Select(u1,true);c.Select(u2,true);int alive=c.AlliesAlive;
            Check(c.IssueTreat(clinic)==3 && clinic.ApproachingCount==3,"group clinic command accepted for all wounded units");
            Check(c.IssueTreat(clinic)==3 && clinic.ApproachingCount==3,"repeated clinic click does not duplicate patients");
            yield return Until(()=>u0.IsInClinic && u1.IsInClinic && u2.IsInClinic,12);
            Check(u0.IsInClinic && u1.IsInClinic && u2.IsInClinic && clinic.InsideCount==3,"all three species enter the clinic");
            if(!u0.IsInClinic) {Application.Quit(1);yield break;}
            bool hidden=true,collisionFree=true;foreach(var r in u0.GetComponentsInChildren<Renderer>(true)) hidden&=!r.enabled;foreach(var col in u0.GetComponentsInChildren<Collider>(true)) collisionFree&=!col.enabled;
            Check(hidden && collisionFree && !u0.Agent.enabled,"inside unit disappears and releases physical navigation space");
            Check(u0.IsAlive && c.AlliesAlive==alive && !u0.IsSelected && c.Selected.Count==0,"inside patients stay alive and count toward population but leave selection");
            Check(!c.GetComponent<BattleMinimap>().Shows(u0) && !u0.IsAvailable,"inside patient is absent from minimap and commandable world");
            Check(!u0.MoveTo(new Vector3(-18,0,8)) && !c.enemies[0].GetComponent<UnitCombat>().Attack(u0),"inside unit cannot move or be targeted");
            hp=h.CurrentHealth;h.TakeDamage(10,UnitTeam.Enemy);Check(h.CurrentHealth==hp,"inside patient protected from direct hostile damage");
            c.SetPaused(true);hp=h.CurrentHealth;yield return new WaitForSecondsRealtime(.2f);Check(h.CurrentHealth==hp && u0.IsInClinic,"pause freezes treatment without ejecting patient");c.SetPaused(false);
            yield return Until(()=>!u0.IsInClinic && !u1.IsInClinic && !u2.IsInClinic,15);
            Check(!u0.IsInClinic && !u1.IsInClinic && !u2.IsInClinic && h.CurrentHealth==h.MaxHealth && u1.GetComponent<UnitHealth>().CurrentHealth==u1.stats.maxHealth && u2.GetComponent<UnitHealth>().CurrentHealth==u2.stats.maxHealth,"all admitted units return with full health");
            Check(u0.Agent.enabled && u0.Agent.isOnNavMesh && u0.transform.Find("Visual").GetComponent<Renderer>().enabled==false && u0.GetComponentInChildren<RaptorMotion>().torso.GetComponent<Renderer>().enabled,"exit restores navigation and art without reviving old capsule graphics");
            Check(clinic.InsideCount==0 && clinic.ApproachingCount==0 && c.AlliesAlive==alive,"completed patients removed without changing army population");
            h.TakeDamage(8,UnitTeam.Enemy);u0.Agent.Warp(new Vector3(-9.7f,0,6));clinic.Request(u0);hp=h.CurrentHealth;
            yield return new WaitForSeconds(.8f);Check(!u0.IsInClinic && h.CurrentHealth==hp,"recent hostile damage delays admission and healing");
            yield return Until(()=>u0.IsInClinic,7);Check(u0.IsInClinic && Time.time-h.LastHostileDamageAt>=HealingClinic.CombatDelay,"patient enters after combat recovery delay");
            yield return Until(()=>!u0.IsInClinic,4);
            h.TakeDamage(12);u0.Agent.Warp(new Vector3(-18,0,-18));Check(clinic.Request(u0),"distant wounded unit can receive clinic order");
            Check(u0.MoveTo(new Vector3(-17,0,-18)) && !u0.IsOnClinicOrder && clinic.ApproachingCount==0,"manual movement cancels approach to clinic");u0.Stop();
            u0.Agent.Warp(new Vector3(-9.7f,0,6));Check(clinic.Request(u0),"unit can return after cancelling treatment approach");yield return Until(()=>u0.IsInClinic,8);
            var blocker=new GameObject("Blocked clinic exits");blocker.layer=9;var box=blocker.AddComponent<BoxCollider>();box.center=new Vector3(-6,.6f,6);box.size=new Vector3(40,2,40);Physics.SyncTransforms();
            yield return Until(()=>h.CurrentHealth==h.MaxHealth,6);yield return null;
            Check(u0.IsInClinic && u0.Treatment.WaitingForExit,"fully healed unit waits safely if all exits are blocked");
            Destroy(blocker);yield return null;yield return Until(()=>!u0.IsInClinic,3);
            Check(!u0.IsInClinic && u0.Agent.isOnNavMesh,"unit exits when navigation space becomes available");
            var gatherer=worker.GetComponent<WorkerGatherer>();gatherer.autoGatherEnabled=false;
            var node=Array.Find(FindObjectsByType<ResourceNode>(),n=>n.Remaining>0);Check(gatherer.Gather(node),"worker receives real resource gathering order before treatment");yield return Until(()=>gatherer.Carried>0,30);worker.Stop();int cargo=gatherer.Carried;
            worker.Agent.Warp(new Vector3(-9.5f,0,6));worker.GetComponent<UnitHealth>().TakeDamage(12);Check(cargo>0 && clinic.Request(worker),"wounded worker with cargo can enter treatment");
            yield return Until(()=>worker.IsInClinic,8);Check(worker.IsInClinic && gatherer.Carried==cargo,"hidden worker retains carried resources");yield return Until(()=>!worker.IsInClinic,8);Check(!worker.IsInClinic && gatherer.Carried==cargo && worker.GetComponent<UnitHealth>().CurrentHealth==worker.stats.maxHealth,"worker returns healed with the same cargo");
            h.TakeDamage(40);u0.Agent.Warp(new Vector3(-9.7f,0,6));clinic.Request(u0);yield return Until(()=>u0.IsInClinic,8);hp=h.CurrentHealth;
            u1.GetComponent<UnitHealth>().TakeDamage(8);u1.Agent.Warp(new Vector3(-18,0,-18));clinic.Request(u1);
            site.Health.TakeDamage(100000);yield return Until(()=>!u0.IsInClinic,3);
            Check(u0.IsAlive && !u0.IsInClinic && u0.Agent.isOnNavMesh && h.CurrentHealth<=hp+.1f && h.CurrentHealth<h.MaxHealth,"clinic destruction evacuates patient with partial restored health");
            Check(!clinic.Request(u0) && !c.MatchOver,"destroyed clinic rejects patients without ending the match");
            Check(!u1.IsOnClinicOrder && !u1.Agent.hasPath,"clinic destruction cancels patients still approaching the building");
            worker.Stop();worker.Agent.Warp(new Vector3(-6,0,1));
            yield return Until(()=>c.construction.CanPlace(worker,StructureKind.Clinic,new Vector3(-6,0,6),out _,out _),5);
            var second=c.construction.Place(worker,StructureKind.Clinic,new Vector3(-6,0,6));
            Check(second,"clinic can be rebuilt after evacuation");
            if(second)
            {
                yield return Until(()=>second.Complete,18);worker.Stop();worker.Agent.Warp(new Vector3(20,0,-20));
                u0.Agent.Warp(new Vector3(-9.7f,0,6));var secondClinic=second.GetComponent<HealingClinic>();secondClinic.Request(u0);yield return Until(()=>u0.IsInClinic,8);hp=h.CurrentHealth;
                c.enemyBase.TakeDamage(100000);yield return null;yield return null;
                Check(c.MatchOver && !u0.IsInClinic && u0.Agent.isOnNavMesh && h.CurrentHealth<=hp+.1f,"match completion evacuates patient and stops further healing");
                Check(c.AlliesAlive==alive,"treatment and evacuation never change army population");
            }
            Application.Quit(failed ? 1:0);
        }
    }
}
