using UnityEngine;

namespace PrehistoricFrontiers
{
    [CreateAssetMenu(menuName = "Prehistoric Frontiers/Unit Stats")]
    public sealed class UnitStats : ScriptableObject
    {
        public string displayName;
        public string role;
        public float moveSpeed = 5;
        public float radius = .5f;
        [Min(1)] public float maxHealth = 100;
        [Min(1)] public float attackDamage = 16;
        [Min(.1f)] public float attackCooldown = 1;
        [Min(.1f)] public float attackRange = 1;
        [Min(1)] public float detectionRadius = 7;
        [Min(0)] public int foodCost = 80;
        [Min(0)] public int woodCost = 30;
        [Min(.1f)] public float productionTime = 5;
        public Color color = Color.white;
        public bool isWorker;
    }
}
