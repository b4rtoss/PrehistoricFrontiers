using System;
using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    [DefaultExecutionOrder(-80)]
    public sealed class FogOfWar : MonoBehaviour
    {
        public RTSSelectionController controller;
        public const int Resolution = 96;
        const float MapMin = -25, MapSize = 50;
        public bool FogEnabled { get; private set; } = true;
        public Texture2D Mask { get; private set; }
        readonly bool[][] visible = { new bool[Resolution * Resolution], new bool[Resolution * Resolution] };
        readonly bool[][] explored = { new bool[Resolution * Resolution], new bool[Resolution * Resolution] };
        readonly float[][] scoutReservations = { new float[Resolution * Resolution], new float[Resolution * Resolution] };
        readonly Color32[] pixels = new Color32[Resolution * Resolution];
        float nextRefresh;
        public bool Active => enabled && FogEnabled;
        void Awake()
        {
            Mask = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Fog visibility mask" };
        }
        void Start() { RefreshNow(); }
        void Update() { if (Active && Time.time >= nextRefresh) RefreshNow(); }
        int Cell(Vector3 p)
        {
            int x = Mathf.FloorToInt((p.x - MapMin) / MapSize * Resolution), z = Mathf.FloorToInt((p.z - MapMin) / MapSize * Resolution);
            return x < 0 || x >= Resolution || z < 0 || z >= Resolution ? -1 : z * Resolution + x;
        }
        public bool IsVisible(UnitTeam team, Vector3 p) { int cell = Cell(p); return !Active || cell >= 0 && visible[(int)team][cell]; }
        public bool WasExplored(UnitTeam team, Vector3 p) { int cell = Cell(p); return !Active || cell >= 0 && explored[(int)team][cell]; }
        public void SetFogEnabled(bool value)
        {
            FogEnabled = value;
            if (value) RefreshNow();
            foreach (var visual in FindObjectsByType<FogHiddenVisual>()) visual.ApplyNow();
        }
        public void ResetExploration()
        {
            for (int t = 0; t < 2; t++) { Array.Clear(explored[t], 0, explored[t].Length); Array.Clear(scoutReservations[t], 0, scoutReservations[t].Length); }
            RefreshNow();
        }
        void Reveal(UnitTeam team, Vector3 center, float radius)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((center.x - radius - MapMin) / MapSize * Resolution), 0, Resolution - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((center.x + radius - MapMin) / MapSize * Resolution), 0, Resolution - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((center.z - radius - MapMin) / MapSize * Resolution), 0, Resolution - 1);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt((center.z + radius - MapMin) / MapSize * Resolution), 0, Resolution - 1);
            for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
            {
                float dx = MapMin + (x + .5f) / Resolution * MapSize - center.x, dz = MapMin + (z + .5f) / Resolution * MapSize - center.z;
                if (dx * dx + dz * dz > radius * radius) continue;
                int index = z * Resolution + x; visible[(int)team][index] = true; explored[(int)team][index] = true;
            }
        }
        public void RefreshNow()
        {
            if (!controller || !Mask) return;
            nextRefresh = Time.time + .1f;
            Array.Clear(visible[0], 0, visible[0].Length); Array.Clear(visible[1], 0, visible[1].Length);
            foreach (var unit in controller.units) if (unit && unit.IsAvailable) Reveal(UnitTeam.Player, unit.transform.position, unit.stats.isWorker ? 7 : 8);
            foreach (var unit in controller.enemies) if (unit && unit.IsAvailable) Reveal(UnitTeam.Enemy, unit.transform.position, unit.stats.isWorker ? 7 : 8);
            if (controller.playerBase && controller.playerBase.IsAlive) Reveal(UnitTeam.Player, controller.playerBase.transform.position, 9);
            if (controller.enemyBase && controller.enemyBase.IsAlive) Reveal(UnitTeam.Enemy, controller.enemyBase.transform.position, 9);
            foreach (var site in FindObjectsByType<ConstructionSite>())
                if (site.Complete && site.Health.IsAlive) Reveal(site.Health.team, site.transform.position, 5);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(visible[0][i] ? (byte)255 : (byte)0, explored[0][i] ? (byte)255 : (byte)0, 0, 255);
            Mask.SetPixels32(pixels); Mask.Apply(false);
        }
        public bool TryExplore(RTSUnit unit, out Vector3 result)
        {
            result = Vector3.zero;
            if (!Active || !unit.IsAlive || unit.stats.isWorker || !unit.Agent.isOnNavMesh) return false;
            float best = float.PositiveInfinity; int chosen = -1;
            for (float z = -21; z <= 21; z += 6) for (float x = -21; x <= 21; x += 6)
            {
                var candidate = new Vector3(x, 0, z);
                if (!NavMesh.SamplePosition(candidate, out var hit, 2, unit.Agent.areaMask) || IsVisible(unit.team, hit.position)) continue;
                int cell = Cell(hit.position); if (cell < 0) continue;
                if (!UnitNavigation.TryPath(unit, hit.position, out var path)) continue;
                float cost = WasExplored(unit.team, hit.position) ? 25 : 0;
                if (scoutReservations[(int)unit.team][cell] > Time.time) cost += 25;
                cost += UnitNavigation.PathLength(path);
                if (cost < best) { best = cost; result = hit.position; chosen = cell; }
            }
            if (chosen < 0) return false;
            scoutReservations[(int)unit.team][chosen] = Time.time + 12; return true;
        }
        void OnDestroy() { if (Mask) Destroy(Mask); }
    }
}
