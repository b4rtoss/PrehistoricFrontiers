using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrehistoricFrontiers
{
    public sealed class BattleEffectsSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-battleEffectsSmoke")>=0)
                new GameObject("Battle effects regression").AddComponent<BattleEffectsSmokeTest>();
        }
        bool failed;string report="";
        void Check(bool value,string name)
        {
            string line=(value ? "PASS ":"FAIL ")+name;failed|=!value;report+=line+"\n";
            Debug.Log(line);File.WriteAllText("battleeffects-test.txt",report);
        }
        IEnumerator Until(Func<bool> condition,float seconds)
        {
            float end=Time.time+seconds;while(!condition() && Time.time<end)yield return null;
        }
        IEnumerator Start()
        {
            yield return null;Time.timeScale=3;
            var c=FindAnyObjectByType<RTSSelectionController>();SmokeTestSetup.DisableEnemyProduction(c,false);
            c.SetAutoBattle(false);c.SetAutoScout(false);c.SetWorkerProtection(false);foreach(var unit in c.units)unit.Stop();
            foreach(var unit in c.enemies){unit.Stop();unit.Agent.Warp(new Vector3(21,0,21));}
            var effects=c.GetComponent<BattleEffects>();
            Check(effects && effects.material && effects.particleMesh && effects.material.shader.isSupported,"configured particle mesh and supported material available in built player");
            if(!effects){Application.Quit(1);yield break;}
            var sun=Array.Find(FindObjectsByType<Light>(),light=>light.type==LightType.Directional);
            Check(sun && sun.shadows==LightShadows.Soft && sun.shadowNormalBias<=.15f && sun.shadowStrength>=.7f,"soft contact shadows configured on sun");
            Check(QualitySettings.shadowResolution==ShadowResolution.VeryHigh && QualitySettings.shadowDistance>=60,"shadow quality settings preserved in Windows build");
            bool cosmetic=true;
            foreach(var renderer in effects.GetComponentsInChildren<ParticleSystemRenderer>())cosmetic &= renderer.shadowCastingMode==ShadowCastingMode.Off && !renderer.GetComponent<Collider>();
            Check(cosmetic && effects.GetComponentsInChildren<ParticleSystem>().Length==6,"six particle pools add no colliders or shadow casters");
            var fighter=c.units[0];fighter.Agent.Warp(new Vector3(-6,0,1));fighter.MoveTo(new Vector3(8,0,-3));
            yield return Until(()=>effects.Emitted(BattleEffect.Movement)>0,4);fighter.Stop();fighter.Agent.Warp(new Vector3(-5,0,-7));
            Check(effects.Emitted(BattleEffect.Movement)>0,"actual navigation produces footstep dust");
            int hits=effects.Emitted(BattleEffect.Impact);float health=fighter.GetComponent<UnitHealth>().CurrentHealth;fighter.GetComponent<UnitHealth>().TakeDamage(5,UnitTeam.Enemy);
            Check(effects.Emitted(BattleEffect.Impact)>hits && fighter.GetComponent<UnitHealth>().CurrentHealth==health-5,"hostile damage produces impact particles without changing damage value");
            var worker=Array.Find(c.units,u=>u.stats.isWorker);worker.Agent.Warp(new Vector3(-6,0,1));
            var site=c.construction.Place(worker,StructureKind.Clinic,new Vector3(-6,0,6));
            Check(site,"real clinic construction starts / "+c.construction.Message);if(!site){Application.Quit(1);yield break;}
            yield return Until(()=>site.Progress>.12f && effects.Emitted(BattleEffect.Construction)>0,9);
            Check(effects.Emitted(BattleEffect.Construction)>0,"working builder produces construction dust");
            hits=effects.Emitted(BattleEffect.Impact);
            var attacker=c.enemies[0];attacker.Agent.Warp(new Vector3(-6,0,11));attacker.GetComponent<UnitCombat>().Attack(site.Health);
            yield return Until(()=>site.Health.CurrentHealth<site.Health.maxHealth,5);attacker.Stop();
            Check(site.Health.CurrentHealth<site.Health.maxHealth && effects.Emitted(BattleEffect.Impact)>hits,"actual melee attack produces particles on building contact");
            int count=c.units.Length;c.construction.main.TryQueue(3);
            yield return Until(()=>c.units.Length>count,8);
            Check(c.units.Length==count+1 && effects.Emitted(BattleEffect.Spawn)>0,"normally trained worker produces spawn effect");
            yield return Until(()=>site.Complete,18);
            Check(site.Complete && effects.Emitted(BattleEffect.Completion)>0,"real building completion produces completion effect");
            int deaths=effects.Emitted(BattleEffect.Death);c.units[c.units.Length-1].GetComponent<UnitHealth>().TakeDamage(10000);
            Check(effects.Emitted(BattleEffect.Death)>deaths,"unit death produces dust after visual unit disappears");
            var camera=c.sceneCamera;camera.GetComponent<RTSCameraController>().enabled=false;
            camera.transform.position=new Vector3(-7,16,-19);camera.transform.LookAt(new Vector3(-7,0,-3));camera.orthographicSize=10;
            for(int i=0;i<6;i++)effects.Emit((BattleEffect)i,new Vector3(-11+(i%3)*4,.2f,-5+(i/3)*4),12,1.5f);
            yield return new WaitForSeconds(.15f);
            var target=new RenderTexture(1280,800,24);var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);
            var previous=camera.targetTexture;var active=RenderTexture.active;
            try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();File.WriteAllBytes("effects-preview.png",pixels.EncodeToPNG());}
            finally {camera.targetTexture=previous;RenderTexture.active=active;target.Release();Destroy(target);Destroy(pixels);}
            Check(File.Exists("effects-preview.png"),"rendered shadow and particle preview captured");
            c.SetPaused(true);yield return null;float time=effects.SimulationTime;int emitted=effects.TotalEmitted;
            Check(!effects.Emit(BattleEffect.Spawn,worker.transform.position,5),"pause rejects new effects");
            yield return new WaitForSecondsRealtime(.25f);
            Check(effects.SimulationTime==time && effects.TotalEmitted==emitted,"pause freezes all particle simulation and emissions");
            c.SetPaused(false);yield return new WaitForSeconds(.2f);
            Check(effects.SimulationTime!=time,"particle simulation resumes after pause");
            c.fog.SetFogEnabled(true);c.fog.ResetExploration();c.fog.RefreshNow();
            emitted=effects.TotalEmitted;
            Check(!c.CanSee(UnitTeam.Player,new Vector3(-24,0,24)) && !effects.Emit(BattleEffect.Impact,new Vector3(-24,0,24),5)
                && effects.TotalEmitted==emitted,"hidden events do not reveal themselves through particles");
            c.fog.SetFogEnabled(false);effects.Emit(BattleEffect.Impact,worker.transform.position,10000);
            bool bounded=true;foreach(var system in effects.GetComponentsInChildren<ParticleSystem>())bounded &= system.particleCount<=BattleEffects.ParticleLimit;
            Check(bounded,"particle pools stay bounded under excessive emission requests");
            c.enemyBase.TakeDamage(10000,UnitTeam.Player);yield return null;emitted=effects.TotalEmitted;
            Check(!effects.Emit(BattleEffect.Spawn,worker.transform.position,5),"finished match rejects new effects");
            yield return new WaitForSecondsRealtime(.2f);
            Check(effects.TotalEmitted==emitted,"match completion stops automatic movement and construction emissions");
            Application.Quit(failed ? 1:0);
        }
    }
}
