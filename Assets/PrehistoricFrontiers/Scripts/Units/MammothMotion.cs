using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class MammothMotion : MonoBehaviour
    {
        public Transform torso, head, tail, trunk;
        public Transform[] ears;
        // Front left/right, then rear left/right; the diagonal pairs alternate.
        public Transform[] upperLegs, lowerLegs, feet;
        RTSUnit unit;
        UnitCombat combat;
        Vector3 torsoPosition, headPosition;
        Quaternion headRotation, tailRotation, trunkRotation;
        readonly Quaternion[] earRotations = new Quaternion[2];
        readonly Quaternion[] upperRotations = new Quaternion[4], lowerRotations = new Quaternion[4], footRotations = new Quaternion[4];
        float phase, offset;

        void Awake()
        {
            unit = GetComponentInParent<RTSUnit>(); combat = unit ? unit.GetComponent<UnitCombat>() : null;
            torsoPosition = torso.localPosition; headPosition = head.localPosition;
            headRotation = head.localRotation; tailRotation = tail.localRotation; trunkRotation = trunk.localRotation;
            for(int i=0;i<2;i++) earRotations[i]=ears[i].localRotation;
            for (int i = 0; i < 4; i++)
            {
                upperRotations[i] = upperLegs[i].localRotation;
                lowerRotations[i] = lowerLegs[i].localRotation;
                footRotations[i] = feet[i].localRotation;
            }
            offset = transform.position.x * .37f + transform.position.z * .19f;
        }

        void LateUpdate()
        {
            if (!unit || !unit.IsAlive || combat && combat.battleController && (combat.battleController.IsPaused || combat.battleController.MatchOver)) return;
            float speed = unit.Agent && unit.Agent.isOnNavMesh ? Mathf.Clamp01(unit.Agent.velocity.magnitude / Mathf.Max(.1f, unit.Agent.speed)) : 0;
            phase += Time.deltaTime * Mathf.Lerp(1, 6, speed);
            float stride = Mathf.Sin(phase) * speed;
            torso.localPosition = torsoPosition + Vector3.up * (Mathf.Sin(Time.time * 1.5f + offset) * .008f + Mathf.Abs(Mathf.Sin(phase)) * speed * .018f);
            for (int i = 0; i < 4; i++)
            {
                float step = (i == 0 || i == 3 ? 1 : -1) * stride;
                upperLegs[i].localRotation = upperRotations[i] * Quaternion.Euler(step * 12, 0, 0);
                lowerLegs[i].localRotation = lowerRotations[i] * Quaternion.Euler(Mathf.Max(0, -step) * 17, 0, 0);
                feet[i].localRotation = footRotations[i] * Quaternion.Euler(-step * 10, 0, 0);
            }
            float strike = combat ? Mathf.Clamp01(1 - (Time.time - combat.LastAttackAt) / .38f) : 0;
            head.localPosition = headPosition + Vector3.forward * strike * .13f;
            head.localRotation = headRotation * Quaternion.Euler(-strike * 7, Mathf.Sin(Time.time + offset) * 1.4f, 0);
            trunk.localRotation = trunkRotation * Quaternion.Euler(Mathf.Sin(Time.time * 1.4f + offset) * 2 + strike * 6, 0, Mathf.Sin(Time.time + offset) * 2);
            for(int i=0;i<2;i++) ears[i].localRotation=earRotations[i]*Quaternion.Euler(0,Mathf.Sin(Time.time*1.2f+offset)*(i==0 ? 2:-2),0);
            tail.localRotation = tailRotation * Quaternion.Euler(0, Mathf.Sin(Time.time * 1.6f + offset) * 3 + stride * 4, 0);
        }
    }
}
