using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class MainBaseArtSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() {if(Array.IndexOf(Environment.GetCommandLineArgs(),"-mainBaseArtSmoke")>=0) new GameObject("Main base art regression").AddComponent<MainBaseArtSmokeTest>();}
        string report="";bool failed;
        void Check(bool value,string name) {var line=(value ? "PASS ":"FAIL ")+name;report+=line+"\n";failed|=!value;Debug.Log(line);File.WriteAllText("mainbaseart-test.txt",report);}
        IEnumerator Until(Func<bool> condition,float seconds) {float deadline=Time.time+seconds;while(!condition() && Time.time<deadline) yield return null;}
        IEnumerator Start()
        {
            yield return null;Time.timeScale=4;
            var controller=FindAnyObjectByType<RTSSelectionController>();SmokeTestSetup.DisableEnemyProduction(controller,false);controller.SetAutoBattle(false);controller.SetAutoScout(false);
            foreach(var unit in controller.units) unit.Stop();
            var own=controller.playerBase;var enemy=controller.enemyBase;var playerArt=own.GetComponentInChildren<MainBaseVisual>();var enemyArt=enemy.GetComponentInChildren<MainBaseVisual>();
            Check(playerArt && enemyArt && !playerArt.enemy && enemyArt.enemy,"first reference assigned to player and second to enemy base");
            if(!playerArt || !enemyArt) {Application.Quit(1);yield break;}
            Check(own.IsAlive && enemy.IsAlive && own.CurrentHealth==800 && enemy.CurrentHealth==800,"both bases retain original 800 health");
            Check(own.transform.position==new Vector3(-10,0,-10) && enemy.transform.position==new Vector3(10,0,4),"base positions unchanged");
            foreach(var health in new[] {own,enemy})
            {
                var art=health.GetComponentInChildren<MainBaseVisual>();string team=health==own ? "player":"enemy";
                Check(health.GetComponentsInChildren<Collider>(true).Length==1 && art.GetComponentsInChildren<Collider>(true).Length==0 && health.Body.size==new Vector3(4.5f,2.9f,3.5f) && health.Body.center==new Vector3(0,1.45f,0),team+" retains original single base collider");
                bool hiddenOld=true;foreach(string name in new[] {"Stone foundation","Longhouse","Thatched roof"}) {var old=health.transform.Find(name);if(old) foreach(var renderer in old.GetComponentsInChildren<Renderer>(true)) hiddenOld&=!renderer.enabled;}
                Check(hiddenOld,team+" old cube models disabled");
                Check(Physics.Raycast(health.transform.position+Vector3.up*9,Vector3.down,out var hit,12,1<<13) && hit.collider==health.Body,team+" selection ray hits original base collider");
                int triangles=0;bool valid=true;var bounds=new Bounds(Vector3.zero,Vector3.zero);
                foreach(var filter in art.GetComponentsInChildren<MeshFilter>()) {var mesh=filter.sharedMesh;triangles+=mesh.triangles.Length/3;valid&=mesh.normals.Length==mesh.vertexCount && mesh.vertexCount>0;foreach(var p in mesh.vertices) bounds.Encapsulate(art.transform.InverseTransformPoint(filter.transform.TransformPoint(p)));}
                Check(valid && triangles>1000 && triangles<20000,team+" valid low poly geometry / triangles "+triangles);
                Check(bounds.min.y>=-.03f && bounds.max.y<3.30f && Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x))<2.27f && Mathf.Max(Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z))<1.77f,team+" model fits original footprint and health indicator height");
            }
            var ownRoof=playerArt.transform.Find("Hide roofs").GetComponent<MeshRenderer>().sharedMaterials[3].color;
            var enemyRoof=enemyArt.transform.Find("Hide roofs").GetComponent<MeshRenderer>().sharedMaterials[3].color;
            Check(ownRoof.r>enemyRoof.r+.2f && ownRoof.g>enemyRoof.g+.1f,"orange player and burgundy enemy roof palettes retained");
            var production=own.GetComponent<ProductionBuilding>();var enemyProduction=enemy.GetComponent<ProductionBuilding>();
            Check(production.workersOnly && production.unitPrefabs.Length==4 && enemyProduction.unitPrefabs.Length==4 && enemyProduction.workersOnly && enemy.GetComponent<EnemyProductionAI>(),"production roles and enemy AI components preserved");
            Check(production.depositPoint==new Vector3(-10,0,-13.8f) && enemyProduction.rallyPoint==new Vector3(10,0,0),"resource deposit point and enemy rally point unchanged");
            controller.fog.SetFogEnabled(true);controller.fog.ResetExploration();enemy.GetComponent<FogHiddenVisual>().ApplyNow();
            bool hidden=true;foreach(var renderer in enemyArt.GetComponentsInChildren<Renderer>()) hidden&=renderer.forceRenderingOff;
            Check(hidden && !controller.CanSee(UnitTeam.Player,enemy.transform.position),"all new enemy base pieces hidden outside allied vision");
            var scout=Array.Find(controller.units,u=>!u.stats.isWorker);scout.Agent.Warp(new Vector3(7,0,1));controller.fog.RefreshNow();enemy.GetComponent<FogHiddenVisual>().ApplyNow();
            bool visible=true;foreach(var renderer in enemyArt.GetComponentsInChildren<Renderer>()) visible&=!renderer.forceRenderingOff;
            Check(visible && controller.CanSee(UnitTeam.Player,enemy.transform.position),"complete enemy base model appears after discovery");
            controller.fog.SetFogEnabled(false);enemy.GetComponent<FogHiddenVisual>().ApplyNow();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-mainBaseCapture")>=0)
            {
                var camera=controller.sceneCamera;var controls=camera.GetComponent<RTSCameraController>();if(controls) controls.enabled=false;
                foreach(var health in new[] {own,enemy})
                {
                    camera.transform.position=health.transform.position+new Vector3(-7,8,-10);camera.transform.LookAt(health.transform.position+Vector3.up*1.05f);camera.orthographicSize=5.7f;yield return null;
                    var previous=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
                    try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes(Path.GetFullPath(health==own ? "player-base-game.png":"enemy-base-game.png"),pixels.EncodeToPNG());}
                    finally {camera.targetTexture=previous;RenderTexture.active=active;target.Release();Destroy(target);Destroy(pixels);}
                }
            }
            int count=controller.units.Length;Check(production.TryQueue(3),"player base still accepts worker training");yield return Until(()=>controller.units.Length>count,8);
            var worker=controller.units[controller.units.Length-1];Check(controller.units.Length==count+1 && worker.stats.isWorker && worker.GetComponentInChildren<WorkerMotion>() && worker.GetComponent<WorkerGatherer>().home==production && worker.Agent.isOnNavMesh,"player base produces model worker with correct resource home");
            count=controller.enemies.Length;Check(enemyProduction.TryQueue(3),"enemy base accepts worker training");yield return Until(()=>controller.enemies.Length>count,8);
            var reinforcement=controller.enemies[controller.enemies.Length-1];Check(controller.enemies.Length==count+1 && reinforcement.team==UnitTeam.Enemy && reinforcement.GetComponentInChildren<WorkerMotion>() && reinforcement.Agent.isOnNavMesh,"enemy base produces an enemy model worker on navmesh");
            var pose=playerArt.banners[0].localRotation;yield return new WaitForSeconds(.17f);Check(Quaternion.Angle(pose,playerArt.banners[0].localRotation)>.01f,"living base banners move");
            controller.SetPaused(true);pose=playerArt.banners[0].localRotation;yield return new WaitForSecondsRealtime(.2f);Check(Quaternion.Angle(pose,playerArt.banners[0].localRotation)<.01f,"pause freezes base banners");
            controller.SetPaused(false);enemy.TakeDamage(100000);yield return null;pose=playerArt.banners[0].localRotation;yield return new WaitForSecondsRealtime(.2f);
            Check(!enemy.IsAlive && !enemyArt.gameObject.activeInHierarchy && own.IsAlive && controller.Status.Contains("VICTORY"),"destroying enemy base hides complete model and preserves victory");
            Check(Quaternion.Angle(pose,playerArt.banners[0].localRotation)<.01f,"winning base animation stops at match completion");
            Application.Quit(failed ? 1:0);
        }
    }
}
