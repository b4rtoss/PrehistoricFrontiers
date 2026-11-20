using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class BaseCombatSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        bool failed, defeat;
        string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            report += line + "\n"; failed |= !value; Debug.Log(line);
            File.WriteAllText(defeat ? "basedefeat-test.txt" : "basecombat-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            defeat = Array.IndexOf(args, "-baseDefeatSmoke") >= 0;
            if (!defeat && Array.IndexOf(args, "-baseCombatSmoke") < 0) yield break;
            SmokeTestSetup.DisableEnemyProduction(controller);
            controller.playerBase.GetComponent<ProductionBuilding>().workersOnly = false;
            controller.SetAutoBattle(false); Time.timeScale = 4;
            foreach (var combat in FindObjectsByType<UnitCombat>()) combat.autoAcquire = false;
            yield return null;
            var production = controller.playerBase.GetComponent<ProductionBuilding>();
            if (defeat) { yield return DefeatCase(production); Application.Quit(failed ? 1 : 0); yield break; }
            var own = controller.playerBase; var target = controller.enemyBase;
            Check(own.IsAlive && target.IsAlive && own.CurrentHealth == 800 && target.CurrentHealth == 800 && own.team != target.team, "two opposing bases start with 800 health");
            var screen = controller.sceneCamera.WorldToScreenPoint(target.transform.position + Vector3.up * 1.3f);
            Check(Physics.Raycast(controller.sceneCamera.ScreenPointToRay(screen), out var picked, 300, (1 << 8) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13)) && picked.collider.GetComponentInParent<BuildingHealth>() == target, "mouse ray picks enemy building collider");
            var unit = controller.units[0]; var combatant = unit.GetComponent<UnitCombat>();
            controller.Select(unit);
            Check(controller.IssueAttackBase(own) == 0 && own.CurrentHealth == 800, "friendly base attack rejected");
            target.TakeDamage(-20); target.TakeDamage(float.NaN);
            Check(target.CurrentHealth == 800, "invalid building damage ignored");
            Check(controller.IssueAttackBase(target) == 1 && combatant.BuildingTarget == target, "base attack command accepted");
            Check(unit.Agent.stoppingDistance == 0, "attack approaches surface without movement stopping margin");
            yield return new WaitForSeconds(.2f);
            Check(target.CurrentHealth == 800, "building takes no damage outside attack range");
            yield return Until(() => target.CurrentHealth < 800, 15);
            Check(target.CurrentHealth == 800 - unit.stats.attackDamage, "unit navigates to building surface and deals configured damage");
            var attackPosition = unit.transform.position;
            float afterHit = target.CurrentHealth;
            yield return new WaitForSeconds(.2f);
            Check(target.CurrentHealth == afterHit, "building attacks respect cooldown");
            Check(unit.MoveTo(new Vector3(0, 0, -13)) && !combatant.BuildingTarget && !combatant.Target, "move cancels base attack");
            Check(Mathf.Approximately(unit.Agent.stoppingDistance, .25f), "move restores normal stopping margin");
            yield return new WaitForSeconds(.3f);
            Check(target.CurrentHealth == afterHit, "cancelled base attack causes no more damage");
            combatant.Attack(target); unit.Stop();
            Check(!combatant.BuildingTarget && combatant.Order == UnitOrder.Hold, "stop clears building target");
            combatant.Attack(controller.enemies[0]);
            Check(!combatant.BuildingTarget && combatant.Target == controller.enemies[0], "unit attack replaces building target");
            combatant.Attack(target);
            Check(!combatant.Target && combatant.BuildingTarget == target, "building attack replaces unit target");
            unit.Agent.Warp(attackPosition); unit.Agent.speed = 0;
            var from = unit.transform.position + Vector3.up * .8f;
            var point = target.SurfacePoint(from);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = 9;
            wall.transform.SetPositionAndRotation((from + point) * .5f + Vector3.up * .6f, Quaternion.LookRotation(Vector3.ProjectOnPlane(point - from, Vector3.up)));
            wall.transform.localScale = new Vector3(6, 3, .3f); Physics.SyncTransforms();
            yield return new WaitForSeconds(unit.stats.attackCooldown + .2f);
            Check(target.CurrentHealth == afterHit, "intervening obstacle blocks building damage");
            Destroy(wall); yield return null; unit.Agent.speed = unit.stats.moveSpeed;
            var worker = Instantiate(production.unitPrefabs[3], new Vector3(-6, 0, -15), Quaternion.identity).GetComponent<RTSUnit>();
            Check(!worker.GetComponent<UnitCombat>().Attack(target), "worker cannot attack buildings"); Destroy(worker.gameObject);
            foreach (var enemy in controller.enemies) enemy.GetComponent<UnitHealth>().TakeDamage(10000);
            Check(!controller.MatchOver && target.IsAlive, "destroying enemy army leaves match running");
            unit.Stop(); combatant.autoAcquire = true; controller.SetAutoBattle(true);
            yield return Until(() => combatant.BuildingTarget == target, 3);
            Check(combatant.BuildingTarget == target && combatant.IsAutomaticAttack, "auto battle targets base after enemy army is gone");
            controller.Clear(); foreach (var ally in controller.units) controller.Select(ally, true);
            Check(controller.IssueAttackBase(target) == 3, "group base attack accepted by all combat animals");
            yield return Until(() => target.CurrentHealth <= 200, 65);
            int food = production.wallet.Food, wood = production.wallet.Wood;
            bool queued = target.IsAlive && production.TryQueue(2);
            Check(queued, "production available while attacking enemy base");
            yield return Until(() => controller.MatchOver, 8); yield return null; yield return null;
            Check(target.CurrentHealth == 0 && !target.gameObject.activeSelf && controller.MatchOver && own.IsAlive, "base destroyed and removed while own base survives");
            Check(controller.Status.Contains("VICTORY"), "enemy base destruction produces victory");
            Check(!combatant.Attack(target) && controller.IssueAttackBase(target) == 0, "destroyed building rejects attacks");
            Check(production.QueueCount == 0 && production.wallet.Food == food && production.wallet.Wood == wood, "victory refunds pending production");
            Check(!production.TryQueue(0), "victory disables production");
            Application.Quit(failed ? 1 : 0);
        }
        IEnumerator DefeatCase(ProductionBuilding production)
        {
            foreach (var ally in controller.units) ally.GetComponent<UnitHealth>().TakeDamage(10000);
            Check(controller.AlliesAlive == 0 && !controller.MatchOver && controller.playerBase.IsAlive, "losing entire army does not cause defeat with intact base");
            Check(production.TryQueue(0), "intact base can produce with no surviving army");
            yield return Until(() => controller.AlliesAlive > 0, 10); yield return null;
            Check(controller.AlliesAlive == 1 && controller.units[controller.units.Length - 1].Agent.isOnNavMesh, "reinforcement restores wiped out army");
            var enemy = controller.enemies[0];
            Check(enemy.Agent.Warp(new Vector3(-6, 0, -10)) && enemy.GetComponent<UnitCombat>().Attack(controller.playerBase), "enemy accepts attack on player base");
            yield return Until(() => controller.playerBase.CurrentHealth <= 28, 60);
            Check(controller.playerBase.CurrentHealth <= 28, "enemy drives player base to destruction threshold");
            int food = production.wallet.Food, wood = production.wallet.Wood;
            Check(controller.playerBase.IsAlive && production.TryQueue(2), "production can be queued before own base is destroyed");
            yield return Until(() => controller.MatchOver, 8); yield return null; yield return null;
            Check(!controller.playerBase.IsAlive && controller.enemyBase.IsAlive && controller.Status.Contains("DEFEAT"), "own base destruction produces defeat");
            Check(production.QueueCount == 0 && production.wallet.Food == food && production.wallet.Wood == wood, "destroyed production building refunds queue before disabling");
            Check(!production.TryQueue(0), "destroyed base cannot produce new units");
        }
    }
}
