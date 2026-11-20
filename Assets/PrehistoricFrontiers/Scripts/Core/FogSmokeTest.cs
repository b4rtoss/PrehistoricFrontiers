using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace PrehistoricFrontiers
{
    public sealed class FogSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        string report = "", output;
        bool failed;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText(output, report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        Texture2D Capture(string path)
        {
            foreach (var visual in FindObjectsByType<FogHiddenVisual>()) visual.ApplyNow();
            var camera = controller.sceneCamera;
            var previous = RenderTexture.active;
            var target = new RenderTexture(1440, 900, 24);
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var snapshot = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            snapshot.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); snapshot.Apply();
            File.WriteAllBytes(path, snapshot.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Destroy(target);
            return snapshot;
        }
        float Brightness(Texture2D image, Vector3 position)
        {
            var p = controller.sceneCamera.WorldToScreenPoint(position);
            return image.GetPixel(Mathf.Clamp((int)p.x, 0, image.width - 1), Mathf.Clamp((int)p.y, 0, image.height - 1)).grayscale;
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            bool fullBattle = Array.IndexOf(args, "-fogBattleSmoke") >= 0;
            if (!fullBattle && Array.IndexOf(args, "-fogSmoke") < 0) yield break;
            output = fullBattle ? "fogbattle-test.txt" : "fog-test.txt";
            var fog = controller.fog; Time.timeScale = 4; SmokeTestSetup.SkipPreparation(controller);
            if (fullBattle) { yield return BattleCase(fog); Application.Quit(failed ? 1 : 0); yield break; }
            SmokeTestSetup.DisableEnemyProduction(controller);
            controller.SetAutoBattle(false);
            foreach (var combat in FindObjectsByType<UnitCombat>()) combat.autoAcquire = false;
            fog.SetFogEnabled(true); fog.ResetExploration(); yield return null;
            var ally = controller.units[0]; var enemy = controller.enemies[0];
            var attack = ally.GetComponent<UnitCombat>();
            Check(fog.Active && fog.Mask.width == 96 && fog.Mask.height == 96, "fog enabled with runtime visibility mask");
            var effect = controller.sceneCamera.GetComponent<FogImageEffect>();
            Check(effect && effect.material && effect.material.shader.isSupported, "fog image shader supported in player");
            Check(fog.IsVisible(UnitTeam.Player, controller.playerBase.transform.position) && fog.IsVisible(UnitTeam.Player, ally.transform.position), "allied base and units reveal their surroundings");
            Check(!fog.IsVisible(UnitTeam.Player, enemy.transform.position) && !fog.WasExplored(UnitTeam.Player, controller.enemyBase.transform.position), "enemy army and base initially outside player knowledge");
            enemy.GetComponent<FogHiddenVisual>().ApplyNow();
            Check(enemy.GetComponentInChildren<Renderer>().forceRenderingOff && controller.VisibleEnemies == 0, "hidden enemy geometry and visible count suppressed");
            controller.Select(ally);
            Check(controller.IssueAttack(enemy) == 0 && controller.IssueAttackBase(controller.enemyBase) == 0, "manual attacks cannot target unseen enemy or base");
            var initial = Capture("fog-initial.png");
            float visibleLight = Brightness(initial, controller.playerBase.transform.position);
            float unknownLight = Brightness(initial, enemy.transform.position);
            Check(unknownLight < visibleLight * .45f, "rendered unknown terrain is opaque dark fog");
            Destroy(initial);
            attack.autoAcquire = true; controller.SetAutoBattle(true); controller.SetAutoScout(true);
            yield return Until(() => attack.Order == UnitOrder.Scout, 2);
            Check(attack.Order == UnitOrder.Scout && !attack.Target && !attack.BuildingTarget && ally.Agent.hasPath, "idle automatic unit scouts instead of tracking unseen targets");
            controller.SetAutoScout(false); controller.SetAutoBattle(false); attack.autoAcquire = false;
            Check(!ally.Agent.hasPath && attack.Order != UnitOrder.Scout, "auto scout toggle cancels automatic scouting");
            Check(ally.Agent.Warp(new Vector3(-10, 0, 8)), "scout can approach enemy on navmesh");
            fog.RefreshNow(); enemy.GetComponent<FogHiddenVisual>().ApplyNow();
            Check(fog.IsVisible(UnitTeam.Player, enemy.transform.position) && !enemy.GetComponentInChildren<Renderer>().forceRenderingOff, "enemy becomes visible when scout approaches");
            Check(fog.IsVisible(UnitTeam.Enemy, ally.transform.position), "enemy faction has its own visibility calculation");
            Check(controller.IssueAttack(enemy) == 1, "visible enemy can be attacked");
            ally.Agent.Warp(new Vector3(-5, 0, -7)); fog.RefreshNow(); yield return null;
            Check(!attack.Target && !ally.Agent.hasPath, "losing sight clears target and hidden pursuit");
            enemy.GetComponent<FogHiddenVisual>().ApplyNow();
            Check(!fog.IsVisible(UnitTeam.Player, enemy.transform.position) && fog.WasExplored(UnitTeam.Player, enemy.transform.position) && enemy.GetComponentInChildren<Renderer>().forceRenderingOff, "explored terrain remembered while enemy becomes hidden again");
            var memory = Capture("fog-memory.png");
            float memoryLight = Brightness(memory, enemy.transform.position);
            effect.enabled = false;
            var reference = Capture("fog-memory-reference.png");
            float referenceLight = Brightness(reference, enemy.transform.position);
            effect.enabled = true;
            Check(memoryLight > unknownLight + .025f && memoryLight > referenceLight * .65f && memoryLight < referenceLight * .9f,
                "explored terrain stays readable with only light dimming compared to the same unfogged terrain");
            Destroy(reference); Destroy(memory);
            for (int i = 0; i < 5; i++) { fog.RefreshNow(); yield return null; }
            Check(fog.WasExplored(UnitTeam.Player, enemy.transform.position) && !fog.IsVisible(UnitTeam.Player, enemy.transform.position)
                && enemy.GetComponentInChildren<Renderer>().forceRenderingOff, "repeated visibility updates retain explored terrain without revealing enemy");
            var cameraPosition = controller.sceneCamera.transform.position;
            controller.sceneCamera.transform.position += new Vector3(20, 0, 20); fog.RefreshNow();
            Check(!fog.WasExplored(UnitTeam.Player, new Vector3(22, 0, 22)), "moving camera alone does not reveal map");
            controller.sceneCamera.transform.position = cameraPosition;
            ally.Agent.Warp(new Vector3(-10, 0, 8)); fog.RefreshNow();
            ally.GetComponent<UnitHealth>().TakeDamage(10000); fog.RefreshNow();
            Check(!fog.IsVisible(UnitTeam.Player, enemy.transform.position) && fog.WasExplored(UnitTeam.Player, enemy.transform.position), "dead scout stops providing vision without erasing memory");
            var production = controller.playerBase.GetComponent<ProductionBuilding>();
            production.TryQueue(3);
            yield return Until(() => controller.units.Length == 4, 8); yield return null; fog.RefreshNow();
            var workerUnit = controller.units[controller.units.Length - 1];
            var worker = workerUnit.GetComponent<WorkerGatherer>();
            Check(worker && fog.IsVisible(UnitTeam.Player, workerUnit.transform.position), "new worker contributes vision");
            var resourceObject = new GameObject("Fog resource test"); resourceObject.transform.position = new Vector3(22, 0, -18);
            var resource = resourceObject.AddComponent<ResourceNode>();
            Check(!fog.WasExplored(UnitTeam.Player, resource.transform.position) && !worker.Gather(resource), "worker cannot receive gathering orders for undiscovered source");
            workerUnit.Agent.Warp(new Vector3(22, 0, -18)); fog.RefreshNow();
            Check(fog.WasExplored(UnitTeam.Player, resource.transform.position) && worker.Gather(resource), "worker can gather after source is discovered"); workerUnit.Stop();
            workerUnit.Agent.Warp(new Vector3(-5, 0, -7)); fog.RefreshNow();
            Check(fog.WasExplored(UnitTeam.Player, resource.transform.position) && !fog.IsVisible(UnitTeam.Player, resource.transform.position)
                && worker.Gather(resource), "discovered resource remains known and gatherable after worker leaves vision"); workerUnit.Stop();
            var enemyProduction = controller.enemyBase.GetComponent<ProductionBuilding>();
            int count = controller.enemies.Length; enemyProduction.TryQueue(0);
            yield return Until(() => controller.enemies.Length > count, 8); yield return null;
            var reinforcement = controller.enemies[controller.enemies.Length - 1];
            reinforcement.GetComponent<FogHiddenVisual>().ApplyNow();
            Check(!controller.CanSee(UnitTeam.Player, reinforcement.transform.position) && reinforcement.GetComponentInChildren<Renderer>().forceRenderingOff, "new unseen reinforcement is hidden immediately");
            Check(fog.IsVisible(UnitTeam.Enemy, controller.enemyBase.transform.position) && !fog.IsVisible(UnitTeam.Player, controller.enemyBase.transform.position), "base knowledge is not shared between factions");
            Check(fog.TryExplore(controller.units[1], out var goal) && !fog.IsVisible(UnitTeam.Player, goal) && NavMesh.SamplePosition(goal, out _, .5f, NavMesh.AllAreas), "scout destination is reachable territory outside current vision");
            Application.Quit(failed ? 1 : 0);
        }
        IEnumerator BattleCase(FogOfWar fog)
        {
            controller.SetAutoBattle(true); controller.SetAutoScout(true);
            yield return new WaitForSeconds(.4f);
            Check(fog.Active && controller.AutoBattleEnabled, "full match starts with fog and auto battle enabled");
            bool scouting = false;
            foreach (var unit in controller.units) scouting |= unit.GetComponent<UnitCombat>().Order == UnitOrder.Scout;
            Check(scouting && controller.VisibleEnemies == 0, "initial army explores without omniscient targets");
            bool discovery = false, damage = false;
            float end = Time.time + 180;
            while (!controller.MatchOver && Time.time < end)
            {
                discovery |= controller.VisibleEnemies > 0;
                foreach (var unit in controller.units) damage |= unit.GetComponent<UnitHealth>().CurrentHealth < unit.stats.maxHealth;
                yield return null;
            }
            yield return null;
            Check(discovery, "automatic scouting discovers opposing units");
            Check(damage, "discovered armies fight automatically");
            Check(controller.MatchOver && (controller.Status.Contains("VICTORY") || controller.Status.Contains("DEFEAT")), "fog match with enemy production reaches base destruction within 180 seconds");
        }
    }
}
