using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class EnemyEconomySmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-enemyEconomySmoke") >= 0)
                new GameObject("Enemy economy regression").AddComponent<EnemyEconomySmokeTest>();
        }
        RTSSelectionController c;
        bool failed; string report = "";
        void Check(bool value, string name)
        {
            string line = (value ? "PASS " : "FAIL ") + name; failed |= !value; report += line + "\n";
            Debug.Log(line); File.WriteAllText("enemyeconomy-test.txt", report);
        }
        void HoldFighters()
        {
            foreach (var unit in c.enemies)
                if (unit && unit.IsAlive && !unit.stats.isWorker) { unit.GetComponent<UnitCombat>().autoAcquire = false; unit.Stop(); }
        }
        IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) { HoldFighters(); yield return null; }
        }
        int Stock(ResourceNode[] nodes) { int total = 0; foreach (var node in nodes) total += node.Remaining; return total; }
        IEnumerator Start()
        {
            yield return null; Time.timeScale = 4;
            c = FindAnyObjectByType<RTSSelectionController>(); c.SetAutoBattle(false); c.SetAutoScout(false);
            foreach (var unit in c.units) unit.Stop(); HoldFighters();
            var main = c.enemyBase.GetComponent<ProductionBuilding>(); var ai = main.GetComponent<EnemyProductionAI>();
            var economy = main.GetComponent<EnemyEconomyAI>(); ai.enabled = false;
            Check(c.IsPreparing && c.preparationDuration == 20 && c.units.Length == 4 && c.enemies.Length == 4,
                "both factions start with four units and a shared twenty second preparation phase");
            Check(economy.WorkersAlive == 1 && main.wallet.Food == 1000 && main.wallet.Wood == 1000
                && c.construction.main.wallet.Food == 1000 && c.construction.main.wallet.Wood == 1000,
                "both factions have one worker and equal starting resources");
            Check(main.workersOnly && !main.TryQueue(0) && !c.units[0].GetComponent<UnitCombat>().Attack(c.enemies[0]),
                "main base cannot train warriors and attacks cannot bypass preparation");
            var nodes = Array.FindAll(FindObjectsByType<ResourceNode>(), n => n.name.StartsWith("Enemy "));
            int stock = Stock(nodes);
            Check(nodes.Length == 4 && stock == 900, "enemy has two food and two wood deposits with matching reserves");
            yield return Until(() => economy.construction.WarriorProduction, 45);
            var factory = economy.construction.WarriorProduction;
            Check(factory && c.construction.Sites.Count == 0, "enemy completes its own real barracks without any player barracks");
            if (!factory) { Application.Quit(1); yield break; }
            Check(factory.Team == UnitTeam.Enemy && factory.wallet == main.wallet && factory.GetComponent<ConstructionSite>().Complete
                && factory.GetComponent<BuildingHealth>().maxHealth == 500, "enemy barracks uses normal ownership cost duration and health");
            factory.GetComponent<FogHiddenVisual>().ApplyNow();
            Check(!c.CanSee(UnitTeam.Player,factory.transform.position) && Array.TrueForAll(factory.GetComponentsInChildren<Renderer>(),v=>v.forceRenderingOff),
                "unseen enemy construction stays hidden by fog of war");
            yield return Until(() => economy.WorkersAlive == 3 && main.wallet.Food > 900 && main.wallet.Wood > 850, 65);
            Check(economy.WorkersAlive == 3, "enemy trains additional workers through paid normal production");
            Check(main.wallet.Food > 900 && main.wallet.Wood > 850 && Stock(nodes) < stock,
                "enemy workers harvest and deliver both food and wood into their own wallet");
            int cargo = 0; foreach (var unit in c.enemies) if (unit.IsAlive && unit.stats.isWorker) cargo += unit.GetComponent<WorkerGatherer>().Carried;
            Check(main.wallet.Food + main.wallet.Wood + cargo == 2000 - 150 - 100 + stock - Stock(nodes),
                "resources balance exactly across construction training delivered income and carried cargo");
            c.SetPaused(true); int food = main.wallet.Food, wood = main.wallet.Wood; stock = Stock(nodes);
            yield return new WaitForSecondsRealtime(.25f);
            Check(main.wallet.Food == food && main.wallet.Wood == wood && Stock(nodes) == stock, "pause freezes enemy harvesting and economy decisions");
            c.SetPaused(false);
            yield return Until(() => !c.IsPreparing, 21);
            ai.enabled = true; int army = c.enemies.Length;
            Check(ai.TryPlanNext() && ai.CombatProduction == factory && factory.QueueCount > 0, "reinforcements are ordered in own completed barracks");
            Check(factory.Stats(0) == c.construction.main.Stats(0) && factory.Stats(1) == c.construction.main.Stats(1)
                && factory.Stats(2) == c.construction.main.Stats(2), "both factions share identical combat training prices and durations");
            yield return Until(() => c.enemies.Length > army, 16);
            Check(c.enemies.Length > army && !c.enemies[c.enemies.Length-1].stats.isWorker, "paid combat reinforcement actually spawns from barracks");
            ai.enabled = false;
            foreach (var production in FindObjectsByType<ProductionBuilding>()) if (production.Team == UnitTeam.Enemy) production.CancelAll();
            foreach (var unit in c.enemies) if (unit.IsAlive && unit.stats.isWorker) unit.GetComponent<UnitHealth>().TakeDamage(10000);
            ai.enabled = true;
            Check(!ai.TryPlanNext() && factory.QueueCount == 0, "worker recovery takes priority over new combat orders");
            ai.enabled = false;
            food = main.wallet.Food;
            economy.Tick();
            Check(economy.WorkersAlive == 0 && main.QueueCount == 1 && main.QueueName(0) == "Worker" && main.wallet.Food == food-50,
                "losing all workers queues a paid replacement instead of creating free units");
            yield return Until(() => economy.WorkersAlive > 0, 8);
            Check(economy.WorkersAlive > 0, "replacement worker completes normal training and restores workforce");
            var oldSite = factory.GetComponent<ConstructionSite>(); oldSite.Health.TakeDamage(10000,UnitTeam.Player);
            wood = main.wallet.Wood; economy.Tick();
            var replacement = economy.construction.Sites.Find(s=>s && s != oldSite && s.Health.IsAlive && s.kind == StructureKind.Barracks);
            Check(!c.MatchOver && replacement && !replacement.Complete && main.wallet.Wood == wood-150,
                "destroyed barracks is replaced through a new paid construction site");
            yield return Until(() => replacement && replacement.Complete, 35);
            Check(replacement && replacement.Complete && ai.CombatProduction == replacement.GetComponent<ProductionBuilding>(),
                "replacement barracks is actually built and becomes new reinforcement source");
            c.enemyBase.TakeDamage(10000,UnitTeam.Player); yield return null;
            food = main.wallet.Food; wood = main.wallet.Wood; army = c.enemies.Length; stock = Stock(nodes);
            yield return new WaitForSeconds(3);
            Check(c.MatchOver && main.wallet.Food == food && main.wallet.Wood == wood && c.enemies.Length == army && Stock(nodes) == stock,
                "match completion stops enemy harvesting construction and all production");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
