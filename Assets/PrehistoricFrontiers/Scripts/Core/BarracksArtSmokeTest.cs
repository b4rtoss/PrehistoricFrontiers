using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public sealed class BarracksArtSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() {if(Array.IndexOf(Environment.GetCommandLineArgs(),"-barracksArtSmoke")>=0) new GameObject("Barracks art regression").AddComponent<BarracksArtSmokeTest>();}
        string report="";bool failed;
        void Check(bool value,string name) {var line=(value ? "PASS ":"FAIL ")+name;report+=line+"\n";failed|=!value;Debug.Log(line);File.WriteAllText("barracksart-test.txt",report);}
        IEnumerator Until(Func<bool> condition,float seconds) {float deadline=Time.time+seconds;while(!condition() && Time.time<deadline) yield return null;}
        IEnumerator Start()
        {
            yield return null;Time.timeScale=4;
            var controller=FindAnyObjectByType<RTSSelectionController>();SmokeTestSetup.DisableEnemyProduction(controller,false);controller.SetAutoBattle(false);controller.SetAutoScout(false);
            foreach(var unit in controller.units) unit.Stop();foreach(var enemy in controller.enemies) {enemy.Stop();enemy.Agent.Warp(new Vector3(21,0,21));}
            var system=controller.construction;
            Check(system.barracksModelPrefab && system.barracksModelPrefab.GetComponent<BarracksVisual>(),"saved scene references the barracks art prefab");
            Check(system.clinicModelPrefab && system.clinicModelPrefab.GetComponent<ClinicVisual>(),"previous clinic model reference retained");
            var worker=Array.Find(controller.units,u=>u.stats.isWorker);worker.Agent.Warp(new Vector3(-6,0,1));
            int food=system.main.wallet.Food,wood=system.main.wallet.Wood;
            controller.Clear();
            Check(system.WarriorsSelected && !system.WarriorProduction,"warrior tab initially explains missing barracks");
            system.PrepareWarriorProduction();
            Check(system.Pending && system.PendingKind==StructureKind.Barracks && controller.Selected.Contains(worker),"warrior production action automatically selects worker and arms barracks placement");
            system.PlacePending(new Vector3(-6,0,6));
            var site=system.Sites.Count>0 ? system.Sites[system.Sites.Count-1]:null;
            Check(site && system.main.wallet.Food==food && system.main.wallet.Wood==wood-150,"barracks placement retains original cost / "+system.Message);
            if(!site) {Application.Quit(1);yield break;}
            int siteCount=system.Sites.Count;
            int paidWood=system.main.wallet.Wood;
            system.PrepareWarriorProduction();
            Check(system.Sites.Count==siteCount && system.main.wallet.Wood==paidWood && !system.Pending,"warrior action resumes existing construction without duplicating building or payment");
            var art=site.GetComponentInChildren<BarracksVisual>();Check(art,"newly placed barracks uses the cave and hide model");
            if(!art) {Application.Quit(1);yield break;}
            Check(site.GetComponentsInChildren<Collider>(true).Length==1 && art.GetComponentsInChildren<Collider>(true).Length==0,"decorative timbers trees shields and dummy add no colliders");
            Check(site.Health.maxHealth==500 && site.Health.Body.size==new Vector3(4,2.6f,4) && site.GetComponent<NavMeshObstacle>().size==new Vector3(4,2.6f,4),"original health hitbox and navigation footprint retained");
            Check(Physics.Raycast(site.transform.position+Vector3.up*8,Vector3.down,out var hit,10,1<<13) && hit.collider==site.Health.Body,"selection ray hits the original building collider");
            int triangles=0;bool valid=true;var bounds=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var filter in art.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;triangles+=mesh.triangles.Length/3;valid&=mesh.normals.Length==mesh.vertexCount && mesh.vertexCount>0;
                foreach(var p in mesh.vertices) bounds.Encapsulate(art.transform.InverseTransformPoint(filter.transform.TransformPoint(p)));
            }
            Check(valid && triangles>1000 && triangles<14000,"valid low poly barracks meshes / triangles "+triangles);
            Check(bounds.min.y>=-.03f && bounds.max.y<2.9f && Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x),Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z))<2.2f,"roof yard and trees fit existing building visual footprint");
            Check(!site.GetComponent<ProductionBuilding>() && !art.Operational,"unfinished barracks has no production or banner motion");
            yield return Until(()=>site.Progress>.1f,8);
            Check(site.Progress>.1f && site.Progress<1 && Mathf.Abs(art.transform.localScale.y-(.18f+site.Progress*.82f))<.001f,"visual height follows real construction progress");
            var pose=art.banners[0].localRotation;yield return new WaitForSeconds(.15f);
            Check(Quaternion.Angle(pose,art.banners[0].localRotation)<.01f,"unfinished banners remain stationary");
            yield return Until(()=>site.Complete,18);
            var production=site.GetComponent<ProductionBuilding>();
            Check(site.Complete && production && art.Operational && art.transform.localScale==Vector3.one,"completed barracks enables production and banner motion");
            if(!production) {Application.Quit(1);yield break;}
            Check(!site.transform.Find("Roof") && !site.transform.Find("Building"),"new model replaces the old cube body and roof");
            Check(production.wallet==system.main.wallet && production.unitPrefabs.Length==3 && system.SelectedProduction==production,"completed barracks retains shared wallet and combat unit production");
            Check(production.rallyPoint==site.transform.position+Vector3.back*4.5f && Vector3.Dot(art.transform.forward,(production.rallyPoint-site.transform.position).normalized)>.99f,"entrance faces the unchanged rally point");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-barracksCapture")>=0)
            {
                var camera=controller.sceneCamera;var controls=camera.GetComponent<RTSCameraController>();if(controls) controls.enabled=false;
                controller.Clear();camera.transform.position=site.transform.position+new Vector3(7,7.8f,-10);camera.transform.LookAt(site.transform.position+Vector3.up*.8f);camera.orthographicSize=6;yield return null;
                var previous=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
                try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes(Path.GetFullPath("barracks-game.png"),pixels.EncodeToPNG());}
                finally {camera.targetTexture=previous;RenderTexture.active=active;target.Release();Destroy(target);Destroy(pixels);}
            }
            Check(system.WarriorsSelected && system.WarriorProduction==production && system.SelectedProduction==production,"completed barracks opens warrior production");
            system.SelectProduction(false);
            Check(!system.WarriorsSelected && system.SelectedProduction==system.main,"worker tab selects main base");
            system.SelectProduction(true);
            Check(system.WarriorsSelected && system.SelectedProduction==production,"warrior tab returns to ready barracks");
            int count=controller.units.Length;
            Check(production.TryQueue(0) && production.TryQueue(1) && production.TryQueue(2) && production.QueueCount==3,"all three combat categories can train simultaneously");
            food=production.wallet.Food;wood=production.wallet.Wood;
            Check(!system.main.TryQueue(3) && production.wallet.Food==food && production.wallet.Wood==wood,"fourth global training order rejected without charge");
            yield return Until(()=>controller.units.Length>=count+3,18);
            Check(controller.units.Length==count+3 && production.QueueCount==0,"all three trained units actually spawn");
            bool spawnValid=true;
            for(int i=count;i<controller.units.Length;i++)
            {
                var unit=controller.units[i];var p=unit.transform.position+Vector3.up*.6f;
                spawnValid&=unit.Agent.isOnNavMesh && (site.Health.SurfacePoint(p)-p).sqrMagnitude>.01f && unit.GetComponentsInChildren<MeshFilter>().Length>1;
            }
            Check(spawnValid && controller.units.Length>=count+3,"trained model units appear outside the building on the navmesh");
            pose=art.banners[0].localRotation;yield return new WaitForSeconds(.17f);
            Check(Quaternion.Angle(pose,art.banners[0].localRotation)>.01f,"completed banners move gently");
            food=production.wallet.Food;wood=production.wallet.Wood;Check(production.TryQueue(0),"another order can start after training slots clear");
            controller.SetPaused(true);pose=art.banners[0].localRotation;float remaining=production.RemainingSeconds(0);yield return new WaitForSecondsRealtime(.2f);
            Check(Quaternion.Angle(pose,art.banners[0].localRotation)<.01f && production.RemainingSeconds(0)==remaining,"pause freezes banners and training");
            controller.SetPaused(false);controller.enemyBase.TakeDamage(100000);yield return null;yield return null;
            pose=art.banners[0].localRotation;yield return new WaitForSecondsRealtime(.2f);
            Check(controller.MatchOver && production.QueueCount==0 && production.wallet.Food==food && production.wallet.Wood==wood && Quaternion.Angle(pose,art.banners[0].localRotation)<.01f,"match end freezes banners and refunds unfinished training");
            site.Health.TakeDamage(100000);yield return null;
            Check(!site.Health.IsAlive && !art.gameObject.activeInHierarchy,"destruction hides the full barracks model");
            Application.Quit(failed ? 1:0);
        }
    }
}
