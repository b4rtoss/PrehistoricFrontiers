using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class StartingArmySmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        string report = "";
        bool failed;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText("startingarmy-test.txt", report);
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-startingArmySmoke") < 0) yield break;
            controller.SetAutoBattle(false); controller.SetAutoScout(false);
            yield return null;
            SmokeTestSetup.DisableEnemyProduction(controller, false);
            foreach (var unit in controller.units) unit.Stop();
            var main = controller.construction.main;
            Check(controller.AlliesAlive == 4, "four living player units present at start");
            foreach (var name in new[] { "Raptor", "Triceratops", "Mammoth", "Worker" })
                Check(Array.FindAll(controller.units, unit => unit.stats.displayName == name).Length == 1, "one starting " + name);
            bool navigation = true, overlap = false, workersReady = true, registered = true;
            foreach (var unit in controller.units)
            {
                navigation &= unit.Agent.enabled && unit.Agent.isOnNavMesh && unit.GetComponent<UnitHealth>().CurrentHealth == unit.stats.maxHealth;
                registered &= Array.IndexOf(main.healthBars.units, unit) >= 0 && unit.GetComponent<UnitCombat>().battleController == controller;
                if (unit.stats.isWorker) workersReady &= unit.GetComponent<WorkerGatherer>().home == main && !unit.GetComponent<WorkerGatherer>().IsRetreating;
                foreach (var other in controller.units) if (other != unit)
                    overlap |= Vector3.Distance(unit.transform.position, other.transform.position) < unit.Agent.radius + other.Agent.radius;
            }
            Check(navigation, "all starting units have valid navigation and full health");
            Check(!overlap, "starting units do not overlap each other");
            Check(workersReady && registered, "starting workers reference base and all units registered in HUD and combat");
            Check(main.wallet.Food == 1000 && main.wallet.Wood == 1000, "player starts with one thousand food and wood");
            Check(main.wallet.CanAfford(ConstructionSystem.FoodCost(StructureKind.Barracks) + ConstructionSystem.FoodCost(StructureKind.Clinic),
                ConstructionSystem.WoodCost(StructureKind.Barracks) + ConstructionSystem.WoodCost(StructureKind.Clinic)), "starting wallet covers both buildings together");
            Check(controller.enemies.Length == 4 && Array.FindAll(controller.enemies,u=>u.stats.isWorker).Length == 1
                && controller.enemyBase.GetComponent<ProductionBuilding>().wallet.Food == 1000
                && controller.enemyBase.GetComponent<ProductionBuilding>().wallet.Wood == 1000,
                "enemy starts with matching army and resources");
            Check(main.ArmyAlive < ProductionBuilding.PopulationLimit && main.TryQueue(3), "initial population leaves room to produce another worker");
            main.CancelAll();
            Application.Quit(failed ? 1 : 0);
        }
    }
}
