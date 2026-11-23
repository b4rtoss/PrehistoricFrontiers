using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class DeathVisualSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-deathVisualSmoke") >= 0)
                new GameObject("Death visual regression").AddComponent<DeathVisualSmokeTest>();
        }
        bool failed;
        string report = "";
        void Check(bool value, string name)
        {
            var line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText("deathvisual-test.txt", report);
        }
        IEnumerator Start()
        {
            yield return null;
            var c = FindAnyObjectByType<RTSSelectionController>();
            SmokeTestSetup.DisableEnemyProduction(c, false);
            c.SetAutoBattle(false); c.SetAutoScout(false); c.SetWorkerProtection(false);
            var all = new List<RTSUnit>(c.units); all.AddRange(c.enemies);
            for (int i = 0; i < c.units.Length; i++)
            {
                c.units[i].Stop(); c.units[i].Agent.Warp(new Vector3(-12 + i * 4, 0, -5));
            }
            var bodies = new List<UnitDeathVisual>();
            foreach (var unit in all)
            {
                unit.Stop();
                var model = UnitHitReaction.FindModel(unit);
                c.Select(unit);
                unit.GetComponent<UnitHealth>().TakeDamage(10000, unit.team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player,
                    unit.transform.position + unit.transform.right * 3);
                var body = Array.Find(FindObjectsByType<UnitDeathVisual>(), b => b.Source == unit);
                Check(body && model && model.IsChildOf(body.transform) && model.gameObject.activeInHierarchy,
                    unit.name + " keeps actual rendered model for falling body");
                Check(!unit.IsAlive && !unit.gameObject.activeSelf && !unit.Agent.enabled && !unit.IsSelected,
                    unit.name + " immediately leaves combat navigation and selection");
                Check(body && !body.GetComponentInChildren<RTSUnit>() && !body.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>()
                    && Array.TrueForAll(body.GetComponentsInChildren<Collider>(), col => !col.enabled),
                    unit.name + " body is cosmetic and adds no target or blocker");
                if (body) bodies.Add(body);
                int count = FindObjectsByType<UnitDeathVisual>().Length;
                unit.Die(); unit.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
                Check(FindObjectsByType<UnitDeathVisual>().Length == count, unit.name + " repeated death creates no duplicate body");
            }
            c.PruneSelection();
            Check(c.AlliesAlive == 0 && c.EnemiesAlive == 0 && c.Selected.Count == 0 && !c.MatchOver,
                "fallen bodies do not occupy army counts or selection or decide base victory");
            Check(c.statistics.AlliesLost == 4 && c.statistics.EnemiesLost == 4, "losses recorded once per unit");
            yield return new WaitForSeconds(.2f);
            Check(bodies.TrueForAll(b => Vector3.Dot(b.transform.up, Vector3.up) < .999f && Vector3.Dot(b.transform.up, Vector3.up) > .01f),
                "all body types visibly tilt during fall instead of disappearing");
            c.SetPaused(true);
            float age = bodies[0].Age; var pose = bodies[0].transform.rotation; var position = bodies[0].transform.position;
            yield return new WaitForSecondsRealtime(.25f);
            Check(bodies[0].Age == age && bodies[0].transform.rotation == pose && bodies[0].transform.position == position, "pause freezes falling body");
            c.SetPaused(false);
            yield return new WaitForSeconds(.65f);
            foreach (var body in bodies)
            {
                Check(Mathf.Abs(Vector3.Dot(body.transform.up, Vector3.up)) < .01f, body.Source.name + " lies fully on its side");
                float lowest = float.PositiveInfinity;
                foreach (var renderer in body.GetComponentsInChildren<Renderer>()) lowest = Mathf.Min(lowest, renderer.bounds.min.y);
                Check(lowest >= -.005f && lowest <= .06f, body.Source.name + " rests on ground without clipping");
            }
            var cam = c.sceneCamera; cam.GetComponent<RTSCameraController>().enabled = false;
            cam.transform.position = new Vector3(-6, 13, -19); cam.transform.LookAt(new Vector3(-6, 0, -5)); cam.orthographicSize = 8;
            var rt = new RenderTexture(1440, 900, 24); var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            var old = cam.targetTexture; var active = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); pixels.Apply(); File.WriteAllBytes("death-preview.png", pixels.EncodeToPNG()); }
            finally { cam.targetTexture = old; RenderTexture.active = active; rt.Release(); Destroy(rt); Destroy(pixels); }
            c.fog.SetFogEnabled(true); c.fog.ResetExploration(); c.fog.RefreshNow();
            foreach (var body in bodies) body.transform.position = new Vector3(24, 0, 24);
            // Visibility is based on the death location, not a hidden enemy's later transform.
            yield return null;
            foreach (var body in bodies)
            {
                bool visible = c.CanSee(UnitTeam.Player, body.Source.transform.position);
                Check(Array.TrueForAll(body.GetComponentsInChildren<Renderer>(), r => r.forceRenderingOff == !visible),
                    body.Source.name + " body obeys fog at death location");
            }
            c.fog.SetFogEnabled(false);
            yield return new WaitForSeconds(1);
            Check(bodies.TrueForAll(b => b), "bodies remain on ground long enough to read casualties");
            yield return new WaitForSeconds(UnitDeathVisual.FallDuration + UnitDeathVisual.RestDuration + UnitDeathVisual.SinkDuration);
            Check(FindObjectsByType<UnitDeathVisual>().Length == 0, "body lifetime ends with cleanup");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
