using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class MatchResultsSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        static bool restarting, failed;
        string output;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name; failed |= !value;
            Debug.Log(line); File.AppendAllText(output, line + "\n");
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); bool victory = Array.IndexOf(args, "-resultsVictorySmoke") >= 0;
            if (!victory && Array.IndexOf(args, "-resultsSmoke") < 0) yield break;
            output = victory ? "resultsvictory-test.txt" : "results-test.txt";
            var stats = controller.statistics;
            if (restarting)
            {
                yield return null;
                Check(!MatchResultsScreen.IsVisible && !stats.Finished && stats.AlliesLost == 0 && stats.EnemiesLost == 0 && stats.UnitsTrained == 0
                    && stats.FoodDelivered == 0 && stats.BarracksBuilt == 0 && stats.BuildingsLost == 0 && stats.Duration < 1,
                    "new match clears all result counters and elapsed time");
                Application.Quit(failed ? 1 : 0); yield break;
            }
            File.WriteAllText(output, ""); failed = false;
            yield return null; Time.timeScale = 4;
            SmokeTestSetup.DisableEnemyProduction(controller, false); SmokeTestSetup.EnsureWorkers(controller, 2); controller.SetAutoBattle(false); controller.SetAutoScout(false);
            foreach (var unit in controller.units) unit.Stop();
            foreach (var unit in controller.enemies) { unit.Stop(); unit.Agent.Warp(new Vector3(21, 0, 21)); }
            Check(stats.FoodDelivered == 0 && stats.WoodDelivered == 0 && stats.UnitsTrained == 0 && !MatchResultsScreen.IsVisible,
                "starting resources and starting army excluded from earned statistics");
            var main = controller.construction.main; main.TryQueue(3); main.CancelAll();
            Check(stats.FoodDelivered == 0 && stats.UnitsTrained == 0, "cancelled production and refunds do not inflate statistics");
            var workers = Array.FindAll(controller.units, unit => unit.stats.isWorker);
            workers[0].GetComponent<UnitHealth>().TakeDamage(10000); workers[0].Die();
            controller.units[1].GetComponent<UnitHealth>().TakeDamage(10000);
            controller.enemies[0].GetComponent<UnitHealth>().TakeDamage(10000);
            Check(stats.AlliesLost == 2 && stats.WorkersLost == 1 && stats.EnemiesLost == 1, "unit losses counted once for each team including worker subset");
            main.TryQueue(3);
            yield return Until(() => stats.UnitsTrained > 0, 8);
            Check(stats.UnitsTrained == 1, "completed training counted once");
            var worker = workers[1]; worker.Agent.Warp(new Vector3(-20, 0, 12));
            var source = new GameObject("Results food test"); source.transform.position = new Vector3(-21, 0, 12);
            var node = source.AddComponent<ResourceNode>(); node.Extract(195);
            worker.GetComponent<WorkerGatherer>().Gather(node);
            yield return Until(() => stats.FoodDelivered > 0, 15); worker.Stop();
            Check(stats.FoodDelivered == 5 && stats.WoodDelivered == 0, "only delivered harvested food counted and refunds excluded");
            worker.Agent.Warp(new Vector3(-6, 0, 1)); Physics.SyncTransforms();
            var site = controller.construction.Place(worker, StructureKind.Barracks, new Vector3(-6, 0, 6));
            Check(site && stats.BarracksBuilt == 0, "unfinished construction excluded from completed building count");
            if (!site) { Application.Quit(1); yield break; }
            yield return Until(() => site.Complete, 18);
            Check(site.Complete && stats.BarracksBuilt == 1 && stats.ClinicsBuilt == 0, "completed construction recorded once by building type");
            site.Health.TakeDamage(10000, UnitTeam.Enemy);
            Check(stats.BuildingsLost == 1 && stats.BarracksBuilt == 1, "destroyed barracks remains in historical completion count");
            float elapsed = stats.Duration; controller.SetPaused(true);
            yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(stats.Duration - elapsed) < .01f, "match duration excludes pause"); controller.SetPaused(false);
            if (victory) controller.enemyBase.TakeDamage(10000, UnitTeam.Player); else controller.playerBase.TakeDamage(10000, UnitTeam.Enemy);
            Check(stats.Finished && stats.Won == victory && MatchResultsScreen.IsVisible, "results modal opens with correct victory or defeat outcome");
            Check(stats.BuildingsLost == (victory ? 1 : 2) && stats.EnemyBuildingsLost == (victory ? 1 : 0), "decisive base destruction included in final building losses");
            int survivors = stats.Survivors; float duration = stats.Duration;
            controller.enemies[1].GetComponent<UnitHealth>().TakeDamage(10000);
            yield return new WaitForSecondsRealtime(.2f);
            Check(stats.EnemiesLost == 1 && stats.Survivors == survivors && stats.Duration == duration, "final statistics remain frozen after match ends");
            Check(stats.DurationText.Contains(":"), "duration displayed as minutes and seconds");
            restarting = true; controller.RestartMatch();
        }
    }
}
