using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ManualStartSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-manualStartSmoke") >= 0)
                new GameObject("Manual start regression").AddComponent<ManualStartSmokeTest>();
        }
        bool failed; string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name; failed |= !value; report += line + "\n";
            Debug.Log(line); File.WriteAllText("manualstart-test.txt",report);
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return null; Time.timeScale = 4;
            var c = FindAnyObjectByType<RTSSelectionController>();
            Check(!c.AutoBattleEnabled && !c.AutoScoutEnabled, "saved scene starts with allied battle and scouting disabled");
            Check(!c.IsPreparingFor(UnitTeam.Player) && c.IsPreparingFor(UnitTeam.Enemy), "saved scene delays only enemy combat");
            SmokeTestSetup.DisableEnemyProduction(c,false);
            var positions=new Vector3[c.units.Length];for(int i=0;i<c.units.Length;i++)positions[i]=c.units[i].transform.position;
            yield return new WaitForSeconds(1);
            bool stationary=true,idle=true;
            for(int i=0;i<c.units.Length;i++)
            {
                var unit=c.units[i];var combat=unit.GetComponent<UnitCombat>();
                stationary &= Vector3.Distance(unit.transform.position,positions[i])<.05f;
                idle &= !unit.Agent.hasPath && !combat.Target && !combat.BuildingTarget && combat.Order!=UnitOrder.Scout;
            }
            Check(!c.IsPreparing && stationary && idle, "allied units wait for player orders without a preparation phase");
            Check(!c.AutoBattleEnabled && !c.AutoScoutEnabled, "instant match start does not enable allied automation");
            Check(c.AutoBattleFor(UnitTeam.Enemy) && c.AutoScoutFor(UnitTeam.Enemy), "allied defaults leave enemy automation independent");
            var enemy=c.enemies[0];enemy.Agent.Warp(c.units[0].transform.position+Vector3.right*4);
            yield return new WaitForSeconds(.5f);
            Check(!c.units[0].GetComponent<UnitCombat>().Target && !c.units[0].Agent.hasPath, "visible enemies do not trigger unsolicited allied pursuit");
            c.SetAutoBattle(true);c.SetAutoScout(true);
            yield return new WaitForSeconds(.5f);
            Check(c.AutoBattleEnabled && c.AutoScoutEnabled && c.units[0].GetComponent<UnitCombat>().Target,
                "explicitly enabling automation still starts allied combat");
            var previous=c;c.RestartMatch();yield return null;
            c=FindAnyObjectByType<RTSSelectionController>();
            Check(c && c!=previous && !c.AutoBattleEnabled && !c.AutoScoutEnabled, "restart restores disabled automation even after both modes were enabled");
            Check(c && !c.IsPreparingFor(UnitTeam.Player) && c.IsPreparingFor(UnitTeam.Enemy) && c.AlliesAlive==4, "restart preserves enemy-only preparation and complete starting army");
            Application.Quit(failed ? 1:0);
        }
    }
}
