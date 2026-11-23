using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class BattleMinimap : MonoBehaviour
    {
        public RTSSelectionController controller;
        public ResourceNode[] resources;
        public Texture2D Terrain { get; private set; }
        readonly List<Bounds> obstacles = new List<Bounds>();
        Color32[] pixels;
        float nextRefresh;
        GUIStyle heading, hint;
        public static Rect PanelRect => HudLayout.Current.MinimapPanel;
        public static Rect MapRect => HudLayout.Current.Minimap;
        public static bool IsOverPanel(Vector3 mouse) => PanelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        // GUI coordinates increase downwards; world Z increases towards the top of the map.
        public static Vector2 WorldToMap(Vector3 world)
        {
            var r = MapRect;
            return new Vector2(r.x + (world.x + 25) / 50 * r.width, r.y + (25 - world.z) / 50 * r.height);
        }
        public static Vector3 MapToWorld(Vector2 point)
        {
            var r = MapRect;
            return new Vector3(Mathf.Clamp01((point.x - r.x) / r.width) * 50 - 25, 0,
                25 - Mathf.Clamp01((point.y - r.y) / r.height) * 50);
        }
        void Start()
        {
            foreach (var collider in FindObjectsByType<Collider>())
                if (collider.gameObject.layer == 9) obstacles.Add(collider.bounds);
            RefreshTerrain();
        }
        void Update() { if (Time.unscaledTime >= nextRefresh) RefreshTerrain(); }
        public void RefreshTerrain()
        {
            if (!controller) return;
            const int size = FogOfWar.Resolution;
            if (!Terrain)
            {
                Terrain = new Texture2D(size, size, TextureFormat.RGB24, false) { name = "Minimap terrain", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                pixels = new Color32[size * size];
            }
            for (int z = 0; z < size; z++) for (int x = 0; x < size; x++)
            {
                var p = new Vector3(-25 + (x + .5f) / size * 50, 0, -25 + (z + .5f) / size * 50);
                Color color = new Color(.025f, .035f, .04f);
                if (controller.HasExplored(UnitTeam.Player, p))
                {
                    color = GroundCover.Active ? GroundCover.Active.MapColor(p) : new Color(.34f, .53f, .35f);
                    foreach (var obstacle in obstacles)
                        if (p.x >= obstacle.min.x && p.x <= obstacle.max.x && p.z >= obstacle.min.z && p.z <= obstacle.max.z)
                        { color = new Color(.52f, .57f, .58f); break; }
                    if (!controller.CanSee(UnitTeam.Player, p)) color *= .78f;
                }
                pixels[z * size + x] = color;
            }
            Terrain.SetPixels32(pixels); Terrain.Apply(false);
            nextRefresh = Time.unscaledTime + .1f;
        }
        public bool Shows(RTSUnit unit) => unit && unit.IsAvailable &&
            (unit.team == UnitTeam.Player || controller.CanSee(UnitTeam.Player, unit.transform.position));
        public bool Shows(BuildingHealth building) => building && building.IsAlive &&
            (building.team == UnitTeam.Player || controller.CanSee(UnitTeam.Player, building.transform.position));
        public bool Shows(ResourceNode resource) => resource && resource.Remaining > 0 && controller.HasExplored(UnitTeam.Player, resource.transform.position);
        public bool FocusAt(Vector2 guiPoint)
        {
            if (MatchResultsScreen.IsVisible || !MapRect.Contains(guiPoint) || !controller || !controller.sceneCamera) return false;
            var cameraController = controller.sceneCamera.GetComponent<RTSCameraController>();
            if (!cameraController) return false;
            cameraController.FocusOn(MapToWorld(guiPoint));
            return true;
        }
        public Vector3 GroundCorner(float x, float y)
        {
            var ray = controller.sceneCamera.ViewportPointToRay(new Vector3(x, y));
            return ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
        }
        static void Fill(Rect rect, Color color)
        {
            var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        static void Outline(Rect r, Color color, float width = 1)
        {
            Fill(new Rect(r.x, r.y, r.width, width), color); Fill(new Rect(r.x, r.yMax - width, r.width, width), color);
            Fill(new Rect(r.x, r.y, width, r.height), color); Fill(new Rect(r.xMax - width, r.y, width, r.height), color);
        }
        static void Dot(Vector3 position, Color color, float size, bool selected = false)
        {
            size = Mathf.Max(3, size * HudLayout.Current.Scale);
            var p = WorldToMap(position) - MapRect.position;
            var r = new Rect(p.x - size / 2, p.y - size / 2, size, size);
            if (selected) Outline(new Rect(r.x - 2, r.y - 2, size + 4, size + 4), Color.white);
            Fill(r, color);
        }
        static void Line(Vector2 a, Vector2 b)
        {
            var delta = b - a; var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
            Fill(new Rect(a.x, a.y, delta.magnitude, 1), new Color(1, 1, 1, .8f)); GUI.matrix = old;
        }
        public void Draw()
        {
            if (!Terrain || !controller) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, padding = new RectOffset(), alignment = TextAnchor.MiddleLeft };
                hint = new GUIStyle(GUI.skin.label) { fontSize = 10, padding = new RectOffset(), alignment = TextAnchor.MiddleLeft };
            }
            var layout = HudLayout.Current;
            heading.fontSize = layout.Font(14); hint.fontSize = layout.Font(11);
            HudLayout.DrawPanel(PanelRect);
            GUI.Label(new Rect(PanelRect.x + 14 * layout.Scale, PanelRect.y + 8 * layout.Scale, PanelRect.width - 28 * layout.Scale, 22 * layout.Scale), "МИНИ-КАРТА", heading);
            GUI.DrawTexture(MapRect, Terrain);
            var evt = Event.current;
            if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && evt.button == 0 && FocusAt(evt.mousePosition)) evt.Use();
            GUI.BeginGroup(MapRect);
            if (resources != null) foreach (var node in resources) if (Shows(node))
                Dot(node.transform.position, node.kind == ResourceKind.Food ? new Color(.8f, 1, .25f) : new Color(.85f, .57f, .25f), 4);
            if (Shows(controller.playerBase)) Dot(controller.playerBase.transform.position, Color.cyan, 9);
            if (Shows(controller.enemyBase)) Dot(controller.enemyBase.transform.position, new Color(1, .22f, .18f), 9);
            if (controller.construction) foreach (var site in controller.construction.Sites) if (site && Shows(site.Health)) Dot(site.transform.position, site.kind == StructureKind.Clinic ? Color.green : Color.cyan, 8);
            foreach (var unit in controller.units) if (Shows(unit))
                Dot(unit.transform.position, unit.stats.isWorker ? Color.yellow : Color.cyan, 5, unit.IsSelected);
            foreach (var unit in controller.enemies) if (Shows(unit)) Dot(unit.transform.position, new Color(1, .22f, .18f), 5);
            if (controller.alerts) foreach (var alert in controller.alerts.Alerts)
            {
                var p = WorldToMap(alert.Target && alert.Target.gameObject.activeInHierarchy ? alert.Target.position : alert.LastPosition) - MapRect.position;
                float radius = (6 + Mathf.PingPong(Time.unscaledTime * 9, 6)) * layout.Scale;
                Outline(new Rect(p.x - radius, p.y - radius, radius * 2, radius * 2), new Color(1, .35f, .05f), 2);
            }
            var corners = new[] { GroundCorner(0, 0), GroundCorner(1, 0), GroundCorner(1, 1), GroundCorner(0, 1) };
            for (int i = 0; i < 4; i++) Line(WorldToMap(corners[i]) - MapRect.position, WorldToMap(corners[(i + 1) % 4]) - MapRect.position);
            GUI.EndGroup();
            Outline(MapRect, new Color(.6f, .75f, .7f));
            GUI.Label(new Rect(PanelRect.x + 14 * layout.Scale, MapRect.yMax + 3 * layout.Scale, PanelRect.width - 28 * layout.Scale, 20 * layout.Scale), "ЛКМ — обзор камеры", hint);
        }
        void OnDestroy() { if (Terrain) Destroy(Terrain); }
    }
}
