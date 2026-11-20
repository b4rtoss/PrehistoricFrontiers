using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public enum BuilderState { Idle, Travelling, Building, Blocked }

    public sealed class WorkerBuilder : MonoBehaviour
    {
        sealed class Job
        {
            public ConstructionSite site;
            public Vector3 approach;
        }
        readonly List<Job> queue = new List<Job>();
        public ConstructionSite Site { get; private set; }
        public int QueuedCount => queue.Count;
        public ConstructionSite QueuedSite(int index) => index >= 0 && index < queue.Count ? queue[index].site : null;
        bool CanEditQueue => Site && Site.Health.IsAlive && GetComponent<RTSUnit>().IsAvailable
            && !Site.system.controller.IsPaused && !Site.system.controller.MatchOver;
        public bool MoveQueued(int fromIndex, int toIndex)
        {
            if (!CanEditQueue || fromIndex < 0 || fromIndex >= queue.Count || toIndex < 0 || toIndex >= queue.Count || fromIndex == toIndex) return false;
            var job = queue[fromIndex]; queue.RemoveAt(fromIndex); queue.Insert(toIndex, job);
            return true;
        }
        public bool CancelQueued(int index)
        {
            if (!CanEditQueue || index < 0 || index >= queue.Count) return false;
            var site = queue[index].site; queue.RemoveAt(index);
            if (site && site.Builder == this) site.Builder = null;
            return true;
        }
        public bool CancelCurrent()
        {
            if (!CanEditQueue) return false;
            RemoveSite(Site);
            return true;
        }
        public BuilderState State { get; private set; }
        Vector3 approach;
        float nextRetryAt;
        public string Status
        {
            get
            {
                if (!Site) return "";
                string name = ConstructionSystem.Name(Site.kind);
                string queued = QueuedCount > 0 ? " / В очереди: " + QueuedCount : "";
                if (State == BuilderState.Blocked) return "Нет прохода к стройке: " + name + " / ожидаю" + queued;
                if (State == BuilderState.Travelling) return "Иду к стройке: " + name + queued;
                return "Строю: " + name + " " + Mathf.RoundToInt(Site.Progress * 100) + "%" + queued;
            }
        }
        public bool Assign(ConstructionSite site, Vector3 destination)
        {
            var unit = GetComponent<RTSUnit>();
            if (!site || site.Complete || !site.Health.IsAlive || !unit.IsAvailable || !unit.stats.isWorker
                || site.system.controller.IsPaused || site.system.controller.MatchOver) return false;
            if (Site == site || queue.Exists(job => job.site == site)) return true;
            // A new order must never steal another worker's active or queued job.
            if (site.Builder && site.Builder != this) return false;
            if (Site)
            {
                queue.Add(new Job { site = site, approach = destination });
                site.Builder = this;
                return true;
            }
            unit.Stop();
            StartJob(site, destination);
            return true;
        }
        void StartJob(ConstructionSite site, Vector3 destination)
        {
            Site = site; site.Builder = this; approach = destination;
            nextRetryAt = Time.time + 1;
            State = GetComponent<RTSUnit>().NavigateTo(approach) ? BuilderState.Travelling : BuilderState.Blocked;
        }
        void Advance()
        {
            State = BuilderState.Idle;
            var unit = GetComponent<RTSUnit>();
            if (unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh) unit.Agent.ResetPath();
            while (queue.Count > 0)
            {
                var job = queue[0]; queue.RemoveAt(0);
                if (!job.site || !job.site.Health.IsAlive || job.site.Complete || job.site.Builder != this) continue;
                if (!unit.IsAvailable || job.site.system.controller.MatchOver)
                {
                    if (job.site.Builder == this) job.site.Builder = null;
                    Cancel(); return;
                }
                // Navigation can change while this building waits for its turn.
                if (job.site.system.TryFindApproach(unit, job.site, out var point)) job.approach = point;
                StartJob(job.site, job.approach);
                return;
            }
        }
        public void Finish(ConstructionSite site)
        {
            if (Site != site) return;
            if (site && site.Builder == this) site.Builder = null;
            Site = null;
            Advance();
        }
        public void RemoveSite(ConstructionSite site)
        {
            if (site && site.Builder == this) site.Builder = null;
            queue.RemoveAll(job => job.site == site);
            if (Site == site) { Site = null; Advance(); }
        }
        public void Cancel()
        {
            if (Site && Site.Builder == this) Site.Builder = null;
            foreach (var job in queue) if (job.site && job.site.Builder == this) job.site.Builder = null;
            queue.Clear(); Site = null; State = BuilderState.Idle;
        }
        void Update()
        {
            if (!Site || Site.system.controller.IsPaused) return;
            var unit = GetComponent<RTSUnit>();
            if (!unit.IsAlive || Site.system.controller.MatchOver) { unit.Stop(); return; }
            if (!Site.Health.IsAlive) { RemoveSite(Site); return; }
            var agent = unit.Agent;
            if (!agent || !agent.enabled || !agent.isOnNavMesh) { State = BuilderState.Blocked; return; }
            if (agent.pathPending) { State = BuilderState.Travelling; return; }
            if (Vector3.Distance(transform.position + Vector3.up, Site.Health.SurfacePoint(transform.position + Vector3.up)) <= 2.2f)
            {
                State = BuilderState.Building;
                agent.ResetPath(); Site.Work(Time.deltaTime); return;
            }
            State = agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete ? BuilderState.Travelling : BuilderState.Blocked;
            if (State == BuilderState.Blocked && Time.time >= nextRetryAt)
            {
                nextRetryAt = Time.time + 1;
                if (Site.system.TryFindApproach(unit, Site, out var point)) approach = point;
                if (unit.NavigateTo(approach)) State = BuilderState.Travelling;
            }
        }
        void OnDisable() { Cancel(); }
    }
}
