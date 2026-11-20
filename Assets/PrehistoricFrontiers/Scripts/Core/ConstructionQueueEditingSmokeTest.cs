using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ConstructionQueueEditingSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-constructionQueueEditingSmoke") >= 0)
                new GameObject("Construction queue editing regression").AddComponent<ConstructionQueueEditingSmokeTest>();
        }
        bool failed; string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name; failed |= !value; report += line + "\n";
            Debug.Log(line); File.WriteAllText("constructionqueueediting-test.txt", report);
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
            SmokeTestSetup.DisableEnemyProduction(c,false);c.SetAutoBattle(false);c.SetAutoScout(false);c.SetWorkerProtection(false);
            foreach (var unit in c.units) unit.Stop();
            foreach (var unit in c.enemies) { unit.Stop(); unit.Agent.Warp(new Vector3(21,0,21)); }
            var worker=Array.Find(c.units,u=>u.stats.isWorker);worker.Agent.Warp(new Vector3(12,0,-3));
            var system=c.construction;var wallet=system.main.wallet;c.Select(worker);
            // Keep the unfinished foundation away from the narrow central passage.
            var first=system.Place(worker,StructureKind.Barracks,new Vector3(8,0,-3));
            Check(first,"first construction starts");if(!first){Application.Quit(1);yield break;}
            yield return Until(()=>first.Progress>.12f,8);
            var second=SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Clinic);var third=SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Barracks);
            Check(second && third,"two waiting buildings placed");if(!second || !third){Application.Quit(1);yield break;}
            var builder=worker.GetComponent<WorkerBuilder>();
            Check(builder.Site==first && builder.QueuedSite(0)==second && builder.QueuedSite(1)==third,"readable queue separates active construction from waiting sites");
            Check(builder.MoveQueued(1,0) && builder.QueuedSite(0)==third && builder.QueuedSite(1)==second && builder.Site==first,
                "moving job up changes only waiting order");
            Check(builder.MoveQueued(0,1) && builder.QueuedSite(0)==second && builder.Site==first,"moving job down preserves active building");
            builder.MoveQueued(1,0);
            Check(!builder.MoveQueued(-1,0) && !builder.MoveQueued(0,2) && !builder.CancelQueued(2) && !builder.QueuedSite(-1),
                "invalid queue operations leave assignments unchanged");
            c.SetPaused(true);float progress=first.Progress;
            Check(!builder.MoveQueued(0,1) && !builder.CancelQueued(0) && !builder.CancelCurrent(),"pause blocks queue edits and cancellation");
            yield return new WaitForSecondsRealtime(.2f);
            Check(first.Progress==progress && builder.QueuedCount==2,"pause preserves progress and complete queue");c.SetPaused(false);
            int food=wallet.Food,wood=wallet.Wood;
            Check(builder.CancelQueued(1) && builder.Site==first && builder.QueuedCount==1 && builder.QueuedSite(0)==third && !second.Builder,
                "cancel waiting job releases only that assignment");
            Check(second.Health.IsAlive && !second.Complete && wallet.Food==food && wallet.Wood==wood,
                "removed job retains paid unfinished building without duplicating refunds");
            system.Resume(second);
            Check(builder.QueuedCount==2 && builder.QueuedSite(1)==second && wallet.Food==food && wallet.Wood==wood,
                "removed job can be resumed at end of queue without another payment");
            progress=first.Progress;
            Check(builder.CancelCurrent() && !first.Builder && first.Health.IsAlive && first.Progress==progress
                && builder.Site==third && builder.QueuedSite(0)==second,"cancelling active job advances to reordered next job while preserving unfinished progress");
            yield return Until(()=>third.Complete,25);
            Check(third.Complete && !first.Complete && builder.Site==second,"reordered building actually completes before remaining clinic");
            yield return Until(()=>second.Complete,24);
            Check(second.Complete && second.GetComponent<HealingClinic>() && !builder.Site && builder.QueuedCount==0,
                "last reordered job completes and queue becomes empty");
            Check(!builder.CancelCurrent() && !builder.CancelQueued(0) && !builder.MoveQueued(0,1),"empty queue controls are harmless");
            system.Resume(first);
            Check(builder.Site==first && first.Progress==progress,"interrupted current construction can resume preserved progress");
            var pending=SmokeTestSetup.PlaceOnFreeGround(system,worker,StructureKind.Clinic);
            Check(pending && builder.QueuedCount==1,"new waiting job can be added after editing and resuming queue");
            c.enemyBase.TakeDamage(10000,UnitTeam.Player);yield return null;
            Check(c.MatchOver && !builder.Site && builder.QueuedCount==0 && !builder.CancelCurrent() && !builder.CancelQueued(0),
                "match completion clears queue and rejects later edits");
            Application.Quit(failed ? 1:0);
        }
    }
}
