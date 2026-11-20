using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class CombatSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        bool failed;
        string report = "";
        void Check(bool result, string name) { report += (result ? "PASS " : "FAIL ") + name + "\n"; failed |= !result; }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-combatSmoke") < 0) yield break;
            SmokeTestSetup.DisableEnemyProduction(controller);
            foreach (var combat in FindObjectsByType<UnitCombat>()) combat.autoAcquire = false;
            yield return null;
            var ally = controller.units[0]; var enemy = controller.enemies[0];
            var attack = ally.GetComponent<UnitCombat>(); var response = enemy.GetComponent<UnitCombat>();
            var hp = enemy.GetComponent<UnitHealth>(); var alliedHP = ally.GetComponent<UnitHealth>();
            enemy.Stop(); ally.Stop();
            Check(ally.Agent.Warp(new Vector3(-6, 0, -12)) && enemy.Agent.Warp(new Vector3(4, 0, -12)), "combatants placed on navigation mesh");
            controller.Select(ally);
            Check(controller.IssueAttack(controller.units[1]) == 0 && hp.CurrentHealth == hp.MaxHealth, "friendly fire command rejected");
            Check(controller.IssueAttack(enemy) == 1 && attack.Target == enemy, "enemy attack command accepted");
            yield return new WaitForSeconds(.2f);
            Check(hp.CurrentHealth == hp.MaxHealth, "no damage outside melee range");
            float deadline = Time.time + 6;
            while (hp.CurrentHealth == hp.MaxHealth && Time.time < deadline) yield return null;
            Check(hp.CurrentHealth == hp.MaxHealth - ally.stats.attackDamage, "pursuit reaches enemy and deals configured damage");
            float afterHit = hp.CurrentHealth;
            yield return new WaitForSeconds(.2f);
            Check(hp.CurrentHealth == afterHit, "attack cooldown prevents damage every frame");
            Check(ally.MoveTo(new Vector3(-6, 0, -12)) && attack.Target == null && attack.Order == UnitOrder.Move, "move cancels attack");
            yield return new WaitForSeconds(.3f);
            Check(hp.CurrentHealth == afterHit, "cancelled attack causes no further damage");
            ally.Stop();
            Check(attack.Order == UnitOrder.Hold && attack.Target == null, "stop clears target and holds position");
            ally.Agent.Warp(new Vector3(-2.8f, 0, 1)); enemy.Agent.Warp(new Vector3(2.8f, 0, 1));
            var originalStats = ally.stats;
            ally.stats = Instantiate(originalStats); ally.stats.attackRange = 10;
            attack.Attack(enemy);
            yield return new WaitForSeconds(.2f);
            Check(hp.CurrentHealth == afterHit, "rock blocks melee hit even within configured range");
            ally.Stop(); Destroy(ally.stats); ally.stats = originalStats;
            ally.Agent.Warp(new Vector3(-6, 0, -12)); enemy.Agent.Warp(new Vector3(-4, 0, -12));
            response.autoAcquire = true; response.BeginMove();
            deadline = Time.time + 3;
            while (alliedHP.CurrentHealth == alliedHP.MaxHealth && Time.time < deadline) yield return null;
            Check(response.Target == ally && alliedHP.CurrentHealth < alliedHP.MaxHealth, "enemy automatically acquires nearby ally and attacks");
            enemy.Stop(); response.autoAcquire = false;
            attack.Attack(enemy);
            deadline = Time.time + 9;
            while (enemy.IsAlive && Time.time < deadline) yield return null;
            Check(!enemy.IsAlive && hp.CurrentHealth == 0 && !enemy.gameObject.activeSelf && !enemy.Agent.enabled, "repeated attacks kill and remove enemy");
            yield return null;
            Check(attack.Target == null, "dead target is cleared");
            Check(controller.IssueAttack(enemy) == 0, "dead unit cannot be attacked");
            hp.Heal(1000);
            Check(hp.IsDepleted && !enemy.gameObject.activeSelf, "dead enemy cannot be healed back into battle");
            alliedHP.TakeDamage(10000); controller.PruneSelection();
            Check(!ally.IsSelected && controller.Selected.Count == 0 && !ally.selectionIndicator.activeSelf, "dead ally removed from selection");
            controller.Select(ally);
            Check(controller.Selected.Count == 0, "dead ally cannot be selected again");
            Check(!ally.MoveTo(Vector3.zero) && !attack.Attack(controller.enemies[1]), "dead ally cannot move or attack");
            Check(controller.AlliesAlive == 2 && controller.EnemiesAlive == 2, "remaining army counts exclude casualties");
            foreach (var remaining in controller.enemies) if (remaining.IsAlive) remaining.GetComponent<UnitHealth>().TakeDamage(10000);
            yield return null;
            Check(controller.EnemiesAlive == 0 && !controller.MatchOver, "eliminating opposing army does not end base battle");
            controller.enemyBase.TakeDamage(10000); yield return null;
            Check(controller.Status.Contains("VICTORY"), "destroying opposing base produces victory");
            File.WriteAllText("combat-test.txt", report); Debug.Log(report);
            Application.Quit(failed ? 1 : 0);
        }
    }
}
