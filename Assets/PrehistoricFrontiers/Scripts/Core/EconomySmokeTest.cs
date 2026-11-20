using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class EconomySmokeTest : MonoBehaviour
    {
        public ProductionBuilding production;
        bool failed;
        string report = "";
        void Check(bool value, string name) { report += (value ? "PASS " : "FAIL ") + name + "\n"; failed |= !value; }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-economySmoke") < 0) yield break;
            var controller = production.controller;
            SmokeTestSetup.DisableEnemyProduction(controller);
            controller.playerBase.GetComponent<ProductionBuilding>().workersOnly = false;
            controller.SetAutoBattle(false); Time.timeScale = 4;
            yield return null;
            var wallet = production.wallet;
            int initialFood = wallet.Food, initialWood = wallet.Wood;
            Check(initialFood == 600 && initialWood == 350, "starting food and wood available");
            Check(!production.TryQueue(-1) && !production.TryQueue(production.unitPrefabs.Length) && wallet.Food == initialFood, "invalid unit types do not spend resources");
            for (int i = 0; i < 3; i++) Check(production.TryQueue(0), "production slot " + (i + 1) + " accepts raptor");
            Check(!production.TryQueue(0) && production.QueueCount == 3 && wallet.Food == initialFood - 240 && wallet.Wood == initialWood - 90, "full production rejects fourth order without charging");
            production.CancelAll();
            Check(wallet.Food == initialFood && wallet.Wood == initialWood && production.QueueCount == 0, "cancellation fully refunds all paid orders");
            production.TryQueue(2); production.TryQueue(2);
            int beforeFood = wallet.Food, beforeWood = wallet.Wood;
            Check(!production.TryQueue(2) && wallet.Food == beforeFood && wallet.Wood == beforeWood && production.QueueCount == 2, "insufficient resources cannot create or charge order");
            production.CancelAll();
            production.TryQueue(0);
            yield return new WaitForSeconds(1);
            Check(production.Progress > 0 && production.Progress < 1 && controller.units.Length == 3, "training takes time before unit appears");
            int originalCost = production.Stats(0).foodCost;
            production.Stats(0).foodCost += 999;
            production.Cancel(0); production.Stats(0).foodCost = originalCost;
            Check(wallet.Food == initialFood && wallet.Wood == initialWood && production.Progress == 0, "active order refunds original paid cost and resets progress");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Test blocked production exit"; blocker.layer = 9;
            blocker.transform.position = Vector3.zero; blocker.transform.localScale = new Vector3(100, 10, 100);
            Physics.SyncTransforms(); production.TryQueue(0);
            yield return new WaitForSeconds(6);
            Check(production.QueueCount == 1 && production.Progress == 1 && controller.units.Length == 3, "blocked exit waits without losing completed order");
            Check(wallet.Food == initialFood - 80 && wallet.Wood == initialWood - 30, "blocked exit never charges twice");
            Destroy(blocker); yield return null; Physics.SyncTransforms();
            float deadline = Time.time + 5;
            while (controller.units.Length < 4 && Time.time < deadline) yield return null;
            yield return null;
            Check(controller.units.Length == 4 && production.QueueCount == 0, "cleared exit releases completed animal");
            var first = controller.units[controller.units.Length - 1];
            Check(first.team == UnitTeam.Player && first.Agent.isOnNavMesh && first.GetComponent<UnitHealth>().CurrentHealth == first.stats.maxHealth, "reinforcement has correct team navigation and full health");
            Check(Array.IndexOf(production.healthBars.units, first) >= 0 && first.GetComponent<UnitCombat>().battleController == controller, "reinforcement registered for indicators and auto battle");
            controller.Select(first); Check(first.IsSelected && controller.Selected.Contains(first), "reinforcement can be selected"); controller.Clear();
            Check(production.TryQueue(2) && production.TryQueue(1) && production.TryQueue(0), "mixed parallel orders accepted longest first");
            beforeFood = wallet.Food; beforeWood = wallet.Wood;
            float startedAt = Time.time;
            yield return new WaitForSeconds(1);
            Check(production.SlotProgress(0) > 0 && production.SlotProgress(1) > 0 && production.SlotProgress(2) > 0, "all three animal types train at once");
            deadline = Time.time + 16;
            while (production.QueueCount > 0 && Time.time < deadline) yield return null;
            yield return null;
            Check(controller.units.Length == 7 && production.QueueCount == 0, "mixed queue completes all three orders");
            Check(controller.units.Length >= 7 && controller.units[4].stats.displayName == "Raptor" && controller.units[5].stats.displayName == "Triceratops" && controller.units[6].stats.displayName == "Mammoth" && Time.time - startedAt < 16, "shorter orders complete first and all finish within longest training duration");
            Check(wallet.Food == beforeFood && wallet.Wood == beforeWood, "completion does not spend resources again");
            var originalArmy = controller.units;
            var capped = new List<RTSUnit>(originalArmy);
            var temporary = new List<GameObject>();
            while (capped.Count < ProductionBuilding.PopulationLimit)
            {
                var extra = Instantiate(production.unitPrefabs[0], new Vector3(15, 0, -15), Quaternion.identity);
                extra.GetComponent<UnitCombat>().enabled = false;
                temporary.Add(extra); capped.Add(extra.GetComponent<RTSUnit>());
            }
            controller.units = capped.ToArray();
            Check(!production.TryQueue(0) && wallet.Food == beforeFood && wallet.Wood == beforeWood, "population cap rejects order without charging");
            controller.units = originalArmy;
            foreach (var extra in temporary) Destroy(extra);
            Check(production.TryQueue(0), "production resumes below population cap");
            foreach (var enemy in controller.enemies) enemy.GetComponent<UnitHealth>().TakeDamage(10000);
            controller.enemyBase.TakeDamage(10000);
            yield return null; yield return null;
            Check(production.QueueCount == 0 && wallet.Food == beforeFood && wallet.Wood == beforeWood, "match end cancels and refunds unfinished queue");
            Check(!production.TryQueue(0), "production disabled after match ends");
            File.WriteAllText("economy-test.txt", report); Debug.Log(report);
            Application.Quit(failed ? 1 : 0);
        }
    }
}
