using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public enum ResourceKind { Food, Wood }
    public sealed class ResourceNode : MonoBehaviour
    {
        public ResourceKind kind;
        public int capacity = 200;
        public int Remaining { get; private set; }
        void Awake() { Remaining = Mathf.Max(0, capacity); }
        public int Extract(int requested)
        {
            int amount = Mathf.Min(Mathf.Max(0, requested), Remaining);
            Remaining -= amount;
            if (Remaining == 0)
            {
                foreach (var obstacle in GetComponentsInChildren<NavMeshObstacle>()) obstacle.enabled = false;
                foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
                foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            }
            return amount;
        }
    }
}
