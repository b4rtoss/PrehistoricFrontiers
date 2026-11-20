using UnityEngine;

namespace PrehistoricFrontiers
{
    [RequireComponent(typeof(ProductionBuilding))]
    public sealed class EnemyProductionAI : MonoBehaviour
    {
        public ProductionBuilding production;
        int preferredType;
        float nextDecisionAt;
        public EnemyEconomyAI Economy => GetComponent<EnemyEconomyAI>();
        public ProductionBuilding CombatProduction => production && !production.workersOnly ? production
            : Economy && Economy.construction ? Economy.construction.WarriorProduction : null;
        public string Status { get; private set; } = "Подготовка";
        public bool TryPlanNext()
        {
            if (!isActiveAndEnabled || !production || production.Team != UnitTeam.Enemy) return false;
            if (production.controller.MatchOver || !GetComponent<BuildingHealth>().IsAlive)
            { Status = "Производство остановлено"; return false; }
            if (production.controller.IsPreparing) { Status = "Подготовка: " + Mathf.CeilToInt(production.controller.PreparationRemaining) + " с"; return false; }
            var factory = CombatProduction;
            if (!factory) { Status = Economy && Economy.isActiveAndEnabled ? Economy.Status : "Нужна своя казарма"; return false; }
            if (Economy && Economy.isActiveAndEnabled && Economy.WorkersAlive + production.QueueCount < Economy.workerTarget
                && production.wallet.CanAfford(production.Stats(3).foodCost, production.Stats(3).woodCost))
            { Status = "Восстановление рабочих"; return false; }
            if (factory.TeamQueueCount >= ProductionBuilding.QueueLimit) { Status = "Все места обучения заняты"; return false; }
            if (factory.ArmyAlive + factory.TeamQueueCount >= ProductionBuilding.PopulationLimit) { Status = "Лимит отряда"; return false; }
            int reserve = Economy && Economy.isActiveAndEnabled ? 50 : 0;
            for (int offset = 0; offset < factory.unitPrefabs.Length; offset++)
            {
                int type = (preferredType + offset) % factory.unitPrefabs.Length;
                var stats = factory.Stats(type);
                if (stats.isWorker || factory.wallet.Food - stats.foodCost < reserve || !factory.wallet.CanAfford(stats.foodCost, stats.woodCost)) continue;
                if (!factory.TryQueue(type)) continue;
                preferredType = (type + 1) % factory.unitPrefabs.Length;
                Status = "Обучение: " + stats.displayName; return true;
            }
            Status = factory.QueueCount > 0 ? "Обучение: " + factory.QueueCount : "Ожидает ресурсы";
            return false;
        }
        void Update()
        {
            if (!production || production.controller.IsPaused || Time.time < nextDecisionAt) return;
            nextDecisionAt = Time.time + .5f;
            for (int i = 0; i < ProductionBuilding.QueueLimit; i++) if (!TryPlanNext()) break;
        }
    }
}
