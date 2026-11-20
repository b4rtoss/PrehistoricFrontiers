using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class MatchResultsScreen : MonoBehaviour
    {
        public RTSSelectionController controller;
        static MatchResultsScreen owner;
        public static bool IsVisible => owner && owner.controller && owner.controller.MatchOver;
        GUIStyle title, message, restart, rowLabel, value;
        Vector2 scroll;
        public static Rect WindowRect
        {
            get
            {
                float scale = HudLayout.Current.Scale;
                float width = Mathf.Min(600 * scale, Screen.width - 32 * scale), height = Mathf.Min(520 * scale, Screen.height - 32 * scale);
                return new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            }
        }
        Rect R(float x, float y, float width, float height) => HudLayout.Current.Rect(x, y, width, height);
        void Awake() { owner = this; }
        void OnGUI()
        {
            if (!IsVisible) return;
            var oldSkin = GUI.skin; GUI.skin = HudTheme.Skin;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                message = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                message.normal.textColor = HudTheme.Ink;
                rowLabel = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
                rowLabel.normal.textColor = HudTheme.Muted;
                value = new GUIStyle(rowLabel) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
                value.normal.textColor = HudTheme.Ink;
                restart = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            }
            var layout = HudLayout.Current;
            title.fontSize = layout.Font(36); message.fontSize = layout.Font(17); restart.fontSize = layout.Font(18);
            rowLabel.fontSize = layout.Font(15); value.fontSize = layout.Font(15);
            int oldDepth = GUI.depth; bool oldEnabled = GUI.enabled;
            var oldColor = GUI.color; var oldBackground = GUI.backgroundColor; var oldContent = GUI.contentColor;
            GUI.depth = -1000; GUI.enabled = true;
            GUI.color = new Color(0, 0, 0, .8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white; GUI.contentColor = Color.white; GUI.backgroundColor = Color.white;
            GUI.ModalWindow(90412, WindowRect, DrawWindow, GUIContent.none);
            GUI.skin = oldSkin; GUI.depth = oldDepth; GUI.enabled = oldEnabled; GUI.color = oldColor; GUI.backgroundColor = oldBackground; GUI.contentColor = oldContent;
        }
        void DrawWindow(int id)
        {
            var stats = controller.statistics;
            stats.FinishIfEnded();
            float width = WindowRect.width / HudLayout.Current.Scale, height = WindowRect.height / HudLayout.Current.Scale;
            title.normal.textColor = stats.Won ? HudTheme.Accent : HudTheme.Danger;
            GUI.Label(R(20, 20, width - 40, 54), stats.Won ? "ПОБЕДА" : "ПОРАЖЕНИЕ", title);
            GUI.Label(R(24, 81, width - 48, 32), stats.Won ? "Вражеская главная база уничтожена." : "Ваша главная база уничтожена.", message);
            GUI.Label(R(24, 118, width - 48, 28), "ИТОГИ МАТЧА", message);
            string[] labels = { "Длительность матча", "Потери юнитов: свои / враг", "Из своих потерь — рабочих", "Обучено новых юнитов", "Доставлено: еда / древесина", "Построено: казарм / лечебниц", "Потеряно зданий: своих / врага", "Осталось союзников" };
            string[] values = { stats.DurationText, stats.AlliesLost + " / " + stats.EnemiesLost, stats.WorkersLost.ToString(), stats.UnitsTrained.ToString(),
                stats.FoodDelivered + " / " + stats.WoodDelivered, stats.BarracksBuilt + " / " + stats.ClinicsBuilt,
                stats.BuildingsLost + " / " + stats.EnemyBuildingsLost, stats.Survivors.ToString() };
            scroll = GUI.BeginScrollView(R(28, 154, width - 56, height - 246), scroll, R(0, 0, width - 78, labels.Length * 30));
            for (int i = 0; i < labels.Length; i++)
            {
                GUI.Label(R(4, i * 30, width - 220, 28), labels[i], rowLabel);
                GUI.Label(R(width - 210, i * 30, 124, 28), values[i], value);
            }
            GUI.EndScrollView();
            if (GUI.Button(R(40, height - 74, width - 80, 48), "Начать заново [R]", restart)) controller.RestartMatch();
        }
        void OnDestroy() { if (owner == this) owner = null; }
    }
}
