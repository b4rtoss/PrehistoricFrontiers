using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class RTSSelectionController : MonoBehaviour
    {
        public Camera sceneCamera;
        public RTSUnit[] units;
        public RTSUnit[] enemies;
        public BuildingHealth playerBase;
        public BuildingHealth enemyBase;
        public HealingClinic SelectedClinic {get;private set;}
        public FogOfWar fog;
        public AttackNotifications alerts;
        public ConstructionSystem construction;
        public MatchStatistics statistics;
        public float preparationDuration = 20;
        public float PreparationRemaining => Mathf.Max(0, preparationDuration - (statistics ? statistics.Duration : Time.timeSinceLevelLoad));
        public bool IsPreparing => PreparationRemaining > 0;
        bool matchEndHandled;

        public void FinishMatchIfEnded()
        {
            if (matchEndHandled || !MatchOver) return;
            matchEndHandled = true;
            StopLivingUnits(units);
            StopLivingUnits(enemies);
            AttackMovePending = false;
            construction?.CancelPlacement();
            markerUntil = 0;
            if (destinationMarker) destinationMarker.gameObject.SetActive(false);
        }

        static void StopLivingUnits(RTSUnit[] army)
        {
            if (army == null) return;
            foreach (var unit in army)
            {
                if (!unit || !unit.IsAlive) continue;
                unit.Stop();
                if (unit.Agent && unit.Agent.enabled && unit.Agent.isOnNavMesh) unit.Agent.velocity = Vector3.zero;
            }
        }

        public bool IsPaused { get; private set; }
        float resumeTimeScale = 1, pauseStartedAt, pausedDuration;
        public float UnpausedTime => Time.unscaledTime - pausedDuration - (IsPaused ? Time.unscaledTime - pauseStartedAt : 0);
        public void SetPaused(bool value)
        {
            if (IsPaused == value || value && MatchOver) return;
            if (value)
            {
                resumeTimeScale = Time.timeScale > 0 ? Time.timeScale : 1;
                pauseStartedAt = Time.unscaledTime; IsPaused = true;
                Time.timeScale = 0; AudioListener.pause = true;
                AttackMovePending = false; construction?.CancelPlacement();
            }
            else
            {
                pausedDuration += Time.unscaledTime - pauseStartedAt; IsPaused = false;
                Time.timeScale = resumeTimeScale; AudioListener.pause = false;
            }
        }
        void OnDestroy() { if (IsPaused) { Time.timeScale = resumeTimeScale; AudioListener.pause = false; } }
        public void RestartMatch()
        {
            SetPaused(false); Time.timeScale = 1; AudioListener.pause = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
        public void CancelAttackMoveInput() { AttackMovePending = false; }
        public BuildingHealth FindEnemyBuilding(UnitTeam team, Vector3 origin)
        {
            BuildingHealth best = null; float distance = float.PositiveInfinity;
            foreach (var building in FindObjectsByType<BuildingHealth>())
            {
                if (!building.IsAlive || building.team == team || !CanSee(team, building.transform.position)) continue;
                float d = (origin - building.transform.position).sqrMagnitude;
                if (d < distance) { best = building; distance = d; }
            }
            return best;
        }
        public bool CanSee(UnitTeam team, Vector3 position) => !fog || fog.IsVisible(team, position);
        public bool HasExplored(UnitTeam team, Vector3 position) => !fog || fog.WasExplored(team, position);
        public int VisibleEnemies
        {
            get { int count = 0; foreach (var enemy in enemies) if (enemy && enemy.IsAlive && CanSee(UnitTeam.Player, enemy.transform.position)) count++; return count; }
        }
        public BuildingHealth OpposingBase(UnitTeam team) => team == UnitTeam.Player ? enemyBase : playerBase;
        public LineRenderer destinationMarker;
        [SerializeField] bool autoBattleEnabled;
        [SerializeField] bool autoScoutEnabled;
        [SerializeField] bool workerProtectionEnabled = true;
        public bool WorkerProtectionEnabled => workerProtectionEnabled;
        public void SetWorkerProtection(bool enabled)
        {
            workerProtectionEnabled = enabled;
            Status = enabled ? "Защита рабочих ВКЛ / При нападении отступают к базе" : "Защита рабочих ВЫКЛ / Отступление вручную";
        }
        public bool AutoBattleEnabled => autoBattleEnabled;
        public bool AutoScoutEnabled => autoScoutEnabled;
        public bool AutoBattleFor(UnitTeam team) => team == UnitTeam.Enemy || autoBattleEnabled;
        public bool AutoScoutFor(UnitTeam team) => team == UnitTeam.Enemy || autoScoutEnabled;
        public void SetAutoScout(bool enabled)
        {
            autoScoutEnabled = enabled;
            foreach (var unit in units) if (unit && unit.IsAlive) unit.GetComponent<UnitCombat>()?.SetScoutAutomation(enabled);
            Status = enabled ? "Auto scouting ON / Explore unknown territory" : "Auto scouting OFF / Scout manually";
        }
        public void SetAutoBattle(bool enabled)
        {
            autoBattleEnabled = enabled;
            foreach (var unit in units) if (unit && unit.IsAlive) unit.GetComponent<UnitCombat>()?.SetAutomation(enabled);
            Status = enabled ? "Auto battle ON / Armies engage automatically" : "Auto battle OFF / Give manual orders";
        }
        public readonly List<RTSUnit> Selected = new List<RTSUnit>();
        public string Status { get; private set; } = "Выберите отряд и отдайте приказ / G — автобой / H — разведка";
        float markerUntil;
        public bool AttackMovePending { get; private set; }
        public void ArmAttackMove()
        {
            if (IsPaused) return;
            PruneSelection();
            construction?.CancelPlacement();
            AttackMovePending = Selected.Exists(unit => !unit.stats.isWorker);
            Status = AttackMovePending ? "ATTACK MOVE / Left-click destination / Esc to cancel" : "Select combat animals first";
        }
        public int AlliesAlive => CountAlive(units);
        public int EnemiesAlive => CountAlive(enemies);
        public bool MatchOver => playerBase && enemyBase && (!playerBase.IsAlive || !enemyBase.IsAlive);
        static int CountAlive(RTSUnit[] squad)
        {
            int count = 0;
            foreach (var unit in squad) if (unit && unit.IsAlive) count++;
            return count;
        }
        public void PruneSelection() { Selected.RemoveAll(unit => !unit || !unit.IsAvailable); }
        void Update()
        {
            PruneSelection();
            if(SelectedClinic && !SelectedClinic.isActiveAndEnabled) { SelectedClinic=null; Status="Лечебница недоступна / Лечение прекращено"; }
            if (Input.GetKeyDown(KeyCode.R))
            { RestartMatch(); return; }
            if (MatchOver)
            {
                FinishMatchIfEnded();
                Status = !playerBase.IsAlive ? "DEFEAT / Base destroyed / Press R to restart" : "VICTORY / Enemy base destroyed / Press R to restart";
                return;
            }
            if (Input.GetKeyDown(KeyCode.P)) SetPaused(!IsPaused);
            if (IsPaused) return;
            if (Input.GetKeyDown(KeyCode.G)) SetAutoBattle(!AutoBattleEnabled);
            if (Input.GetKeyDown(KeyCode.H)) SetAutoScout(!AutoScoutEnabled);
            if (destinationMarker) destinationMarker.gameObject.SetActive(Time.time < markerUntil);
            if (Input.GetKeyDown(KeyCode.Escape)) Clear();
            if (Input.GetKeyDown(KeyCode.F)) ArmAttackMove();
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space)) AttackMovePending = false;
            if (Input.GetKeyDown(KeyCode.A) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            { Clear(); foreach (var unit in units) Select(unit, true); }
            if (Input.GetKeyDown(KeyCode.Space)) foreach (var unit in Selected) unit.Stop();
            if (BattleHUD.IsOverPanel(Input.mousePosition)) return;
            var ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
            {
                if (construction && construction.Pending)
                {
                    if (Physics.Raycast(ray, out var buildPoint, 300, 1 << 8)) construction.PlacePending(buildPoint.point);
                    return;
                }
                if (AttackMovePending)
                {
                    if (Physics.Raycast(ray, out var destination, 300, 1 << 8)) IssueAttackMove(destination.point);
                    return;
                }
                // A single ray includes obstacles so units cannot be picked through a rock.
                if (Physics.Raycast(ray, out var hit, 300, (1 << 8) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13)))
                {
                    var clinic = hit.collider.GetComponentInParent<HealingClinic>();
                    if (clinic && clinic.Health.team == UnitTeam.Player) { SelectedClinic=clinic; if(Selected.Count>0) IssueTreat(clinic); else Status=clinic.Summary; return; }
                    var factory = hit.collider.GetComponentInParent<ProductionBuilding>();
                    if (factory && factory.Team == UnitTeam.Player && construction) { construction.ShowProduction(factory); return; }
                    var unit = hit.collider.GetComponentInParent<RTSUnit>();
                    bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (unit && (unit.team == UnitTeam.Player || CanSee(UnitTeam.Player, unit.transform.position))) Select(unit, additive); else if (!additive) Clear();
                }
            }
            if (Input.GetMouseButtonDown(1) && Selected.Count > 0 &&
                Physics.Raycast(ray, out var ground, 300, (1 << 8) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13)))
            {
                var target = ground.collider.GetComponentInParent<RTSUnit>();
                var resource = ground.collider.GetComponentInParent<ResourceNode>();
                var home = ground.collider.GetComponentInParent<ProductionBuilding>();
                var building = ground.collider.GetComponentInParent<BuildingHealth>();
                bool unknown = target && target.team == UnitTeam.Enemy && !CanSee(UnitTeam.Player, target.transform.position)
                    || building && building.team == UnitTeam.Enemy && !CanSee(UnitTeam.Player, building.transform.position)
                    || resource && !HasExplored(UnitTeam.Player, resource.transform.position);
                if (unknown)
                {
                    if (Physics.Raycast(ray, out var terrain, 300, 1 << 8)) IssueMove(terrain.point);
                    return;
                }
                var site = building ? building.GetComponent<ConstructionSite>() : null;
                if (site && site.Health.team == UnitTeam.Player && !site.Complete) construction.Resume(site);
                else if (building && building.GetComponent<HealingClinic>() && building.team == UnitTeam.Player) IssueTreat(building.GetComponent<HealingClinic>());
                else if (building && building.team == UnitTeam.Enemy) IssueAttackBase(building);
                else if (resource) IssueGather(resource);
                else if (home) IssueReturn();
                else if (target && target.team == UnitTeam.Enemy) IssueAttack(target);
                else if (ground.collider.gameObject.layer == 8) IssueMove(ground.point);
            }
        }
        public void Clear()
        {
            SelectedClinic=null;
            AttackMovePending = false;
            construction?.CancelPlacement();
            foreach (var unit in Selected) if (unit) unit.SetSelected(false);
            Selected.Clear();
            Status = "Select an animal to begin";
        }
        public void Select(RTSUnit unit, bool additive = false)
        {
            if (!additive) Clear();
            if (!unit || !unit.IsAvailable || unit.team != UnitTeam.Player)
            {
                Status = unit && unit.IsInClinic ? "Юнит находится на лечении" : "Enemy unit / cannot be commanded";
                return;
            }
            if (Selected.Contains(unit)) { unit.SetSelected(false); Selected.Remove(unit); }
            else { Selected.Add(unit); unit.SetSelected(true); }
            Status = Selected.Count == 0 ? "Select an animal to begin" : "Right-click enemy to attack / ground to move";
        }
        public int IssueAttack(RTSUnit target)
        {
            if (IsPaused || MatchOver) return 0;
            PruneSelection();
            int accepted = 0;
            foreach (var unit in Selected)
                if (unit.GetComponent<UnitCombat>() && unit.GetComponent<UnitCombat>().Attack(target)) accepted++;
            Status = accepted > 0 ? "Attacking " + target.stats.displayName : "Invalid attack target";
            return accepted;
        }
        public int IssueAttackBase(BuildingHealth target)
        {
            if (IsPaused || MatchOver) return 0;
            PruneSelection(); int accepted = 0;
            foreach (var unit in Selected)
                if (unit.GetComponent<UnitCombat>() && unit.GetComponent<UnitCombat>().Attack(target)) accepted++;
            Status = accepted > 0 ? "Attacking enemy base" : "Select combat animals / invalid base target";
            return accepted;
        }
        public int IssueTreat(HealingClinic clinic)
        {
            if(IsPaused || MatchOver || !clinic) return 0;
            PruneSelection();int accepted=0;
            foreach(var unit in Selected) if(clinic.Request(unit)) accepted++;
            SelectedClinic=clinic;
            Status=accepted>0 ? "На лечение: "+accepted+" / "+clinic.Summary : "Нужны раненые юниты и доступная своя лечебница";
            return accepted;
        }
        public int IssueGather(ResourceNode node)
        {
            if (IsPaused || MatchOver) return 0;
            PruneSelection(); int accepted = 0;
            foreach (var unit in Selected)
                if (unit.GetComponent<WorkerGatherer>() && unit.GetComponent<WorkerGatherer>().Gather(node)) accepted++;
            Status = accepted > 0 ? "Gathering " + node.kind : "Select a worker / resource unavailable";
            return accepted;
        }
        public int IssueReturn()
        {
            if (IsPaused || MatchOver) return 0;
            PruneSelection(); int accepted = 0;
            foreach (var unit in Selected)
                if (unit.GetComponent<WorkerGatherer>() && unit.GetComponent<WorkerGatherer>().ReturnToBase()) accepted++;
            Status = accepted > 0 ? "Returning resources to base" : "No worker cargo to deposit";
            return accepted;
        }
        public int IssueAttackMove(Vector3 point)
        {
            if (IsPaused || MatchOver) return 0;
            AttackMovePending = false;
            PruneSelection();
            int accepted = 0, index = 0;
            int combatCount = Selected.FindAll(unit => !unit.stats.isWorker).Count;
            foreach (var unit in Selected)
            {
                if (unit.stats.isWorker) continue;
                var offset = new Vector3((index++ - (combatCount - 1) * .5f) * 4, 0, 0);
                if (unit.GetComponent<UnitCombat>().AttackMove(point + offset)) accepted++;
            }
            Status = accepted > 0 ? "ATTACK MOVE / Engage nearby enemies, then resume route" : "Select combat animals / destination unreachable";
            if (accepted > 0 && destinationMarker)
            {
                destinationMarker.transform.position = point + Vector3.up * .08f;
                markerUntil = Time.time + 1.5f;
            }
            return accepted;
        }
        public int IssueMove(Vector3 point)
        {
            if (IsPaused || MatchOver) return 0;
            PruneSelection();
            int accepted = 0;
            for (int i = 0; i < Selected.Count; i++)
            {
                // Leave enough space for the largest capsules when issuing a group order.
                var offset = Selected.Count == 1 ? Vector3.zero : new Vector3((i - (Selected.Count - 1) * .5f) * 4, 0, 0);
                if (Selected[i].MoveTo(point + offset)) accepted++;
            }
            Status = accepted > 0 ? "Moving to destination" : "Destination is unreachable";
            if (accepted > 0 && destinationMarker)
            {
                destinationMarker.transform.position = point + Vector3.up * .08f;
                markerUntil = Time.time + 1.5f;
            }
            return accepted;
        }
    }
}
