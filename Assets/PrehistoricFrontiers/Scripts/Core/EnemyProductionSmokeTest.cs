using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class EnemyProductionSmokeTest : MonoBehaviour
    {
        public ProductionBuilding production;
        bool failed;
        string report = "", fileName;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            report += line + "\n"; failed |= !value; Debug.Log(line); File.WriteAllText(fileName, report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            bool destroyed = Array.IndexOf(args, "-enemyProductionDestroySmoke") >= 0;
            bool ended = Array.IndexOf(args, "-enemyProductionEndSmoke") >= 0;
            if (!destroyed && !ended && Array.IndexOf(args, "-enemyProductionSmoke") < 0) yield break;
            fileName = destroyed ? "enemyproductiondestroy-test.txt" : ended ? "enemyproductionend-test.txt" : "enemyproduction-test.txt";
            var controller = production.controller;
            SmokeTestSetup.DisableFog(controller);
            SmokeTestSetup.UseSmallArmyFixture(controller);
            SmokeTestSetup.DisableEnemyAutomation(controller);
            var ai = production.GetComponent<EnemyProductionAI>(); ai.enabled = false;
            controller.SetAutoBattle(false); Time.timeScale = 4;
            foreach (var combat in FindObjectsByType<UnitCombat>()) combat.autoAcquire = false;
            yield return null;
            var player = controller.playerBase.GetComponent<ProductionBuilding>();
            player.workersOnly = false; // Symmetric combat-production fixture.
            var wallet = production.wallet;
            Check(production.Team == UnitTeam.Enemy && player.Team == UnitTeam.Player && wallet != player.wallet, "production and wallet belong to separate teams");
            Check(wallet.Food == 600 && wallet.Wood == 350 && player.wallet.Food == 600 && player.wallet.Wood == 350, "both sides have equal finite starting budgets");
            if (destroyed || ended)
            {
                Check(production.TryQueue(2) && production.TryQueue(0) && wallet.Food == 300 && wallet.Wood == 200, "pending enemy orders charge normal prices");
                int count = controller.enemies.Length; ai.enabled = true;
                (destroyed ? controller.enemyBase : controller.playerBase).TakeDamage(10000);
                yield return null; yield return null;
                Check(controller.MatchOver && production.QueueCount == 0 && wallet.Food == 600 && wallet.Wood == 350, destroyed ? "destroyed enemy base refunds orders before disabling" : "defeat cancels and refunds surviving enemy production");
                Check(!production.TryQueue(0) && !ai.TryPlanNext(), "no orders accepted after match ends");
                yield return new WaitForSeconds(15);
                Check(controller.enemies.Length == count && wallet.Food == 600 && wallet.Wood == 350, "cancelled orders never spawn or charge later");
                Check(player.wallet.Food == 600 && player.wallet.Wood == 350, "enemy refunds never modify player budget");
                Application.Quit(failed ? 1 : 0); yield break;
            }
            for (int i = 0; i < 3; i++) Check(production.Stats(i) == player.Stats(i), "same cost time and stats asset for species " + i);
            var original = controller.enemies;
            var capped = new List<RTSUnit>(original); var temporary = new List<GameObject>();
            while (capped.Count < ProductionBuilding.PopulationLimit)
            {
                var extra = Instantiate(production.unitPrefabs[0], new Vector3(15, 0, -15), Quaternion.identity);
                extra.GetComponent<UnitCombat>().enabled = false; temporary.Add(extra); capped.Add(extra.GetComponent<RTSUnit>());
            }
            controller.enemies = capped.ToArray();
            Check(!production.TryQueue(0) && wallet.Food == 600 && wallet.Wood == 350, "enemy population cap checked against enemy army");
            controller.enemies = original; foreach (var extra in temporary) Destroy(extra);
            for (int i = 0; i < 3; i++) production.TryQueue(0);
            Check(production.QueueCount == 3 && !production.TryQueue(0) && wallet.Food == 360 && wallet.Wood == 260, "enemy has same three parallel slot limit and exact prices");
            production.CancelAll();
            Check(wallet.Food == 600 && wallet.Wood == 350, "enemy cancellation refunds own budget only");
            ai.enabled = true;
            ai.TryPlanNext(); production.CancelAll(); // Next preference is Triceratops.
            wallet.TrySpend(500, 300);
            Check(ai.TryPlanNext() && production.QueueName(0) == "Raptor" && wallet.Food == 20 && wallet.Wood == 20, "AI falls back to affordable raptor when preferred species is too expensive");
            production.CancelAll(); wallet.Refund(500, 300); ai.enabled = false;
            Check(wallet.Food == 600 && wallet.Wood == 350, "cancelled fallback order refunds its exact paid price");
            Check(production.TryQueue(0) && wallet.Food == 520 && wallet.Wood == 320 && player.wallet.Food == 600, "enemy order charges enemy wallet only");
            yield return new WaitForSeconds(1);
            Check(production.Progress > 0 && production.Progress < 1 && controller.enemies.Length == 3, "enemy training respects duration");
            production.CancelAll();
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.layer = 9;
            blocker.transform.position = Vector3.zero; blocker.transform.localScale = new Vector3(100, 10, 100); Physics.SyncTransforms();
            production.TryQueue(0); yield return new WaitForSeconds(6);
            Check(production.QueueCount == 1 && production.Progress == 1 && controller.enemies.Length == 3 && wallet.Food == 520, "blocked enemy exit waits without duplicate charges");
            Destroy(blocker); yield return null; Physics.SyncTransforms();
            yield return Until(() => controller.enemies.Length == 4, 5); yield return null;
            var first = controller.enemies[controller.enemies.Length - 1];
            Check(first.team == UnitTeam.Enemy && first.Agent.isOnNavMesh && first.GetComponent<UnitHealth>().CurrentHealth == first.stats.maxHealth, "enemy reinforcement spawns on navigation mesh with full health");
            Check(Array.IndexOf(controller.units, first) < 0 && Array.IndexOf(production.healthBars.units, first) >= 0 && first.GetComponent<UnitCombat>().battleController == controller, "reinforcement registered only in enemy army and indicators");
            Check(first.GetComponentInChildren<Renderer>().sharedMaterial == production.unitPrefabs[0].GetComponentInChildren<Renderer>().sharedMaterial && first.transform.Find("EnemyTeamRing"), "reinforcement preserves enemy appearance and team ring");
            controller.Select(first);
            Check(!first.IsSelected && controller.Selected.Count == 0, "enemy reinforcement rejects player control");
            var ally = controller.units[0]; var allyHP = ally.GetComponent<UnitHealth>();
            first.Agent.Warp(new Vector3(11, 0, -1)); ally.Agent.Warp(new Vector3(12, 0, -1));
            first.Stop(); ally.Stop(); first.GetComponent<UnitCombat>().autoAcquire = true; controller.SetAutoBattle(true);
            first.GetComponent<UnitCombat>().SetAutomation(true);
            yield return Until(() => allyHP.CurrentHealth < allyHP.MaxHealth, 3);
            Check(first.GetComponent<UnitCombat>().Target == ally && allyHP.CurrentHealth < allyHP.MaxHealth, "new reinforcement automatically engages nearby attackers");
            controller.SetAutoBattle(false);
            foreach (var enemy in controller.enemies) if (enemy.IsAlive) enemy.GetComponent<UnitHealth>().TakeDamage(10000);
            Check(controller.EnemiesAlive == 0 && !controller.MatchOver, "enemy production can recover a wiped out army");
            var attack = ally.GetComponent<UnitCombat>(); attack.autoAcquire = true;
            ally.Agent.Warp(new Vector3(6, 0, 4)); ally.Stop(); controller.SetAutoBattle(true);
            yield return Until(() => attack.BuildingTarget == controller.enemyBase, 3);
            Check(attack.BuildingTarget == controller.enemyBase && attack.IsAutomaticAttack, "automatic siege begins when defenders are gone");
            Check(production.TryQueue(0), "enemy queues replacement with no surviving troops");
            yield return Until(() => controller.enemies.Length == 5, 8); yield return null;
            var replacement = controller.enemies[controller.enemies.Length - 1];
            // Let the new defender approach: the economic fixture otherwise holds enemy units.
            var replacementCombat = replacement.GetComponent<UnitCombat>();
            replacementCombat.autoAcquire = true; replacementCombat.SetAutomation(true);
            yield return Until(() => attack.Target == replacement || Vector3.Distance(ally.transform.position, replacement.transform.position) <= ally.stats.detectionRadius, 8);
            Check(attack.Target == replacement || Vector3.Distance(ally.transform.position, replacement.transform.position) <= ally.stats.detectionRadius,
                "replacement defender actually reaches automatic siege detection radius");
            yield return Until(() => attack.Target == replacement, .7f);
            Check(attack.Target == replacement && !attack.BuildingTarget, "automatic siege reacts to approaching reinforcement defender");
            replacementCombat.autoAcquire = false; replacement.Stop();
            controller.SetAutoBattle(false);
            ai.enabled = true;
            Check(ai.TryPlanNext() && production.QueueCount == 1, "enemy AI places next affordable paid order");
            Check(ai.TryPlanNext() && ai.TryPlanNext() && production.QueueCount == 3, "enemy AI fills three parallel slots");
            int foodAfterOrder = wallet.Food, woodAfterOrder = wallet.Wood;
            Check(!ai.TryPlanNext() && production.QueueCount == 3 && wallet.Food == foodAfterOrder && wallet.Wood == woodAfterOrder, "AI cannot exceed three active production slots");
            yield return Until(() => production.QueueCount == 0 && ai.Status == "Ожидает ресурсы", 50);
            Check(wallet.Food == 0 && wallet.Wood == 70 && controller.enemies.Length == 8, "finite budget buys exactly five reinforcements including affordable fallback");
            Check(production.QueueCount == 0 && ai.Status == "Ожидает ресурсы", "AI stops ordering when budget is exhausted");
            Check(controller.enemies[5].stats.displayName == "Raptor" && controller.enemies[6].stats.displayName == "Triceratops" && controller.enemies[7].stats.displayName == "Mammoth", "parallel enemy orders complete by their training durations");
            Check(!controller.AutoBattleEnabled && controller.enemies.Length == 8, "production continues while combat automation is off");
            int total = controller.enemies.Length;
            yield return new WaitForSeconds(3);
            Check(controller.enemies.Length == total && wallet.Food == 0 && wallet.Wood == 70 && !ai.TryPlanNext(), "no free resources or reinforcements appear after exhaustion");
            Check(player.wallet.Food == 600 && player.wallet.Wood == 350, "all enemy spending leaves player wallet unchanged");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
