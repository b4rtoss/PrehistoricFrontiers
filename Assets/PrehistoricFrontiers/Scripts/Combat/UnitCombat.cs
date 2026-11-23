using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public enum UnitOrder { Idle, Move, Attack, Hold, Scout, AttackMove }

    [RequireComponent(typeof(RTSUnit), typeof(UnitHealth))]
    public sealed class UnitCombat : MonoBehaviour
    {
        static readonly HashSet<UnitCombat> active = new HashSet<UnitCombat>();
        public bool autoAcquire = true;
        public RTSSelectionController battleController;
        public float LastAttackAt { get; private set; } = -100;
        public bool IsAutomaticAttack { get; private set; }
        public UnitOrder Order { get; private set; }
        public bool HasAttackMove { get; private set; }
        public Vector3 AttackMoveDestination { get; private set; }
        public RTSUnit Target { get; private set; }
        public BuildingHealth BuildingTarget { get; private set; }
        RTSUnit unit;
        float nextAttackAt, nextScanAt, nextPathAt;
        LineRenderer strike;
        float strikeUntil;
        float movementStoppingDistance;
        float nextScoutAt;
        float scoutProgressCheck, previousScoutDistance;
        void Awake() { unit = GetComponent<RTSUnit>(); movementStoppingDistance = GetComponent<UnityEngine.AI.NavMeshAgent>().stoppingDistance; }
        void OnEnable() { active.Add(this); }
        void OnDisable() { active.Remove(this); if (strike) strike.gameObject.SetActive(false); }
        public bool Attack(RTSUnit target)
        {
            if (battleController && (battleController.IsPaused || battleController.IsPreparingFor(unit.team))) return false;
            if (unit.stats.isWorker || !unit.IsAvailable || !target || !target.IsAvailable || target.team == unit.team) return false;
            if (battleController && !battleController.CanSee(unit.team, target.transform.position)) return false;
            unit.Treatment?.Cancel();
            HasAttackMove = false; Target = target; BuildingTarget = null; Order = UnitOrder.Attack; nextPathAt = 0; IsAutomaticAttack = false;
            unit.Agent.stoppingDistance = 0;
            return true;
        }
        public bool Attack(BuildingHealth building)
        {
            if (battleController && (battleController.IsPaused || battleController.IsPreparingFor(unit.team))) return false;
            if (unit.stats.isWorker || !unit.IsAvailable || !building || !building.IsAlive || building.team == unit.team || (battleController && battleController.MatchOver)) return false;
            if (battleController && !battleController.CanSee(unit.team, building.transform.position)) return false;
            unit.Treatment?.Cancel();
            HasAttackMove = false; Target = null; BuildingTarget = building; Order = UnitOrder.Attack; nextPathAt = 0; IsAutomaticAttack = false;
            unit.Agent.stoppingDistance = 0;
            return true;
        }
        public bool AttackMove(Vector3 destination)
        {
            if (battleController && battleController.IsPaused) return false;
            if (unit.stats.isWorker || !unit.IsAvailable || battleController && battleController.MatchOver || !unit.NavigateTo(destination)) return false;
            unit.Treatment?.Cancel();
            var corners = unit.Agent.path.corners;
            AttackMoveDestination = corners.Length > 0 ? corners[corners.Length - 1] : transform.position;
            Target = null; BuildingTarget = null; HasAttackMove = true;
            Order = UnitOrder.AttackMove; IsAutomaticAttack = false; nextScanAt = 0;
            RestoreMovementStoppingDistance();
            return true;
        }
        void EngageOnRoute(RTSUnit target)
        {
            if (Attack(target)) HasAttackMove = true;
        }
        void EngageOnRoute(BuildingHealth target)
        {
            if (Attack(target)) HasAttackMove = true;
        }
        void UpdateAttackMove()
        {
            // A manual route only reacts to nearby visible targets, independently of G/H.
            if (Target && Vector3.Distance(transform.position, Target.transform.position) > unit.stats.detectionRadius * 1.5f)
            { Target = null; Order = UnitOrder.Idle; unit.Agent.ResetPath(); RestoreMovementStoppingDistance(); }
            if (Time.time >= nextScanAt)
            {
                nextScanAt = Time.time + .3f;
                if (!Target)
                {
                    var closest = FindClosestEnemy(unit.stats.detectionRadius, true);
                    if (closest) EngageOnRoute(closest);
                    else if (!BuildingTarget && battleController)
                    {
                        var building = battleController.FindEnemyBuilding(unit.team, transform.position);
                        if (building && Vector3.Distance(transform.position, building.SurfacePoint(transform.position)) <= unit.stats.detectionRadius)
                            EngageOnRoute(building);
                    }
                }
            }
            if (Target || BuildingTarget) return;
            if (Order != UnitOrder.AttackMove)
            {
                RestoreMovementStoppingDistance();
                if (!unit.NavigateTo(AttackMoveDestination)) { Hold(); unit.Agent.ResetPath(); return; }
                Order = UnitOrder.AttackMove;
            }
            if (!unit.Agent.pathPending && (!unit.Agent.hasPath || unit.Agent.remainingDistance <= unit.Agent.stoppingDistance + .2f))
            { unit.Agent.ResetPath(); Hold(); }
        }
        RTSUnit FindClosestEnemy(float radius, bool requirePath = false)
        {
            RTSUnit closest = null;
            float best = radius * radius;
            foreach (var other in active)
            {
                var candidate = other.unit;
                if (!candidate || !candidate.IsAvailable || candidate.team == unit.team
                    || battleController && !battleController.CanSee(unit.team, candidate.transform.position)) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= best || requirePath && !UnitNavigation.TryPath(unit, candidate.transform.position, out _)) continue;
                best = distance; closest = candidate;
            }
            return closest;
        }
        void SetOrder(UnitOrder order)
        {
            HasAttackMove = false; Target = null; BuildingTarget = null; Order = order; IsAutomaticAttack = false;
            RestoreMovementStoppingDistance();
        }
        void RestoreMovementStoppingDistance() { if (unit.Agent) unit.Agent.stoppingDistance = movementStoppingDistance; }
        public void BeginMove() { SetOrder(UnitOrder.Move); }
        public void Hold() { SetOrder(UnitOrder.Hold); }
        public void SetAutomation(bool enabled)
        {
            if (!enabled && (IsAutomaticAttack || Order == UnitOrder.Scout && (!battleController || !battleController.AutoScoutFor(unit.team))))
            {
                Target = null; BuildingTarget = null; Order = UnitOrder.Idle; IsAutomaticAttack = false;
                RestoreMovementStoppingDistance();
                if (unit.Agent && unit.Agent.isOnNavMesh) unit.Agent.ResetPath();
            }
            if (enabled && Order == UnitOrder.Hold) Order = UnitOrder.Idle;
            nextScanAt = 0;
        }
        public void SetScoutAutomation(bool enabled)
        {
            if (!enabled && Order == UnitOrder.Scout && (!battleController || !battleController.AutoBattleFor(unit.team)))
            {
                Order = UnitOrder.Idle; IsAutomaticAttack = false;
                RestoreMovementStoppingDistance();
                if (unit.Agent && unit.Agent.isOnNavMesh) unit.Agent.ResetPath();
            }
            if (enabled && Order == UnitOrder.Hold) Order = UnitOrder.Idle;
            nextScoutAt = 0;
        }
        void Update()
        {
            if (battleController && battleController.IsPaused) return;
            if (strike) strike.gameObject.SetActive(Time.time < strikeUntil);
            if (unit.IsOnClinicOrder || unit.stats.isWorker || !unit.IsAvailable || !unit.Agent || !unit.Agent.isOnNavMesh) return;
            if (battleController && battleController.MatchOver) { unit.Agent.ResetPath(); Hold(); return; }
            if (battleController && battleController.IsPreparingFor(unit.team)) return;
            if (Order == UnitOrder.Move)
            {
                if (!unit.Agent.pathPending && (!unit.Agent.hasPath || unit.Agent.remainingDistance <= unit.Agent.stoppingDistance + .1f)) Order = UnitOrder.Idle;
                return;
            }
            if (Order == UnitOrder.Hold) return;
            // Losing sight discards the live entity reference; scouting does not track hidden transforms.
            if (battleController && (Target && !battleController.CanSee(unit.team, Target.transform.position) || BuildingTarget && !battleController.CanSee(unit.team, BuildingTarget.transform.position)))
            { Target = null; BuildingTarget = null; Order = UnitOrder.Idle; IsAutomaticAttack = false; unit.Agent.ResetPath(); RestoreMovementStoppingDistance(); }
            if (Target && !Target.IsAvailable) { Target = null; Order = UnitOrder.Idle; unit.Agent.ResetPath(); RestoreMovementStoppingDistance(); }
            if (BuildingTarget && !BuildingTarget.IsAlive) { BuildingTarget = null; Order = UnitOrder.Idle; unit.Agent.ResetPath(); RestoreMovementStoppingDistance(); }
            if (HasAttackMove)
            {
                UpdateAttackMove();
                if (!Target && !BuildingTarget) return;
            }
            // Automatic sieges react to nearby defenders; explicit base orders keep their target.
            if (BuildingTarget && IsAutomaticAttack && Time.time >= nextScanAt)
            {
                nextScanAt = Time.time + .3f;
                var defender = FindClosestEnemy(unit.stats.detectionRadius);
                if (defender && Attack(defender)) IsAutomaticAttack = true;
            }
            if (!HasAttackMove && !Target && !BuildingTarget && autoAcquire && (!battleController || battleController.AutoBattleFor(unit.team)) && Time.time >= nextScanAt)
            {
                nextScanAt = Time.time + .3f;
                var closest = FindClosestEnemy(battleController ? float.PositiveInfinity : unit.stats.detectionRadius);
                if (closest && Attack(closest)) IsAutomaticAttack = true;
                else if (!closest && battleController && Attack(battleController.FindEnemyBuilding(unit.team, transform.position))) IsAutomaticAttack = true;
            }
            if (BuildingTarget) { UpdateBuildingAttack(); return; }
            if (!Target)
            {
                if (autoAcquire && battleController && (battleController.AutoScoutFor(unit.team) || battleController.AutoBattleFor(unit.team)) && battleController.fog && battleController.fog.Active)
                {
                    bool arrived = !unit.Agent.pathPending && (!unit.Agent.hasPath || unit.Agent.remainingDistance <= unit.Agent.stoppingDistance + .2f);
                    if (Order == UnitOrder.Scout && Time.time >= scoutProgressCheck)
                    {
                        arrived |= previousScoutDistance - unit.Agent.remainingDistance < .5f;
                        previousScoutDistance = unit.Agent.remainingDistance; scoutProgressCheck = Time.time + 4;
                    }
                    if (Time.time >= nextScoutAt && (Order != UnitOrder.Scout || arrived))
                    {
                        nextScoutAt = Time.time + 1;
                        if (battleController.fog.TryExplore(unit, out var destination) && unit.NavigateTo(destination))
                        { Order = UnitOrder.Scout; IsAutomaticAttack = false; RestoreMovementStoppingDistance(); previousScoutDistance = unit.Agent.remainingDistance; scoutProgressCheck = Time.time + 4; }
                    }
                }
                return;
            }
            float reach = unit.Agent.radius + Target.Agent.radius + unit.stats.attackRange;
            Vector3 from = transform.position + Vector3.up * .8f;
            Vector3 to = Target.transform.position + Vector3.up * .8f;
            bool inRange = Vector3.Distance(transform.position, Target.transform.position) <= reach;
            bool blocked = Physics.Linecast(from, to, (1 << 9) | (1 << 12) | (1 << 13));
            if (inRange && !blocked)
            {
                unit.Agent.ResetPath();
                var direction = Vector3.ProjectOnPlane(to - from, Vector3.up);
                if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
                if (Time.time >= nextAttackAt)
                {
                    nextAttackAt = Time.time + Mathf.Max(.1f, unit.stats.attackCooldown);
                    ShowStrike(from, to);
                    Target.GetComponent<UnitHealth>().TakeDamage(unit.stats.attackDamage, unit.team, unit.transform.position);
                }
            }
            else if (Time.time >= nextPathAt)
            {
                nextPathAt = Time.time + .3f;
                if (!unit.NavigateTo(Target.transform.position))
                { Target = null; Order = UnitOrder.Idle; unit.Agent.ResetPath(); }
            }
        }
        void UpdateBuildingAttack()
        {
            var from = transform.position + Vector3.up * .8f;
            var point = BuildingTarget.SurfacePoint(from);
            float reach = unit.Agent.radius + unit.stats.attackRange + .25f;
            bool blocked = Physics.Linecast(from, point, out var hit, (1 << 9) | (1 << 12) | (1 << 13)) && hit.collider.GetComponentInParent<BuildingHealth>() != BuildingTarget;
            if (Vector3.Distance(from, point) <= reach && !blocked)
            {
                unit.Agent.ResetPath();
                var direction = Vector3.ProjectOnPlane(point - from, Vector3.up);
                if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
                if (Time.time >= nextAttackAt)
                {
                    nextAttackAt = Time.time + Mathf.Max(.1f, unit.stats.attackCooldown);
                    ShowStrike(from, point);
                    BattleEffects.Burst(BattleEffect.Impact, point + (from - point).normalized * .15f, 6);
                    BuildingTarget.TakeDamage(unit.stats.attackDamage, unit.team);
                }
            }
            else if (Time.time >= nextPathAt)
            {
                nextPathAt = Time.time + .5f;
                BuildingTarget.NavigateAttacker(unit);
            }
        }
        void ShowStrike(Vector3 from, Vector3 to)
        {
            LastAttackAt = Time.time;
            if (!strike)
            {
                var go = new GameObject("Attack flash");
                go.transform.SetParent(transform);
                strike = go.AddComponent<LineRenderer>();
                strike.sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(1, .78f, .25f) };
                strike.widthMultiplier = .1f; strike.positionCount = 2;
                strike.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            strike.SetPosition(0, from); strike.SetPosition(1, to);
            strikeUntil = Time.time + .12f; strike.gameObject.SetActive(true);
        }
        void OnDestroy() { if (strike && strike.sharedMaterial) Destroy(strike.sharedMaterial); }
    }
}
