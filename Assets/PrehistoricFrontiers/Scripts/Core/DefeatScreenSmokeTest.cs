using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class DefeatScreenSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        static bool restarting, failed;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name; failed |= !value;
            Debug.Log(line); File.AppendAllText("defeatscreen-test.txt", line + "\n");
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-defeatScreenSmoke") < 0) yield break;
            if (restarting)
            {
                yield return null;
                Check(!MatchResultsScreen.IsVisible && !controller.MatchOver && controller.playerBase.IsAlive, "restart closes defeat screen and restores both bases");
                Check(controller.AlliesAlive == 4 && controller.construction.main.wallet.Food == 1000 && controller.construction.main.wallet.Wood == 1000,
                    "restart restores starting army and resources");
                foreach (var name in new[] { "Raptor", "Triceratops", "Mammoth", "Worker" })
                    Check(Array.FindAll(controller.units, u => u && u.IsAlive && u.stats.displayName == name).Length == 1,
                        "restart restores exactly one " + name);
                Check(Time.timeScale == 1 && !AudioListener.pause && !controller.IsPaused, "restart starts unpaused at normal speed");
                Application.Quit(failed ? 1 : 0); yield break;
            }
            File.WriteAllText("defeatscreen-test.txt", ""); failed = false;
            yield return null;
            SmokeTestSetup.DisableEnemyProduction(controller, false); controller.SetAutoBattle(false); controller.SetAutoScout(false);
            Check(!MatchResultsScreen.IsVisible, "defeat screen hidden during active match");
            controller.Select(controller.units[0]);
            controller.construction.main.TryQueue(3);
            controller.playerBase.TakeDamage(10000, UnitTeam.Enemy); yield return null;
            Check(controller.MatchOver && MatchResultsScreen.IsVisible, "destroying own base shows defeat screen");
            Check(BattleHUD.IsOverPanel(new Vector3(Screen.width / 2, Screen.height / 2, 0)), "defeat screen blocks battlefield input");
            Check(controller.IssueMove(new Vector3(0, 0, -5)) == 0 && controller.IssueAttackMove(new Vector3(0, 0, -5)) == 0,
                "new unit orders rejected after defeat");
            Check(!controller.GetComponent<BattleMinimap>().FocusAt(BattleMinimap.MapRect.center), "minimap cannot move camera through defeat overlay");
            Check(controller.construction.main.QueueCount == 0, "pending production refunded before restart");
            yield return new WaitForSecondsRealtime(.2f);
            restarting = true; controller.RestartMatch();
        }
    }
}
