using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace PrehistoricFrontiers.Editor
{
    public static class EnemyEconomySetup
    {
        const string Root = "Assets/PrehistoricFrontiers/";
        public static void ApplyAndBuild()
        {
            EditorSceneManager.OpenScene(Root + "Scenes/Battle.unity", OpenSceneMode.Single);
            ApplyToOpenScene();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            FoundationBuilder.Build();
        }
        public static void ApplyToOpenScene()
        {
            var c = Object.FindAnyObjectByType<RTSSelectionController>();
            var player = c.playerBase.GetComponent<ProductionBuilding>();
            var main = c.enemyBase.GetComponent<ProductionBuilding>();
            var workerPath = Root + "Prefabs/Units/Enemy_Worker.prefab";
            var worker = Object.Instantiate(player.unitPrefabs[3]);
            worker.name = "Enemy_Worker"; worker.GetComponent<RTSUnit>().team = UnitTeam.Enemy;
            var ring = Object.Instantiate(main.unitPrefabs[0].transform.Find("EnemyTeamRing").gameObject,worker.transform,false);
            ring.name = "EnemyTeamRing"; ring.transform.localScale *= .8f/.9f;
            var workerPrefab = PrefabUtility.SaveAsPrefabAsset(worker,workerPath);
            Object.DestroyImmediate(worker);
            main.unitPrefabs = new[] { main.unitPrefabs[0], main.unitPrefabs[1], main.unitPrefabs[2], workerPrefab };
            main.workersOnly = true; main.wallet.Initialize(1000,1000);
            main.depositPoint = main.transform.position + Vector3.right * 3.5f;
            var economy = main.GetComponent<EnemyEconomyAI>() ?? main.gameObject.AddComponent<EnemyEconomyAI>();
            var construction = main.GetComponent<ConstructionSystem>() ?? main.gameObject.AddComponent<ConstructionSystem>();
            construction.controller=c;construction.main=main;construction.SelectedProduction=main;
            construction.barracksModelPrefab=c.construction.barracksModelPrefab;construction.clinicModelPrefab=c.construction.clinicModelPrefab;
            economy.construction=construction;
            if(!Array.Exists(c.enemies,u=>u.stats.isWorker))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(workerPrefab);
                instance.transform.position = new Vector3(14,0,9); instance.name="Enemy Worker Start";
                var unit=instance.GetComponent<RTSUnit>(); unit.GetComponent<UnitCombat>().battleController=c;
                unit.GetComponent<WorkerGatherer>().home=main;
                instance.AddComponent<FogHiddenVisual>().fog=c.fog;
                var enemies=new List<RTSUnit>(c.enemies){unit};c.enemies=enemies.ToArray();
            }
            var all=new List<RTSUnit>(c.units);all.AddRange(c.enemies);main.healthBars.units=all.ToArray();
            var nodes=new List<ResourceNode>(Object.FindObjectsByType<ResourceNode>());
            foreach(var source in nodes.ToArray())
            {
                if(source.name.StartsWith("Enemy ") || GameObject.Find("Enemy "+source.name))continue;
                var node=Object.Instantiate(source.gameObject);node.name="Enemy "+source.name;
                node.transform.position=new Vector3(-source.transform.position.x,source.transform.position.y,-source.transform.position.z);
                var obstacle=node.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;
                obstacle.center=Vector3.up;obstacle.size=source.kind==ResourceKind.Food ? new Vector3(1.8f,2,1.8f):new Vector3(2.3f,4,2.3f);obstacle.carving=true;
                node.AddComponent<FogHiddenVisual>().fog=c.fog;nodes.Add(node.GetComponent<ResourceNode>());
            }
            foreach (var node in nodes)
                if (node.name.StartsWith("Enemy "))
                {
                    var obstacle=node.GetComponent<NavMeshObstacle>();
                    obstacle.size=node.kind==ResourceKind.Food ? new Vector3(1.8f,2,1.8f):new Vector3(2.3f,4,2.3f);
                }
            c.GetComponent<ResourceNodeLabels>().nodes=nodes.ToArray();
            c.GetComponent<BattleMinimap>().resources=nodes.ToArray();
            c.preparationDuration=20;
            EditorUtility.SetDirty(c);EditorUtility.SetDirty(main);EditorUtility.SetDirty(construction);EditorUtility.SetDirty(economy);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
