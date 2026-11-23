using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public sealed class ProductionBuilding : MonoBehaviour
    {
        public RTSSelectionController controller;
        public UnitHealthBars healthBars;
        public bool workersOnly;
        public int TeamQueueCount
        {
            get { int count = 0; foreach (var factory in FindObjectsByType<ProductionBuilding>()) if (factory.Team == Team) count += factory.QueueCount; return count; }
        }
        public ResourceWallet wallet;
        public GameObject[] unitPrefabs;
        public Vector3 rallyPoint = new Vector3(-6, 0, -15);
        public Vector3 depositPoint = new Vector3(-10, 0, -13.8f);
        public const int QueueLimit = 3, PopulationLimit = 20;
        public event System.Action<RTSUnit> UnitProduced;
        public UnitTeam Team => GetComponent<BuildingHealth>().team;
        public int ArmyAlive => Team == UnitTeam.Player ? controller.AlliesAlive : controller.EnemiesAlive;
        sealed class Order
        {
            public int type, food, wood;
            public float duration, elapsed;
        }
        readonly List<Order> queue = new List<Order>();
        public int QueueCount => queue.Count;
        public float Progress => queue.Count == 0 ? 0 : SlotProgress(0);
        public float SlotProgress(int index) => Mathf.Clamp01(queue[index].elapsed / queue[index].duration);
        public float RemainingSeconds(int index) => Mathf.Max(0, queue[index].duration - queue[index].elapsed);
        public string Message { get; private set; } = "Choose an animal to train";
        public UnitStats Stats(int type) => unitPrefabs[type].GetComponent<RTSUnit>().stats;
        public string QueueName(int index) => Stats(queue[index].type).displayName;
        public bool TryQueue(int type)
        {
            if (controller.IsPaused) return false;
            if (type < 0 || type >= unitPrefabs.Length) return false;
            if (!GetComponent<BuildingHealth>().IsAlive || controller.MatchOver) { Message = "Battle ended / R to restart"; return false; }
            if (GetComponent<ConstructionSite>() && !GetComponent<ConstructionSite>().Complete) return false;
            if (workersOnly && !Stats(type).isWorker) { Message = "Боевые юниты создаются в казарме"; return false; }
            if (TeamQueueCount >= QueueLimit) { Message = "All production slots are busy (3)"; return false; }
            if (ArmyAlive + TeamQueueCount >= PopulationLimit) { Message = "Population limit (20)"; return false; }
            var stats = Stats(type);
            if (controller.IsPreparingFor(Team) && !stats.isWorker) { Message = "Подготовка: " + Mathf.CeilToInt(controller.PreparationRemaining) + " с"; return false; }
            if (!wallet.TrySpend(stats.foodCost, stats.woodCost)) { Message = "Not enough food or wood"; return false; }
            // Snapshot prices so edits to stats cannot change a previously paid refund.
            queue.Add(new Order { type = type, food = stats.foodCost, wood = stats.woodCost, duration = Mathf.Max(.1f, stats.productionTime) });
            Message = stats.displayName + " added to queue";
            return true;
        }
        public void Cancel(int index)
        {
            if (index < 0 || index >= queue.Count) return;
            var order = queue[index];
            wallet.Refund(order.food, order.wood);
            queue.RemoveAt(index);
            Message = "Cancelled / resources refunded";
        }
        public void CancelAll() { while (queue.Count > 0) Cancel(queue.Count - 1); }
        void Update()
        {
            if (controller.IsPaused) return;
            if (controller.MatchOver) { CancelAll(); return; }
            if (queue.Count == 0) return;
            foreach (var order in queue) order.elapsed += Time.deltaTime;
            for (int i = 0; i < queue.Count;)
            {
            if (queue[i].elapsed < queue[i].duration) { i++; continue; }
            var prefab = unitPrefabs[queue[i].type];
            if (!FindSpawnPosition(prefab.GetComponent<RTSUnit>().stats.radius, out var position))
            { Message = "Exit blocked / waiting for space"; i++; continue; }
            var spawned = Instantiate(prefab, position, Quaternion.identity).GetComponent<RTSUnit>();
            spawned.name = (Team == UnitTeam.Enemy ? "Enemy " : "") + spawned.stats.displayName + " Reinforcement";
            spawned.team = Team;
            spawned.GetComponent<UnitCombat>().battleController = controller;
            if (spawned.GetComponent<WorkerGatherer>()) spawned.GetComponent<WorkerGatherer>().home = this;
            var army = new List<RTSUnit>(Team == UnitTeam.Player ? controller.units : controller.enemies) { spawned };
            if (Team == UnitTeam.Player) controller.units = army.ToArray(); else controller.enemies = army.ToArray();
            if (Team == UnitTeam.Enemy)
            {
                var visual = spawned.gameObject.AddComponent<FogHiddenVisual>(); visual.fog = controller.fog; visual.ApplyNow();
            }
            var visible = new List<RTSUnit>(healthBars.units) { spawned }; healthBars.units = visible.ToArray();
            controller.statistics?.RecordTrained(Team);
            BattleEffects.Burst(BattleEffect.Spawn, position, 10);
            UnitProduced?.Invoke(spawned);
            queue.RemoveAt(i);
            // Other completed slots must see this frame's newly spawned colliders.
            Physics.SyncTransforms();
            Message = spawned.stats.displayName + " ready";
            }
        }
        public bool FindSpawnPosition(float radius, out Vector3 result)
        {
            for (int i = 0; i < 33; i++)
            {
                float distance = i == 0 ? 0 : 2.5f + ((i - 1) / 8) * 2f;
                float angle = i * Mathf.PI / 4;
                var candidate = rallyPoint + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                if (!NavMesh.SamplePosition(candidate, out var hit, 1, NavMesh.AllAreas)) continue;
                if (Physics.CheckSphere(hit.position + Vector3.up * .6f, radius + .15f, (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13))) continue;
                result = hit.position; return true;
            }
            result = Vector3.zero; return false;
        }
    }
}
