using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ParallelProductionSmokeTest : MonoBehaviour
    {
        public ProductionBuilding production;
        bool failed;
        string report = "";
        void Check(bool value, string name)
        {
            var line = (value ? "PASS " : "FAIL ") + name;
            report += line + "\n"; failed |= !value; Debug.Log(line); File.WriteAllText("parallelproduction-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        bool DistinctSpawns(RTSUnit[] units)
        {
            for (int i = 3; i < units.Length; i++)
                for (int j = 3; j < i; j++)
                    if (Vector3.Distance(units[i].transform.position, units[j].transform.position) < units[i].stats.radius + units[j].stats.radius) return false;
            return true;
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-parallelProductionSmoke") < 0) yield break;
            var controller = production.controller;
            SmokeTestSetup.DisableEnemyProduction(controller);
            controller.playerBase.GetComponent<ProductionBuilding>().workersOnly = false; controller.SetAutoBattle(false); Time.timeScale = 4;
            yield return null;
            Check(ProductionBuilding.QueueLimit == 3 && ProductionBuilding.PopulationLimit == 20, "three production slots and twenty unit population limit");
            Check(production.TryQueue(0) && production.TryQueue(0) && production.TryQueue(0), "three simultaneous orders accepted");
            int food = production.wallet.Food, wood = production.wallet.Wood;
            Check(!production.TryQueue(0) && production.wallet.Food == food && production.wallet.Wood == wood, "fourth order rejected without resource charge");
            yield return new WaitForSeconds(1);
            float progress = production.SlotProgress(2);
            Check(progress > 0 && Mathf.Approximately(progress, production.SlotProgress(0)) && Mathf.Approximately(progress, production.SlotProgress(1)), "three equal orders advance together");
            production.Cancel(1);
            Check(production.QueueCount == 2 && Mathf.Approximately(production.SlotProgress(0), progress) && Mathf.Approximately(production.SlotProgress(1), progress), "cancelling middle slot preserves other timers");
            Check(production.wallet.Food == food + 80 && production.wallet.Wood == wood + 30, "slot cancellation refunds exactly one paid order");
            Check(production.TryQueue(0) && production.SlotProgress(2) == 0 && production.SlotProgress(0) == progress, "new order starts at zero without resetting ongoing work");
            production.Cancel(0);
            Check(production.SlotProgress(0) == progress && production.SlotProgress(1) == 0, "cancelling first slot preserves remaining timers");
            production.CancelAll();
            Check(production.wallet.Food == 600 && production.wallet.Wood == 350, "all cancelled parallel orders fully refunded");
            for (int i = 0; i < 3; i++) production.TryQueue(0);
            float started = Time.time;
            yield return Until(() => production.QueueCount == 0, 7); yield return null;
            Check(controller.units.Length == 6 && Time.time - started < 6, "three player raptors complete in one five second training period");
            Check(DistinctSpawns(controller.units), "same frame player spawns do not overlap");
            foreach (var unit in controller.units) Check(unit.Agent.isOnNavMesh, unit.name + " has valid navigation after parallel spawn");
            var enemy = controller.enemyBase.GetComponent<ProductionBuilding>();
            var ai = enemy.GetComponent<EnemyProductionAI>(); ai.enabled = true;
            Check(ai.TryPlanNext() && ai.TryPlanNext() && ai.TryPlanNext() && enemy.QueueCount == 3, "enemy AI can fill three parallel slots");
            food = enemy.wallet.Food; wood = enemy.wallet.Wood;
            Check(!ai.TryPlanNext() && enemy.wallet.Food == food && enemy.wallet.Wood == wood, "enemy AI respects slot cap without extra spending");
            ai.enabled = false;
            yield return new WaitForSeconds(1);
            Check(enemy.SlotProgress(0) > 0 && enemy.SlotProgress(1) > 0 && enemy.SlotProgress(2) > 0, "enemy species train simultaneously");
            enemy.CancelAll();
            Check(enemy.wallet.Food == 600 && enemy.wallet.Wood == 350, "enemy parallel cancellation refunds all orders");
            for (int i = 0; i < 3; i++) enemy.TryQueue(0);
            started = Time.time;
            yield return Until(() => enemy.QueueCount == 0, 7); yield return null;
            Check(controller.enemies.Length == 6 && Time.time - started < 6, "three enemy raptors complete in one five second training period");
            Check(DistinctSpawns(controller.enemies), "same frame enemy spawns do not overlap");
            Check(production.wallet.Food == 360 && production.wallet.Wood == 260 && enemy.wallet.Food == 360 && enemy.wallet.Wood == 260, "both teams pay identical prices for parallel production");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
