using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace PrehistoricFrontiers
{
    public sealed class EnvironmentArtSmokeTest:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() {if(Array.IndexOf(Environment.GetCommandLineArgs(),"-environmentArtSmoke")>=0) new GameObject("Environment art regression").AddComponent<EnvironmentArtSmokeTest>();}
        string report="";bool failed;
        void Check(bool value,string name) {var line=(value ? "PASS ":"FAIL ")+name;report+=line+"\n";failed|=!value;Debug.Log(line);File.WriteAllText("environmentart-test.txt",report);}
        IEnumerator Start()
        {
            yield return null;
            var c=FindAnyObjectByType<RTSSelectionController>();SmokeTestSetup.DisableEnemyProduction(c,false);c.SetAutoBattle(false);c.SetAutoScout(false);foreach(var unit in c.units) unit.Stop();
            var models=FindObjectsByType<EnvironmentArt>();int trees=0,deciduous=0,conifer=0,rocks=0,resources=0;bool colliders=true,geometry=true,oldHidden=true;
            var rotations=new System.Collections.Generic.HashSet<int>();var variants=new System.Collections.Generic.HashSet<int>();
            foreach(var model in models)
            {
                colliders&=model.GetComponentsInChildren<Collider>(true).Length==0;
                var mesh=model.GetComponent<MeshFilter>().sharedMesh;geometry&=mesh.vertexCount>0 && mesh.normals.Length==mesh.vertexCount && mesh.triangles.Length/3<1200;
                if(model.resource) resources++;
                else if(model.kind==EnvironmentArtKind.Rock) rocks++;
                else {trees++;if(model.kind==EnvironmentArtKind.Deciduous) deciduous++;if(model.kind==EnvironmentArtKind.Conifer) conifer++;rotations.Add(Mathf.RoundToInt(model.transform.localEulerAngles.y));variants.Add(model.variant);}
            }
            Check(trees==19 && deciduous>0 && conifer>0,"all 19 decorative trees replaced with a mixture of deciduous and conifer forms / "+deciduous+" orange, "+conifer+" green");
            Check(rotations.Count>8 && variants.Count==3,"saved scene contains randomized rotations and all three shape variants");
            Check(rocks==3 && resources==8,"three obstacle rocks and eight resource nodes have models");
            Check(colliders,"new decorative models add no colliders");
            Check(geometry,"all environment meshes have valid flat normals and low poly budgets");
            foreach(var t in FindObjectsByType<Transform>()) if(t.name=="Tree") {var trunk=t.Find("Trunk");var canopy=t.Find("Canopy");if(trunk && canopy) oldHidden&=!trunk.GetComponent<Renderer>().enabled && !canopy.GetComponent<Renderer>().enabled && trunk.GetComponent<Collider>().enabled;}
            Check(oldHidden,"original tree colliders remain while smooth tree graphics are disabled");
            var central=GameObject.Find("Central basalt");var outcrop=GameObject.Find("Basalt outcrop");var western=GameObject.Find("Western stone");
            Check(central && central.GetComponent<BoxCollider>().enabled && central.transform.position==new Vector3(0,1.3f,1) && central.transform.localScale==new Vector3(4,2.6f,5) && !central.GetComponent<Renderer>().enabled,"central basalt keeps original physical position dimensions and collider");
            Check(outcrop && outcrop.GetComponent<BoxCollider>().enabled && western && western.GetComponent<SphereCollider>().enabled,"secondary rock colliders retained");
            var nodes=FindObjectsByType<ResourceNode>();bool nodeModels=true,capacity=true;int foods=0,woods=0;
            foreach(var node in nodes)
            {
                var art=node.GetComponentInChildren<EnvironmentArt>();nodeModels&=art && art.resource;
                capacity&=node.Remaining==node.capacity && node.capacity==(node.kind==ResourceKind.Food ? 200:250);
                if(node.kind==ResourceKind.Food) {foods++;nodeModels&=art.kind==EnvironmentArtKind.BerryBush;}else {woods++;nodeModels&=art.kind==EnvironmentArtKind.Deciduous || art.kind==EnvironmentArtKind.Conifer;}
            }
            Check(nodeModels && foods==4 && woods==4,"berry bushes remain food and resource trees remain wood");
            Check(capacity,"all resource capacities and starting reserves unchanged");
            Check(Array.Find(nodes,n=>n.name=="Berry patch").transform.position==new Vector3(-4,0,-17) && Array.Find(nodes,n=>n.name=="Wood grove").transform.position==new Vector3(-16,0,-6),"resource locations preserved");
            var worker=Array.Find(c.units,u=>u.stats.isWorker);Check(worker.MoveTo(new Vector3(-6,0,6)) && worker.Agent.hasPath,"worker can navigate across the unchanged battlefield");worker.Stop();
            Check(c.playerBase.GetComponentInChildren<MainBaseVisual>() && c.enemyBase.GetComponentInChildren<MainBaseVisual>() && c.construction.clinicModelPrefab && c.construction.barracksModelPrefab,"unit and building artwork retained");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-environmentCapture")>=0)
            {
                var camera=c.sceneCamera;var controls=camera.GetComponent<RTSCameraController>();if(controls) controls.enabled=false;
                foreach(var view in new[] {"overview","resources"})
                {
                    var point=view=="overview" ? new Vector3(-1,0,-1):new Vector3(-15,0,-5);
                    camera.transform.position=point+new Vector3(0,23,-23);camera.transform.LookAt(point);camera.orthographicSize=view=="overview" ? 26:10;yield return null;
                    var previous=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(1440,1000,24);var pixels=new Texture2D(1440,1000,TextureFormat.RGB24,false);
                    try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1440,1000),0,0);pixels.Apply();File.WriteAllBytes(Path.GetFullPath("environment-"+view+".png"),pixels.EncodeToPNG());}
                    finally {camera.targetTexture=previous;RenderTexture.active=active;target.Release();Destroy(target);Destroy(pixels);}
                }
            }
            var food=Array.Find(nodes,n=>n.kind==ResourceKind.Food);food.Extract(food.Remaining);bool hidden=true,disabled=true;foreach(var renderer in food.GetComponentsInChildren<Renderer>(true)) hidden&=!renderer.enabled;foreach(var col in food.GetComponentsInChildren<Collider>(true)) disabled&=!col.enabled;
            Check(food.Remaining==0 && hidden && disabled,"depleted berry node hides all new artwork and releases original colliders");
            var wood=Array.Find(nodes,n=>n.kind==ResourceKind.Wood);wood.Extract(wood.Remaining);hidden=true;foreach(var renderer in wood.GetComponentsInChildren<Renderer>(true)) hidden&=!renderer.enabled;
            Check(wood.Remaining==0 && hidden,"depleted resource tree hides its new trunk and canopy");
            Application.Quit(failed ? 1:0);
        }
    }
}
