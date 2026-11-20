using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ConstructionQueueSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-constructionQueueSmoke") >= 0)
                new GameObject("Construction queue regression").AddComponent<ConstructionQueueSmokeTest>();
        }
        bool failed;
        string report = "";
        void Check(bool result, string name)
        {
            string line = (result ? "PASS " : "FAIL ") + name;
            report += line + "\n"; failed |= !result;
            Debug.Log(line); File.WriteAllText("constructionqueue-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
        }
        IEnumerator Start()
        {
            yield return null; Time.timeScale = 4;
            var c = FindAnyObjectByType<RTSSelectionController>();
            SmokeTestSetup.DisableEnemyProduction(c, false); c.SetAutoBattle(false); c.SetAutoScout(false); c.SetWorkerProtection(false);
            foreach (var unit in c.units) unit.Stop();
            foreach (var unit in c.enemies) { unit.Stop(); unit.Agent.Warp(new Vector3(21,0,21)); }
            var system = c.construction; var wallet = system.main.wallet;
            wallet.Refund(3000,3000);
            var worker = Array.Find(c.units, u => u.stats.isWorker);
            worker.Agent.Warp(new Vector3(-6,0,1));
            var first = SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Barracks);
            Check(first, "first building placement accepted / " + system.Message);
            if (!first) { Application.Quit(1); yield break; }
            yield return Until(() => first.Progress > .12f, 8);
            var builder = worker.GetComponent<WorkerBuilder>();
            Check(first.Progress > .12f && !first.Complete,"first building is actively under construction");
            int food = wallet.Food, wood = wallet.Wood;
            var second = SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Clinic);
            var third = SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Barracks);
            Check(builder.Site == first && first.Builder == builder && builder.QueuedCount == 2,"placing more buildings preserves current builder and queues both sites");
            Check(second.Builder == builder && third.Builder == builder && second.Progress == 0 && third.Progress == 0,"waiting sites retain assignment without premature construction");
            Check(wallet.Food == food-100 && wallet.Wood == wood-270,"queued buildings pay their exact costs once");
            c.Select(worker); system.Resume(second); system.Resume(first);
            Check(builder.Site == first && builder.QueuedCount == 2,"repeated resume clicks do not interrupt or duplicate jobs");
            float progress = first.Progress;
            yield return new WaitForSeconds(.5f);
            Check(first.Progress > progress && second.Progress == 0,"worker continues first building after new placement");
            c.SetPaused(true); progress = first.Progress;
            yield return new WaitForSecondsRealtime(.2f);
            Check(first.Progress == progress && second.Progress == 0 && builder.QueuedCount == 2,"pause freezes current construction and preserves queue");
            c.SetPaused(false);
            third.Health.TakeDamage(10000,UnitTeam.Enemy);
            Check(builder.Site == first && first.Builder == builder && builder.QueuedCount == 1,"destroying queued building leaves current construction untouched");
            yield return Until(() => first.Complete,18);
            Check(first.Complete && builder.Site == second && second.Builder == builder && builder.QueuedCount == 0,"completing first building automatically starts next job");
            yield return Until(() => second.Complete,20);
            Check(second.Complete && second.GetComponent<HealingClinic>() && !builder.Site && builder.QueuedCount == 0,"queued clinic completes and worker becomes free");

            SmokeTestSetup.EnsureWorkers(c,2); yield return null;
            var workers = Array.FindAll(c.units,u => u.stats.isWorker);
            var other = workers[1]; worker.Stop(); other.Stop();
            worker.Agent.Warp(new Vector3(12,0,-3)); other.Agent.Warp(new Vector3(-18,0,-15)); Physics.SyncTransforms();
            var parallelFirst = SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Barracks);
            var parallelSecond = SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Clinic);
            var otherBuilder = other.GetComponent<WorkerBuilder>();
            Check(parallelFirst.Builder == builder && parallelSecond.Builder == otherBuilder && builder.Site == parallelFirst,
                "free second worker takes new building without reassigning first worker");
            yield return Until(() => parallelFirst.Progress > 0 && parallelSecond.Progress > 0,15);
            Check(parallelFirst.Progress > 0 && parallelSecond.Progress > 0,"two workers construct different buildings concurrently");
            var queued = SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Barracks);
            Check(builder.Site == parallelFirst && builder.QueuedCount == 1,"when both workers are busy new building waits for selected worker");
            worker.Stop();
            Check(!builder.Site && builder.QueuedCount == 0 && !parallelFirst.Builder && !queued.Builder && parallelSecond.Builder == otherBuilder,
                "explicit stop clears only this worker's active and queued assignments");
            system.Resume(parallelFirst); system.Resume(queued);
            Check(builder.Site == parallelFirst && builder.QueuedCount == 1,"unfinished sites can be resumed and queued without paying again");
            parallelFirst.Health.TakeDamage(10000,UnitTeam.Enemy);
            Check(builder.Site == queued && queued.Builder == builder,"destroying active building advances worker to next surviving job");
            yield return new WaitForSeconds(.3f); // Let NavMesh carving settle after building destruction.
            var pending = SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Clinic);
            Check(builder.QueuedCount == 1 && pending.Builder == builder,"new job can be queued after previous building destruction");
            worker.GetComponent<UnitHealth>().TakeDamage(10000);
            Check(!queued.Builder && !pending.Builder && builder.QueuedCount == 0,"worker death releases all assigned sites");
            c.Select(other); system.Resume(queued);
            Check(otherBuilder.Site == parallelSecond && otherBuilder.QueuedCount == 1,"surviving worker can queue an abandoned site without dropping current job");
            c.enemyBase.TakeDamage(10000,UnitTeam.Player);
            yield return null;
            Check(c.MatchOver && !otherBuilder.Site && otherBuilder.QueuedCount == 0 && !parallelSecond.Builder && !queued.Builder,
                "match completion clears active construction and queued assignments");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
