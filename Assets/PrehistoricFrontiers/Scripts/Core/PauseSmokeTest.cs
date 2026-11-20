using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class PauseSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        string report = "";
        bool failed;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name; failed |= !value;
            report += line + "\n"; Debug.Log(line); File.WriteAllText("pause-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pauseSmoke") < 0) yield break;
            yield return null;
            Time.timeScale = 3; SmokeTestSetup.DisableEnemyProduction(controller, false); SmokeTestSetup.EnsureWorkers(controller, 3);
            controller.SetAutoBattle(false); controller.SetAutoScout(false);
            foreach (var ally in controller.units) ally.Stop();
            foreach (var enemyUnit in controller.enemies) { enemyUnit.Stop(); enemyUnit.Agent.Warp(new Vector3(21, 0, 21)); }
            var workers = Array.FindAll(controller.units, unit => unit.stats.isWorker);
            var system = controller.construction; var main = system.main;
            workers[0].Agent.Warp(new Vector3(-6, 0, -5)); Physics.SyncTransforms();
            var clinic = system.Place(workers[0], StructureKind.Clinic, new Vector3(-6, 0, -1));
            Check(clinic, "clinic placed for pause healing test");
            if (!clinic) { Application.Quit(1); yield break; }
            yield return Until(() => clinic.Complete, 18);
            Check(clinic.Complete, "clinic completed before pause");
            workers[1].Agent.Warp(new Vector3(-6, 0, 1)); Physics.SyncTransforms();
            var site = system.Place(workers[1], StructureKind.Barracks, new Vector3(-6, 0, 6));
            Check(site && !site.Complete, "unfinished construction ready for pause test");
            var nodeObject = new GameObject("Pause test source"); nodeObject.transform.position = new Vector3(-21, 0, 12);
            var node = nodeObject.AddComponent<ResourceNode>();
            workers[2].Agent.Warp(new Vector3(-20, 0, 12)); workers[2].GetComponent<WorkerGatherer>().Gather(node);
            yield return Until(() => workers[2].GetComponent<WorkerGatherer>().Carried > 0, 5);
            var patient = controller.units[2]; patient.Agent.Warp(new Vector3(-11, 0, -1)); patient.GetComponent<UnitHealth>().TakeDamage(60);
            var treatment = clinic.GetComponent<HealingClinic>();
            Check(treatment.Request(patient), "explicit clinic order accepted before pause");
            yield return Until(() => patient.IsInClinic, 10);
            Check(patient.IsInClinic, "patient admitted before checking pause healing");
            Check(main.TryQueue(3), "production started before pause");
            var fighter = controller.units[0]; fighter.Agent.Warp(new Vector3(-20, 0, -2));
            var enemy = controller.enemies[0]; enemy.Agent.Warp(new Vector3(-20, 0, 1));
            fighter.GetComponent<UnitCombat>().Attack(enemy);
            fighter.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
            controller.Select(fighter); controller.ArmAttackMove();
            controller.SetPaused(true);
            Check(controller.IsPaused && Time.timeScale == 0 && AudioListener.pause && !controller.AttackMovePending,
                "pause freezes game clock and sound and cancels pending input");
            float gameTime = Time.time, queue = main.SlotProgress(0), progress = site ? site.Progress : 0;
            float patientHealth = patient.GetComponent<UnitHealth>().CurrentHealth, enemyHealth = enemy.GetComponent<UnitHealth>().CurrentHealth;
            int stock = node.Remaining, cargo = workers[2].GetComponent<WorkerGatherer>().Carried, food = main.wallet.Food;
            var position = fighter.transform.position; int alerts = controller.alerts.Alerts.Count;
            Check(!main.TryQueue(3) && controller.IssueMove(new Vector3(5, 0, 5)) == 0 && controller.IssueAttackMove(new Vector3(5, 0, 5)) == 0
                && main.wallet.Food == food, "pause blocks new orders and resource spending");
            var mapPoint = BattleMinimap.WorldToMap(new Vector3(8, 0, -3));
            Check(controller.GetComponent<BattleMinimap>().FocusAt(mapPoint), "minimap remains usable during pause");
            controller.SetPaused(true);
            yield return new WaitForSecondsRealtime(8.3f);
            Check(Time.time == gameTime && main.SlotProgress(0) == queue && (!site || site.Progress == progress), "production construction and game clock stay frozen");
            Check(Vector3.Distance(position, fighter.transform.position) < .02f && enemy.GetComponent<UnitHealth>().CurrentHealth == enemyHealth,
                "movement and combat stay frozen including pending first attack");
            Check(node.Remaining == stock && workers[2].GetComponent<WorkerGatherer>().Carried == cargo, "gathering stays frozen");
            Check(patient.GetComponent<UnitHealth>().CurrentHealth == patientHealth, "clinic healing stays frozen");
            Check(alerts > 0 && controller.alerts.Alerts.Count == alerts, "attack notifications do not expire during long pause");
            Check(fighter.GetComponent<UnitCombat>().Target == enemy && workers[2].GetComponent<WorkerGatherer>().AutoGatherActive,
                "pause preserves combat and gathering orders");
            controller.SetPaused(false);
            Check(!controller.IsPaused && Time.timeScale == 3 && !AudioListener.pause, "resume restores previous simulation speed and sound");
            yield return new WaitForSeconds(.8f);
            Check(main.QueueCount > 0 && main.SlotProgress(0) > queue && (!site || site.Progress > progress), "production and construction resume");
            Check(enemy.GetComponent<UnitHealth>().CurrentHealth < enemyHealth || Vector3.Distance(position, fighter.transform.position) > .1f,
                "combat route resumes after pause");
            Check(patient.GetComponent<UnitHealth>().CurrentHealth > patientHealth, "clinic healing resumes after pause");
            controller.SetPaused(true); controller.SetPaused(false);
            Check(Time.timeScale == 3, "repeated pause cycles preserve simulation speed");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
