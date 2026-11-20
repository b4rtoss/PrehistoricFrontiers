using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace PrehistoricFrontiers.Editor
{
    public static class FoundationBuilder
    {
        const string Root = "Assets/PrehistoricFrontiers/";
        [MenuItem("Prehistoric Frontiers/Generate Draft Scene...")]
        static void GenerateDraftFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Пересоздать черновик?",
                "Battle.unity и черновые префабы будут перезаписаны. Параметры видов и материалов будут обновлены исходными значениями. GUID существующих assets сохранятся. Для обычной сборки используйте Build Windows Player.",
                "Пересоздать черновик", "Отмена")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create();
        }

        // Explicit draft generation only. Ordinary builds never call this method.
        public static void Create()
        {
            foreach (var folder in new[] { "Art/Models", "Art/Materials", "Art/Environment", "Prefabs/Units", "Prefabs/Buildings", "Scenes", "Scripts/Camera", "Scripts/Units", "Scripts/Input", "Scripts/Combat", "Scripts/Economy", "Scripts/Vision", "Scripts/Core", "Data" })
                Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();
            SetLayer(8, "Ground"); SetLayer(9, "Obstacle"); SetLayer(10, "Unit"); SetLayer(11, "Decoration");
            SetLayer(12, "Resource");
            SetLayer(13, "Building");
            PlayerSettings.companyName = "Prehistoric Frontiers";
            PlayerSettings.productName = "Prehistoric Frontiers";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            // A common bake reserves clearance for the largest placeholder (Mammoth).
            var navSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var agentSettings = navSettings.FindProperty("m_Settings").GetArrayElementAtIndex(0);
            agentSettings.FindPropertyRelative("agentRadius").floatValue = 1.2f;
            agentSettings.FindPropertyRelative("agentHeight").floatValue = 4f;
            navSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(.62f, .71f, .72f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = false;
            var grass = Mat("Grass_Mat", "#729D64");
            var rock = Mat("Rock_Mat", "#77878A");
            var bark = Mat("Bark_Mat", "#806049");
            var leaf = Mat("Canopy_Mat", "#33715A");
            var leafLight = Mat("CanopyLight_Mat", "#4F8961");
            var sand = Mat("Sand_Mat", "#C4B586");
            var ringMat = Mat("Selection_Mat", "#CAFF7A", true);
            var ground = Primitive("Ground", PrimitiveType.Plane, Vector3.zero, new Vector3(5, 1, 5), grass, 8);
            // Decorative tiles have no colliders and never enter the navigation bake.
            var random = new System.Random(14);
            for (int i = 0; i < 60; i++)
            {
                float x = (float)random.NextDouble() * 46 - 23, z = (float)random.NextDouble() * 46 - 23;
                var tile = Primitive("Meadow patch", PrimitiveType.Cylinder, new Vector3(x, .009f, z), new Vector3(1 + (float)random.NextDouble() * 2, .008f, 1.3f), i % 4 == 0 ? sand : grass, 11);
                Object.DestroyImmediate(tile.GetComponent<Collider>());
            }
            // Tall, broad stones make the detour visible from the starting position.
            Primitive("Central basalt", PrimitiveType.Cube, new Vector3(0, 1.3f, 1), new Vector3(4, 2.6f, 5), rock, 9).transform.rotation = Quaternion.Euler(0, 12, 0);
            Primitive("Basalt outcrop", PrimitiveType.Cube, new Vector3(3.2f, .85f, 3.4f), new Vector3(2.7f, 1.7f, 2.6f), rock, 9).transform.rotation = Quaternion.Euler(0, -18, 0);
            Primitive("Western stone", PrimitiveType.Sphere, new Vector3(-11, .7f, 3), new Vector3(3.8f, 2.2f, 3), rock, 9);
            for (int i = 0; i < 32; i++)
            {
                float x = (float)random.NextDouble() * 44 - 22, z = (float)random.NextDouble() * 44 - 22;
                if (Mathf.Abs(x) < 13 && z > -13 && z < 12) continue;
                var tree = new GameObject("Tree");
                var trunk = Primitive("Trunk", PrimitiveType.Cylinder, new Vector3(x, 1, z), new Vector3(.6f, 1, .6f), bark, 9);
                trunk.transform.SetParent(tree.transform);
                var canopy = Primitive("Canopy", PrimitiveType.Sphere, new Vector3(x, 3, z), new Vector3(3, 3.2f, 3), i % 2 == 0 ? leaf : leafLight, 11);
                Object.DestroyImmediate(canopy.GetComponent<Collider>());
                canopy.transform.SetParent(tree.transform);
            }
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48, -30, 0);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera"; cam.orthographic = true; cam.orthographicSize = 14;
            cam.transform.SetPositionAndRotation(new Vector3(0, 12, -14), Quaternion.Euler(45, 0, 0));
            cam.backgroundColor = new Color(.15f, .24f, .25f); cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 160;
            cam.gameObject.AddComponent<RTSCameraController>();
            cam.gameObject.AddComponent<AudioListener>();
            var baseObject = new GameObject("Main Building");
            baseObject.transform.position = new Vector3(-10, 0, -10);
            var foundation = Primitive("Stone foundation", PrimitiveType.Cube, new Vector3(-10, .3f, -10), new Vector3(4.5f, .6f, 3.5f), rock, 9);
            foundation.transform.SetParent(baseObject.transform, true);
            var hut = Primitive("Longhouse", PrimitiveType.Cube, new Vector3(-10, 1.3f, -10), new Vector3(3.8f, 2, 2.8f), bark, 9);
            hut.transform.SetParent(baseObject.transform, true);
            var roof = Primitive("Thatched roof", PrimitiveType.Cube, new Vector3(-10, 2.5f, -10), new Vector3(4.2f, .7f, 3.2f), sand, 9);
            roof.transform.SetParent(baseObject.transform, true);
            foreach (var collider in baseObject.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            foreach (var child in baseObject.GetComponentsInChildren<Transform>()) child.gameObject.layer = 11;
            baseObject.layer = 13;
            var baseCollider = baseObject.AddComponent<BoxCollider>(); baseCollider.center = new Vector3(0, 1.45f, 0); baseCollider.size = new Vector3(4.5f, 2.9f, 3.5f);
            var playerBase = baseObject.AddComponent<BuildingHealth>(); playerBase.team = UnitTeam.Player;
            PrefabUtility.SaveAsPrefabAsset(baseObject, Root + "Prefabs/Buildings/MainBuilding.prefab");
            var enemyBaseObject = Object.Instantiate(baseObject);
            enemyBaseObject.name = "Enemy Main Building"; enemyBaseObject.transform.position = new Vector3(10, 0, 4);
            var enemyBase = enemyBaseObject.GetComponent<BuildingHealth>(); enemyBase.team = UnitTeam.Enemy;
            foreach (var renderer in enemyBaseObject.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = Mat("EnemyBuilding_Mat", "#A3494E");
            MakeRing("Enemy base marker", 3.1f, Mat("EnemyTeam_Mat", "#FF574F", true), enemyBaseObject.transform);
            PrefabUtility.SaveAsPrefabAsset(enemyBaseObject, Root + "Prefabs/Buildings/EnemyMainBuilding.prefab");
            var resourceNodes = new[] {
                CreateResource("Berry patch", ResourceKind.Food, new Vector3(-4, 0, -17), leafLight, bark),
                CreateResource("Berry grove", ResourceKind.Food, new Vector3(-15, 0, -1), leafLight, bark),
                CreateResource("Wood grove", ResourceKind.Wood, new Vector3(-16, 0, -6), leaf, bark),
                CreateResource("Wood reserve", ResourceKind.Wood, new Vector3(-17, 0, -16), leaf, bark) };
            var surface = ground.AddComponent<NavMeshSurface>();
            surface.layerMask = (1 << 8) | (1 << 9) | (1 << 12) | (1 << 13);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            BakeNavigation(surface, Root + "Data/BattleNavMesh.asset");
            var units = new RTSUnit[3];
            units[0] = CreateUnit("Raptor", "Scout", new Vector3(-5, 0, -7), new Vector3(.8f, .8f, 1.3f), 6.5f, .55f, "#D69458", ringMat);
            units[1] = CreateUnit("Triceratops", "Vanguard", new Vector3(0, 0, -7), new Vector3(1.4f, 1.3f, 1.8f), 4.0f, .95f, "#70ABB0", ringMat);
            units[2] = CreateUnit("Mammoth", "Heavy", new Vector3(5, 0, -7), new Vector3(1.8f, 2, 2.2f), 2.8f, 1.15f, "#AA7765", ringMat);
            var enemies = new RTSUnit[3];
            for (int i = 0; i < units.Length; i++)
            {
                var enemy = Object.Instantiate(units[i].gameObject);
                enemy.name = "Enemy_" + units[i].name;
                enemy.transform.SetPositionAndRotation(new Vector3((i - 1) * 8, 0, 8), Quaternion.Euler(0, 180, 0));
                enemies[i] = enemy.GetComponent<RTSUnit>();
                enemies[i].team = UnitTeam.Enemy;
                enemy.GetComponentInChildren<Renderer>().sharedMaterial = Mat("Enemy_" + units[i].name + "_Mat", i == 0 ? "#CE534B" : i == 1 ? "#BA4049" : "#973748");
                MakeRing("EnemyTeamRing", units[i].stats.radius + .35f, Mat("EnemyTeam_Mat", "#FF574F", true), enemy.transform);
                PrefabUtility.SaveAsPrefabAsset(enemy, Root + "Prefabs/Units/" + enemy.name + ".prefab");
            }
            var workerPrototype = CreateUnit("Worker", "Gatherer", new Vector3(0, 0, -20), new Vector3(.55f, .8f, .55f), 4.3f, .45f, "#D9C05C", ringMat);
            Object.DestroyImmediate(workerPrototype.gameObject);
            var systems = new GameObject("RTS Systems");
            var controller = systems.AddComponent<RTSSelectionController>();
            controller.statistics = systems.AddComponent<MatchStatistics>(); controller.statistics.controller = controller;
            controller.sceneCamera = cam; controller.units = units;
            controller.enemies = enemies;
            controller.playerBase = playerBase; controller.enemyBase = enemyBase;
            playerBase.controller = controller; enemyBase.controller = controller;
            var alerts = systems.AddComponent<AttackNotifications>(); alerts.controller = controller; controller.alerts = alerts;
            var fog = systems.AddComponent<FogOfWar>(); fog.controller = controller; controller.fog = fog;
            foreach (var enemy in enemies) enemy.gameObject.AddComponent<FogHiddenVisual>().fog = fog;
            enemyBaseObject.AddComponent<FogHiddenVisual>().fog = fog;
            var fogMaterial = LoadExistingAsset<Material>(Root + "Art/Materials/FogEffect.mat");
            if (!fogMaterial) { fogMaterial = new Material(Shader.Find("Hidden/PrehistoricFrontiers/FogOfWar")); AssetDatabase.CreateAsset(fogMaterial, Root + "Art/Materials/FogEffect.mat"); }
            var fogEffect = cam.gameObject.AddComponent<FogImageEffect>(); fogEffect.fog = fog; fogEffect.material = fogMaterial;
            foreach (var unit in units) unit.GetComponent<UnitCombat>().battleController = controller;
            foreach (var unit in enemies) unit.GetComponent<UnitCombat>().battleController = controller;
            var healthBars = systems.AddComponent<UnitHealthBars>();
            healthBars.sceneCamera = cam;
            healthBars.fog = fog;
            healthBars.buildings = new[] { playerBase, enemyBase };
            healthBars.units = new[] { units[0], units[1], units[2], enemies[0], enemies[1], enemies[2] };
            var production = baseObject.AddComponent<ProductionBuilding>();
            production.controller = controller; production.healthBars = healthBars; production.workersOnly = true;
            production.wallet = systems.AddComponent<ResourceWallet>();
            production.wallet.Initialize(1000, 1000);
            production.unitPrefabs = new[] {
                AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Units/Raptor.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Units/Triceratops.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Units/Mammoth.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Units/Worker.prefab") };
            var enemyProduction = enemyBaseObject.AddComponent<ProductionBuilding>();
            enemyProduction.controller = controller; enemyProduction.healthBars = healthBars;
            enemyProduction.wallet = enemyBaseObject.AddComponent<ResourceWallet>();
            enemyProduction.rallyPoint = new Vector3(10, 0, 0);
            enemyProduction.unitPrefabs = new[] {
                AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Units/Enemy_Raptor.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Units/Enemy_Triceratops.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Units/Enemy_Mammoth.prefab") };
            var enemyAI = enemyBaseObject.AddComponent<EnemyProductionAI>(); enemyAI.production = enemyProduction;
            var labels = systems.AddComponent<ResourceNodeLabels>(); labels.sceneCamera = cam; labels.nodes = resourceNodes; labels.fog = fog;
            controller.destinationMarker = MakeRing("Move destination", 1, ringMat, null);
            var hud = systems.AddComponent<BattleHUD>(); hud.controller = controller; hud.production = production;
            hud.enemyProduction = enemyAI;
            var construction = systems.AddComponent<ConstructionSystem>(); construction.controller = controller; construction.main = production; construction.SelectedProduction = production; controller.construction = construction;
            var minimap = systems.AddComponent<BattleMinimap>(); minimap.controller = controller; minimap.resources = resourceNodes; hud.minimap = minimap;
            systems.AddComponent<MinimapSmokeTest>().minimap = minimap;
            systems.AddComponent<FoundationSmokeTest>().controller = controller;
            systems.AddComponent<CombatSmokeTest>().controller = controller;
            systems.AddComponent<AttackMoveSmokeTest>().controller = controller;
            systems.AddComponent<WorkerProtectionSmokeTest>().controller = controller;
            systems.AddComponent<ConstructionSmokeTest>().controller = controller;
            systems.AddComponent<AutoBattleSmokeTest>().controller = controller;
            systems.AddComponent<EconomySmokeTest>().production = production;
            var gatherTest = systems.AddComponent<GatheringSmokeTest>(); gatherTest.production = production; gatherTest.nodes = resourceNodes;
            var autoGatherTest = systems.AddComponent<AutoGatherSmokeTest>(); autoGatherTest.production = production; autoGatherTest.nodes = resourceNodes;
            systems.AddComponent<BaseCombatSmokeTest>().controller = controller;
            systems.AddComponent<EnemyProductionSmokeTest>().production = enemyProduction;
            systems.AddComponent<ParallelProductionSmokeTest>().production = production;
            systems.AddComponent<FogSmokeTest>().controller = controller;
            systems.AddComponent<AutomationControlsSmokeTest>().controller = controller;
            systems.AddComponent<AttackAlertsSmokeTest>().controller = controller;
            var startingUnits = new List<RTSUnit>(units);
            var savedRally = production.rallyPoint;
            production.rallyPoint = new Vector3(-14, 0, -16);
            Physics.SyncTransforms();
            if (!production.FindSpawnPosition(.45f, out var workerPosition)) throw new System.Exception("No space for starting worker");
            var startingWorker = Object.Instantiate(production.unitPrefabs[3], workerPosition, Quaternion.identity).GetComponent<RTSUnit>();
            startingWorker.name = "Worker Start 1";
            startingWorker.GetComponent<UnitCombat>().battleController = controller;
            startingWorker.GetComponent<WorkerGatherer>().home = production;
            startingUnits.Add(startingWorker);
            production.rallyPoint = savedRally;
            controller.units = startingUnits.ToArray();
            var startingBars = new List<RTSUnit>(startingUnits); startingBars.AddRange(enemies); healthBars.units = startingBars.ToArray();
            systems.AddComponent<StartingArmySmokeTest>().controller = controller;
            systems.AddComponent<PauseSmokeTest>().controller = controller;
            systems.AddComponent<MatchResultsScreen>().controller = controller;
            systems.AddComponent<DefeatScreenSmokeTest>().controller = controller;
            systems.AddComponent<MatchResultsSmokeTest>().controller = controller;
            EnemyEconomySetup.ApplyToOpenScene();
            BattleEffectsSetup.ApplyToOpenScene();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Root + "Scenes/Battle.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "Scenes/Battle.unity", true) };
            AssetDatabase.SaveAssets();
            Debug.Log("FOUNDATION_SCENE_READY");
        }
        [MenuItem("Prehistoric Frontiers/Build Windows Player")]
        public static void Build()
        {
            string path = Path.GetFullPath("../PrehistoricFrontiers-Windows/PrehistoricFrontiers.exe");
            BuildPlayer(path);
        }

        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath)) throw new System.ArgumentException("Specify a Windows player output path.", nameof(outputPath));
            var scenes = new List<string>();
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;
                if (string.IsNullOrEmpty(entry.path) || !AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path))
                    throw new System.InvalidOperationException("Enabled build scene is missing: " + entry.path + ". Configure the scene list; draft generation is a separate command.");
                scenes.Add(entry.path);
            }
            if (scenes.Count == 0) throw new System.InvalidOperationException("No enabled scenes in Build Settings. Add a saved scene, or explicitly generate a draft first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty && scenes.Contains(scene.path))
                    throw new System.InvalidOperationException("Save the modified build scene before building: " + scene.path);
            }
            string path = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(scenes.ToArray(), path, BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Windows build failed: " + report.summary.result);
            Debug.Log("FOUNDATION_BUILD_READY");
            return report;
        }

        [MenuItem("Prehistoric Frontiers/Update Navigation (Keep GUID)")]
        static void UpdateNavigationFromMenu()
        {
            var surfaces = Object.FindObjectsByType<NavMeshSurface>();
            var surface = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<NavMeshSurface>() : null;
            if (!surface && surfaces.Length == 1) surface = surfaces[0];
            if (!surface) throw new System.InvalidOperationException("Select the GameObject with the NavMeshSurface to update.");
            string path = surface.navMeshData ? AssetDatabase.GetAssetPath(surface.navMeshData) : "";
            BakeNavigation(surface, string.IsNullOrEmpty(path) ? Root + "Data/BattleNavMesh.asset" : path);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        }

        public static void BakeNavigation(NavMeshSurface surface, string assetPath)
        {
            if (!surface) throw new System.ArgumentNullException(nameof(surface));
            var existing = LoadExistingAsset<NavMeshData>(assetPath);
            var previous = surface.navMeshData;
            surface.BuildNavMesh();
            var baked = surface.navMeshData;
            if (!baked || baked == previous) throw new System.InvalidOperationException("Navigation baking produced no new data.");
            if (existing && existing != baked)
            {
                surface.RemoveData();
                EditorUtility.CopySerialized(baked, existing);
                EditorUtility.SetDirty(existing);
                surface.navMeshData = existing;
                if (surface.isActiveAndEnabled) surface.AddData();
                Object.DestroyImmediate(baked);
            }
            else if (!existing) AssetDatabase.CreateAsset(baked, assetPath);
            EditorUtility.SetDirty(surface);
        }

        static T LoadExistingAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset && !(asset is T))
                throw new System.InvalidOperationException("Cannot update " + path + ": expected " + typeof(T).Name + ", found " + asset.GetType().Name + ". Existing asset was left intact.");
            return asset as T;
        }
        static RTSUnit CreateUnit(string name, string role, Vector3 position, Vector3 scale, float speed, float radius, string hex, Material ring)
        {
            var statsPath = Root + "Data/" + name + "Stats.asset";
            var stats = LoadExistingAsset<UnitStats>(statsPath);
            if (!stats) { stats = ScriptableObject.CreateInstance<UnitStats>(); AssetDatabase.CreateAsset(stats, statsPath); }
            stats.isWorker = name == "Worker";
            stats.displayName = name; stats.role = role; stats.moveSpeed = speed; stats.radius = radius;
            stats.maxHealth = stats.isWorker ? 60 : name == "Raptor" ? 80 : name == "Triceratops" ? 180 : 300;
            stats.attackDamage = name == "Raptor" ? 14 : name == "Triceratops" ? 25 : 44;
            stats.attackCooldown = name == "Raptor" ? .75f : name == "Triceratops" ? 1.3f : 2f;
            stats.attackRange = name == "Mammoth" ? 1.4f : .85f;
            stats.detectionRadius = 7;
            stats.foodCost = stats.isWorker ? 50 : name == "Raptor" ? 80 : name == "Triceratops" ? 140 : 220;
            stats.woodCost = stats.isWorker ? 0 : name == "Raptor" ? 30 : name == "Triceratops" ? 70 : 120;
            stats.productionTime = stats.isWorker ? 4 : name == "Raptor" ? 5 : name == "Triceratops" ? 9 : 14;
            ColorUtility.TryParseHtmlString(hex, out stats.color);
            EditorUtility.SetDirty(stats);
            var go = new GameObject(name) { layer = 10 };
            go.transform.position = position;
            var body = Primitive("Visual", PrimitiveType.Capsule, position + Vector3.up * scale.y, scale, Mat(name + "_Mat", hex), 10);
            body.transform.SetParent(go.transform, true);
            var agent = go.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = radius; agent.height = 2 * scale.y; agent.baseOffset = 0;
            agent.angularSpeed = 360; agent.acceleration = 14; agent.stoppingDistance = .25f;
            var unit = go.AddComponent<RTSUnit>(); unit.stats = stats;
            go.AddComponent<UnitHealth>();
            go.AddComponent<UnitCombat>();
            if (stats.isWorker) { go.AddComponent<WorkerGatherer>(); go.GetComponent<UnitCombat>().autoAcquire = false; }
            unit.selectionIndicator = MakeRing("SelectionRing", radius + .35f, ring, go.transform).gameObject;
            unit.selectionIndicator.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/Units/" + name + ".prefab");
            return unit;
        }
        static ResourceNode CreateResource(string name, ResourceKind kind, Vector3 position, Material leaves, Material bark)
        {
            var root = new GameObject(name) { layer = 12 }; root.transform.position = position;
            var node = root.AddComponent<ResourceNode>(); node.kind = kind; node.capacity = kind == ResourceKind.Food ? 200 : 250;
            if (kind == ResourceKind.Food)
            {
                var bush = Primitive("Bush", PrimitiveType.Sphere, position + Vector3.up * .55f, new Vector3(1.8f, 1.1f, 1.8f), leaves, 12);
                bush.transform.SetParent(root.transform, true);
                var berries = Mat("Berries_Mat", "#B94E62");
                for (int i = 0; i < 4; i++)
                {
                    var berry = Primitive("Berries", PrimitiveType.Sphere, position + new Vector3((i % 2 - .5f) * .8f, 1, (i / 2 - .5f) * .8f), Vector3.one * .3f, berries, 11);
                    Object.DestroyImmediate(berry.GetComponent<Collider>()); berry.transform.SetParent(root.transform, true);
                }
            }
            else
            {
                var trunk = Primitive("Resource trunk", PrimitiveType.Cylinder, position + Vector3.up, new Vector3(.8f, 1, .8f), bark, 12);
                trunk.transform.SetParent(root.transform, true);
                var crown = Primitive("Resource crown", PrimitiveType.Sphere, position + Vector3.up * 2.5f, new Vector3(2.3f, 2.4f, 2.3f), leaves, 12);
                crown.transform.SetParent(root.transform, true);
            }
            return node;
        }
        static LineRenderer MakeRing(string name, float radius, Material mat, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = Vector3.up * .06f;
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = mat; line.useWorldSpace = false;
            line.loop = true; line.widthMultiplier = .075f; line.positionCount = 64;
            for (int i = 0; i < 64; i++) { float a = i * Mathf.PI * 2 / 64; line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius)); }
            return line;
        }
        static GameObject Primitive(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = layer;
            go.transform.position = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
        static Material Mat(string name, string hex, bool unlit = false)
        {
            string path = Root + "Art/Materials/" + name + ".mat";
            var mat = LoadExistingAsset<Material>(path);
            if (!mat) { mat = new Material(Shader.Find(unlit ? "Unlit/Color" : "Standard")); AssetDatabase.CreateAsset(mat, path); }
            ColorUtility.TryParseHtmlString(hex, out var color); mat.color = color;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", .08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
        static void SetLayer(int index, string name)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(index).stringValue = name;
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
