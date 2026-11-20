using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class AttackAlertsSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        bool failed;
        string report = "", output;
        void Check(bool value, string name)
        {
            var line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText(output, report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        Vector3 CameraCenter()
        {
            var ray = controller.sceneCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            return ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
        }
        AttackNotifications.Alert Find(Transform target)
        {
            foreach (var alert in controller.alerts.Alerts) if (alert.Target == target) return alert;
            return null;
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            bool autonomy = Array.IndexOf(args, "-enemyAutonomySmoke") >= 0;
            if (!autonomy && Array.IndexOf(args, "-attackAlertsSmoke") < 0) yield break;
            output = autonomy ? "enemyautonomy-test.txt" : "attackalerts-test.txt";
            Time.timeScale = 4;
            if (autonomy)
            {
                SmokeTestSetup.SkipPreparation(controller);
                controller.enemyBase.GetComponent<EnemyEconomyAI>().enabled = false;
                yield return EnemyAutonomy(); Application.Quit(failed ? 1 : 0); yield break;
            }
            SmokeTestSetup.DisableEnemyProduction(controller);
            controller.SetAutoBattle(false); controller.SetAutoScout(false);
            yield return null;
            var alerts = controller.alerts;
            Check(alerts && alerts.Alerts.Count == 0, "alerts initially empty");
            Check(alerts.WarningSound && alerts.WarningSound.samples > 0 && controller.sceneCamera.GetComponent<AudioListener>(), "warning tone and audio listener configured");
            var production = controller.playerBase.GetComponent<ProductionBuilding>(); production.TryQueue(3);
            yield return Until(() => controller.units.Length == 4, 8); yield return null;
            var worker = controller.units[3]; var hp = worker.GetComponent<UnitHealth>();
            hp.TakeDamage(0, UnitTeam.Enemy); hp.TakeDamage(-1, UnitTeam.Enemy); hp.TakeDamage(float.NaN, UnitTeam.Enemy);
            hp.TakeDamage(1, UnitTeam.Player); hp.TakeDamage(1);
            Check(alerts.Alerts.Count == 0 && alerts.SoundCount == 0, "invalid friendly and untagged damage do not trigger attack alerts");
            var before = controller.sceneCamera.transform.position;
            hp.TakeDamage(2, UnitTeam.Enemy);
            var workerAlert = Find(worker.transform);
            Check(workerAlert != null && workerAlert.IsWorker && workerAlert.Text == "Рабочий под атакой!" && workerAlert.Hits == 1, "hostile worker damage creates correctly labelled warning");
            Check(alerts.SoundCount == 1 && controller.sceneCamera.transform.position == before, "first warning sounds without automatically moving camera");
            for (int i = 0; i < 5; i++) hp.TakeDamage(1, UnitTeam.Enemy);
            Check(alerts.Alerts.Count == 1 && workerAlert.Hits == 6 && alerts.SoundCount == 1, "rapid hits coalesce and do not repeat warning sound");
            yield return new WaitForSecondsRealtime(AttackNotifications.RepeatSoundDelay + .1f);
            hp.TakeDamage(1, UnitTeam.Enemy);
            Check(alerts.SoundCount == 2 && workerAlert.Hits == 7, "ongoing attack can sound again after per target delay");
            controller.enemies[0].GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Player);
            controller.enemyBase.TakeDamage(1, UnitTeam.Player);
            Check(alerts.Alerts.Count == 1, "enemy casualties never generate allied warnings");
            controller.playerBase.TakeDamage(1, UnitTeam.Enemy);
            Check(alerts.Alerts.Count == 2 && alerts.Alerts[0].IsBase && alerts.Alerts[0].Text == "База под атакой!", "base warning takes priority over unit warning");
            Check(alerts.Focus(workerAlert) && Vector3.Distance(CameraCenter(), worker.transform.position) < .3f && controller.sceneCamera.transform.position.y == 12, "click action centers camera on living target and preserves height");
            worker.Agent.Warp(new Vector3(-3, 0, -12));
            Check(alerts.Focus(workerAlert) && Vector3.Distance(CameraCenter(), worker.transform.position) < .3f, "notification focuses current position of moving ally");
            var last = worker.transform.position;
            hp.TakeDamage(10000, UnitTeam.Enemy);
            Check(!worker.IsAlive && workerAlert.Text == "Рабочий погиб" && alerts.Focus(workerAlert) && Vector3.Distance(CameraCenter(), last) < .3f, "fatal attack keeps safe camera focus at last position");
            foreach (var unit in controller.units) if (unit.IsAlive) unit.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
            Check(alerts.Alerts.Count == AttackNotifications.MaxAlerts && alerts.Alerts[0].IsBase, "visible cards capped at four while retaining base priority");
            controller.units[0].GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
            Check(Find(controller.units[0].transform).Hits == 2 && alerts.Alerts.Count == 4, "coalescing survives visible card limit");
            var alertCenter = AttackNotifications.PanelRect.center;
            var point = new Vector3(alertCenter.x, Screen.height - alertCenter.y, 0);
            Check(AttackNotifications.IsOverPanel(point) && BattleHUD.IsOverPanel(point), "notification cards block world mouse orders");
            var world = HudLayout.Current.WorldArea.center;
            Check(!BattleHUD.IsOverPanel(new Vector3(world.x, Screen.height - world.y, 0)), "world space between panels remains free for orders");
            Check(AttackNotifications.PanelRect.yMax < HudLayout.Current.ProductionViewport.yMin && HudLayout.Current.ProductionViewport.yMax < HudLayout.Current.Footer.yMin, "notifications leave room for scrollable production above footer");
            yield return new WaitForSecondsRealtime(AttackNotifications.Lifetime + .2f); yield return null;
            Check(alerts.Alerts.Count == 0 && !AttackNotifications.IsOverPanel(point), "expired warnings disappear and release mouse area");
            var enemy = controller.enemies[0]; var ally = controller.units[0];
            float previousHealth = ally.GetComponent<UnitHealth>().CurrentHealth;
            enemy.Agent.Warp(ally.transform.position + Vector3.left * 2);
            enemy.GetComponent<UnitCombat>().Attack(ally);
            yield return Until(() => ally.GetComponent<UnitHealth>().CurrentHealth < previousHealth, 3);
            Check(Find(ally.transform) != null, "actual enemy attack emits allied unit warning through combat pipeline");
            enemy.Stop(); enemy.Agent.Warp(new Vector3(-6, 0, -10));
            previousHealth = controller.playerBase.CurrentHealth;
            enemy.GetComponent<UnitCombat>().Attack(controller.playerBase);
            yield return Until(() => controller.playerBase.CurrentHealth < previousHealth, 3);
            Check(Find(controller.playerBase.transform) != null && alerts.Alerts[0].IsBase, "actual enemy building attack emits priority base warning");
            int count = alerts.Alerts.Count, hits = Find(ally.transform).Hits;
            controller.enemyBase.TakeDamage(10000);
            ally.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
            Check(alerts.Alerts.Count == count && Find(ally.transform).Hits == hits, "no new attack warnings after match end");
            Application.Quit(failed ? 1 : 0);
        }
        IEnumerator EnemyAutonomy()
        {
            controller.SetAutoBattle(false); controller.SetAutoScout(false);
            controller.enemyBase.GetComponent<EnemyProductionAI>().enabled = false;
            yield return new WaitForSeconds(.4f);
            Check(!controller.AutoBattleEnabled && !controller.AutoScoutEnabled && controller.AutoBattleFor(UnitTeam.Enemy) && controller.AutoScoutFor(UnitTeam.Enemy), "allied toggles do not disable enemy battle or scout policy");
            var enemy = controller.enemies[0]; var combat = enemy.GetComponent<UnitCombat>();
            Check(combat.Order == UnitOrder.Scout && enemy.Agent.hasPath, "enemy scouts while allied scouting is off");
            var destination = enemy.Agent.destination;
            controller.SetAutoScout(false); controller.SetAutoBattle(false);
            Check(combat.Order == UnitOrder.Scout && enemy.Agent.destination == destination, "allied toggles preserve existing enemy scout path");
            var ally = controller.units[0]; var hp = ally.GetComponent<UnitHealth>();
            enemy.Stop(); enemy.Agent.Warp(ally.transform.position + Vector3.left * 2); combat.BeginMove(); controller.fog.RefreshNow();
            float before = hp.CurrentHealth;
            yield return Until(() => hp.CurrentHealth < before, 3);
            Check(combat.Target == ally && combat.IsAutomaticAttack && hp.CurrentHealth < before, "enemy automatically attacks with both allied modes off");
            Check(!ally.GetComponent<UnitCombat>().Target && !ally.GetComponent<UnitCombat>().BuildingTarget, "allied unit does not automatically retaliate when battle is off");
            before = hp.CurrentHealth;
            controller.SetAutoBattle(false); controller.SetAutoScout(false);
            yield return Until(() => hp.CurrentHealth < before, 2);
            Check(combat.Target == ally && hp.CurrentHealth < before, "allied toggles do not interrupt ongoing enemy attack");
            Check(Find(ally.transform) != null, "autonomous enemy attack creates player warning");
            var factory = controller.enemyBase.GetComponent<ProductionBuilding>();
            controller.playerBase.GetComponent<ProductionBuilding>().workersOnly = false; // Reinforcement fixture.
            factory.workersOnly = false;
            int enemyCount = controller.enemies.Length;
            factory.GetComponent<EnemyProductionAI>().enabled = true;
            yield return Until(() => controller.enemies.Length > enemyCount, 8); yield return null;
            var reinforcement = controller.enemies[controller.enemies.Length - 1];
            yield return Until(() => reinforcement.GetComponent<UnitCombat>().Order == UnitOrder.Scout || reinforcement.GetComponent<UnitCombat>().IsAutomaticAttack, 2);
            Check(reinforcement.team == UnitTeam.Enemy && (reinforcement.GetComponent<UnitCombat>().Order == UnitOrder.Scout || reinforcement.GetComponent<UnitCombat>().IsAutomaticAttack), "new enemy reinforcement stays autonomous while allied modes are off");
            Check(!controller.AutoBattleEnabled && !controller.AutoScoutEnabled, "enemy activity never changes player toggle states");
        }
    }
}
