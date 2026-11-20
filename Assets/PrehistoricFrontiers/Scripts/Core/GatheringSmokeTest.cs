using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class GatheringSmokeTest : MonoBehaviour
    {
        public ProductionBuilding production;
        public ResourceNode[] nodes;
        bool failed;
        string report = "";
        void Check(bool value, string name) { report += (value ? "PASS " : "FAIL ") + name + "\n"; failed |= !value; }
        IEnumerator Until(Func<bool> predicate, float seconds)
        {
            float deadline = Time.time + seconds;
            while (!predicate() && Time.time < deadline) yield return null;
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gatheringSmoke") < 0) yield break;
            var controller = production.controller; controller.SetAutoBattle(false); Time.timeScale = 5;
            SmokeTestSetup.DisableEnemyProduction(controller);
            yield return null;
            Check(production.TryQueue(3), "worker can be queued in main building");
            yield return Until(() => controller.units.Length == 4, 8); yield return null;
            var unit = controller.units[controller.units.Length - 1];
            var worker = unit.GetComponent<WorkerGatherer>();
            Check(worker && unit.stats.isWorker && worker.home == production && unit.Agent.isOnNavMesh, "trained worker has gatherer home and navigation");
            if (!worker) { File.WriteAllText("gathering-test.txt", report); Application.Quit(1); yield break; }
            worker.autoGatherEnabled = false; // Isolate the original single-source route checks.
            Check(!unit.GetComponent<UnitCombat>().Attack(controller.enemies[0]), "worker cannot attack");
            var wallet = production.wallet;
            int foodBefore = wallet.Food, woodBefore = wallet.Wood;
            var food = nodes[0]; food.Extract(food.Remaining - 45);
            controller.Select(controller.units[0]);
            Check(controller.IssueGather(food) == 0, "combat animal cannot gather");
            controller.Select(unit);
            Check(controller.IssueGather(food) == 1, "worker accepts resource command");
            yield return Until(() => worker.Carried > 0, 20);
            Check(worker.Carried > 0 && worker.Carried <= WorkerGatherer.CarryCapacity && wallet.Food == foodBefore, "harvesting fills cargo before crediting wallet");
            int carried = worker.Carried, remaining = food.Remaining;
            unit.Stop(); yield return new WaitForSeconds(2);
            Check(worker.State == GatherState.Idle && worker.Node == null && worker.Carried == carried && food.Remaining == remaining, "stop cancels work without destroying cargo");
            worker.Gather(food);
            Check(unit.MoveTo(new Vector3(0, 0, -13)) && worker.State == GatherState.Idle && worker.Node == null, "move command cancels gathering");
            Check(controller.IssueReturn() == 1, "right click base orders deposit of retained cargo");
            yield return Until(() => wallet.Food == foodBefore + carried, 25);
            Check(wallet.Food == foodBefore + carried && worker.Carried == 0 && worker.State == GatherState.Idle, "base credits cargo exactly once");
            worker.Gather(food);
            yield return Until(() => food.Remaining == 0 && worker.Carried == 0 && worker.State == GatherState.Idle, 75);
            Check(food.Remaining == 0 && wallet.Food == foodBefore + 45 && wallet.Wood == woodBefore, "automatic trips deliver full food stock including partial final load");
            Check(worker.State == GatherState.Idle && !food.GetComponentInChildren<Renderer>().enabled, "depleted source disappears and worker stops");
            Check(!worker.Gather(food), "depleted resource rejects new gather order");
            var wood = nodes[2]; wood.Extract(wood.Remaining - 25);
            worker.Gather(wood); yield return Until(() => worker.Carried > 0, 25);
            int woodCargo = worker.Carried;
            Check(woodCargo > 0 && worker.CarriedKind == ResourceKind.Wood && wallet.Wood == woodBefore, "wood is carried separately from food");
            var otherFood = nodes[1]; otherFood.Extract(otherFood.Remaining - 10);
            worker.Gather(otherFood);
            yield return Until(() => wallet.Wood == woodBefore + woodCargo, 25);
            Check(wallet.Wood == woodBefore + woodCargo, "changing resource deposits old cargo in correct wallet");
            yield return Until(() => otherFood.Remaining == 0 && worker.Carried == 0 && worker.State == GatherState.Idle, 60);
            Check(wallet.Food == foodBefore + 55, "new food route credits only food");
            int sharedWood = wood.Remaining, woodAtStart = wallet.Wood;
            int count = controller.units.Length;
            production.TryQueue(3); yield return Until(() => controller.units.Length > count, 8); yield return null;
            var second = controller.units[controller.units.Length - 1].GetComponent<WorkerGatherer>();
            Check(second && second != worker, "second worker can be trained");
            if (!second) { File.WriteAllText("gathering-test.txt", report); Application.Quit(1); yield break; }
            second.autoGatherEnabled = false;
            worker.Gather(wood); second.Gather(wood);
            yield return Until(() => wood.Remaining == 0 && worker.Carried == 0 && second.Carried == 0 && worker.State == GatherState.Idle && second.State == GatherState.Idle, 75);
            Check(wood.Remaining == 0 && wallet.Wood == woodAtStart + sharedWood, "two workers share finite source without duplicating resources");
            Check(worker.State == GatherState.Idle && second.State == GatherState.Idle, "both workers stop after shared source is exhausted");
            var reserve = nodes[3]; reserve.Extract(reserve.Remaining - 10);
            second.Gather(reserve); yield return Until(() => second.Carried > 0, 35);
            int deathWood = wallet.Wood;
            Check(second.Carried > 0, "worker obtains cargo before death test");
            second.GetComponent<UnitHealth>().TakeDamage(10000);
            yield return new WaitForSeconds(10);
            Check(!second.gameObject.activeSelf && second.Carried == 0 && wallet.Wood == deathWood, "dead worker stops and undelivered cargo is lost");
            foreach (var enemy in controller.enemies) enemy.GetComponent<UnitHealth>().TakeDamage(10000);
            controller.enemyBase.TakeDamage(10000);
            yield return null;
            Check(!worker.Gather(reserve), "gathering orders rejected after match end");
            File.WriteAllText("gathering-test.txt", report); Debug.Log(report); Application.Quit(failed ? 1 : 0);
        }
    }
}
