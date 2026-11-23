using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class AutoBattleSearchSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autoBattleSearchSmoke") >= 0)
                new GameObject("Battle-only search regression").AddComponent<AutoBattleSearchSmokeTest>();
        }
        bool failed;
        string report = "";
        void Check(bool value, string name)
        {
            var line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText("autobattlesearch-test.txt", report);
        }
        IEnumerator Start()
        {
            yield return null;
            Time.timeScale = 4;
            var c = FindAnyObjectByType<RTSSelectionController>();
            var fighter = c.units[0]; var origin = fighter.transform.position;
            Check(c.fog.Active && !c.IsPreparingFor(UnitTeam.Player) && c.IsPreparingFor(UnitTeam.Enemy) && !c.AutoBattleEnabled && !c.AutoScoutEnabled && c.VisibleEnemies == 0,
                "player starts immediately while only enemy has preparation delay");
            c.SetAutoBattle(true);
            yield return new WaitForSeconds(.4f);
            var combat = fighter.GetComponent<UnitCombat>();
            Check(c.AutoBattleEnabled && !c.AutoScoutEnabled && fighter.Agent.hasPath && combat.Order == UnitOrder.Scout,
                "G alone starts searching within 0.4 seconds with H off");
            yield return new WaitForSeconds(2);
            Check(Vector3.Distance(origin, fighter.transform.position) > .5f, "automatic search actually moves the fighter");
            c.SetAutoBattle(false);
            Check(!fighter.Agent.hasPath && combat.Order == UnitOrder.Idle, "G off stops search when H is off");
            c.SetAutoBattle(true);
            bool discovered = false, fought = false, feedback = false;
            float end = Time.time + 180;
            while (!c.MatchOver && Time.time < end)
            {
                discovered |= c.VisibleEnemies > 0;
                foreach (var u in c.enemies) if (u) fought |= u.GetComponent<UnitHealth>().CurrentHealth < u.stats.maxHealth;
                foreach (var u in c.units) if (u) feedback |= u.GetComponent<UnitHitReaction>().HitCount > 0;
                yield return null;
            }
            Check(!c.AutoScoutEnabled, "battle search never silently enables the independent H toggle");
            Check(discovered, "G alone discovers enemies in a normal match");
            Check(fought && feedback, "G alone reaches combat and hit reactions still work");
            Check(c.MatchOver, "G alone resolves a normal fog match within 180 seconds after search starts");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
