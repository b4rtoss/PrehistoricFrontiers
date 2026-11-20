using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public sealed class ClinicArtSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() {if(Array.IndexOf(Environment.GetCommandLineArgs(),"-clinicArtSmoke")>=0) new GameObject("Clinic art regression").AddComponent<ClinicArtSmokeTest>();}
        string report="";bool failed;
        void Check(bool value,string name) {var line=(value ? "PASS ":"FAIL ")+name;report+=line+"\n";failed|=!value;Debug.Log(line);File.WriteAllText("clinicart-test.txt",report);}
        IEnumerator Until(Func<bool> condition,float seconds) {float deadline=Time.time+seconds;while(!condition() && Time.time<deadline) yield return null;}
        IEnumerator Start()
        {
            yield return null;Time.timeScale=4;
            var controller=FindAnyObjectByType<RTSSelectionController>();SmokeTestSetup.DisableEnemyProduction(controller,false);controller.SetAutoBattle(false);controller.SetAutoScout(false);
            foreach(var unit in controller.units) unit.Stop();
            foreach(var enemy in controller.enemies) {enemy.Stop();enemy.Agent.Warp(new Vector3(21,0,21));}
            var system=controller.construction;Check(system.clinicModelPrefab && system.clinicModelPrefab.GetComponent<ClinicVisual>(),"saved scene explicitly references the clinic model prefab");
            var worker=Array.Find(controller.units,u=>u.stats.isWorker);worker.Agent.Warp(new Vector3(-6,0,1));
            int food=system.main.wallet.Food,wood=system.main.wallet.Wood;
            var site=system.Place(worker,StructureKind.Clinic,new Vector3(-6,0,6));
            Check(site && system.main.wallet.Food==food-100 && system.main.wallet.Wood==wood-120,"clinic placement uses original cost / "+system.Message);
            if(!site) {Application.Quit(1);yield break;}
            var art=site.GetComponentInChildren<ClinicVisual>();Check(art,"newly placed clinic uses the pond model");
            if(!art) {Application.Quit(1);yield break;}
            Check(site.GetComponentsInChildren<Collider>(true).Length==1 && art.GetComponentsInChildren<Collider>(true).Length==0,"decorative rocks trees and pond add no collision obstacles");
            Check(site.Health.maxHealth==400 && site.Health.Body.size==new Vector3(4,2.6f,4) && site.GetComponent<NavMeshObstacle>().size==new Vector3(4,2.6f,4),"original clinic health hitbox and navigation footprint retained");
            Check(Physics.Raycast(site.transform.position+Vector3.up*8,Vector3.down,out var hit,10,1<<13) && hit.collider==site.Health.Body,"selection and attack ray hits the original building collider");
            int triangles=0;bool valid=true;var bounds=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var filter in art.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;triangles+=mesh.triangles.Length/3;valid&=mesh.normals.Length==mesh.vertexCount && mesh.vertexCount>0;
                foreach(var p in mesh.vertices) bounds.Encapsulate(art.transform.InverseTransformPoint(filter.transform.TransformPoint(p)));
            }
            Check(valid && triangles>1000 && triangles<10000,"valid low poly clinic geometry / triangles "+triangles);
            Check(bounds.min.y>=-.03f && bounds.max.y<2.9f && Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x),Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z))<2.2f,"pond and trees fit the existing building visual footprint");
            Check(!site.GetComponent<HealingClinic>() && !art.Operational,"unfinished clinic has no healing and no running waterfall");
            yield return Until(()=>site.Progress>.1f,8);
            Check(site.Progress>.1f && site.Progress<1 && Mathf.Abs(art.transform.localScale.y-(.18f+site.Progress*.82f))<.001f,"unfinished pond follows real construction progress");
            var flow=art.droplets[0].localPosition;yield return new WaitForSeconds(.15f);
            Check(art.droplets[0].localPosition==flow && !art.Operational,"water effects remain stopped while building");
            yield return Until(()=>site.Complete,16);
            Check(site.Complete && site.GetComponent<HealingClinic>() && art.Operational && art.transform.localScale==Vector3.one,"completed construction enables original healing and water effects");
            Check(!site.transform.Find("Roof") && !site.transform.Find("Medical cross horizontal") && !site.transform.Find("Building"),"completed pond has no old cube roof or medical cross");
            Check(HealingClinic.Rate==4 && HealingClinic.CombatDelay==5,"treatment rate and combat delay unchanged");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-clinicCapture")>=0)
            {
                var cameraController=controller.sceneCamera.GetComponent<RTSCameraController>();if(cameraController) cameraController.enabled=false;
                controller.Clear();controller.sceneCamera.transform.position=site.transform.position+new Vector3(7,7.8f,10);
                controller.sceneCamera.transform.LookAt(site.transform.position+Vector3.up*.7f);controller.sceneCamera.orthographicSize=6;
                yield return null;
                // Explicit rendering also works when the test player has a hidden window.
                var camera=controller.sceneCamera;var previous=camera.targetTexture;var active=RenderTexture.active;
                var target=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
                try
                {
                    camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.GetFullPath("clinic-game.png"),pixels.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture=previous;RenderTexture.active=active;target.Release();Destroy(target);Destroy(pixels);
                }
            }
            var ally=Array.Find(controller.units,u=>!u.stats.isWorker);ally.Agent.Warp(new Vector3(-2,0,6));ally.Stop();
            var health=ally.GetComponent<UnitHealth>();health.TakeDamage(20,UnitTeam.Enemy);float hp=health.CurrentHealth;
            Check(site.GetComponent<HealingClinic>().Request(ally),"wounded ally accepts a clinic admission order");
            yield return new WaitForSeconds(HealingClinic.CombatDelay+2);
            Check(health.CurrentHealth>hp,"finished pond actually heals an admitted injured ally");
            flow=art.droplets[0].localPosition;yield return new WaitForSeconds(.17f);
            Check(art.droplets[0].localPosition!=flow,"completed waterfall has moving flow streaks");
            controller.SetPaused(true);flow=art.droplets[0].localPosition;var ring=art.ripples[0].localScale;hp=health.CurrentHealth;
            yield return new WaitForSecondsRealtime(.2f);
            Check(art.droplets[0].localPosition==flow && art.ripples[0].localScale==ring && health.CurrentHealth==hp,"pause freezes water ripples and healing");
            controller.SetPaused(false);controller.enemyBase.TakeDamage(100000);yield return null;
            flow=art.droplets[0].localPosition;yield return new WaitForSecondsRealtime(.2f);
            Check(controller.MatchOver && art.droplets[0].localPosition==flow,"match completion freezes waterfall animation");
            site.Health.TakeDamage(100000);yield return null;
            Check(!site.Health.IsAlive && !art.gameObject.activeInHierarchy,"destroying the clinic removes the complete pond model");
            Application.Quit(failed ? 1:0);
        }
    }
}
