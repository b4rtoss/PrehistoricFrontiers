using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricFrontiers
{
    [RequireComponent(typeof(BuildingHealth))]
    public sealed class HealingClinic : MonoBehaviour
    {
        public RTSSelectionController controller;
        public const float Rate = 4, CombatDelay = 5;
        readonly List<ClinicPatient> patients = new List<ClinicPatient>();
        public BuildingHealth Health => GetComponent<BuildingHealth>();
        public int InsideCount => CountPatients(true);
        public int ApproachingCount => CountPatients(false);
        public string Summary => "ЛЕЧЕБНИЦА / Внутри: " + InsideCount + " / Подходят: " + ApproachingCount + " / До полного HP";
        int CountPatients(bool inside)
        {
            int count = 0;
            foreach (var patient in patients) if (patient && patient.Inside == inside) count++;
            return count;
        }
        public bool Request(RTSUnit unit)
        {
            var site = GetComponent<ConstructionSite>();
            if (!controller || controller.IsPaused || controller.MatchOver || !isActiveAndEnabled || !Health.IsAlive || site && !site.Complete
                || !unit || !unit.IsAvailable || unit.team != Health.team || unit.team != UnitTeam.Player || !unit.Agent.isOnNavMesh) return false;
            var health = unit.GetComponent<UnitHealth>();
            if (health.CurrentHealth >= health.MaxHealth) return false;
            if (unit.Treatment && unit.Treatment.Clinic == this) return true;
            if (!UnitNavigation.TryApproach(unit, Health, 24, .6f, out var approach)) return false;
            unit.Stop();
            if (!unit.NavigateTo(approach)) return false;
            var patient = unit.Treatment ? unit.Treatment : unit.gameObject.AddComponent<ClinicPatient>();
            patient.Assign(this, approach);
            patients.Add(patient);
            return true;
        }
        internal void Forget(ClinicPatient patient) { patients.Remove(patient); }
        void OnDisable()
        {
            foreach (var patient in patients.ToArray()) if (patient) patient.ClinicClosed();
            patients.Clear();
        }
    }
}
