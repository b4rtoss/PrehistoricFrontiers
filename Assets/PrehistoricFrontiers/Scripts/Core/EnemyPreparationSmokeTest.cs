using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class EnemyPreparationSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-enemyPreparationSmoke") >= 0 && !FindAnyObjectByType<EnemyPreparationSmokeTest>())
                new GameObject("Enemy-only preparation regression").AddComponent<EnemyPreparationSmokeTest>();
        }
        bool failed; string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText("enemypreparation-test.txt", report);
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return null; Time.timeScale = 3;
            var c = FindAnyObjectByType<RTSSelectionController>();
            c.fog.SetFogEnabled(false);
            c.enemyBase.GetComponent<EnemyProductionAI>().enabled = false;
            Check(c.preparationDuration == 20 && c.IsPreparingFor(UnitTeam.Enemy) && !c.IsPreparingFor(UnitTeam.Player),
                "only enemy has twenty second preparation in ordinary scene");
            var player = c.units[0]; var enemy = c.enemies[0];
            player.Agent.Warp(new Vector3(-6, 0, -12)); enemy.Agent.Warp(new Vector3(-4.5f, 0, -12));
            yield return null;
            var start = enemy.transform.position;
            var hp = player.GetComponent<UnitHealth>(); var enemyHp = enemy.GetComponent<UnitHealth>();
            c.Select(player);
            Check(c.IssueAttack(enemy) == 1, "player can order attack immediately while enemy waits");
            yield return new WaitForSeconds(.8f); player.Stop();
            Check(enemyHp.CurrentHealth < enemyHp.MaxHealth && hp.CurrentHealth == hp.MaxHealth,
                "player deals damage before twenty seconds and enemy does not retaliate early");
            Check(!enemy.Agent.hasPath && Vector3.ProjectOnPlane(start - enemy.transform.position, Vector3.up).magnitude < .05f
                && enemy.GetComponent<UnitCombat>().LastAttackAt < 0, "enemy fighter neither scouts nor attacks during preparation");
            c.SetPaused(true); float remaining = c.PreparationRemaining;
            yield return new WaitForSecondsRealtime(.3f);
            Check(c.PreparationRemaining == remaining, "pause freezes enemy-only countdown");
            c.SetPaused(false);
            while (c.IsPreparing) yield return null;
            float deadline = Time.time + 4;
            while (hp.CurrentHealth == hp.MaxHealth && Time.time < deadline) yield return null;
            Check(hp.CurrentHealth < hp.MaxHealth && enemy.GetComponent<UnitCombat>().LastAttackAt >= 20,
                "enemy automatically begins combat only after twenty game seconds");
            c.RestartMatch(); yield return null;
            c = FindAnyObjectByType<RTSSelectionController>();
            Check(c.IsPreparingFor(UnitTeam.Enemy) && !c.IsPreparingFor(UnitTeam.Player) && c.PreparationRemaining > 19,
                "restart resets enemy delay without delaying player");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
