using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ProductionTimingSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-productionTimingSmoke") >= 0)
                new GameObject("Production timing regression").AddComponent<ProductionTimingSmokeTest>();
        }
        bool failed;
        string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            report += line + "\n"; failed |= !value;
            Debug.Log(line); File.WriteAllText("productiontiming-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
        }
        IEnumerator Start()
        {
            yield return null; Time.timeScale = 3;
            var c = FindAnyObjectByType<RTSSelectionController>();
            SmokeTestSetup.DisableEnemyProduction(c, false); c.SetAutoBattle(false); c.SetAutoScout(false);
            foreach (var u in c.units) u.Stop(); foreach (var u in c.enemies) { u.Stop(); if(!u.stats.isWorker)u.Agent.Warp(new Vector3(21,0,21)); }
            var enemyMain=c.enemyBase.GetComponent<ProductionBuilding>();var ai=enemyMain.GetComponent<EnemyProductionAI>();
            var enemyConstruction=enemyMain.GetComponent<EnemyEconomyAI>().construction;
            var worker=Array.Find(c.units,u=>u.stats.isWorker);worker.Agent.Warp(new Vector3(-6,0,1));
            var site=c.construction.Place(worker,StructureKind.Barracks,new Vector3(-6,0,6));
            var enemyWorker=Array.Find(c.enemies,u=>u.stats.isWorker);enemyWorker.Agent.Warp(new Vector3(12,0,-3));
            var enemySite=enemyConstruction.Place(enemyWorker,StructureKind.Barracks,new Vector3(8,0,-3));
            Check(site && enemySite,"both factions pay for real barracks construction");
            if(!site || !enemySite){Application.Quit(1);yield break;}
            yield return Until(()=>site.Complete && enemySite.Complete,25);
            var player=site.GetComponent<ProductionBuilding>();var enemy=enemySite.GetComponent<ProductionBuilding>();
            Check(player && enemy && player.Team==UnitTeam.Player && enemy.Team==UnitTeam.Enemy,"both own barracks complete before timing comparison");
            if(!player || !enemy){Application.Quit(1);yield break;}
            c.preparationDuration=c.statistics.Duration+2;
            int food=enemy.wallet.Food;
            Check(player.TryQueue(0) && !enemy.TryQueue(0) && enemy.wallet.Food==food,"preparation blocks only enemy combat training and leaves player available");
            player.CancelAll();
            yield return Until(()=>!c.IsPreparing,3);
            var enemyTimes = new Dictionary<string, float>();
            enemy.UnitProduced += u => { if (!enemyTimes.ContainsKey(u.stats.displayName)) enemyTimes[u.stats.displayName] = Time.time; };
            var playerTimes = new Dictionary<string, float>();
            player.UnitProduced += u => { if (!playerTimes.ContainsKey(u.stats.displayName)) playerTimes[u.stats.displayName] = Time.time; u.Stop(); };
            float started = Time.time;
            for (int i = 0; i < 3; i++)
            {
                Check(player.Stats(i) == enemy.Stats(i), "both teams share training time and costs for " + player.Stats(i).displayName);
                Check(player.TryQueue(i), "player can queue " + player.Stats(i).displayName);
            }
            ai.enabled=true;
            for(int i=0;i<3;i++) ai.TryPlanNext();
            yield return Until(() => enemy.QueueCount == 3, 1);
            Check(enemy.QueueCount == 3 && player.QueueCount == 3, "both sides use the same three simultaneous slots in their own barracks");
            c.SetPaused(true);
            float playerProgress = player.SlotProgress(0), enemyProgress = enemy.SlotProgress(0);
            yield return new WaitForSecondsRealtime(.25f);
            Check(player.SlotProgress(0) == playerProgress && enemy.SlotProgress(0) == enemyProgress, "pause freezes both sides equally");
            c.SetPaused(false);
            yield return Until(() => playerTimes.Count == 3 && enemyTimes.Count == 3, 18);
            foreach (var name in new[] { "Raptor", "Triceratops", "Mammoth" })
            {
                bool present = playerTimes.ContainsKey(name) && enemyTimes.ContainsKey(name);
                Check(present && Mathf.Abs(playerTimes[name] - enemyTimes[name]) <= .8f,
                    "actual " + name + " spawn times differ by at most 0.8 game seconds / "
                    + (present ? (playerTimes[name] - started).ToString("F2") + " vs " + (enemyTimes[name] - started).ToString("F2") : "missing spawn"));
            }
            ai.enabled = false; enemy.CancelAll();
            enemy.wallet.Refund(80, 30); // Isolate unlock persistence from the remaining finite budget.
            site.Health.TakeDamage(10000, UnitTeam.Enemy);
            ai.enabled = true;
            Check(ai.TryPlanNext(), "destroying player barracks cannot stop independent enemy production");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
