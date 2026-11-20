using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class FogHiddenVisual : MonoBehaviour
    {
        public FogOfWar fog;
        Renderer[] renderers;
        int childCount = -1;
        void LateUpdate() { ApplyNow(); }
        public void ApplyNow()
        {
            if (renderers == null || childCount != transform.childCount)
            { renderers = GetComponentsInChildren<Renderer>(true); childCount = transform.childCount; }
            bool hidden = fog && !fog.IsVisible(UnitTeam.Player, transform.position);
            foreach (var renderer in renderers) if (renderer) renderer.forceRenderingOff = hidden;
        }
        void OnDisable() { if (renderers != null) foreach (var renderer in renderers) if (renderer) renderer.forceRenderingOff = false; }
    }
}
