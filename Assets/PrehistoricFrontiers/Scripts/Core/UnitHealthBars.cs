using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class UnitHealthBars : MonoBehaviour
    {
        public Camera sceneCamera;
        public RTSUnit[] units;
        public BuildingHealth[] buildings;
        public FogOfWar fog;
        GUIStyle label, boxLabel;
        void OnGUI()
        {
            if (label == null) { label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter }; boxLabel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset() }; }
            var layout = HudLayout.Current; float scale = layout.Scale; label.fontSize = layout.Font(12); boxLabel.fontSize = layout.Font(12);
            foreach (var unit in units)
            {
                if (!unit || !unit.IsAvailable || unit.team == UnitTeam.Enemy && fog && !fog.IsVisible(UnitTeam.Player, unit.transform.position)) continue;
                var health = unit.GetComponent<UnitHealth>();
                if (!health) continue;
                var p = sceneCamera.WorldToScreenPoint(unit.transform.position + Vector3.up * (unit.Agent.height + .65f));
                if (p.z <= 0 || p.x < 0 || p.x > Screen.width || p.y < 0 || p.y > Screen.height) continue;
                if (BattleHUD.IsOverPanel(p)) continue;
                if (layout.OverlapsPanel(new Rect(p.x - 70 * scale, Screen.height - p.y - 24 * scale, 140 * scale, 54 * scale))) continue;
                var bar = new Rect(p.x - 44 * scale, Screen.height - p.y, 88 * scale, 7 * scale);
                var previous = GUI.color;
                GUI.color = new Color(.06f, .1f, .1f, .9f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = unit.team == UnitTeam.Enemy ? new Color(1, .28f, .25f) : new Color(.5f, .95f, .4f);
                GUI.DrawTexture(new Rect(bar.x + scale, bar.y + scale, 86 * scale * health.CurrentHealth / health.MaxHealth, 5 * scale), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(bar.x - 26 * scale, bar.y - 24 * scale, 140 * scale, 23 * scale), (unit.team == UnitTeam.Enemy ? "ENEMY / " : "") + unit.stats.displayName, label);
                GUI.Label(new Rect(bar.x - 10 * scale, bar.y + 8 * scale, 108 * scale, 22 * scale), health.CurrentHealth.ToString("0") + " / " + health.MaxHealth.ToString("0"), label);
                GUI.color = previous;
            }
            if (buildings == null) return;
            foreach (var building in buildings)
            {
                if (!building || !building.IsAlive || building.team == UnitTeam.Enemy && fog && !fog.IsVisible(UnitTeam.Player, building.transform.position)) continue;
                var p = sceneCamera.WorldToScreenPoint(building.transform.position + Vector3.up * 3.4f);
                if (p.z <= 0 || p.x < 0 || p.x > Screen.width || p.y < 0 || p.y > Screen.height || BattleHUD.IsOverPanel(p)) continue;
                if (layout.OverlapsPanel(new Rect(p.x - 110 * scale, Screen.height - p.y - 28 * scale, 220 * scale, 64 * scale))) continue;
                float x = p.x - 75 * scale, y = Screen.height - p.y;
                var previous = GUI.color;
                GUI.color = new Color(.06f, .1f, .1f, .9f); GUI.DrawTexture(new Rect(x, y, 150 * scale, 10 * scale), Texture2D.whiteTexture);
                GUI.color = building.team == UnitTeam.Player ? new Color(.5f, .95f, .4f) : new Color(1, .28f, .25f);
                GUI.DrawTexture(new Rect(x + scale, y + scale, 148 * scale * building.CurrentHealth / Mathf.Max(1, building.maxHealth), 8 * scale), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var site = building.GetComponent<ConstructionSite>();
                GUI.Box(new Rect(x - 35 * scale, y - 28 * scale, 220 * scale, 25 * scale), site ? ConstructionSystem.Name(site.kind) + (site.Complete ? "" : " / " + Mathf.RoundToInt(site.Progress * 100) + "%") : building.team == UnitTeam.Player ? "ВАША БАЗА" : "БАЗА ВРАГА", boxLabel);
                GUI.Box(new Rect(x + 20 * scale, y + 12 * scale, 110 * scale, 24 * scale), building.CurrentHealth.ToString("0") + " / " + building.maxHealth.ToString("0"), boxLabel);
                GUI.color = previous;
            }
        }
    }
}
