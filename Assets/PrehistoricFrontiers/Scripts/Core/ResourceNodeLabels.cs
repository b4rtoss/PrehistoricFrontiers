using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ResourceNodeLabels : MonoBehaviour
    {
        public Camera sceneCamera;
        public ResourceNode[] nodes;
        public FogOfWar fog;
        GUIStyle style;
        void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.box) { fontSize = 13, padding = new RectOffset(), alignment = TextAnchor.MiddleCenter };
            var layout = HudLayout.Current; style.fontSize = layout.Font(13);
            foreach (var node in nodes)
            {
                if (!node || node.Remaining <= 0 || fog && !fog.IsVisible(UnitTeam.Player, node.transform.position)) continue;
                var p = sceneCamera.WorldToScreenPoint(node.transform.position + Vector3.up * (node.kind == ResourceKind.Wood ? 3.5f : 1.5f));
                if (p.z <= 0 || p.x < 0 || p.x > Screen.width || p.y < 0 || p.y > Screen.height || BattleHUD.IsOverPanel(p)) continue;
                var bounds = new Rect(p.x - 62 * layout.Scale, Screen.height - p.y, 124 * layout.Scale, 24 * layout.Scale);
                if (!layout.OverlapsPanel(bounds)) GUI.Box(bounds, (node.kind == ResourceKind.Food ? "ЕДА " : "ДЕРЕВО ") + node.Remaining, style);
            }
        }
    }
}
