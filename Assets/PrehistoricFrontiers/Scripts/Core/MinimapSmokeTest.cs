using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class MinimapSmokeTest : MonoBehaviour
    {
        public BattleMinimap minimap;
        string report = "";
        bool failed;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText("minimap-test.txt", report);
        }
        float Light(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x + 25) / 50 * FogOfWar.Resolution), 0, FogOfWar.Resolution - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((p.z + 25) / 50 * FogOfWar.Resolution), 0, FogOfWar.Resolution - 1);
            return minimap.Terrain.GetPixel(x, z).grayscale;
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-minimapSmoke") < 0) yield break;
            yield return null;
            var controller = minimap.controller; var fog = controller.fog;
            SmokeTestSetup.DisableEnemyProduction(controller);
            controller.SetAutoBattle(false); controller.SetAutoScout(false);
            foreach (var unit in controller.units) unit.Stop();
            fog.SetFogEnabled(true); fog.ResetExploration(); minimap.RefreshTerrain();
            Check(minimap.Terrain && minimap.Terrain.width == FogOfWar.Resolution, "minimap creates terrain texture using fog grid");
            var r = BattleMinimap.MapRect;
            Check((BattleMinimap.WorldToMap(new Vector3(-25, 0, 25)) - r.position).sqrMagnitude < .001f
                && (BattleMinimap.WorldToMap(new Vector3(25, 0, -25)) - new Vector2(r.xMax, r.yMax)).sqrMagnitude < .001f,
                "map corners and north orientation match world XZ axes");
            var goal = new Vector3(8, 0, -3);
            Check(Vector3.Distance(BattleMinimap.MapToWorld(BattleMinimap.WorldToMap(goal)), goal) < .001f, "world and minimap coordinate conversion roundtrips");
            Check(BattleMinimap.MapToWorld(new Vector2(-999, 9999)) == new Vector3(-25, 0, -25), "map coordinate conversion clamps to battlefield edges");
            var screenPoint = new Vector3(r.center.x, Screen.height - r.center.y, 0);
            Check(BattleHUD.IsOverPanel(screenPoint) && BattleMinimap.IsOverPanel(screenPoint), "minimap consumes world clicks and camera wheel input");
            Check(BattleMinimap.PanelRect.yMin > HudLayout.Current.Roster.yMax && BattleMinimap.PanelRect.yMax < HudLayout.Current.Footer.yMin, "minimap fits between roster and footer");
            var enemy = controller.enemies[0]; var ally = controller.units[0];
            Check(minimap.Shows(ally) && minimap.Shows(controller.playerBase) && !minimap.Shows(enemy) && !minimap.Shows(controller.enemyBase), "allies and own base shown while unseen enemies stay hidden");
            float unknown = Light(enemy.transform.position);
            Check(unknown < .05f && Light(controller.playerBase.transform.position) > .2f, "unknown terrain black and visible terrain colored");
            controller.Select(ally); var order = ally.GetComponent<UnitCombat>().Order;
            var cam = controller.sceneCamera; float zoom = cam.orthographicSize;
            Check(minimap.FocusAt(BattleMinimap.WorldToMap(goal)), "minimap click focuses camera");
            Check(Vector3.Distance(minimap.GroundCorner(.5f, .5f), goal) < .05f, "camera view centers on clicked world position");
            Check(controller.Selected.Count == 1 && controller.Selected[0] == ally && ally.GetComponent<UnitCombat>().Order == order
                && cam.orthographicSize == zoom, "minimap navigation preserves selection orders and zoom");
            var before = cam.transform.position;
            Check(!minimap.FocusAt(new Vector2(r.x - 1, r.y - 1)) && cam.transform.position == before, "click outside map does not move camera");
            Check(!fog.WasExplored(UnitTeam.Player, new Vector3(22, 0, 22)), "camera navigation does not reveal unknown territory");
            Check(ally.Agent.Warp(new Vector3(-10, 0, 8)), "scout reaches test exploration position");
            fog.RefreshNow(); minimap.RefreshTerrain();
            Check(minimap.Shows(enemy), "enemy marker appears inside current allied vision");
            ally.Agent.Warp(new Vector3(-5, 0, -7)); fog.RefreshNow(); minimap.RefreshTerrain();
            Check(!minimap.Shows(enemy) && fog.WasExplored(UnitTeam.Player, enemy.transform.position)
                && Light(enemy.transform.position) > unknown + .1f, "enemy marker disappears but explored map stays colored");
            var node = minimap.resources[0];
            ally.Agent.Warp(node.transform.position); fog.RefreshNow(); minimap.RefreshTerrain();
            ally.Agent.Warp(new Vector3(20, 0, 20)); fog.RefreshNow(); minimap.RefreshTerrain();
            Check(minimap.Shows(node) && !fog.IsVisible(UnitTeam.Player, node.transform.position), "discovered resources remain marked outside current vision");
            node.Extract(10000);
            Check(!minimap.Shows(node), "depleted resource marker disappears");
            ally.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
            Check(controller.alerts.Alerts.Count == 1, "attack warning available for minimap pulse");
            ally.GetComponent<UnitHealth>().TakeDamage(10000);
            Check(!minimap.Shows(ally), "dead ally marker disappears");
            File.WriteAllBytes("minimap-terrain.png", minimap.Terrain.EncodeToPNG());
            Application.Quit(failed ? 1 : 0);
        }
    }
}
