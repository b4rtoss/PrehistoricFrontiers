using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrehistoricFrontiers
{
    public enum BattleEffect { Movement, Construction, Impact, Spawn, Completion, Death }

    public sealed class BattleEffects : MonoBehaviour
    {
        public Material material;
        public Mesh particleMesh;
        public const int ParticleLimit = 128;
        public static BattleEffects Active { get; private set; }
        ParticleSystem[] systems;
        readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[ParticleLimit];
        readonly Dictionary<RTSUnit,float> nextMovement = new Dictionary<RTSUnit,float>();
        readonly Dictionary<RTSUnit,float> nextConstruction = new Dictionary<RTSUnit,float>();
        readonly int[] emitted = new int[6];
        readonly System.Random random = new System.Random(374);
        RTSSelectionController controller;
        public int Emitted(BattleEffect effect) => emitted[(int)effect];
        public int TotalEmitted { get; private set; }
        public float SimulationTime => systems == null ? 0 : systems[0].time;
        public int ParticleCount
        {
            get { int count=0;if(systems!=null)foreach(var system in systems)count+=system.particleCount;return count; }
        }
        void Awake()
        {
            controller=GetComponent<RTSSelectionController>();Active=this;
            systems=new ParticleSystem[6];
            for(int i=0;i<systems.Length;i++)
            {
                var go=new GameObject(((BattleEffect)i).ToString()+" particles");go.layer=11;go.transform.SetParent(transform,false);
                var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=system.main;main.playOnAwake=false;main.loop=true;main.duration=2;main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.useUnscaledTime=false;main.maxParticles=ParticleLimit;main.gravityModifier=i==(int)BattleEffect.Impact ? .9f:.18f;main.startSpeed=0;
                main.startRotation3D=true;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
                var size=system.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
                var color=system.colorOverLifetime;color.enabled=true;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=particleMesh;
                renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                systems[i]=system;system.Play();
            }
        }
        float Range(float min,float max) => min+(max-min)*(float)random.NextDouble();
        public static void Hit(RTSUnit unit)
        {
            if(!Active || !Active.controller || !Active.controller.CanSee(UnitTeam.Player,unit.transform.position))return;
            var direction=Vector3.ProjectOnPlane(Active.controller.sceneCamera.transform.position-unit.transform.position,Vector3.up).normalized;
            var point=unit.transform.position+Vector3.up*(unit.Agent.height*.65f)+direction*unit.stats.radius*.9f;
            Burst(BattleEffect.Impact,point,5,Mathf.Clamp(unit.stats.radius,.5f,1.2f));
        }
        public static void Burst(BattleEffect effect,Vector3 position,int count,float scale=1)
        {
            if(Active)Active.Emit(effect,position,count,scale);
        }
        public bool Emit(BattleEffect effect,Vector3 position,int count,float scale=1)
        {
            if(!isActiveAndEnabled || !material || !particleMesh || !controller || controller.IsPaused || controller.MatchOver
                || !controller.CanSee(UnitTeam.Player,position) || count<=0)return false;
            int index=(int)effect;if(index<0 || index>=systems.Length)return false;
            var system=systems[index];count=Mathf.Min(count,ParticleLimit-system.particleCount);if(count<=0)return false;
            var tint=effect==BattleEffect.Impact ? new Color(1,.76f,.3f,.9f) : effect==BattleEffect.Spawn ? new Color(.65f,.84f,1,.75f)
                : effect==BattleEffect.Completion ? new Color(.55f,.9f,.65f,.8f) : new Color(.66f,.58f,.44f,.45f);
            bool sparks=effect==BattleEffect.Impact || effect==BattleEffect.Spawn || effect==BattleEffect.Completion;
            for(int i=0;i<count;i++)
            {
                float angle=Range(0,Mathf.PI*2),speed=Range(.3f,sparks ? 1.6f:1);
                var particle=new ParticleSystem.EmitParams {
                    position=position+new Vector3(Range(-.25f,.25f),Range(.04f,.15f),Range(-.25f,.25f))*scale,
                    velocity=new Vector3(Mathf.Cos(angle)*speed,Range(.5f,sparks ? 2.2f:1.2f),Mathf.Sin(angle)*speed)*scale,
                    startLifetime=Range(.35f,sparks ? .65f:.9f),startSize=Range(.08f,sparks ? .18f:.28f)*scale,
                    startColor=tint,rotation3D=new Vector3(Range(0,360),Range(0,360),Range(0,360)) };
                system.Emit(particle,1);
            }
            emitted[index]+=count;TotalEmitted+=count;return true;
        }
        void UnitEffects(RTSUnit[] units)
        {
            foreach(var unit in units)
            {
                if(!unit || !unit.IsAvailable || !unit.Agent.enabled || !unit.Agent.isOnNavMesh)continue;
                float scale=Mathf.Clamp(unit.stats.radius,.5f,1.2f);
                if(unit.Agent.hasPath && unit.Agent.velocity.sqrMagnitude>.25f
                    && (!nextMovement.TryGetValue(unit,out var next) || Time.time>=next))
                { Burst(BattleEffect.Movement,unit.transform.position,2,scale);nextMovement[unit]=Time.time+.3f; }
                var builder=unit.GetComponent<WorkerBuilder>();
                if(builder && builder.Site && builder.State==BuilderState.Building
                    && (!nextConstruction.TryGetValue(unit,out next) || Time.time>=next))
                {
                    var point=builder.Site.Health.SurfacePoint(unit.transform.position+Vector3.up*.4f);
                    Burst(BattleEffect.Construction,point,3);nextConstruction[unit]=Time.time+.5f;
                }
            }
        }
        void Update()
        {
            bool paused=controller.IsPaused || controller.MatchOver;
            foreach(var system in systems)
            {
                if(paused) {if(!system.isPaused)system.Pause();}
                else if(!system.isPlaying)system.Play();
            }
            if(paused)return;
            UnitEffects(controller.units);UnitEffects(controller.enemies);
        }
        void LateUpdate()
        {
            if(!controller.fog || !controller.fog.Active)return;
            foreach(var system in systems)
            {
                int count=system.GetParticles(buffer);bool changed=false;
                for(int i=0;i<count;i++)if(!controller.CanSee(UnitTeam.Player,buffer[i].position)) {buffer[i].remainingLifetime=0;changed=true;}
                if(changed)system.SetParticles(buffer,count);
            }
        }
        void OnDestroy() {if(Active==this)Active=null;}
    }
}
