using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class AutoBattleSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        bool failed;
        string report = "";
        void Check(bool value, string name) { report += (value ? "PASS " : "FAIL ") + name + "\n"; failed |= !value; }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autoBattleSmoke") < 0) yield break;
            SmokeTestSetup.DisableFog(controller); SmokeTestSetup.SkipPreparation(controller);
            controller.SetAutoBattle(true); controller.SetAutoScout(true);
            Time.timeScale = 3;
            yield return new WaitForSeconds(.4f);
            Check(controller.AutoBattleEnabled, "auto battle enabled by explicit player choice");
            var all = FindObjectsByType<UnitCombat>();
            int expected = 0, targets = 0;
            foreach (var combat in all)
            {
                var unit = combat.GetComponent<RTSUnit>();
                if (!unit || !unit.IsAlive || unit.stats.isWorker) continue;
                expected++;
                if (combat.Target && combat.Target.IsAlive && combat.IsAutomaticAttack && combat.Target.team != unit.team) targets++;
            }
            Check(expected > 0 && targets == expected, "all living combat units acquire live enemy targets without input (" + targets + "/" + expected + ")");
            controller.SetAutoBattle(false);
            yield return new WaitForSeconds(.5f);
            bool paused = true;
            foreach (var combat in all) if (combat.GetComponent<RTSUnit>().team == UnitTeam.Player) paused &= combat.Target == null && !combat.GetComponent<RTSUnit>().Agent.hasPath;
            Check(paused && !controller.AutoBattleEnabled, "toggle off cancels allied automatic pursuit only");
            controller.Select(controller.units[0]);
            Check(controller.IssueAttack(controller.enemies[0]) == 1, "manual attack remains available with automation off");
            controller.SetAutoBattle(false);
            Check(controller.units[0].GetComponent<UnitCombat>().Target == controller.enemies[0], "automation toggle preserves manual attack");
            controller.units[0].Stop();
            controller.SetAutoBattle(true);
            yield return new WaitForSeconds(.4f);
            Check(controller.units[0].GetComponent<UnitCombat>().IsAutomaticAttack, "toggle on resumes held unit");
            Check(controller.units[1].MoveTo(new Vector3(0, 0, -13)), "manual movement accepted during auto battle");
            yield return new WaitForSeconds(.3f);
            Check(controller.units[1].GetComponent<UnitCombat>().Order == UnitOrder.Move && !controller.units[1].GetComponent<UnitCombat>().Target, "automatic targeting respects manual movement");
            float deadline = Time.time + 90;
            bool playerDamaged = false, enemyDamaged = false;
            while (!controller.MatchOver && Time.time < deadline)
            {
                foreach (var unit in controller.units) playerDamaged |= unit.GetComponent<UnitHealth>().CurrentHealth < unit.stats.maxHealth;
                foreach (var unit in controller.enemies) enemyDamaged |= unit.GetComponent<UnitHealth>().CurrentHealth < unit.stats.maxHealth;
                yield return null;
            }
            yield return null;
            Check(playerDamaged && enemyDamaged, "both armies inflict damage automatically");
            Check(controller.MatchOver && (!controller.playerBase.IsAlive || !controller.enemyBase.IsAlive), "automatic match destroys a base within 90 game seconds");
            Check(controller.Status.Contains("VICTORY") || controller.Status.Contains("DEFEAT"), "automatic match shows result");
            File.WriteAllText("autobattle-test.txt", report); Debug.Log(report);
            Application.Quit(failed ? 1 : 0);
        }
    }
}
