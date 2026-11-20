using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ConstructionSmokeTest : MonoBehaviour
    {
        public RTSSelectionController controller;
        string report = "";
        bool failed;
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name; failed |= !value;
            report += line + "\n"; Debug.Log(line); File.WriteAllText("construction-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-constructionSmoke") < 0) yield break;
            yield return null; Time.timeScale = 4;
            SmokeTestSetup.DisableEnemyProduction(controller, false); controller.SetAutoBattle(false); controller.SetAutoScout(false);
            foreach (var ally in controller.units) ally.Stop();
            foreach (var enemyUnit in controller.enemies) { enemyUnit.Stop(); enemyUnit.Agent.Warp(new Vector3(21, 0, 21)); }
            var system = controller.construction; var main = system.main;
            int startingCount = controller.units.Length;
            Check(main.workersOnly && !main.TryQueue(0) && main.TryQueue(3), "base only produces workers and rejects combat units");
            yield return Until(() => controller.units.Length == startingCount + 1, 8);
            var worker = controller.units[controller.units.Length - 1]; worker.Stop();
            main.wallet.Refund(500, 500);
            var point = new Vector3(-6, 0, 6); worker.Agent.Warp(new Vector3(-6, 0, 1)); Physics.SyncTransforms();
            int food = main.wallet.Food, wood = main.wallet.Wood;
            Check(!system.Place(worker, StructureKind.Barracks, controller.playerBase.transform.position) && main.wallet.Wood == wood, "occupied placement rejected without charge");
            Check(!system.Place(worker, StructureKind.Barracks, new Vector3(24, 0, 24)), "placement outside battlefield rejected");
            controller.fog.SetFogEnabled(true); controller.fog.ResetExploration();
            Check(!system.Place(worker, StructureKind.Barracks, new Vector3(18, 0, -18)), "unseen placement rejected");
            controller.fog.SetFogEnabled(false);
            var barracks = system.Place(worker, StructureKind.Barracks, point);
            Check(barracks && main.wallet.Food == food && main.wallet.Wood == wood - 150, "valid barracks starts and pays exact one time construction cost / " + system.Message);
            if (!barracks) { Application.Quit(1); yield break; }
            Check(!barracks.Complete && !barracks.GetComponent<ProductionBuilding>() && barracks.Health.IsAlive, "unfinished barracks is damageable but cannot produce");
            var builder = worker.GetComponent<WorkerBuilder>();
            Check(builder.State == BuilderState.Travelling && builder.Status.StartsWith("Иду к стройке"), "builder reports travel before reaching construction");
            yield return Until(() => barracks.Progress > .12f, 8);
            Check(barracks.Progress > .12f && barracks.Progress < 1, "nearby worker advances construction gradually");
            Check(builder.State == BuilderState.Building && builder.Status.StartsWith("Строю"), "builder reports construction only while working");
            float blockedProgress = barracks.Progress;
            worker.Agent.enabled = false;
            yield return null;
            Check(builder.State == BuilderState.Blocked && builder.Status.StartsWith("Нет прохода"), "unavailable navigation reports blocked construction");
            yield return new WaitForSeconds(.3f);
            Check(barracks.Progress == blockedProgress, "blocked navigation cannot advance construction");
            worker.Agent.enabled = true;
            yield return null;
            Check(builder.State == BuilderState.Building, "restored navigation resumes construction");
            worker.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy);
            Check(worker.GetComponent<WorkerGatherer>().IsRetreating && !barracks.Builder, "worker protection interrupts construction and preserves unfinished site");
            worker.Stop(); float progress = barracks.Progress;
            yield return new WaitForSeconds(1);
            Check(barracks.Progress == progress && !barracks.Builder, "stop detaches builder and pauses construction");
            controller.Select(worker); system.Resume(barracks);
            yield return Until(() => barracks.Complete, 18);
            Check(barracks.Complete && barracks.GetComponent<ProductionBuilding>(), "right click resume completes saved construction");
            var factory = barracks.GetComponent<ProductionBuilding>();
            if (!factory) { Application.Quit(1); yield break; }
            Check(factory.wallet == main.wallet && factory.unitPrefabs.Length == 3 && system.SelectedProduction == factory, "completed barracks shares wallet and exposes combat production");
            Check(factory.TryQueue(0) && main.TryQueue(3) && factory.TryQueue(1) && !main.TryQueue(3), "base and barracks share global three training slots");
            main.CancelAll(); factory.CancelAll();
            int alive = controller.AlliesAlive;
            factory.TryQueue(0);
            yield return Until(() => controller.AlliesAlive > alive, 8);
            Check(controller.AlliesAlive == alive + 1 && controller.units[controller.units.Length - 1].Agent.isOnNavMesh, "barracks spawns valid combat reinforcement");
            var clinicPoint = new Vector3(-6, 0, -1); worker.Agent.Warp(new Vector3(-6, 0, -5)); Physics.SyncTransforms();
            var clinic = system.Place(worker, StructureKind.Clinic, clinicPoint);
            Check(clinic && !clinic.GetComponent<HealingClinic>(), "unfinished clinic cannot heal");
            if (!clinic) { Application.Quit(1); yield break; }
            yield return Until(() => clinic.Complete, 18);
            Check(clinic.Complete && clinic.GetComponent<HealingClinic>(), "completed clinic activates healing");
            var patient=controller.units[1];patient.Stop();patient.Agent.Warp(new Vector3(-11,0,-1));
            var health=patient.GetComponent<UnitHealth>();health.TakeDamage(20);float damaged=health.CurrentHealth;
            var hospital=clinic.GetComponent<HealingClinic>();yield return new WaitForSeconds(6);
            Check(health.CurrentHealth==damaged,"clinic does not heal nearby units without an order");
            Check(hospital.Request(patient),"completed clinic accepts wounded allied patient");
            yield return Until(()=>patient.IsInClinic,10);
            Check(patient.IsInClinic,"ordered patient enters and disappears inside clinic");
            yield return Until(()=>!patient.IsInClinic && health.CurrentHealth==health.MaxHealth,10);
            Check(!patient.IsInClinic && health.CurrentHealth==health.MaxHealth && patient.Agent.isOnNavMesh,"patient returns fully healed on navigation mesh");
            var enemy=controller.enemies[0];enemy.GetComponent<UnitHealth>().TakeDamage(20);
            Check(!hospital.Request(enemy),"clinic rejects enemy patients");
            health.TakeDamage(40);patient.Agent.Warp(new Vector3(-11,0,-1));hospital.Request(patient);
            yield return Until(()=>patient.IsInClinic,10);float beforeCollapse=health.CurrentHealth;
            clinic.Health.TakeDamage(10000,UnitTeam.Enemy);yield return Until(()=>!patient.IsInClinic,3);
            Check(!clinic.Health.IsAlive && !patient.IsInClinic && health.CurrentHealth<=beforeCollapse+.1f && !controller.MatchOver,"destroyed clinic evacuates living patient without ending match");
            worker.Agent.Warp(new Vector3(12, 0, -3)); Physics.SyncTransforms();
            var abandoned = system.Place(worker, StructureKind.Barracks, new Vector3(8, 0, -3));
            Check(abandoned && !abandoned.Complete, "second construction site placed");
            if (abandoned)
            {
                worker.Stop(); worker.Agent.Warp(new Vector3(-22, 0, -21));
                foreach (var ally in controller.units) if (ally != worker) { ally.Stop(); ally.Agent.Warp(new Vector3(20, 0, -20)); }
                var opponent=controller.enemies[2];opponent.Agent.Warp(new Vector3(8, 0, -8));
                opponent.GetComponent<UnitCombat>().autoAcquire = true; opponent.GetComponent<UnitCombat>().SetAutomation(true);
                controller.fog.SetFogEnabled(true); controller.fog.RefreshNow();
                float structureHealth = abandoned.Health.CurrentHealth;
                yield return Until(() => abandoned.Health.CurrentHealth < structureHealth, 8);
                Check(abandoned.Health.CurrentHealth < structureHealth && opponent.GetComponent<UnitCombat>().BuildingTarget == abandoned.Health,
                    "enemy autonomously discovers and attacks unfinished player structure");
                abandoned.Health.TakeDamage(10000, UnitTeam.Enemy); yield return null;
                Check(!abandoned.Health.IsAlive && !worker.GetComponent<WorkerBuilder>().Site && !worker.Agent.hasPath, "destroyed unfinished building releases and stops builder");
            }
            int refundFood = main.wallet.Food, refundWood = main.wallet.Wood;
            factory.TryQueue(0);
            barracks.Health.TakeDamage(10000, UnitTeam.Enemy);
            Check(factory.QueueCount == 0 && main.wallet.Food == refundFood && main.wallet.Wood == refundWood, "destroyed barracks refunds pending training through shared wallet");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
