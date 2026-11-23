using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class AutomationControlsSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        bool failed;
        string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText("automationcontrols-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-automationControlsSmoke") < 0) yield break;
            Check(!controller.AutoBattleEnabled && !controller.AutoScoutEnabled, "battle and scouting default to disabled independently");
            SmokeTestSetup.DisableEnemyProduction(controller);
            controller.fog.SetFogEnabled(true); controller.fog.ResetExploration();
            controller.SetAutoBattle(false); controller.SetAutoScout(false); Time.timeScale = 3;
            foreach (var combat in FindObjectsByType<UnitCombat>()) combat.autoAcquire = false;
            yield return null;
            var unit = controller.units[0]; var attack = unit.GetComponent<UnitCombat>();
            var enemy = controller.enemies[0]; var health = enemy.GetComponent<UnitHealth>();
            attack.autoAcquire = true;
            yield return new WaitForSeconds(.4f);
            Check(!controller.AutoBattleEnabled && !controller.AutoScoutEnabled && !attack.Target && !unit.Agent.hasPath, "both modes off leaves unit idle");
            controller.SetAutoScout(true);
            yield return Until(() => attack.Order == UnitOrder.Scout, 2);
            Check(attack.Order == UnitOrder.Scout && !attack.Target && !controller.AutoBattleEnabled, "scout only explores with battle disabled");
            var destination = unit.Agent.destination;
            controller.SetAutoBattle(false);
            Check(attack.Order == UnitOrder.Scout && unit.Agent.hasPath && unit.Agent.destination == destination && controller.AutoScoutEnabled, "battle toggle does not cancel scouting");
            controller.SetAutoScout(false);
            Check(attack.Order == UnitOrder.Idle && !unit.Agent.hasPath && !controller.AutoBattleEnabled, "scout toggle cancels scout path only");
            controller.SetAutoBattle(true);
            yield return new WaitForSeconds(.4f);
            Check(!controller.AutoScoutEnabled && !attack.Target && attack.Order == UnitOrder.Scout && unit.Agent.hasPath, "battle alone searches for unseen enemies without requiring scout toggle");
            destination = unit.Agent.destination;
            controller.SetAutoScout(false);
            Check(attack.Order == UnitOrder.Scout && unit.Agent.hasPath && unit.Agent.destination == destination,
                "scout off preserves search required by enabled auto battle");
            controller.SetAutoBattle(false);
            Check(attack.Order == UnitOrder.Idle && !unit.Agent.hasPath, "battle off stops automatic search when scouting is off");
            controller.SetAutoBattle(true);
            Check(unit.Agent.Warp(new Vector3(-10, 0, 8)), "unit placed near visible opponent"); controller.fog.RefreshNow();
            yield return Until(() => attack.Target == enemy, 2);
            Check(attack.Target == enemy && attack.IsAutomaticAttack && !controller.AutoScoutEnabled, "battle only acquires visible enemy");
            controller.SetAutoScout(false);
            Check(attack.Target == enemy && attack.IsAutomaticAttack && controller.AutoBattleEnabled, "scout toggle does not cancel automatic combat");
            controller.SetAutoBattle(false);
            Check(!attack.Target && !attack.BuildingTarget && !unit.Agent.hasPath && !controller.AutoScoutEnabled, "battle toggle cancels automatic combat only");
            float hp = health.CurrentHealth;
            controller.SetAutoScout(true);
            yield return Until(() => attack.Order == UnitOrder.Scout, 2);
            yield return new WaitForSeconds(.3f);
            Check(attack.Order == UnitOrder.Scout && !attack.Target && health.CurrentHealth == hp, "scout only ignores visible enemies without dealing damage");
            unit.Stop(); controller.SetAutoScout(false);
            unit.Agent.Warp(new Vector3(-10, 0, 8)); controller.fog.RefreshNow();
            Check(attack.Attack(enemy) && !attack.IsAutomaticAttack, "manual attack works with both automation modes off");
            controller.SetAutoBattle(false); controller.SetAutoScout(true); controller.SetAutoScout(false);
            Check(attack.Target == enemy && !attack.IsAutomaticAttack, "both toggles preserve explicit manual attack");
            Check(unit.MoveTo(new Vector3(0, 0, -13)), "manual movement accepted");
            controller.SetAutoBattle(true); controller.SetAutoScout(true);
            yield return new WaitForSeconds(.2f);
            Check(attack.Order == UnitOrder.Move && !attack.Target && !attack.BuildingTarget, "manual move takes priority over both enabled modes");
            unit.Stop(); yield return new WaitForSeconds(.5f);
            Check(attack.Order == UnitOrder.Hold && !unit.Agent.hasPath, "stop holds position with both modes enabled");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
