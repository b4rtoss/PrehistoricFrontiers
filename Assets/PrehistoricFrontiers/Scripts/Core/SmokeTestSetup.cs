using UnityEngine;
using System.Collections.Generic;

namespace PrehistoricFrontiers
{
    public static class SmokeTestSetup
    {
        public static void DisableFog(RTSSelectionController controller) { if (controller.fog) controller.fog.SetFogEnabled(false); }
        public static void SkipPreparation(RTSSelectionController controller) { controller.preparationDuration = 0; }
        static void StopEnemyUnit(RTSUnit unit) { unit.GetComponent<UnitCombat>().autoAcquire = false; unit.Stop(); }
        public static void DisableEnemyAutomation(RTSSelectionController controller)
        {
            SkipPreparation(controller);
            var economy = controller.enemyBase.GetComponent<EnemyEconomyAI>();
            if (economy) economy.enabled = false;
            foreach (var unit in controller.enemies) if (unit && unit.IsAlive) StopEnemyUnit(unit);
            var production = controller.enemyBase.GetComponent<ProductionBuilding>();
            if (production) production.UnitProduced += StopEnemyUnit;
        }
        public static ConstructionSite PlaceOnFreeGround(ConstructionSystem system, RTSUnit worker, StructureKind kind)
        {
            Physics.SyncTransforms();
            var points = new List<Vector3> { new Vector3(-6,0,6), new Vector3(-6,0,-1), new Vector3(8,0,-3),
                new Vector3(8,0,7), new Vector3(-6,0,15), new Vector3(8,0,-13), new Vector3(-18,0,-3), new Vector3(15,0,4) };
            for (int x = -18; x <= 18; x += 6) for (int z = -18; z <= 18; z += 6) points.Add(new Vector3(x,0,z));
            foreach (var point in points)
            {
                if (!system.CanPlace(worker, kind, point, out _, out _)) continue;
                var site = system.Place(worker, kind, point);
                if (site) return site;
            }
            throw new System.InvalidOperationException("No free construction test location");
        }
        public static void EnsureWorkers(RTSSelectionController controller, int count)
        {
            var main = controller.construction.main;
            var units = new List<RTSUnit>(controller.units);
            int workers = units.FindAll(u => u && u.IsAlive && u.stats.isWorker).Count;
            while (workers < count)
            {
                Physics.SyncTransforms();
                if (!main.FindSpawnPosition(main.Stats(3).radius, out var position))
                    throw new System.InvalidOperationException("No space for test worker");
                var unit = Object.Instantiate(main.unitPrefabs[3], position, Quaternion.identity).GetComponent<RTSUnit>();
                unit.Agent.enabled = true;
                unit.team = UnitTeam.Player;
                unit.GetComponent<UnitCombat>().battleController = controller;
                unit.GetComponent<WorkerGatherer>().home = main;
                units.Add(unit); workers++;
            }
            controller.units = units.ToArray();
            var bars = new List<RTSUnit>(units); bars.AddRange(controller.enemies);
            main.healthBars.units = bars.ToArray();
        }
        // Isolated fixture: three combat units and a 600/350 resource wallet.
        public static void UseSmallArmyFixture(RTSSelectionController controller)
        {
            SkipPreparation(controller);
            var economy = controller.enemyBase.GetComponent<EnemyEconomyAI>();
            if (economy) economy.enabled = false;
            var enemies = new List<RTSUnit>();
            foreach (var enemy in controller.enemies)
            {
                if (enemy.stats.isWorker) { enemy.Stop(); enemy.gameObject.SetActive(false); }
                else enemies.Add(enemy);
            }
            controller.enemies = enemies.ToArray();
            foreach (var node in Object.FindObjectsByType<ResourceNode>())
                if (node.name.StartsWith("Enemy ")) node.gameObject.SetActive(false);
            var enemyMain = controller.enemyBase.GetComponent<ProductionBuilding>();
            enemyMain.workersOnly = false;
            enemyMain.unitPrefabs = new[] { enemyMain.unitPrefabs[0], enemyMain.unitPrefabs[1], enemyMain.unitPrefabs[2] };
            enemyMain.wallet.Initialize(600,350);
            if (controller.units.Length <= 3) return;
            for (int i = 3; i < controller.units.Length; i++) { controller.units[i].Stop(); controller.units[i].gameObject.SetActive(false); }
            controller.units = new[] { controller.units[0], controller.units[1], controller.units[2] };
            var production = controller.playerBase.GetComponent<ProductionBuilding>();
            production.wallet.Initialize(600, 350);
            var units = new List<RTSUnit>(controller.units); units.AddRange(controller.enemies); production.healthBars.units = units.ToArray();
            controller.Clear();
        }
        public static void DisableEnemyProduction(RTSSelectionController controller, bool useSmallArmy = true)
        {
            if (useSmallArmy) UseSmallArmyFixture(controller);
            DisableFog(controller);
            DisableEnemyAutomation(controller);
            var ai = controller.enemyBase.GetComponent<EnemyProductionAI>();
            if (!ai) return;
            ai.enabled = false;
            ai.production.CancelAll();
        }
    }
}
