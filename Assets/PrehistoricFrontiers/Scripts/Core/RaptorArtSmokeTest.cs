using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class RaptorArtSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-raptorArtSmoke") >= 0)
                new GameObject("Raptor art regression").AddComponent<RaptorArtSmokeTest>();
        }
        string report = "";
        bool failed;
        void Check(bool valid, string name)
        {
            var line = (valid ? "PASS " : "FAIL ") + name; report += line + "\n"; failed |= !valid;
            Debug.Log(line); File.WriteAllText("raptorart-test.txt", report);
        }
        IEnumerator Start()
        {
            yield return null;
            var controller = FindAnyObjectByType<RTSSelectionController>();
            SmokeTestSetup.DisableEnemyProduction(controller, false); controller.SetAutoBattle(false); controller.SetAutoScout(false);
            foreach (var unit in controller.units) unit.Stop();
            var player = Array.Find(controller.units, unit => unit.GetComponentInChildren<RaptorMotion>());
            var enemy = Array.Find(controller.enemies, unit => unit.GetComponentInChildren<RaptorMotion>());
            Check(player && enemy && Array.FindAll(controller.units, unit => unit.GetComponentInChildren<RaptorMotion>()).Length == 1,
                "single player raptors and enemy raptor use new art");
            if (!player || !enemy) { Application.Quit(1); yield break; }
            var art = player.GetComponentInChildren<RaptorMotion>(); var enemyArt = enemy.GetComponentInChildren<RaptorMotion>();
            Check(art.GetComponentsInChildren<Collider>(true).Length == 0 && player.GetComponentsInChildren<Collider>(true).Length == 1,
                "art adds no colliders and original selection collider is retained");
            var oldVisual = player.transform.Find("Visual");
            Check(oldVisual.GetComponent<CapsuleCollider>().enabled && !oldVisual.GetComponent<Renderer>().enabled,
                "old capsule remains a hitbox without visible placeholder");
            Check(player.Agent.isOnNavMesh && player.Agent.radius == player.stats.radius && player.Agent.speed == player.stats.moveSpeed
                && player.GetComponent<UnitHealth>().MaxHealth == player.stats.maxHealth, "movement stats health and nav agent unchanged");
            bool selected = Physics.Raycast(player.transform.position + Vector3.up * 8, Vector3.down, out var hit, 10, 1 << 10)
                && hit.collider.GetComponentInParent<RTSUnit>() == player;
            Check(selected, "selection ray still hits original raptor unit");
            int triangles = 0; bool geometry = true;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var filter in art.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh; triangles += mesh.triangles.Length / 3;
                geometry &= mesh.vertexCount > 0 && mesh.normals.Length == mesh.vertexCount;
                foreach (var vertex in mesh.vertices) bounds.Encapsulate(art.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            }
            Check(geometry && triangles > 500 && triangles < 5000, "complete low poly geometry with valid normals / triangles " + triangles);
            Check(bounds.min.y >= -.025f && bounds.max.y <= player.Agent.height + .3f, "feet rest on ground and model fits health indicator height");
            Check(art.torso.GetComponent<MeshRenderer>().sharedMaterials[0].color != enemyArt.torso.GetComponent<MeshRenderer>().sharedMaterials[0].color,
                "player and enemy models have distinct palettes");
            var playerSkin = art.torso.GetComponent<MeshRenderer>().sharedMaterials[0].color;
            var enemySkin = enemyArt.torso.GetComponent<MeshRenderer>().sharedMaterials[0].color;
            Check(Mathf.Abs(playerSkin.r - playerSkin.g) < .05f && Mathf.Abs(playerSkin.g - playerSkin.b) < .1f
                && enemySkin.r > enemySkin.g * 2 && enemySkin.r > enemySkin.b * 2,
                "reference team assignment: grey playable raptor and red enemy raptor");
            Check(controller.construction.main.unitPrefabs[0].GetComponentInChildren<RaptorMotion>(true), "trained raptor prefab includes art and motion rig");
            controller.fog.SetFogEnabled(true); controller.fog.ResetExploration();
            enemy.GetComponent<FogHiddenVisual>().ApplyNow();
            bool hidden = true; foreach (var renderer in enemyArt.GetComponentsInChildren<MeshRenderer>()) hidden &= renderer.forceRenderingOff;
            Check(hidden && !controller.CanSee(UnitTeam.Player, enemy.transform.position), "all enemy art pieces hidden outside allied vision");
            Check(player.Agent.Warp(new Vector3(-10, 0, 8)), "raptor can approach enemy using unchanged nav agent");
            controller.fog.RefreshNow(); enemy.GetComponent<FogHiddenVisual>().ApplyNow();
            bool visible = true; foreach (var renderer in enemyArt.GetComponentsInChildren<MeshRenderer>()) visible &= !renderer.forceRenderingOff;
            Check(visible && controller.CanSee(UnitTeam.Player, enemy.transform.position), "all model pieces appear together after discovery");
            yield return null; controller.SetPaused(true);
            var head = art.head.localPosition; var pose = art.leftHip.localRotation;
            yield return new WaitForSecondsRealtime(.2f);
            Check(art.head.localPosition == head && Quaternion.Angle(art.leftHip.localRotation, pose) < .01f, "model motion freezes on game pause");
            controller.SetPaused(false);
            Check(player.MoveTo(new Vector3(-18, 0, 10)) && player.Agent.hasPath, "manual movement still works with articulated visual model");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
