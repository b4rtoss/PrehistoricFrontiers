using UnityEngine;

namespace PrehistoricFrontiers
{
    [RequireComponent(typeof(RTSUnit))]
    public sealed class UnitHealth : MonoBehaviour
    {
        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public float LastHostileDamageAt { get; private set; } = -100;
        public bool IsDepleted => CurrentHealth <= 0;
        RTSUnit unit;

        void Awake()
        {
            unit = GetComponent<RTSUnit>();
            MaxHealth = Mathf.Max(1, unit.stats.maxHealth);
            CurrentHealth = MaxHealth;
        }
        public void TakeDamage(float amount, UnitTeam? attackerTeam = null)
        {
            if (unit.IsInClinic || IsDepleted || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return;
            bool hostile = attackerTeam.HasValue && attackerTeam.Value != unit.team;
            if (hostile)
            {
                if (unit.team == UnitTeam.Player) unit.GetComponent<UnitCombat>()?.battleController?.alerts?.Notify(unit);
                LastHostileDamageAt = Time.time;
                BattleEffects.Hit(unit);
            }
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (IsDepleted) unit.Die();
            else if (hostile)
                unit.GetComponent<WorkerGatherer>()?.RetreatForProtection();
        }
        public void Heal(float amount)
        {
            if (IsDepleted || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }
    }
}
