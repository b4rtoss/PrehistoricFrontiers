using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BuildingHealth : MonoBehaviour
    {
        public UnitTeam team;
        public RTSSelectionController controller;
        public float maxHealth = 800;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => gameObject.activeInHierarchy && CurrentHealth > 0;
        public BoxCollider Body { get; private set; }
        void Awake() { CurrentHealth = Mathf.Max(1, maxHealth); Body = GetComponent<BoxCollider>(); }
        public void Initialize(float maximum) { maxHealth = Mathf.Max(1, maximum); CurrentHealth = maxHealth; }
        public Vector3 SurfacePoint(Vector3 from) => Body.ClosestPoint(from);
        public void TakeDamage(float damage, UnitTeam? attackerTeam = null)
        {
            if (!IsAlive || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0) return;
            if (team == UnitTeam.Player && attackerTeam.HasValue && attackerTeam.Value != team) controller?.alerts?.Notify(this);
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            if (CurrentHealth > 0) return;
            controller?.FinishMatchIfEnded();
            controller?.statistics?.RecordBuildingLost(team);
            // Production must refund before the building's Update is disabled by destruction.
            GetComponent<ProductionBuilding>()?.CancelAll();
            BattleEffects.Burst(BattleEffect.Death, transform.position, 18, 1.5f);
            gameObject.SetActive(false);
        }
        public bool NavigateAttacker(RTSUnit unit)
        {
            float best = float.PositiveInfinity;
            Vector3 point = Vector3.zero;
            bool found = false;
            float reach = unit.Agent.radius + unit.stats.attackRange + .25f;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var edge = Body.ClosestPoint(transform.position + direction * 10 + Vector3.up * .8f);
                var candidate = edge + direction * Mathf.Max(1.6f, unit.Agent.radius + unit.stats.attackRange * .7f);
                candidate.y = 0;
                if (!NavMesh.SamplePosition(candidate, out var hit, 1, unit.Agent.areaMask)) continue;
                var origin = hit.position + Vector3.up * .8f;
                if (Vector3.Distance(origin, SurfacePoint(origin)) > reach) continue;
                bool occupied = false;
                foreach (var collider in Physics.OverlapSphere(origin, unit.Agent.radius + .05f, 1 << 10))
                    if (collider.GetComponentInParent<RTSUnit>() != unit) { occupied = true; break; }
                if (occupied) continue;
                if (!UnitNavigation.TryPath(unit, hit.position, out var path)) continue;
                float length = UnitNavigation.PathLength(path);
                if (length < best) { best = length; point = hit.position; found = true; }
            }
            return found && unit.NavigateTo(point);
        }
    }
}
