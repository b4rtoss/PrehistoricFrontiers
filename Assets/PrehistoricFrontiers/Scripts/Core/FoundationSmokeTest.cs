using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public sealed class FoundationSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        bool failed;
        string report = "";
        void Check(bool value, string name) { report += (value ? "PASS " : "FAIL ") + name + "\n"; failed |= !value; }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-foundationSmoke") < 0) yield break;
            SmokeTestSetup.DisableEnemyProduction(controller);
            foreach (var combat in FindObjectsByType<UnitCombat>()) combat.enabled = false;
            yield return null;
            foreach (var unit in controller.units)
            {
                Check(unit.Agent.isOnNavMesh, unit.name + " on baked NavMesh");
                Check(Mathf.Abs(unit.transform.position.y) < .2f, unit.name + " feet at ground");
                controller.Select(unit);
                Check(unit.IsSelected && unit.selectionIndicator.activeSelf, unit.name + " selection ring");
                // Follow the same collider selection route as mouse input.
                var p = controller.sceneCamera.WorldToScreenPoint(unit.transform.position + Vector3.up * unit.stats.radius);
                Check(Physics.Raycast(controller.sceneCamera.ScreenPointToRay(p), out var hit, 300, (1 << 8) | (1 << 9) | (1 << 10)) && hit.collider.GetComponentInParent<RTSUnit>() == unit, unit.name + " mouse ray picking");
            }
            controller.Clear();
            Check(!controller.units[2].IsSelected && !controller.units[2].selectionIndicator.activeSelf, "deselect hides ring");
            Check(controller.enemies.Length == 3, "three enemies spawned");
            foreach (var enemy in controller.enemies)
            {
                Check(enemy.team == UnitTeam.Enemy && enemy.Agent.isOnNavMesh, enemy.name + " enemy team and valid NavMesh");
                controller.Select(enemy);
                Check(!enemy.IsSelected && controller.Selected.Count == 0 && !enemy.selectionIndicator.activeSelf, enemy.name + " rejects player selection");
                Check(enemy.GetComponent<UnitHealth>().CurrentHealth == enemy.stats.maxHealth, enemy.name + " starts at full health");
            }
            var enemyHealth = controller.enemies[0].GetComponent<UnitHealth>();
            var alliedHealth = controller.units[0].GetComponent<UnitHealth>();
            enemyHealth.TakeDamage(25);
            Check(enemyHealth.CurrentHealth == enemyHealth.MaxHealth - 25 && alliedHealth.CurrentHealth == alliedHealth.MaxHealth, "damage is isolated per unit despite shared stats");
            enemyHealth.TakeDamage(-5); enemyHealth.TakeDamage(float.NaN);
            Check(enemyHealth.CurrentHealth == enemyHealth.MaxHealth - 25, "invalid damage ignored");
            enemyHealth.Heal(10000);
            Check(enemyHealth.CurrentHealth == enemyHealth.MaxHealth && !enemyHealth.IsDepleted, "healing clamps to maximum");
            var dummy = Instantiate(controller.enemies[0].gameObject);
            var dummyHealth = dummy.GetComponent<UnitHealth>();
            dummyHealth.TakeDamage(10000);
            Check(dummyHealth.CurrentHealth == 0 && dummyHealth.IsDepleted && !dummy.activeSelf, "lethal damage removes unit");
            dummyHealth.Heal(10000);
            Check(dummyHealth.IsDepleted, "healing does not resurrect dead unit");
            Destroy(dummy);
            var cam = controller.sceneCamera.GetComponent<RTSCameraController>();
            cam.transform.position = new Vector3(100, 30, 100); cam.ClampPosition();
            Check(cam.transform.position == new Vector3(22, 12, 10), "camera bounds"); cam.ResetView();
            var raptor = controller.units[0];
            raptor.Agent.Warp(new Vector3(0, 0, -7));
            controller.Select(raptor);
            var detourDestination = new Vector3(-4, 0, 9);
            var path = new NavMeshPath();
            Check(raptor.Agent.CalculatePath(detourDestination, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 2, "route detours around central rock");
            Check(controller.IssueMove(detourDestination) == 1, "right-click move command accepted");
            float deadline = Time.realtimeSinceStartup + 14;
            while (Time.realtimeSinceStartup < deadline && Vector3.Distance(raptor.transform.position, detourDestination) > .65f) yield return null;
            Check(Vector3.Distance(raptor.transform.position, detourDestination) < .65f, "agent reaches destination around obstacle");
            controller.Clear(); foreach (var unit in controller.units) controller.Select(unit, true);
            Check(controller.IssueMove(new Vector3(0, 0, -13)) == 3, "group formation accepts three paths");
            foreach (var unit in controller.units) unit.Stop();
            controller.Clear(); controller.Select(controller.units[1]);
            yield return new WaitForEndOfFrame();
            // Render directly so verification also works with a hidden player window.
            var camera = controller.sceneCamera;
            var target = new RenderTexture(1440, 900, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var snapshot = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            snapshot.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
            snapshot.Apply();
            File.WriteAllBytes("foundation-preview.png", snapshot.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Destroy(snapshot); target.Release(); Destroy(target);
            File.WriteAllText("foundation-test.txt", report);
            Debug.Log(report);
            yield return new WaitForSeconds(2);
            Application.Quit(failed ? 1 : 0);
        }
    }
}
