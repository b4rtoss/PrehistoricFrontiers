using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class RTSUnit : MonoBehaviour
    {
        public UnitStats stats;
        public UnitTeam team = UnitTeam.Player;
        public GameObject selectionIndicator;
        public bool IsSelected { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public bool IsAlive => gameObject.activeInHierarchy && GetComponent<UnitHealth>() && !GetComponent<UnitHealth>().IsDepleted;
        public ClinicPatient Treatment { get; internal set; }
        public bool IsInClinic => Treatment && Treatment.Inside;
        public bool IsAvailable => IsAlive && !IsInClinic;
        public bool IsOnClinicOrder => Treatment && Treatment.Clinic;
        void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Agent.speed = stats.moveSpeed;
            SetSelected(false);
        }
        void Start()
        {
            // Surface.OnEnable loads the baked data before any Start callback.
            Agent.enabled = true;
            if (!Agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 3, Agent.areaMask))
                Agent.Warp(hit.position);
        }
        public void SetSelected(bool value)
        {
            value = value && team == UnitTeam.Player && IsAvailable;
            IsSelected = value;
            if (selectionIndicator) selectionIndicator.SetActive(value);
        }
        public bool MoveTo(Vector3 destination)
        {
            if (!NavigateTo(destination)) return false;
            Treatment?.Cancel();
            GetComponent<WorkerGatherer>()?.Cancel();
            GetComponent<UnitCombat>()?.BeginMove();
            return true;
        }
        public bool NavigateTo(Vector3 destination)
        {
            return Agent && NavMesh.SamplePosition(destination, out var hit, 2f, Agent.areaMask)
                && UnitNavigation.TryPath(this, hit.position, out var path) && Agent.SetPath(path);
        }
        public void Stop()
        {
            Treatment?.Cancel();
            GetComponent<WorkerGatherer>()?.Cancel();
            GetComponent<UnitCombat>()?.Hold();
            if (Agent && Agent.isOnNavMesh) Agent.ResetPath();
        }
        public void Die(Vector3? attackerPosition = null)
        {
            if (!gameObject.activeSelf) return;
            GetComponent<UnitCombat>()?.battleController?.statistics?.RecordUnitLost(this);
            BattleEffects.Burst(BattleEffect.Death, transform.position, 12, Mathf.Clamp(stats.radius,.5f,1.2f));
            SetSelected(false);
            Stop();
            if (Agent) Agent.enabled = false;
            var reaction = GetComponent<UnitHitReaction>();
            if (reaction) reaction.enabled = false;
            UnitDeathVisual.Play(this, attackerPosition);
            gameObject.SetActive(false);
        }
    }
}
