using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class MatchStatistics : MonoBehaviour
    {
        public RTSSelectionController controller;
        public int AlliesLost { get; private set; }
        public int WorkersLost { get; private set; }
        public int EnemiesLost { get; private set; }
        public int UnitsTrained { get; private set; }
        public int FoodDelivered { get; private set; }
        public int WoodDelivered { get; private set; }
        public int BarracksBuilt { get; private set; }
        public int ClinicsBuilt { get; private set; }
        public int BuildingsLost { get; private set; }
        public int EnemyBuildingsLost { get; private set; }
        public int Survivors { get; private set; }
        public bool Finished { get; private set; }
        public bool Won { get; private set; }
        float startedAt, duration;
        public float Duration => Finished ? duration : Mathf.Max(0, Time.time - startedAt);
        public string DurationText => Mathf.FloorToInt(Duration / 60).ToString("00") + ":" + Mathf.FloorToInt(Duration % 60).ToString("00");
        void Awake() { startedAt = Time.time; }
        void Update() { FinishIfEnded(); }
        bool CanRecord => !Finished && controller && !controller.MatchOver;
        public void RecordUnitLost(RTSUnit unit)
        {
            if (!CanRecord) return;
            if (unit.team == UnitTeam.Player) { AlliesLost++; if (unit.stats.isWorker) WorkersLost++; }
            else EnemiesLost++;
        }
        public void RecordTrained(UnitTeam team) { if (CanRecord && team == UnitTeam.Player) UnitsTrained++; }
        public void RecordResources(UnitTeam team, ResourceKind kind, int amount)
        {
            if (!CanRecord || team != UnitTeam.Player || amount <= 0) return;
            if (kind == ResourceKind.Food) FoodDelivered += amount; else WoodDelivered += amount;
        }
        public void RecordBuilt(StructureKind kind)
        {
            if (!CanRecord) return;
            if (kind == StructureKind.Barracks) BarracksBuilt++; else ClinicsBuilt++;
        }
        public void RecordBuildingLost(UnitTeam team)
        {
            if (Finished) return;
            if (team == UnitTeam.Player) BuildingsLost++; else EnemyBuildingsLost++;
            FinishIfEnded();
        }
        public void FinishIfEnded()
        {
            if (Finished || !controller || !controller.MatchOver) return;
            duration = Mathf.Max(0, Time.time - startedAt); Survivors = controller.AlliesAlive;
            Won = controller.playerBase.IsAlive; Finished = true;
        }
    }
}
