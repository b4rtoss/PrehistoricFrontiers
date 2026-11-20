using UnityEngine;

namespace PrehistoricFrontiers
{
    // All public rectangles are screen pixels, matching IMGUI and pointer coordinates.
    public readonly struct HudLayout
    {
        public static HudLayout Current => new HudLayout(Screen.width, Screen.height, AttackNotifications.VisibleCount);
        public readonly float Scale, Width, Height;
        public readonly bool CompactHeader;
        public readonly Rect Header, Title, Pause, Resources, OwnBase, EnemyBase, Army, AutoBattle, AutoScout;
        public readonly Rect Roster, RosterTitle, RosterViewport, RosterHint, MinimapPanel, Minimap;
        public readonly Rect Alerts, AlertTitle, AlertViewport, ProductionViewport, WorldArea;
        public readonly Rect Footer, Status, AttackMove, WorkerProtection, Controls, Hints;
        public HudLayout(float width, float height, int alertCount)
        {
            Scale = Mathf.Clamp(Mathf.Min(width / 1440f, height / 900f), .8f, 1.5f);
            Width = width / Scale; Height = height / Scale;
            CompactHeader = Width < 1180;
            const float margin = 16, gap = 12, left = 224, right = 332;
            float headerHeight = CompactHeader ? 118 : 84;
            Header = Pixels(new Rect(margin, margin, Width - margin * 2, headerHeight), Scale);
            if (CompactHeader)
            {
                Title = Pixels(new Rect(32, 24, Width - 296, 32), Scale);
                Pause = Pixels(new Rect(Width - 252, 25, 220, 30), Scale);
                float half = (Width - 76) / 2;
                Resources = Pixels(new Rect(32, 62, half, 25), Scale);
                Army = Pixels(new Rect(44 + half, 62, half, 25), Scale);
                float column = (Width - 100) / 4;
                OwnBase = Pixels(new Rect(32, 98, column, 25), Scale);
                EnemyBase = Pixels(new Rect(44 + column, 98, column, 25), Scale);
                AutoBattle = Pixels(new Rect(56 + column * 2, 94, column, 30), Scale);
                AutoScout = Pixels(new Rect(68 + column * 3, 94, column, 30), Scale);
            }
            else
            {
                float usable = Width - 88;
                float titleWidth = usable * .36f, dataWidth = usable * .32f, actionsWidth = usable - titleWidth - dataWidth;
                float dataX = 44 + titleWidth, actionsX = dataX + dataWidth + gap;
                Title = Pixels(new Rect(32, 24, titleWidth, 34), Scale);
                Pause = Pixels(new Rect(32, 62, Mathf.Min(220, titleWidth), 28), Scale);
                Resources = Pixels(new Rect(dataX, 23, dataWidth, 23), Scale);
                OwnBase = Pixels(new Rect(dataX, 48, dataWidth, 20), Scale);
                EnemyBase = Pixels(new Rect(dataX, 70, dataWidth, 22), Scale);
                Army = Pixels(new Rect(actionsX, 24, actionsWidth, 26), Scale);
                AutoBattle = Pixels(new Rect(actionsX, 60, (actionsWidth - gap) / 2, 30), Scale);
                AutoScout = Pixels(new Rect(actionsX + (actionsWidth + gap) / 2, 60, (actionsWidth - gap) / 2, 30), Scale);
            }
            float footerTop = Height - margin - 118;
            Footer = Pixels(new Rect(margin, footerTop, Width - margin * 2, 118), Scale);
            float statusWidth = Width - 340;
            Status = Pixels(new Rect(32, footerTop + 8, statusWidth, 40), Scale);
            Controls = Pixels(new Rect(32, footerTop + 54, statusWidth, 24), Scale);
            Hints = Pixels(new Rect(32, footerTop + 80, statusWidth, 24), Scale);
            AttackMove = Pixels(new Rect(Width - 292, footerTop + 12, 260, 34), Scale);
            WorkerProtection = Pixels(new Rect(Width - 292, footerTop + 62, 260, 34), Scale);
            float top = margin + headerHeight + gap, bottom = footerTop - gap, bodyHeight = bottom - top;
            float mapSize = Mathf.Clamp(bodyHeight - 250, 96, 196);
            float mapHeight = mapSize + 58, rosterHeight = bodyHeight - mapHeight - gap;
            Roster = Pixels(new Rect(margin, top, left, rosterHeight), Scale);
            RosterTitle = Pixels(new Rect(30, top + 10, left - 28, 25), Scale);
            RosterViewport = Pixels(new Rect(30, top + 42, left - 28, Mathf.Max(35, rosterHeight - 76)), Scale);
            RosterHint = Pixels(new Rect(30, top + rosterHeight - 28, left - 28, 22), Scale);
            MinimapPanel = Pixels(new Rect(margin, bottom - mapHeight, left, mapHeight), Scale);
            Minimap = Pixels(new Rect(margin + (left - mapSize) / 2, bottom - mapHeight + 32, mapSize, mapSize), Scale);
            float rightX = Width - margin - right;
            float preferredAlertHeight = alertCount > 0 ? 42 + Mathf.Min(alertCount, AttackNotifications.MaxAlerts) * 56 : 68;
            float alertHeight = Mathf.Min(preferredAlertHeight, Mathf.Max(68, bodyHeight - gap - 140));
            Alerts = Pixels(new Rect(rightX, top, right, alertHeight), Scale);
            AlertTitle = Pixels(new Rect(rightX + 14, top + 8, right - 28, 24), Scale);
            AlertViewport = Pixels(new Rect(rightX + 10, top + 38, right - 20, Mathf.Max(20, alertHeight - 42)), Scale);
            ProductionViewport = Pixels(new Rect(rightX, top + alertHeight + gap, right, bodyHeight - alertHeight - gap), Scale);
            WorldArea = Pixels(new Rect(margin + left + gap, top, rightX - gap - margin - left - gap, bodyHeight), Scale);
        }
        static Rect Pixels(Rect rect, float scale) => new Rect(rect.x * scale, rect.y * scale, rect.width * scale, rect.height * scale);
        public Rect Rect(float x, float y, float width, float height) => Pixels(new Rect(x, y, width, height), Scale);
        public int Font(float size) => Mathf.RoundToInt(Mathf.Max(12, size * Scale));
        public static void DrawPanel(Rect rect)
        {
            HudTheme.Panel(rect);
        }
        public bool OverlapsPanel(Rect rect) => Header.Overlaps(rect) || Roster.Overlaps(rect) || MinimapPanel.Overlaps(rect)
            || Alerts.Overlaps(rect) || ProductionViewport.Overlaps(rect) || Footer.Overlaps(rect);
        public bool BlocksWorld(Vector2 guiPoint) => Header.Contains(guiPoint) || Roster.Contains(guiPoint) || MinimapPanel.Contains(guiPoint)
            || Alerts.Contains(guiPoint) || ProductionViewport.Contains(guiPoint) || Footer.Contains(guiPoint);
    }
}
