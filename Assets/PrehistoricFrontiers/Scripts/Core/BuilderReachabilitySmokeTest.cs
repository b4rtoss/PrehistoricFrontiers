using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public sealed class BuilderReachabilitySmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-builderReachabilitySmoke") >= 0)
                new GameObject("Builder reachability regression").AddComponent<BuilderReachabilitySmokeTest>();
        }
        bool failed;
        string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n";
            Debug.Log(line); File.WriteAllText("builderreachability-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
        }
        bool Reachable(RTSUnit worker, Vector3 target)
        {
            var path = new NavMeshPath();
            return worker.Agent.enabled && worker.Agent.isOnNavMesh && worker.Agent.CalculatePath(target, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }
        Vector3 FreePoint(ConstructionSystem system, RTSUnit worker)
        {
            Physics.SyncTransforms();
            foreach (var point in new[] { new Vector3(8,0,-3), new Vector3(8,0,6), new Vector3(-6,0,-1), new Vector3(-6,0,15), new Vector3(15,0,4) })
                if (system.CanPlace(worker,StructureKind.Barracks,point,out _,out _)) return point;
            throw new InvalidOperationException("No valid free point for reachability test / " + system.Message);
        }
        IEnumerator Start()
        {
            yield return null; Time.timeScale = 4;
            var c = FindAnyObjectByType<RTSSelectionController>();
            SmokeTestSetup.DisableEnemyProduction(c, false); c.SetAutoBattle(false); c.SetAutoScout(false); c.SetWorkerProtection(false);
            SmokeTestSetup.EnsureWorkers(c, 3); yield return null;
            foreach (var unit in c.units) unit.Stop();
            foreach (var unit in c.enemies) { unit.Stop(); unit.Agent.Warp(new Vector3(21,0,21)); }
            var workers = Array.FindAll(c.units, u => u.stats.isWorker);
            var selected = workers[0]; var blocked = workers[1]; var available = workers[2];
            selected.Agent.Warp(new Vector3(-6,0,-18)); blocked.Agent.Warp(new Vector3(6,0,-18)); available.Agent.Warp(new Vector3(-6,0,1));
            c.Select(selected);
            var wall = new GameObject("Navigation barrier across battlefield"); wall.layer = 9; wall.transform.position = new Vector3(0,2,-10);
            var collider = wall.AddComponent<BoxCollider>(); collider.size = new Vector3(60,4,1);
            var obstacle = wall.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = collider.size; obstacle.carving = true; obstacle.carveOnlyStationary = false;
            Physics.SyncTransforms();
            var target = new Vector3(-6,0,6);
            yield return Until(() => !Reachable(selected, target) && !Reachable(blocked, target), 4);
            Check(selected.Agent.isOnNavMesh && blocked.Agent.isOnNavMesh && !Reachable(selected,target) && !Reachable(blocked,target)
                && Reachable(available,target), "real carved barrier blocks selected and first free workers but leaves third worker reachable");
            var system = c.construction; var wallet = system.main.wallet;
            Check(system.CanPlace(selected,StructureKind.Barracks,target,out _,out _), "placement preview accepts reachable alternative despite selected worker being blocked");
            int food = wallet.Food, wood = wallet.Wood;
            var first = system.Place(selected,StructureKind.Barracks,target);
            Check(first && first.Builder && first.Builder.GetComponent<RTSUnit>() == available,
                "placement skips both blocked workers and assigns reachable third worker");
            if (!first) { Application.Quit(1); yield break; }
            Check(wallet.Food == food && wallet.Wood == wood-150 && !selected.Agent.hasPath && !blocked.Agent.hasPath,
                "fallback charges once and preserves blocked workers' orders");
            yield return new WaitForSeconds(.5f); // Allow NavMesh carving to settle between placement clicks.
            var secondPoint = FreePoint(system,available);
            var second = system.Place(selected,StructureKind.Clinic,secondPoint);
            var builder = available.GetComponent<WorkerBuilder>();
            Check(second && second.Builder == builder && builder.Site == first && builder.QueuedCount == 1,
                "reachable busy worker queues new building when every free worker is blocked / " + system.Message);
            if (!second) { Application.Quit(1); yield break; }
            available.Stop(); system.Resume(first);
            Check(first.Builder == builder && builder.Site == first && !second.Builder,
                "resume searches past blocked selected and free workers to find reachable builder");
            var probePoint = FreePoint(system,available);
            available.Stop(); available.Agent.enabled = false;
            int sites = system.Sites.Count; food = wallet.Food; wood = wallet.Wood;
            Check(!system.CanPlace(selected,StructureKind.Barracks,probePoint,out _,out _),
                "preview rejects placement when all workers are blocked or navigation is disabled");
            Check(!system.Place(selected,StructureKind.Barracks,probePoint) && system.Sites.Count == sites
                && wallet.Food == food && wallet.Wood == wood, "unreachable placement creates no building and spends no resources");
            system.Resume(second);
            Check(!second.Builder, "unreachable resume leaves unfinished building unassigned");
            available.Agent.enabled = true;
            c.SetPaused(true);
            Check(!system.Place(selected,StructureKind.Barracks,probePoint) && wallet.Food == food && wallet.Wood == wood,
                "alternative builder cannot bypass paused match restrictions");
            c.SetPaused(false);
            wallet.TrySpend(wallet.Food,wallet.Wood);
            Check(!system.CanPlace(selected,StructureKind.Barracks,probePoint,out _,out _)
                && !system.Place(selected,StructureKind.Barracks,probePoint) && system.Sites.Count == sites,
                "alternative builder cannot bypass construction costs");
            wallet.Refund(food,wood);
            system.Resume(first);
            yield return Until(() => first.Complete,20);
            Check(first.Complete, "fallback builder actually reaches and completes first building");
            system.Resume(second);
            yield return Until(() => second.Complete,22);
            Check(second.Complete && second.GetComponent<HealingClinic>(), "fallback resume actually completes previously abandoned clinic");
            wall.SetActive(false); Physics.SyncTransforms();
            yield return Until(() => Reachable(selected,probePoint),4);
            var preferredSite = system.Place(selected,StructureKind.Barracks,probePoint);
            Check(preferredSite && preferredSite.Builder && preferredSite.Builder.GetComponent<RTSUnit>() == selected,
                "selected free worker regains priority when its path becomes reachable");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
