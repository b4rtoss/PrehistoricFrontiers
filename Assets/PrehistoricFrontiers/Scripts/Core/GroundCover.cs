using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class GroundCover : MonoBehaviour
    {
        public Texture2D groundMap;
        public Material grassMaterial;
        public static GroundCover Active { get; private set; }
        readonly Vector4[] clearings = new Vector4[64];
        Material runtimeGrass;
        float nextRefresh;

        void Awake()
        {
            Active = this;
            runtimeGrass = new Material(grassMaterial);
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = runtimeGrass;
            RefreshClearings();
        }
        void LateUpdate()
        {
            if (Time.unscaledTime < nextRefresh) return;
            RefreshClearings();
            nextRefresh = Time.unscaledTime + .25f;
        }
        public void RefreshClearings()
        {
            int count = 0;
            foreach (var building in FindObjectsByType<BuildingHealth>())
            {
                if (!building.IsAlive || count == clearings.Length) continue;
                var point = building.transform.position;
                clearings[count++] = new Vector4(point.x, point.z, building.GetComponent<ConstructionSite>() ? 2.4f : 2.9f, 0);
            }
            runtimeGrass.SetInt("_ClearingCount", count);
            runtimeGrass.SetVectorArray("_Clearings", clearings);
        }
        public Color MapColor(Vector3 world) => groundMap.GetPixelBilinear((world.x + 25) / 50, (world.z + 25) / 50);
        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (runtimeGrass) Destroy(runtimeGrass);
        }
    }
}
