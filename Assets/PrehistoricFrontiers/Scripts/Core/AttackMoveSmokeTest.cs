using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class AttackMoveSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        string report = "";
        bool failed;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText("attackmove-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-attackMoveSmoke") < 0) yield break;
            yield return null;
            Time.timeScale = 3;
            SmokeTestSetup.DisableEnemyProduction(controller);
            controller.SetAutoBattle(false); controller.SetAutoScout(false);
            foreach (var unit in controller.units) unit.Stop();
            foreach (var unit in controller.enemies) { unit.Stop(); unit.Agent.Warp(new Vector3(21, 0, 19)); }
            var ally = controller.units[0]; var combat = ally.GetComponent<UnitCombat>();
            var enemy = controller.enemies[0]; var goal = new Vector3(-21, 0, 15);
            ally.Agent.Warp(new Vector3(-21, 0, -12)); controller.Select(ally);
            controller.ArmAttackMove();
            Check(controller.AttackMovePending, "attack move can be armed for selected combat unit");
            Check(controller.IssueAttackMove(goal) == 1 && combat.HasAttackMove && combat.Order == UnitOrder.AttackMove && !controller.AttackMovePending,
                "manual route accepted with both automation modes disabled");
            yield return new WaitForSeconds(.3f);
            Check(!combat.Target && !combat.BuildingTarget && ally.Agent.hasPath, "route ignores distant enemies even when map visibility allows them");
            enemy.Agent.Warp(new Vector3(-21, 0, -3));
            enemy.GetComponent<UnitHealth>().TakeDamage(enemy.stats.maxHealth - 10);
            yield return Until(() => combat.Target == enemy, 3);
            Check(combat.Target == enemy && combat.HasAttackMove && !combat.IsAutomaticAttack, "route engages nearby enemy as explicit manual order");
            controller.SetAutoBattle(true); controller.SetAutoBattle(false); controller.SetAutoScout(true); controller.SetAutoScout(false);
            Check(combat.HasAttackMove && combat.Target == enemy, "G and H toggles preserve route and its ongoing combat");
            yield return Until(() => !enemy.IsAlive, 6);
            Check(!enemy.IsAlive, "attack move deals damage and kills encountered enemy");
            yield return Until(() => !combat.HasAttackMove, 12);
            Check(Vector3.Distance(ally.transform.position, goal) < 1.2f && combat.Order == UnitOrder.Hold, "route resumes after combat and holds at destination");
            ally.Agent.Warp(new Vector3(-21, 0, -12));
            Check(combat.AttackMove(goal), "new route accepted after arrival");
            var destination = combat.AttackMoveDestination;
            Check(!combat.AttackMove(new Vector3(1000, 0, 1000)) && combat.HasAttackMove && combat.AttackMoveDestination == destination, "unreachable destination preserves existing route");
            Check(ally.MoveTo(new Vector3(-18, 0, -10)) && !combat.HasAttackMove && combat.Order == UnitOrder.Move, "ordinary move cancels attack route");
            combat.AttackMove(goal); ally.Stop();
            Check(!combat.HasAttackMove && !ally.Agent.hasPath && combat.Order == UnitOrder.Hold, "stop cancels route and movement");
            var defender = controller.enemies[1]; defender.Stop(); defender.Agent.Warp(new Vector3(-21, 0, -3));
            combat.AttackMove(goal);
            yield return Until(() => combat.Target == defender, 3);
            Check(combat.Target == defender, "second route can engage another defender");
            controller.fog.SetFogEnabled(true); controller.fog.RefreshNow();
            defender.Agent.Warp(new Vector3(21, 0, 21)); controller.fog.RefreshNow();
            yield return new WaitForSeconds(.5f);
            Check(!combat.Target && combat.HasAttackMove && combat.Order == UnitOrder.AttackMove && ally.Agent.hasPath,
                "losing vision releases enemy and resumes original route without hidden pursuit");
            controller.fog.SetFogEnabled(false);
            defender.Agent.Warp(new Vector3(-21, 0, -2));
            Check(combat.Attack(defender) && !combat.HasAttackMove, "explicit attack replaces route"); ally.Stop();
            controller.ArmAttackMove(); controller.Clear();
            Check(!controller.AttackMovePending, "clearing selection cancels pending destination input");
            var production = controller.playerBase.GetComponent<ProductionBuilding>(); production.TryQueue(3);
            yield return Until(() => controller.units.Length == 4, 8);
            var worker = controller.units[controller.units.Length - 1];
            controller.Select(worker); controller.ArmAttackMove();
            Check(worker.stats.isWorker && !controller.AttackMovePending && !worker.GetComponent<UnitCombat>().AttackMove(goal), "workers cannot arm or receive attack move");
            controller.Select(ally); controller.Select(controller.units[1], true); controller.Select(worker, true);
            defender.Agent.Warp(new Vector3(21, 0, 21));
            Check(controller.IssueAttackMove(new Vector3(-15, 0, 12)) == 2 && !worker.GetComponent<UnitCombat>().HasAttackMove,
                "mixed group routes combat units only");
            Check(Vector3.Distance(combat.AttackMoveDestination, controller.units[1].GetComponent<UnitCombat>().AttackMoveDestination) > 3,
                "group destinations leave formation spacing");
            foreach (var unit in controller.units) unit.Stop();
            ally.Agent.Warp(new Vector3(18, 0, 10));
            Check(combat.AttackMove(new Vector3(10, 0, 12)), "route towards enemy base accepted");
            float baseHealth = controller.enemyBase.CurrentHealth;
            yield return Until(() => controller.enemyBase.CurrentHealth < baseHealth, 8);
            Check(combat.BuildingTarget == controller.enemyBase && combat.HasAttackMove && !combat.IsAutomaticAttack
                && controller.enemyBase.CurrentHealth < baseHealth, "manual route engages nearby base and deals damage with automation disabled");
            controller.playerBase.TakeDamage(10000); yield return null;
            Check(!combat.HasAttackMove && !ally.Agent.hasPath, "match end cancels attack route");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
