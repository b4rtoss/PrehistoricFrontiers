using System;
using UnityEngine;

namespace PrehistoricFrontiers
{
    [RequireComponent(typeof(EnemyProductionAI))]
    public sealed class EnemyEconomyAI : MonoBehaviour
    {
        public ConstructionSystem construction;
        public int workerTarget = 3;
        float nextDecisionAt;
        public int WorkersAlive => Array.FindAll(Controller.enemies, u => u && u.IsAlive && u.stats.isWorker).Length;
        public string Status { get; private set; } = "Подготовка экономики";
        ProductionBuilding Main => GetComponent<ProductionBuilding>();
        RTSSelectionController Controller => Main.controller;
        void Start() { nextDecisionAt = Time.time + 1; }
        public void Tick()
        {
            if (!isActiveAndEnabled || !Main || !construction || Controller.IsPaused || Controller.MatchOver || !GetComponent<BuildingHealth>().IsAlive) return;
            var workers = Array.FindAll(Controller.enemies, u => u && u.IsAvailable && u.stats.isWorker);
            if (WorkersAlive + Main.QueueCount < workerTarget) Main.TryQueue(3);
            ConstructionSite barracks = construction.Sites.Find(s => s && s.Health.IsAlive && s.kind == StructureKind.Barracks);
            if (!barracks && workers.Length > 0 && Main.wallet.CanAfford(0, ConstructionSystem.WoodCost(StructureKind.Barracks)))
            {
                var builder = Array.Find(workers, u => !u.GetComponent<WorkerBuilder>() || !u.GetComponent<WorkerBuilder>().Site) ?? workers[0];
                foreach (var offset in new[] { new Vector3(0,0,8), new Vector3(-6,0,4), new Vector3(6,0,4), new Vector3(-6,0,-4), new Vector3(0,0,-7), new Vector3(7,0,9) })
                {
                    var point = Main.transform.position + offset;
                    if (!construction.CanPlace(builder, StructureKind.Barracks, point, out _, out _)) continue;
                    barracks = construction.Place(builder, StructureKind.Barracks, point);
                    if (barracks) break;
                }
            }
            if (barracks && !barracks.Complete && !barracks.Builder && workers.Length > 0) construction.Resume(workers[0], barracks);
            for (int i = 0; i < workers.Length; i++)
            {
                var unit = workers[i]; var builder = unit.GetComponent<WorkerBuilder>();
                if (builder && builder.Site) continue;
                var gatherer = unit.GetComponent<WorkerGatherer>();
                if (gatherer.State != GatherState.Idle) continue;
                var kind = Main.wallet.Wood < ConstructionSystem.WoodCost(StructureKind.Barracks) && !barracks ? ResourceKind.Wood
                    : i % 3 == 1 ? ResourceKind.Wood : ResourceKind.Food;
                ResourceNode source = null; float best = float.PositiveInfinity;
                foreach (var node in FindObjectsByType<ResourceNode>())
                {
                    if (node.kind != kind || node.Remaining <= 0 || !Controller.HasExplored(UnitTeam.Enemy, node.transform.position)) continue;
                    if (!gatherer.TryApproach(node.transform.position, 3, out _, out float distance)) continue;
                    if (distance < best) { source = node; best = distance; }
                }
                if (source) gatherer.Gather(source);
            }
            Status = workers.Length == 0 ? "Восстановление рабочих" : !barracks ? "Нужны ресурсы для казармы"
                : !barracks.Complete ? "Строит казарму" : "Добывает ресурсы";
        }
        void Update()
        {
            if (Time.time < nextDecisionAt) return;
            nextDecisionAt = Time.time + .75f;
            Tick();
        }
    }
}
