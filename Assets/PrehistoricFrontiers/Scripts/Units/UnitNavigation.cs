using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public static class UnitNavigation
    {
        public static bool TryPath(RTSUnit unit, Vector3 destination, out NavMeshPath path)
        {
            path = new NavMeshPath();
            return unit && unit.IsAvailable && unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh
                && unit.Agent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete;
        }
        public static float PathLength(NavMeshPath path)
        {
            float length = 0;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }
        public static bool TryApproach(RTSUnit unit, BuildingHealth building, int samples, float height, out Vector3 point)
        {
            point = Vector3.zero;
            if (!unit || !unit.IsAvailable || !unit.Agent || !unit.Agent.enabled || !unit.Agent.isOnNavMesh || !building || !building.IsAlive) return false;
            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < samples; i++)
            {
                float angle = i * Mathf.PI * 2 / samples;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var edge = building.SurfacePoint(building.transform.position + direction * 10 + Vector3.up * height);
                var candidate = edge + direction * (unit.Agent.radius + .4f); candidate.y = 0;
                if (!NavMesh.SamplePosition(candidate, out var hit, 1, unit.Agent.areaMask)
                    || !TryPath(unit, hit.position, out var path)) continue;
                float distance = PathLength(path);
                if (distance >= nearest) continue;
                nearest = distance; point = hit.position; found = true;
            }
            return found;
        }
    }
}
