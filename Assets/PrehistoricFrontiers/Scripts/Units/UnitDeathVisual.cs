using UnityEngine;

namespace PrehistoricFrontiers
{
    // A detached visual has no unit, navigation agent, hitboxes, or selection ring.
    public sealed class UnitDeathVisual : MonoBehaviour
    {
        public const float FallDuration = .7f;
        public const float RestDuration = 4;
        public const float SinkDuration = .9f;
        public float Age { get; private set; }
        public RTSUnit Source { get; private set; }
        RTSSelectionController controller;
        Renderer[] renderers;
        Vector3 origin;
        Quaternion upright;
        float side, drift, sinkDepth;
        bool landed;

        public static UnitDeathVisual Play(RTSUnit unit, Vector3? attackerPosition)
        {
            var model = UnitHitReaction.FindModel(unit);
            if (!model) return null;
            var pivot = new GameObject(unit.name + " fallen body");
            pivot.transform.SetPositionAndRotation(unit.transform.position, unit.transform.rotation);
            model.SetParent(pivot.transform, true);
            // Freeze the final pose; motion scripts must not keep references to an active rig.
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var visual = pivot.AddComponent<UnitDeathVisual>();
            visual.Source = unit;
            visual.controller = unit.GetComponent<UnitCombat>()?.battleController;
            visual.origin = pivot.transform.position;
            if (Physics.Raycast(visual.origin + Vector3.up * 5, Vector3.down, out var ground, 50, 1 << 8))
                visual.origin.y = ground.point.y;
            visual.upright = pivot.transform.rotation;
            float direction = attackerPosition.HasValue ? Vector3.Dot(attackerPosition.Value - unit.transform.position, unit.transform.right) : 0;
            visual.side = Mathf.Abs(direction) > .01f ? Mathf.Sign(direction) : (unit.team == UnitTeam.Player ? 1 : -1);
            visual.drift = Mathf.Clamp(unit.stats.radius * .25f, .12f, .35f);
            visual.renderers = model.GetComponentsInChildren<Renderer>(true);
            visual.ApplyPose();
            visual.ApplyVisibility();
            return visual;
        }

        void Update()
        {
            if (controller && (controller.IsPaused || controller.MatchOver)) return;
            Age += Time.deltaTime;
            if (Age >= FallDuration + RestDuration + SinkDuration) { Destroy(gameObject); return; }
            ApplyPose();
            if (!landed && Age >= FallDuration)
            {
                landed = true;
                BattleEffects.Burst(BattleEffect.Death, transform.position, 5, drift * 4);
            }
        }

        void ApplyPose()
        {
            float fall = Mathf.Clamp01(Age / FallDuration);
            float angle = fall * fall * 90;
            transform.rotation = upright * Quaternion.Euler(0, 0, side * angle);
            transform.position = origin + upright * Vector3.left * (side * drift * fall * fall);
            // Keep the lowest model point on the terrain throughout the roll, including wide horns and tusks.
            float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.enabled) continue;
                lowest = Mathf.Min(lowest, renderer.bounds.min.y);
                highest = Mathf.Max(highest, renderer.bounds.max.y);
            }
            if (float.IsInfinity(lowest)) return;
            transform.position += Vector3.up * (origin.y + .025f - lowest);
            if (fall >= 1) sinkDepth = highest - lowest + .15f;
            float sink = Mathf.Clamp01((Age - FallDuration - RestDuration) / SinkDuration);
            transform.position -= Vector3.up * (sinkDepth * sink * sink);
        }

        void LateUpdate() { ApplyVisibility(); }
        void ApplyVisibility()
        {
            bool hidden = controller && !controller.CanSee(UnitTeam.Player, origin);
            foreach (var renderer in renderers) if (renderer) renderer.forceRenderingOff = hidden;
        }
    }
}
