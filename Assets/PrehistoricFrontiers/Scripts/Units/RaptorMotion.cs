using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class RaptorMotion : MonoBehaviour
    {
        public Transform torso, head, jaw, tail, leftHip, rightHip, leftKnee, rightKnee, leftFoot, rightFoot;
        RTSUnit unit;
        UnitCombat combat;
        Vector3 torsoPosition, headPosition;
        Quaternion headRotation, jawRotation, tailRotation, leftHipRotation, rightHipRotation, leftKneeRotation, rightKneeRotation, leftFootRotation, rightFootRotation;
        float phase, offset;
        void Awake()
        {
            unit = GetComponentInParent<RTSUnit>(); combat = unit ? unit.GetComponent<UnitCombat>() : null;
            torsoPosition = torso.localPosition; headPosition = head.localPosition;
            headRotation = head.localRotation; jawRotation = jaw.localRotation; tailRotation = tail.localRotation;
            leftHipRotation = leftHip.localRotation; rightHipRotation = rightHip.localRotation;
            leftKneeRotation = leftKnee.localRotation; rightKneeRotation = rightKnee.localRotation;
            leftFootRotation = leftFoot.localRotation; rightFootRotation = rightFoot.localRotation;
            offset = transform.position.x * .37f + transform.position.z * .19f;
        }
        void LateUpdate()
        {
            if (!unit || !unit.IsAlive || combat && combat.battleController && (combat.battleController.IsPaused || combat.battleController.MatchOver)) return;
            float speed = unit.Agent && unit.Agent.isOnNavMesh ? Mathf.Clamp01(unit.Agent.velocity.magnitude / Mathf.Max(.1f, unit.Agent.speed)) : 0;
            phase += Time.deltaTime * Mathf.Lerp(2, 12, speed);
            float stride = Mathf.Sin(phase) * speed;
            torso.localPosition = torsoPosition + Vector3.up * (Mathf.Sin(Time.time * 2 + offset) * .012f + Mathf.Abs(Mathf.Sin(phase)) * speed * .025f);
            leftHip.localRotation = leftHipRotation * Quaternion.Euler(stride * 22, 0, 0);
            rightHip.localRotation = rightHipRotation * Quaternion.Euler(-stride * 22, 0, 0);
            leftKnee.localRotation = leftKneeRotation * Quaternion.Euler(Mathf.Max(0, -stride) * 25, 0, 0);
            rightKnee.localRotation = rightKneeRotation * Quaternion.Euler(Mathf.Max(0, stride) * 25, 0, 0);
            leftFoot.localRotation = leftFootRotation * Quaternion.Euler(-stride * 12, 0, 0);
            rightFoot.localRotation = rightFootRotation * Quaternion.Euler(stride * 12, 0, 0);
            float bite = combat ? Mathf.Clamp01(1 - (Time.time - combat.LastAttackAt) / .28f) : 0;
            head.localPosition = headPosition + Vector3.forward * bite * .09f;
            head.localRotation = headRotation * Quaternion.Euler(-bite * 5, Mathf.Sin(Time.time * 1.3f + offset) * 2, 0);
            jaw.localRotation = jawRotation * Quaternion.Euler(bite * 19, 0, 0);
            tail.localRotation = tailRotation * Quaternion.Euler(0, Mathf.Sin(Time.time * 2 + offset) * 3 + stride * 5, 0);
        }
    }
}
