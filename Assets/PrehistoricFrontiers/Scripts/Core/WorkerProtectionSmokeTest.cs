using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class WorkerProtectionSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        bool failed;
        string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            failed |= !value; report += line + "\n"; Debug.Log(line); File.WriteAllText("workerprotection-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-workerProtectionSmoke") < 0) yield break;
            yield return null;
            Time.timeScale = 3; SmokeTestSetup.DisableEnemyProduction(controller);
            controller.SetAutoBattle(false); controller.SetAutoScout(false);
            foreach (var ally in controller.units) ally.Stop();
            var production = controller.playerBase.GetComponent<ProductionBuilding>();
            Check(controller.WorkerProtectionEnabled, "worker protection enabled by default independently of G and H");
            production.TryQueue(3);
            yield return Until(() => controller.units.Length == 4, 8);
            var unit = controller.units[controller.units.Length - 1];
            var worker = unit.GetComponent<WorkerGatherer>(); var health = unit.GetComponent<UnitHealth>();
            var nodeObject = new GameObject("Protection test resource"); nodeObject.transform.position = new Vector3(-21, 0, 12);
            var node = nodeObject.AddComponent<ResourceNode>();
            unit.Agent.Warp(node.transform.position + Vector3.right);
            Check(worker.Gather(node), "worker receives harvest order");
            yield return Until(() => worker.Carried > 0, 5);
            Check(worker.Carried > 0 && worker.AutoGatherActive, "worker carrying resources before attack");
            health.TakeDamage(1); health.TakeDamage(1, UnitTeam.Player); health.TakeDamage(float.NaN, UnitTeam.Enemy);
            Check(!worker.IsRetreating && worker.AutoGatherActive, "untagged friendly and invalid damage never trigger retreat");
            int cargo = worker.Carried, food = production.wallet.Food, stock = node.Remaining;
            controller.Select(unit); health.TakeDamage(1, UnitTeam.Enemy);
            Check(worker.IsRetreating && worker.State == GatherState.Retreating && !worker.AutoGatherActive && !worker.Node,
                "hostile damage cancels harvesting and starts retreat");
            Check(worker.Carried == cargo && controller.Selected.Contains(unit) && controller.alerts.Alerts.Count > 0,
                "retreat preserves cargo selection and attack notification");
            var pathDestination = unit.Agent.destination;
            health.TakeDamage(1, UnitTeam.Enemy);
            Check(worker.IsRetreating && Vector3.Distance(pathDestination, unit.Agent.destination) < .1f,
                "repeated hits keep current retreat without resetting its destination");
            controller.SetWorkerProtection(false);
            Check(worker.IsRetreating, "disabling protection preserves already issued retreat");
            yield return Until(() => !worker.IsRetreating, 15);
            Check(worker.State == GatherState.Idle && Vector3.Distance(unit.transform.position, production.depositPoint) < 2.5f,
                "retreat reaches base and waits for new order");
            Check(worker.Carried == 0 && production.wallet.Food == food + cargo, "retreat deposits carried resources exactly once");
            yield return new WaitForSeconds(3);
            Check(!worker.AutoGatherActive && worker.Node == null && node.Remaining == stock && !unit.Agent.hasPath,
                "worker does not automatically return to danger or continue harvesting");
            Check(worker.Gather(node), "manual harvest order resumes work after retreat");
            health.TakeDamage(1, UnitTeam.Enemy);
            Check(!worker.IsRetreating && worker.AutoGatherActive && worker.Node == node, "protection off keeps harvesting order after hostile damage");
            controller.SetWorkerProtection(true);
            unit.MoveTo(new Vector3(-20, 0, 8)); health.TakeDamage(1, UnitTeam.Enemy);
            Check(worker.IsRetreating && worker.Carried == 0, "enabled protection also retreats empty worker during manual movement");
            unit.MoveTo(new Vector3(-15, 0, 5));
            Check(!worker.IsRetreating && worker.State == GatherState.Idle, "manual movement overrides retreat");
            health.TakeDamage(1, UnitTeam.Enemy); unit.Stop();
            Check(!worker.IsRetreating && !unit.Agent.hasPath, "stop overrides retreat");
            health.TakeDamage(1, UnitTeam.Enemy); worker.Gather(node);
            Check(!worker.IsRetreating && worker.AutoGatherActive, "new harvest order overrides retreat");
            var fighter = controller.units[0]; fighter.Stop(); fighter.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
            Check(fighter.GetComponent<UnitCombat>().Order == UnitOrder.Hold, "combat units do not inherit worker retreat behavior");
            health.TakeDamage(10000, UnitTeam.Enemy);
            Check(!unit.IsAlive && !worker.IsRetreating && worker.Carried == 0, "fatal hit kills worker without issuing retreat or retaining cargo");
            production.TryQueue(3);
            yield return Until(() => controller.units.Length == 5, 8);
            var reinforcement = controller.units[controller.units.Length - 1];
            reinforcement.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
            Check(reinforcement.GetComponent<WorkerGatherer>().IsRetreating, "newly produced worker inherits current protection setting");
            controller.playerBase.TakeDamage(10000); yield return null;
            Check(!reinforcement.GetComponent<WorkerGatherer>().IsRetreating && !reinforcement.Agent.hasPath, "match end cancels retreat");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
