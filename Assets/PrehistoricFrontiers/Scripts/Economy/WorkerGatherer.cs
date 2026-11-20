using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public enum GatherState { Idle, Travelling, Gathering, Returning, Blocked, Retreating }
    [RequireComponent(typeof(RTSUnit))]
    public sealed class WorkerGatherer : MonoBehaviour
    {
        public ProductionBuilding home;
        public bool autoGatherEnabled = true;
        public bool AutoGatherActive { get; private set; }
        public bool IsRetreating { get; private set; }
        bool retreatHasPath;
        public ResourceKind DesiredKind { get; private set; }
        public string StatusMessage { get; private set; } = "Готов к работе";
        public const int CarryCapacity = 20;
        public int Carried { get; private set; }
        public ResourceKind CarriedKind { get; private set; }
        public ResourceNode Node { get; private set; }
        public GatherState State { get; private set; }
        RTSUnit unit;
        float nextHarvestAt, retryAt;
        bool retryHome;
        Vector3 destination;
        float travelCheckAt, lastDistance;
        void Awake() { unit = GetComponent<RTSUnit>(); }
        public bool Gather(ResourceNode node)
        {
            if (!unit.IsAvailable || !unit.stats.isWorker || !home || home.controller.MatchOver || !node || node.Remaining <= 0 || !unit.Agent.isOnNavMesh) return false;
            if (!home.controller.HasExplored(unit.team, node.transform.position)) return false;
            unit.Stop(); Node = node; DesiredKind = node.kind; AutoGatherActive = autoGatherEnabled;
            if (Carried > 0) HeadHome(); else HeadToNode();
            return true;
        }
        public bool ReturnToBase()
        {
            if (!unit.IsAvailable || !home || home.controller.MatchOver || Carried == 0) return false;
            unit.Stop(); Node = null; HeadHome(); return true;
        }
        public void Cancel() { GetComponent<WorkerBuilder>()?.Cancel(); IsRetreating = false; retreatHasPath = false; Node = null; State = GatherState.Idle; AutoGatherActive = false; StatusMessage = "Остановлен"; }
        public bool RetreatForProtection()
        {
            if (!unit.IsAvailable || !unit.stats.isWorker || unit.team != UnitTeam.Player || !home || !home.controller
                || home.controller.MatchOver || !home.controller.WorkerProtectionEnabled) return false;
            if (IsRetreating) return true;
            unit.Stop();
            IsRetreating = true; State = GatherState.Retreating;
            AttemptRetreat();
            return true;
        }
        void AttemptRetreat()
        {
            retreatHasPath = unit.Agent && unit.Agent.isOnNavMesh && NavigateNear(home.depositPoint, 1);
            retryAt = Time.time + 1;
            StatusMessage = retreatHasPath ? "Под атакой! Отступаю к базе" : "Отступление: нет прохода к базе, ожидаю";
        }
        void UpdateRetreat()
        {
            if (!retreatHasPath)
            {
                if (Time.time >= retryAt) AttemptRetreat();
                return;
            }
            if (unit.Agent.pathPending) return;
            float distance = Vector3.Distance(transform.position, destination);
            if (distance > .7f)
            {
                if (Time.time >= travelCheckAt)
                {
                    travelCheckAt = Time.time + 3;
                    if (!unit.Agent.hasPath || lastDistance - distance < .15f) AttemptRetreat();
                    lastDistance = distance;
                }
                return;
            }
            unit.Agent.ResetPath();
            DepositCargo();
            Carried = 0; IsRetreating = false; retreatHasPath = false; State = GatherState.Idle;
            StatusMessage = "У базы: жду нового приказа";
        }
        void DepositCargo()
        {
            if (Carried <= 0) return;
            home.wallet.Refund(CarriedKind == ResourceKind.Food ? Carried : 0, CarriedKind == ResourceKind.Wood ? Carried : 0);
            home.controller.statistics?.RecordResources(unit.team, CarriedKind, Carried);
        }
        void OnDisable() { Cancel(); Carried = 0; }
        bool NavigateNear(Vector3 center, float radius)
        {
            return TryApproach(center, radius, out var choice, out _) && NavigateToApproach(choice);
        }
        public bool TryApproach(Vector3 center, float radius, out Vector3 choice, out float best)
        {
            best = float.PositiveInfinity;
            bool found = false;
            choice = Vector3.zero;
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI / 4;
                var candidate = center + (i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius);
                if (!NavMesh.SamplePosition(candidate, out var hit, 1, unit.Agent.areaMask)) continue;
                bool occupied = false;
                foreach (var collider in Physics.OverlapSphere(hit.position + Vector3.up * .6f, unit.stats.radius + .15f, 1 << 10))
                    if (collider.GetComponentInParent<RTSUnit>() != unit) { occupied = true; break; }
                if (occupied) continue;
                if (!UnitNavigation.TryPath(unit, hit.position, out var path)) continue;
                float distance = UnitNavigation.PathLength(path);
                if (distance < best) { best = distance; choice = hit.position; found = true; }
            }
            return found;
        }
        bool NavigateToApproach(Vector3 choice)
        {
            if (!unit.NavigateTo(choice)) return false;
            destination = choice; lastDistance = Vector3.Distance(transform.position, choice); travelCheckAt = Time.time + 3; return true;
        }
        void Block(bool goingHome) { State = GatherState.Blocked; retryHome = goingHome; retryAt = Time.time + 1; StatusMessage = goingHome ? "Нет прохода к базе, ожидаю" : "Нет доступного источника, ожидаю"; }
        void StopWithMessage(string message)
        {
            if (unit.Agent && unit.Agent.isOnNavMesh) unit.Agent.ResetPath();
            Cancel(); StatusMessage = message;
        }
        void FindNextNode()
        {
            ResourceNode closest = null;
            Vector3 point = Vector3.zero;
            float best = float.PositiveInfinity;
            bool stockExists = false;
            foreach (var candidate in FindObjectsByType<ResourceNode>())
            {
                if (candidate.kind != DesiredKind || candidate.Remaining <= 0 || !home.controller.HasExplored(unit.team, candidate.transform.position)) continue;
                stockExists = true;
                if (TryApproach(candidate.transform.position, 3, out var approach, out float length) && length < best)
                { closest = candidate; best = length; point = approach; }
            }
            Node = closest;
            if (closest && NavigateToApproach(point))
            { State = GatherState.Travelling; StatusMessage = DesiredKind == ResourceKind.Food ? "Автосбор еды" : "Автосбор древесины"; return; }
            if (stockExists)
            {
                Block(false);
                StatusMessage = DesiredKind == ResourceKind.Food ? "Нет доступной еды, ожидаю" : "Нет доступной древесины, ожидаю";
            }
            else if (home.controller.fog && home.controller.fog.Active) StopWithMessage(DesiredKind == ResourceKind.Food ? "Нет разведанной еды" : "Нет разведанной древесины");
            else StopWithMessage(DesiredKind == ResourceKind.Food ? "Нет еды" : "Нет древесины");
        }
        void HeadToNode()
        {
            if (!Node || Node.Remaining <= 0)
            {
                if (Carried > 0) HeadHome();
                else if (AutoGatherActive && autoGatherEnabled) FindNextNode();
                else StopWithMessage(Node ? "Источник закончился" : "Готов к работе");
                return;
            }
            if (NavigateNear(Node.transform.position, 3)) { State = GatherState.Travelling; StatusMessage = "Иду к источнику"; }
            else if (AutoGatherActive && autoGatherEnabled) FindNextNode();
            else Block(false);
        }
        void HeadHome()
        {
            if (NavigateNear(home.depositPoint, 1)) { State = GatherState.Returning; StatusMessage = "Несу груз на базу"; }
            else Block(true);
        }
        void Update()
        {
            if (home && home.controller.IsPaused) return;
            if (!unit.IsAvailable || !home || State == GatherState.Idle) return;
            if (home.controller.MatchOver) { unit.Stop(); return; }
            if (!unit.Agent || !unit.Agent.isOnNavMesh) return;
            if (IsRetreating) { UpdateRetreat(); return; }
            if (State == GatherState.Travelling && (!Node || Node.Remaining <= 0))
            { HeadToNode(); return; }
            if (State == GatherState.Blocked)
            {
                if (Time.time >= retryAt) { if (retryHome) HeadHome(); else HeadToNode(); }
                return;
            }
            if (State == GatherState.Travelling || State == GatherState.Returning)
            {
                if (unit.Agent.pathPending) return;
                float distance = Vector3.Distance(transform.position, destination);
                if (distance > .7f)
                {
                    if (Time.time >= travelCheckAt)
                    {
                        travelCheckAt = Time.time + 3;
                        if (lastDistance - distance < .15f) { if (State == GatherState.Returning) HeadHome(); else HeadToNode(); }
                        lastDistance = distance;
                    }
                    return;
                }
                unit.Agent.ResetPath();
                if (State == GatherState.Returning)
                {
                    DepositCargo();
                    Carried = 0; HeadToNode();
                }
                else { State = GatherState.Gathering; nextHarvestAt = Time.time + 1; StatusMessage = "Добываю ресурс"; }
                return;
            }
            if (State != GatherState.Gathering || Time.time < nextHarvestAt) return;
            nextHarvestAt = Time.time + 1;
            if (!Node || Node.Remaining <= 0) { HeadToNode(); return; }
            CarriedKind = Node.kind;
            Carried += Node.Extract(Mathf.Min(5, CarryCapacity - Carried));
            if (Carried >= CarryCapacity || Node.Remaining <= 0) HeadHome();
        }
    }
}
