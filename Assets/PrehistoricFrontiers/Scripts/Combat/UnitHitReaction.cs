using UnityEngine;

namespace PrehistoricFrontiers
{
    // Recoil belongs to the rendered model, never the navigation or selection collider.
    public sealed class UnitHitReaction : MonoBehaviour
    {
        public const float Duration = .32f;
        public float Remaining { get; private set; }
        public int HitCount { get; private set; }
        public bool IsFlashing => Remaining > Duration - .2f;
        RTSUnit unit;
        Transform visual;
        Vector3 restPosition, direction;
        Quaternion restRotation;
        Renderer[] renderers;
        MaterialPropertyBlock[][] originals;
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public static Transform FindModel(RTSUnit unit)
        {
            var raptor = unit.GetComponentInChildren<RaptorMotion>(true);
            if (raptor) return raptor.transform;
            var triceratops = unit.GetComponentInChildren<TriceratopsMotion>(true);
            if (triceratops) return triceratops.transform;
            var mammoth = unit.GetComponentInChildren<MammothMotion>(true);
            if (mammoth) return mammoth.transform;
            var worker = unit.GetComponentInChildren<WorkerMotion>(true);
            if (worker) return worker.transform;
            // Legacy draft models have no articulation and share Visual with the collider.
            // Leave those stationary rather than animate a gameplay collider.
            return null;
        }

        void Awake()
        {
            unit = GetComponent<RTSUnit>();
            visual = FindModel(unit);
            if (!visual) return;
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            originals = new MaterialPropertyBlock[renderers.Length][];
            block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                originals[i] = new MaterialPropertyBlock[renderers[i].sharedMaterials.Length];
                for (int j = 0; j < originals[i].Length; j++)
                {
                    originals[i][j] = new MaterialPropertyBlock();
                    renderers[i].GetPropertyBlock(originals[i][j], j);
                }
            }
        }

        public void Play(Vector3? attackerPosition)
        {
            var c = unit.GetComponent<UnitCombat>()?.battleController;
            if (!visual || !c || c.IsPaused || c.MatchOver || !unit.IsAvailable
                || !c.CanSee(UnitTeam.Player, transform.position)) return;
            Vector3 away = attackerPosition.HasValue ? transform.position - attackerPosition.Value : -transform.forward;
            away.y = 0;
            if (away.sqrMagnitude < .001f) away = -transform.forward;
            direction = transform.InverseTransformDirection(away.normalized);
            Remaining = Duration;
            HitCount++;
            Apply();
        }

        void LateUpdate()
        {
            if (Remaining <= 0) return;
            var c = unit.GetComponent<UnitCombat>()?.battleController;
            if (!unit.IsAvailable || !c || !c.CanSee(UnitTeam.Player, transform.position)) { ResetVisual(); return; }
            if (c.IsPaused || c.MatchOver) return;
            Remaining = Mathf.Max(0, Remaining - Time.deltaTime);
            if (Remaining == 0) ResetVisual(); else Apply();
        }

        void Apply()
        {
            float elapsed = Duration - Remaining;
            float kick = Mathf.Sin(Mathf.Clamp01(elapsed / Duration) * Mathf.PI) * (Remaining / Duration);
            visual.localPosition = restPosition + direction * (Mathf.Clamp(unit.stats.radius * .2f, .08f, .22f) * kick);
            visual.localRotation = restRotation * Quaternion.Euler(direction.z * 9 * kick, 0, -direction.x * 9 * kick);
            float flash = Mathf.Clamp01(1 - elapsed / .2f);
            for (int i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    if (!materials[j] || !materials[j].HasProperty(ColorId)) continue;
                    renderers[i].GetPropertyBlock(block, j);
                    if (flash > 0)
                    {
                        Color original = originals[i][j].HasColor(ColorId) ? originals[i][j].GetColor(ColorId) : materials[j].GetColor(ColorId);
                        Color tint = Color.Lerp(original, new Color(1, .88f, .65f, original.a), flash * .9f);
                        block.SetColor(ColorId, tint);
                        renderers[i].SetPropertyBlock(block, j);
                    }
                    else renderers[i].SetPropertyBlock(originals[i][j], j);
                }
            }
        }

        void ResetVisual()
        {
            Remaining = 0;
            if (!visual) return;
            visual.localPosition = restPosition;
            visual.localRotation = restRotation;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i]) for (int j = 0; j < originals[i].Length; j++) renderers[i].SetPropertyBlock(originals[i][j], j);
        }
        void OnDisable() { ResetVisual(); }
    }
}
