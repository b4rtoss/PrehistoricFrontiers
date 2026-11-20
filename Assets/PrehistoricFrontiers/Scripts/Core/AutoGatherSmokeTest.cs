using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class AutoGatherSmokeTest : MonoBehaviour
    {
        public ProductionBuilding production;
        public ResourceNode[] nodes;
        bool failed;
        string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name;
            report += line + "\n"; failed |= !value;
            Debug.Log(line); File.WriteAllText("autogather-test.txt", report);
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
        }
        ResourceNode NewNode(string name, ResourceKind kind, Vector3 position, int amount)
        {
            var go = new GameObject(name); go.transform.position = position;
            var node = go.AddComponent<ResourceNode>(); node.kind = kind; node.Extract(node.Remaining - amount);
            return node;
        }
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autoGatherSmoke") < 0) yield break;
            var controller = production.controller; controller.SetAutoBattle(false); Time.timeScale = 5;
            SmokeTestSetup.DisableEnemyProduction(controller);
            yield return null;
            Check(production.TryQueue(3), "worker production accepted");
            yield return Until(() => controller.units.Length == 4, 10); yield return null;
            var worker = controller.units[controller.units.Length - 1].GetComponent<WorkerGatherer>();
            Check(worker && worker.autoGatherEnabled, "auto gather enabled by default for trained worker");
            if (!worker) { Application.Quit(1); yield break; }
            var unit = worker.GetComponent<RTSUnit>(); var wallet = production.wallet;
            int foodStart = wallet.Food, woodStart = wallet.Wood;
            var first = nodes[0]; first.Extract(first.Remaining - 5);
            var next = nodes[1]; next.Extract(next.Remaining - 10);
            Check(worker.Gather(first) && worker.AutoGatherActive && worker.DesiredKind == ResourceKind.Food, "first command starts food auto route");
            yield return Until(() => first.Remaining == 0, 25);
            Check(worker.Carried == 5 && worker.State == GatherState.Returning && worker.Node == first && wallet.Food == foodStart, "partial final load returns to base before selecting another source");
            yield return Until(() => wallet.Food >= foodStart + 5, 25);
            Check(worker.Node == next && worker.AutoGatherActive, "worker automatically switches to next food source after deposit");
            Check(wallet.Wood == woodStart && nodes[2].Remaining == nodes[2].capacity && nodes[3].Remaining == nodes[3].capacity, "food route never switches to wood");
            yield return Until(() => worker.State == GatherState.Idle && !worker.AutoGatherActive, 65);
            Check(first.Remaining == 0 && next.Remaining == 0 && wallet.Food == foodStart + 15, "both food sources delivered exactly once");
            Check(worker.State == GatherState.Idle && worker.StatusMessage == "Нет еды", "no remaining food produces explicit idle message");
            var farWood = nodes[3]; farWood.Extract(farWood.Remaining - 5);
            var chosenWood = nodes[2]; chosenWood.Extract(chosenWood.Remaining - 5);
            var nearWood = NewNode("Nearby test wood", ResourceKind.Wood, new Vector3(-9, 0, -17), 5);
            var unreachable = NewNode("Unreachable test wood", ResourceKind.Wood, new Vector3(-40, 0, -15), 5);
            Check(worker.Gather(chosenWood) && worker.Node == chosenWood, "explicit source command takes priority over nearer source");
            yield return Until(() => wallet.Wood >= woodStart + 5, 40);
            Check(worker.Node == nearWood, "next source chosen by shortest reachable navigation route");
            yield return Until(() => wallet.Wood == woodStart + 15 && worker.State == GatherState.Blocked, 85);
            Check(wallet.Wood == woodStart + 15 && chosenWood.Remaining == 0 && nearWood.Remaining == 0 && farWood.Remaining == 0, "reachable wood sources are collected without duplicate credits");
            Check(unreachable.Remaining == 5 && worker.State == GatherState.Blocked && worker.AutoGatherActive, "unreachable source skipped while worker waits for a valid route");
            unreachable.Extract(unreachable.Remaining);
            yield return Until(() => worker.State == GatherState.Idle, 4);
            Check(worker.StatusMessage == "Нет древесины" && !worker.AutoGatherActive, "all wood exhausted produces explicit idle message");
            var freshFood = NewNode("Fresh test food", ResourceKind.Food, new Vector3(-6, 0, -17), 10);
            worker.Gather(freshFood);
            yield return Until(() => worker.Carried > 0, 30);
            int cargo = worker.Carried;
            unit.Stop(); yield return new WaitForSeconds(3);
            Check(!worker.AutoGatherActive && worker.Node == null && worker.State == GatherState.Idle && worker.Carried == cargo && cargo > 0, "stop cancels automatic route and retains cargo");
            worker.Gather(freshFood);
            Check(unit.MoveTo(new Vector3(0, 0, -13)) && !worker.AutoGatherActive && worker.State == GatherState.Idle, "manual move cancels automatic route");
            Check(worker.ReturnToBase(), "manual base return accepted");
            yield return Until(() => worker.Carried == 0 && worker.State == GatherState.Idle, 25);
            Check(!worker.AutoGatherActive && wallet.Food == foodStart + 15 + cargo && freshFood.Remaining == 10 - cargo, "manual deposit does not restart auto route");
            freshFood.Extract(freshFood.Remaining);
            int count = controller.units.Length;
            production.TryQueue(3); yield return Until(() => controller.units.Length > count, 10); yield return null;
            var second = controller.units[controller.units.Length - 1].GetComponent<WorkerGatherer>();
            Check(second && second != worker, "second automatic worker produced");
            if (!second) { Application.Quit(1); yield break; }
            var shared = NewNode("Shared test food", ResourceKind.Food, new Vector3(-5, 0, -17), 10);
            var alternate = NewNode("Alternate test food", ResourceKind.Food, new Vector3(-14, 0, -15), 10);
            int sharedStart = wallet.Food;
            worker.Gather(shared); second.Gather(shared);
            yield return Until(() => shared.Remaining == 0 && alternate.Remaining == 0 && worker.Carried == 0 && second.Carried == 0 && worker.State == GatherState.Idle && second.State == GatherState.Idle, 95);
            Check(wallet.Food == sharedStart + 20 && shared.Remaining == 0 && alternate.Remaining == 0, "two workers retarget finite sources without duplicating resources");
            Check(worker.StatusMessage == "Нет еды" && second.StatusMessage == "Нет еды" && !worker.AutoGatherActive && !second.AutoGatherActive, "both workers finish auto routes after resources run out");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
