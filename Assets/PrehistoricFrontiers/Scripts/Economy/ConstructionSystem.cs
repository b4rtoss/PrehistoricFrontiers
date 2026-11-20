using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public enum StructureKind { Barracks, Clinic }
    public sealed class ConstructionSystem : MonoBehaviour
    {
        public RTSSelectionController controller;
        public ProductionBuilding main;
        public GameObject clinicModelPrefab;
        public GameObject barracksModelPrefab;
        public readonly List<ConstructionSite> Sites = new List<ConstructionSite>();
        public ProductionBuilding SelectedProduction;
        public UnitTeam Team => main.Team;
        RTSUnit[] Workers => Team == UnitTeam.Player ? controller.units : controller.enemies;
        public bool WarriorsSelected { get; private set; } = true;
        public ProductionBuilding WarriorProduction
        {
            get
            {
                if (SelectedProduction && !SelectedProduction.workersOnly && SelectedProduction.GetComponent<BuildingHealth>().IsAlive)
                    return SelectedProduction;
                foreach (var site in Sites)
                    if (site && site.Complete && site.Health.IsAlive && site.kind == StructureKind.Barracks)
                        return site.GetComponent<ProductionBuilding>();
                return null;
            }
        }
        public void ShowProduction(ProductionBuilding factory)
        {
            if (!factory || factory.Team != Team || !factory.GetComponent<BuildingHealth>().IsAlive) return;
            SelectedProduction = factory;
            WarriorsSelected = !factory.workersOnly;
        }
        public void SelectProduction(bool warriors)
        {
            WarriorsSelected = warriors;
            SelectedProduction = warriors ? WarriorProduction ?? main : main;
        }
        public void PrepareWarriorProduction()
        {
            if (controller.IsPaused || controller.MatchOver) return;
            SelectProduction(true);
            if (WarriorProduction) return;
            var worker = controller.Selected.Find(u => u && u.IsAvailable && u.stats.isWorker);
            if (!worker) worker = System.Array.Find(controller.units, u => u && u.IsAvailable && u.stats.isWorker);
            if (!worker) { Message = "Нужен рабочий: обучите его на вкладке Рабочие"; return; }
            controller.Select(worker);
            foreach (var site in Sites)
                if (site && !site.Complete && site.Health.IsAlive && site.kind == StructureKind.Barracks)
                {
                    Resume(site);
                    Message = "Рабочий направлен достраивать казарму";
                    return;
                }
            if (!main.wallet.CanAfford(FoodCost(StructureKind.Barracks), WoodCost(StructureKind.Barracks)))
            { Message = "Для казармы нужно 150 древесины"; return; }
            Begin(StructureKind.Barracks);
        }
        public bool Pending { get; private set; }
        public StructureKind PendingKind { get; private set; }
        public string Message { get; private set; } = "Выделите рабочего для строительства";
        RTSUnit builder;
        LineRenderer preview;
        Material previewMaterial;
        public static string Name(StructureKind kind) => kind == StructureKind.Barracks ? "Казарма" : "Лечебница";
        public static int FoodCost(StructureKind kind) => kind == StructureKind.Barracks ? 0 : 100;
        public static int WoodCost(StructureKind kind) => kind == StructureKind.Barracks ? 150 : 120;
        public static float Duration(StructureKind kind) => kind == StructureKind.Barracks ? 12 : 10;
        public void Begin(StructureKind kind)
        {
            if (controller.IsPaused) return;
            builder = controller.Selected.Find(IsEligibleBuilder);
            Pending = builder && !controller.MatchOver;
            PendingKind = kind;
            Message = Pending ? "ЛКМ: поставить / Esc или ПКМ: отмена" : "Сначала выделите рабочего";
            controller.CancelAttackMoveInput();
        }
        public void CancelPlacement() { Pending = false; if (preview) preview.enabled = false; }
        void Update()
        {
            if (controller.IsPaused) return;
            if (!Pending) return;
            if (!builder || !builder.IsAlive || !controller.Selected.Contains(builder) || controller.MatchOver || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            { CancelPlacement(); return; }
            bool overUI = BattleHUD.IsOverPanel(Input.mousePosition);
            var ray = controller.sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (!overUI && Physics.Raycast(ray, out var hit, 300, 1 << 8))
            {
                bool valid = CanPlace(builder, PendingKind, hit.point, out _, out _);
                if (!preview)
                {
                    var go = new GameObject("Construction footprint"); preview = go.AddComponent<LineRenderer>();
                    previewMaterial = new Material(Shader.Find("Unlit/Color")); preview.sharedMaterial = previewMaterial;
                    preview.widthMultiplier = .13f; preview.positionCount = 5;
                }
                preview.enabled = true; previewMaterial.color = valid ? Color.green : Color.red;
                var p = hit.point + Vector3.up * .12f;
                preview.SetPositions(new[] { p + new Vector3(-2, 0, -2), p + new Vector3(2, 0, -2), p + new Vector3(2, 0, 2), p + new Vector3(-2, 0, 2), p + new Vector3(-2, 0, -2) });
            }
            else if (preview) preview.enabled = false;
        }
        public bool CanPlace(RTSUnit preferred, StructureKind kind, Vector3 point, out Vector3 approach, out string reason)
        {
            return TryResolvePlacement(preferred, kind, point, out _, out approach, out reason);
        }
        bool IsEligibleBuilder(RTSUnit unit)
        {
            return unit && unit.IsAvailable && unit.stats.isWorker && unit.team == Team;
        }
        bool IsFreeBuilder(RTSUnit unit)
        {
            if (!IsEligibleBuilder(unit)) return false;
            var assigned = unit.GetComponent<WorkerBuilder>();
            return !assigned || !assigned.Site && assigned.QueuedCount == 0;
        }
        IEnumerable<RTSUnit> BuilderCandidates(RTSUnit preferred)
        {
            var candidates = new List<RTSUnit> { preferred };
            candidates.AddRange(controller.Selected);
            candidates.AddRange(Workers);
            var seen = new HashSet<RTSUnit>();
            for (int pass = 0; pass < 2; pass++)
                foreach (var unit in candidates)
                    if (IsEligibleBuilder(unit) && IsFreeBuilder(unit) == (pass == 0) && seen.Add(unit)) yield return unit;
        }
        bool TryResolvePlacement(RTSUnit preferred, StructureKind kind, Vector3 point,
            out RTSUnit worker, out Vector3 approach, out string reason)
        {
            worker = null; approach = point; reason = "";
            if (controller.IsPaused || controller.MatchOver || !IsEligibleBuilder(preferred))
            { reason = "Нужен живой рабочий и активный матч"; return false; }
            if (Mathf.Abs(point.x) > 22 || Mathf.Abs(point.z) > 22 || !NavMesh.SamplePosition(point, out _, .3f, NavMesh.AllAreas))
            { reason = "Строить можно на свободной земле внутри карты"; return false; }
            for (int x = -2; x <= 2; x += 2) for (int z = -2; z <= 2; z += 2)
                if (!controller.CanSee(Team, point + new Vector3(x, 0, z))) { reason = "Нужен обзор всей площадки"; return false; }
            if (Physics.CheckBox(point + Vector3.up * 1.3f, new Vector3(2.4f, 1.2f, 2.4f), Quaternion.identity, (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13)))
            { reason = "Место занято: отступите от юнитов, ресурсов и зданий"; return false; }
            if (!main.wallet.CanAfford(FoodCost(kind), WoodCost(kind))) { reason = "Не хватает ресурсов"; return false; }
            foreach (var candidate in BuilderCandidates(preferred))
            {
                if (!TryFindPlacementApproach(candidate, point, out approach)) continue;
                worker = candidate; return true;
            }
            reason = "Ни один рабочий не может подойти";
            return false;
        }
        bool TryFindPlacementApproach(RTSUnit worker, Vector3 point, out Vector3 approach)
        {
            approach = point;
            if (!worker.Agent || !worker.Agent.enabled || !worker.Agent.isOnNavMesh
                || !NavMesh.SamplePosition(point, out _, .3f, worker.Agent.areaMask)) return false;
            bool reachable = false;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                if (!NavMesh.SamplePosition(point + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 3.5f, out var hit, .5f, worker.Agent.areaMask)) continue;
                if (!UnitNavigation.TryPath(worker, hit.position, out _)) continue;
                if (!reachable || Vector3.Distance(worker.transform.position, hit.position) < Vector3.Distance(worker.transform.position, approach)) approach = hit.position;
                reachable = true;
            }
            return reachable;
        }
        public bool TryFindApproach(RTSUnit worker, ConstructionSite site, out Vector3 point)
        {
            return UnitNavigation.TryApproach(worker, site ? site.Health : null, 16, 1, out point);
        }
        public ConstructionSite Place(RTSUnit worker, StructureKind kind, Vector3 point)
        {
            Physics.SyncTransforms();
            if (!TryResolvePlacement(worker, kind, point, out var assigned, out var approach, out var reason)) { Message = reason; return null; }
            worker = assigned;
            if (!main.wallet.TrySpend(FoodCost(kind), WoodCost(kind))) return null;
            var go = new GameObject(Name(kind)); go.layer = 13; go.transform.position = new Vector3(point.x, 0, point.z);
            var body = go.AddComponent<BoxCollider>(); body.center = Vector3.up * 1.3f; body.size = new Vector3(4, 2.6f, 4);
            var health = go.AddComponent<BuildingHealth>(); health.controller = controller; health.team = Team;
            health.Initialize(kind == StructureKind.Barracks ? 500 : 400);
            var obstacle = go.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = body.center; obstacle.size = body.size; obstacle.carving = true;
            var site = go.AddComponent<ConstructionSite>(); site.system = this; site.kind = kind; site.CreateVisual();
            if (Team == UnitTeam.Enemy) { var hidden = go.AddComponent<FogHiddenVisual>(); hidden.fog = controller.fog; hidden.ApplyNow(); }
            Sites.Add(site);
            var bars = new List<BuildingHealth>(main.healthBars.buildings) { health }; main.healthBars.buildings = bars.ToArray();
            var constructionWorker = worker.GetComponent<WorkerBuilder>() ?? worker.gameObject.AddComponent<WorkerBuilder>();
            if (!constructionWorker.Assign(site, approach))
            {
                Sites.Remove(site);
                var buildings = new List<BuildingHealth>(main.healthBars.buildings); buildings.Remove(health); main.healthBars.buildings = buildings.ToArray();
                main.wallet.Refund(FoodCost(kind), WoodCost(kind)); Destroy(go);
                Message = "Рабочий недоступен: строительство отменено, ресурсы возвращены";
                return null;
            }
            Message = Name(kind) + (constructionWorker.Site == site ? ": строительство начато" : ": добавлено в очередь / впереди: " + constructionWorker.QueuedCount);
            CancelPlacement();
            return site;
        }
        public void PlacePending(Vector3 point) { if (Pending) Place(builder, PendingKind, point); }
        public void Resume(ConstructionSite site)
        {
            if (!site || site.Complete || !site.Health.IsAlive || controller.IsPaused || controller.MatchOver) return;
            if (site.Builder)
            {
                Message = site.Builder.Site == site ? "У здания уже есть строитель" : "Здание уже в очереди строителя";
                return;
            }
            var preferred = controller.Selected.Find(IsEligibleBuilder);
            Resume(preferred, site);
        }
        public void Resume(RTSUnit preferred, ConstructionSite site)
        {
            if (!site || site.Complete || !site.Health.IsAlive || site.Health.team != Team || controller.IsPaused || controller.MatchOver || !IsEligibleBuilder(preferred)) return;
            if (site.Builder) return;
            foreach (var worker in BuilderCandidates(preferred))
            {
                if (!TryFindApproach(worker, site, out var approach)) continue;
                var constructionWorker = worker.GetComponent<WorkerBuilder>() ?? worker.gameObject.AddComponent<WorkerBuilder>();
                if (!constructionWorker.Assign(site, approach)) continue;
                Message = constructionWorker.Site == site ? "Строительство возобновлено" : "Здание добавлено в очередь строителя";
                return;
            }
            Message = "Ни один рабочий не может подойти к стройке";
        }
        void OnDestroy() { if (preview) Destroy(preview.gameObject); if (previewMaterial) Destroy(previewMaterial); }
    }
}
