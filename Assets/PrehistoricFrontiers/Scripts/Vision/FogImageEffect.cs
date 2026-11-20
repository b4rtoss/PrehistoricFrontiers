using UnityEngine;

namespace PrehistoricFrontiers
{
    [RequireComponent(typeof(Camera))]
    public sealed class FogImageEffect : MonoBehaviour
    {
        public FogOfWar fog;
        public Material material;
        Material runtimeMaterial;
        Camera cam;
        void Awake() { cam = GetComponent<Camera>(); if (material) runtimeMaterial = new Material(material); }
        Vector3 GroundPoint(float x, float y)
        {
            var ray = cam.ViewportPointToRay(new Vector3(x, y, 0));
            return ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
        }
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!fog || !fog.Active || !runtimeMaterial || !fog.Mask) { Graphics.Blit(source, destination); return; }
            runtimeMaterial.SetTexture("_FogMask", fog.Mask);
            runtimeMaterial.SetVector("_BottomLeft", GroundPoint(0, 0)); runtimeMaterial.SetVector("_BottomRight", GroundPoint(1, 0)); runtimeMaterial.SetVector("_TopLeft", GroundPoint(0, 1));
            Graphics.Blit(source, destination, runtimeMaterial);
        }
        void OnDestroy() { if (runtimeMaterial) Destroy(runtimeMaterial); }
    }
}
