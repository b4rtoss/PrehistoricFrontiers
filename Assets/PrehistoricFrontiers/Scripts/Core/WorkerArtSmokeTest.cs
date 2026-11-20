using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class WorkerArtSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-workerArtSmoke")>=0) new GameObject("Worker art regression").AddComponent<WorkerArtSmokeTest>();
        }
        string report=""; bool failed;
        void Check(bool valid,string name) {var line=(valid ? "PASS ":"FAIL ")+name;report+=line+"\n";failed|=!valid;Debug.Log(line);File.WriteAllText("workerart-test.txt",report);}
        IEnumerator Until(Func<bool> condition,float seconds) {float deadline=Time.time+seconds;while(!condition() && Time.time<deadline) yield return null;}
        IEnumerator Start()
        {
            yield return null;
            var controller=FindAnyObjectByType<RTSSelectionController>();
            SmokeTestSetup.DisableEnemyProduction(controller,false);controller.SetAutoBattle(false);controller.SetAutoScout(false);
            foreach(var ally in controller.units) ally.Stop();
            var workers=Array.FindAll(controller.units,u=>u.stats.isWorker);var unit=workers.Length>0 ? workers[0]:null;
            Check(workers.Length==1 && Array.TrueForAll(workers,u=>u.GetComponentInChildren<WorkerMotion>()),"single starting worker uses the reference model");
            if(!unit) {Application.Quit(1);yield break;}
            var art=unit.GetComponentInChildren<WorkerMotion>();var gatherer=unit.GetComponent<WorkerGatherer>();
            Check(art && art.GetComponentsInChildren<Collider>(true).Length==0 && unit.GetComponentsInChildren<Collider>(true).Length==1,"visual model adds no colliders");
            if(!art) {Application.Quit(1);yield break;}
            var old=unit.transform.Find("Visual");
            Check(old.GetComponent<CapsuleCollider>().enabled && !old.GetComponent<Renderer>().enabled,"original worker selection capsule retained without visible placeholder");
            Check(unit.Agent.isOnNavMesh && unit.Agent.radius==unit.stats.radius && unit.Agent.speed==unit.stats.moveSpeed && unit.GetComponent<UnitHealth>().MaxHealth==unit.stats.maxHealth,"worker navigation speed and health unchanged");
            Check(Physics.Raycast(unit.transform.position+Vector3.up*8,Vector3.down,out var hit,10,1<<10) && hit.collider.GetComponentInParent<RTSUnit>()==unit,"selection ray still selects original worker");
            int triangles=0;bool valid=true;var bounds=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var filter in art.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;triangles+=mesh.triangles.Length/3;valid&=mesh.vertexCount>0 && mesh.normals.Length==mesh.vertexCount;
                foreach(var p in mesh.vertices) bounds.Encapsulate(art.transform.InverseTransformPoint(filter.transform.TransformPoint(p)));
            }
            Check(valid && triangles>500 && triangles<5000,"valid low poly meshes / triangles "+triangles);
            Check(bounds.min.y>=-.025f && bounds.max.y<=unit.Agent.height+.3f,"grounded feet and model fits existing health indicator height");
            var colors=art.torso.GetComponent<MeshRenderer>().sharedMaterials;
            Check(colors[1].color.grayscale>colors[0].color.grayscale+.1f,"brown fur and lighter chest match the reference");
            Check(art.upperArms.Length==2 && art.forearms.Length==2 && art.upperLegs.Length==2 && art.lowerLegs.Length==2 && art.feet.Length==2 && art.tail,"two arms two legs and tail connected to the rig");
            Check(controller.construction.main.unitPrefabs[3].GetComponentInChildren<WorkerMotion>(true),"worker training prefab includes the visual rig");
            Check(!unit.GetComponent<UnitCombat>().Attack(controller.enemies[0]),"clawed appearance does not enable worker combat");
            Check(unit.MoveTo(new Vector3(-12,0,-14)) && unit.Agent.hasPath,"manual worker movement still creates a path");
            yield return new WaitForSeconds(.2f);var step=art.upperLegs[0].localRotation;
            yield return new WaitForSeconds(.15f);
            Check(Quaternion.Angle(step,art.upperLegs[0].localRotation)>.1f,"walking animates the worker legs");
            unit.Stop();gatherer.autoGatherEnabled=false;Time.timeScale=3;
            var node=Array.Find(FindObjectsByType<ResourceNode>(),n=>n.Remaining>0);
            Check(node && gatherer.Gather(node),"reference worker accepts real harvesting order");
            yield return Until(()=>gatherer.State==GatherState.Gathering && art.IsWorking,25);yield return null;
            var arm=art.forearms[0].localRotation;yield return new WaitForSeconds(.17f);
            Check(gatherer.State==GatherState.Gathering && art.IsWorking && Quaternion.Angle(arm,art.forearms[0].localRotation)>.1f,"real harvesting animates the arms / state "+gatherer.State+" working "+art.IsWorking);
            unit.Stop();yield return null;yield return null;
            Check(!art.IsWorking,"stop cancels the gathering pose");
            unit.Agent.Warp(new Vector3(-6,0,1));
            foreach(var enemy in controller.enemies) { enemy.Stop(); enemy.Agent.Warp(new Vector3(21,0,21)); }
            var site=controller.construction.Place(unit,StructureKind.Barracks,new Vector3(-6,0,6));
            yield return Until(()=>site && site.Progress>0 && art.IsWorking,15);yield return null;
            Check(site && site.Progress>0 && art.IsWorking,"real construction activates work animation / progress "+(site ? site.Progress : -1)+" working "+art.IsWorking+" / "+controller.construction.Message);
            controller.SetPaused(true);var pose=art.forearms[0].localRotation;var tail=art.tail.localRotation;
            yield return new WaitForSecondsRealtime(.2f);
            Check(Quaternion.Angle(pose,art.forearms[0].localRotation)<.01f && Quaternion.Angle(tail,art.tail.localRotation)<.01f,"pause freezes arms and tail during work");
            controller.SetPaused(false);controller.enemyBase.TakeDamage(100000);yield return null;
            pose=art.forearms[0].localRotation;yield return new WaitForSecondsRealtime(.2f);
            Check(controller.MatchOver && !unit.Agent.hasPath && Quaternion.Angle(pose,art.forearms[0].localRotation)<.01f,"match completion stops both worker navigation and visual motion");
            Application.Quit(failed ? 1:0);
        }
    }
}
