using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class MatchEndStopSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-matchEndStopSmoke") >= 0 || Array.IndexOf(args, "-matchEndStopVictorySmoke") >= 0
                || Array.IndexOf(args, "-matchEndStopFallbackSmoke") >= 0)
                new GameObject("Match end stop regression").AddComponent<MatchEndStopSmokeTest>();
        }
        string output, report = "";
        bool failed;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name; failed |= !value;
            report += line + "\n"; Debug.Log(line); File.WriteAllText(output, report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        bool AllStopped(RTSSelectionController controller, bool settled = false)
        {
            var all = new List<RTSUnit>(controller.units); all.AddRange(controller.enemies);
            foreach (var unit in all)
            {
                if (!unit || !unit.IsAlive) continue;
                // NavMeshAgent reports the preceding simulation tick's velocity until its next update.
                // Check paths and orders immediately, and require zero measured velocity after settling.
                if (unit.Agent.hasPath || settled && unit.Agent.velocity.sqrMagnitude > .0025f || unit.GetComponent<UnitCombat>().Order != UnitOrder.Hold)
                {
                    Debug.Log("Stop diagnostic / " + unit.name + " / path=" + unit.Agent.hasPath + " / velocity=" + unit.Agent.velocity + " / order=" + unit.GetComponent<UnitCombat>().Order);
                    return false;
                }
                var worker = unit.GetComponent<WorkerGatherer>();
                if (worker && (worker.AutoGatherActive || worker.IsRetreating || worker.State != GatherState.Idle)) return false;
                var builder = unit.GetComponent<WorkerBuilder>();
                if (builder && builder.Site) return false;
            }
            return true;
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            bool victory = Array.IndexOf(args, "-matchEndStopVictorySmoke") >= 0;
            bool fallback = Array.IndexOf(args, "-matchEndStopFallbackSmoke") >= 0;
            output = victory ? "matchendstopvictory-test.txt" : fallback ? "matchendstopfallback-test.txt" : "matchendstop-test.txt";
            yield return null;
            var controller = FindAnyObjectByType<RTSSelectionController>();
            Time.timeScale = 3;
            SmokeTestSetup.DisableEnemyProduction(controller, false); SmokeTestSetup.EnsureWorkers(controller, 3); controller.SetAutoBattle(false); controller.SetAutoScout(false);
            foreach (var unit in controller.units) unit.Stop();
            for (int i = 0; i < controller.enemies.Length; i++)
            { controller.enemies[i].Stop(); controller.enemies[i].Agent.Warp(new Vector3(18 - i * 5, 0, 16)); }
            var workers = Array.FindAll(controller.units, unit => unit.stats.isWorker);
            workers[1].Agent.Warp(new Vector3(-6, 0, 1)); Physics.SyncTransforms();
            var site = controller.construction.Place(workers[1], StructureKind.Barracks, new Vector3(-6, 0, 6));
            Check(site, "construction active before match end");
            var nodeObject = new GameObject("Match end resource"); nodeObject.transform.position = new Vector3(-21, 0, 12);
            var node = nodeObject.AddComponent<ResourceNode>();
            workers[0].Agent.Warp(new Vector3(-20, 0, 12)); workers[0].GetComponent<WorkerGatherer>().Gather(node);
            workers[2].Agent.Warp(new Vector3(-18, 0, 12)); workers[2].GetComponent<WorkerGatherer>().Gather(node);
            yield return Until(() => workers[0].GetComponent<WorkerGatherer>().Carried > 0, 5);
            var worker = workers[0]; var gatherer = worker.GetComponent<WorkerGatherer>();
            int cargo = gatherer.Carried;
            Check(cargo > 0 && worker.MoveTo(new Vector3(-21, 0, -14)) && gatherer.State == GatherState.Idle,
                "worker carrying cargo accepts manual movement while gather state is idle");
            var fighter = controller.units[0]; fighter.Agent.Warp(new Vector3(13, 0, -14));
            Check(fighter.GetComponent<UnitCombat>().AttackMove(new Vector3(13, 0, 14))
                && controller.enemies[0].MoveTo(new Vector3(20, 0, -14)), "allied attack route and enemy movement active before end");
            yield return new WaitForSeconds(.25f);
            Check(worker.Agent.hasPath && worker.Agent.velocity.sqrMagnitude > .01f, "manual worker is actually moving before decisive event");
            controller.Select(fighter); controller.ArmAttackMove();
            int stock = node.Remaining, food = controller.construction.main.wallet.Food;
            if (fallback) controller.playerBase.gameObject.SetActive(false);
            else (victory ? controller.enemyBase : controller.playerBase).TakeDamage(10000);
            if (!fallback) Check(AllStopped(controller), "base destruction synchronously stops every living unit on both teams");
            yield return null;
            Check(controller.MatchOver && AllStopped(controller), "match end update leaves all live units held with no navigation paths");
            Check(!controller.AttackMovePending && !controller.construction.Pending && !fighter.GetComponent<UnitCombat>().HasAttackMove,
                "match end cancels pending commands and attack route");
            var position = worker.transform.position; float progress = site ? site.Progress : 0;
            yield return new WaitForSecondsRealtime(.5f);
            Check(Vector3.Distance(position, worker.transform.position) < .02f && !worker.Agent.hasPath,
                "manually moving worker remains stationary after result screen appears");
            Check(AllStopped(controller, true) && node.Remaining == stock && controller.construction.main.wallet.Food == food
                && gatherer.Carried == cargo, "no gathering movement or cargo loss after match end");
            Check(!site || site.Progress == progress && !site.Builder, "construction stops and builder detaches while saved progress remains");
            Check(controller.statistics.Finished && controller.statistics.Won == victory && MatchResultsScreen.IsVisible,
                "results and final outcome remain correct");
            Check(controller.IssueMove(Vector3.zero) == 0, "new movement orders rejected after match end");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
