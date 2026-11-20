using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class HudLayoutSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hudLayoutSmoke") >= 0)
                new GameObject("HUD layout regression").AddComponent<HudLayoutSmokeTest>();
        }
        bool failed, measureText;
        string report = "";
        int measurements;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name; failed |= !value;
            report += line + "\n"; Debug.Log(line); File.WriteAllText("hudlayout-test.txt", report);
        }
        static bool Inside(Rect parent, Rect child) => child.width > 0 && child.height > 0 && child.xMin >= parent.xMin - .1f
            && child.yMin >= parent.yMin - .1f && child.xMax <= parent.xMax + .1f && child.yMax <= parent.yMax + .1f;
        static bool Separate(Rect[] rects)
        {
            for (int i = 0; i < rects.Length; i++) for (int j = i + 1; j < rects.Length; j++) if (rects[i].Overlaps(rects[j])) return false;
            return true;
        }
        static bool PanelsFit(HudLayout l, float width, float height)
        {
            var bounds = new Rect(0, 0, width, height);
            var panels = new[] { l.Header, l.Roster, l.MinimapPanel, l.Alerts, l.ProductionViewport, l.Footer };
            foreach (var rect in panels) if (!Inside(bounds, rect) || !l.BlocksWorld(rect.center)) return false;
            return Separate(panels) && !l.BlocksWorld(l.WorldArea.center) && l.WorldArea.width > 100 && l.ProductionViewport.height >= 100;
        }
        static bool ChildrenFit(HudLayout l)
        {
            var header = new[] { l.Title, l.Pause, l.Resources, l.OwnBase, l.EnemyBase, l.Army, l.AutoBattle, l.AutoScout };
            foreach (var rect in header) if (!Inside(l.Header, rect)) return false;
            var footer = new[] { l.Status, l.AttackMove, l.WorkerProtection, l.Controls, l.Hints };
            foreach (var rect in footer) if (!Inside(l.Footer, rect)) return false;
            return Separate(header) && Separate(footer) && Inside(l.Roster, l.RosterViewport) && Inside(l.MinimapPanel, l.Minimap)
                && Inside(l.Alerts, l.AlertViewport) && l.ProductionViewport.width - 16 >= 300 * l.Scale;
        }
        IEnumerator Start()
        {
            yield return null;
            foreach (var size in new[] { new Vector2Int(640, 480), new Vector2Int(800, 480), new Vector2Int(800, 600), new Vector2Int(960, 540),
                new Vector2Int(1024, 600), new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1440, 900),
                new Vector2Int(1920, 1080), new Vector2Int(2560, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160) })
            {
                bool panels = true, children = true;
                foreach (int count in new[] { 0, 1, 4 })
                {
                    var layout = new HudLayout(size.x, size.y, count);
                    panels &= PanelsFit(layout, size.x, size.y); children &= ChildrenFit(layout) && layout.Font(13) >= 12;
                }
                Check(panels, "panels bounded nonoverlapping and world clickable at " + size);
                Check(children, "header footer minimap and scroll content fit at " + size);
            }
            var controller = FindAnyObjectByType<RTSSelectionController>();
            SmokeTestSetup.DisableEnemyProduction(controller, false); controller.SetAutoBattle(false); controller.SetAutoScout(false);
            foreach (var unit in controller.units) unit.Stop();
            controller.Select(Array.Find(controller.units, unit => unit.stats.isWorker));
            controller.SetPaused(true);
            for (int i = 0; i < 3; i++) controller.units[i].GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
            controller.playerBase.TakeDamage(1, UnitTeam.Enemy);
            Check(controller.alerts.Alerts.Count == 4, "four attack cards available for worst case layout");
            bool capture = Array.IndexOf(Environment.GetCommandLineArgs(), "-hudCapture") >= 0;
            if (capture)
            {
                var hud = controller.GetComponent<BattleHUD>();
                foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(1440, 900) })
                {
                    Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                    float until = Time.realtimeSinceStartup + 5;
                    while ((Screen.width != size.x || Screen.height != size.y) && Time.realtimeSinceStartup < until) yield return null;
                    int before = hud.RepaintCount;
                    until = Time.realtimeSinceStartup + 5;
                    while (hud.RepaintCount < before + 2 && Time.realtimeSinceStartup < until) yield return null;
                    Check(Screen.width == size.x && Screen.height == size.y && hud.RepaintCount > before, "HUD repaints at requested resolution " + size);
                    measureText = true; int measured = measurements;
                    until = Time.realtimeSinceStartup + 2;
                    while (measurements == measured && Time.realtimeSinceStartup < until) yield return null;
                    Check(measurements > measured, "rendered text measured at " + size);
                    var layout = HudLayout.Current; var middle = layout.WorldArea.center;
                    Check(!BattleHUD.IsOverPanel(new Vector3(middle.x, Screen.height - middle.y)), "world input area remains available at " + size);
                    var map = BattleMinimap.MapRect.center;
                    Check(BattleHUD.IsOverPanel(new Vector3(map.x, Screen.height - map.y)) && controller.GetComponent<BattleMinimap>().FocusAt(map), "scaled minimap click mask and navigation agree at " + size);
                    controller.sceneCamera.GetComponent<RTSCameraController>().ResetView();
                    controller.SetPaused(false);
                    yield return new WaitForSecondsRealtime(.2f);
                    string file = Path.GetFullPath("hud-" + size.x + "x" + size.y + ".png");
                    ScreenCapture.CaptureScreenshot(file);
                    until = Time.realtimeSinceStartup + 3;
                    while (!File.Exists(file) && Time.realtimeSinceStartup < until) yield return null;
                    Check(File.Exists(file), "HUD screenshot captured at " + size);
                    controller.SetPaused(true);
                }
            }
            controller.SetPaused(false); Application.Quit(failed ? 1 : 0);
        }
        void OnGUI()
        {
            if (!measureText || Event.current.type != EventType.Repaint) return;
            measureText = false; measurements++;
            var l = HudLayout.Current;
            var heading = new GUIStyle(GUI.skin.label) { fontSize = l.Font(15), fontStyle = FontStyle.Bold, padding = new RectOffset() };
            var title = new GUIStyle(heading) { fontSize = l.Font(25) };
            var small = new GUIStyle(GUI.skin.label) { fontSize = l.Font(13), padding = new RectOffset() };
            bool fits = title.CalcSize(new GUIContent("PREHISTORIC FRONTIERS")).x <= l.Title.width
                && heading.CalcSize(new GUIContent("Еда: 1000   Древесина: 1000")).x <= l.Resources.width
                && heading.CalcSize(new GUIContent("Союзники: 20/20   Враги: 20")).x <= l.Army.width
                && small.CalcSize(new GUIContent("Вражеская база: 800/800")).x <= l.EnemyBase.width
                && small.CalcSize(new GUIContent("Своя база: 800/800")).y <= l.OwnBase.height
                && heading.CalcSize(new GUIContent("Еда: 1000")).y <= l.Resources.height;
            Check(fits, "actual font metrics fit title resources counts and base health at " + Screen.width + "x" + Screen.height);
        }
    }
}
