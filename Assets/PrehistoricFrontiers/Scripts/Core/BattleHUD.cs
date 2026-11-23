using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class BattleHUD : MonoBehaviour
    {
        public RTSSelectionController controller;
        public ProductionBuilding production;
        public EnemyProductionAI enemyProduction;
        public BattleMinimap minimap;
        public int RepaintCount { get; private set; }
        Vector2 rosterScroll, productionScroll;
        ProductionBuilding displayedProduction;
        readonly List<WorkerBuilder> constructionBuilders = new List<WorkerBuilder>();
        GUIStyle title, small, heading, button, statusStyle;
        HudLayout layout;
        bool CanIssueOrders => !controller.MatchOver && !controller.IsPaused;
        Rect R(float x, float y, float width, float height) => layout.Rect(x, y, width, height);
        public static bool IsOverPanel(Vector3 mouse)
        {
            if (MatchResultsScreen.IsVisible) return true;
            return HudLayout.Current.BlocksWorld(new Vector2(mouse.x, Screen.height - mouse.y));
        }
        void Styles()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, padding = new RectOffset(), alignment = TextAnchor.MiddleLeft };
                heading = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, padding = new RectOffset(), alignment = TextAnchor.MiddleLeft };
                small = new GUIStyle(GUI.skin.label) { wordWrap = true, padding = new RectOffset(), alignment = TextAnchor.MiddleLeft };
                statusStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Bold, padding = new RectOffset(), alignment = TextAnchor.MiddleLeft };
                button = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
            }
            title.fontSize = layout.Font(25); heading.fontSize = layout.Font(15);
            small.fontSize = layout.Font(13); statusStyle.fontSize = layout.Font(14); button.fontSize = layout.Font(14);
            button.padding = new RectOffset(Mathf.RoundToInt(10 * layout.Scale), Mathf.RoundToInt(8 * layout.Scale), Mathf.RoundToInt(3 * layout.Scale), Mathf.RoundToInt(3 * layout.Scale));
        }
        void OnGUI()
        {
            var oldSkin = GUI.skin; var oldBackground = GUI.backgroundColor; var oldContent = GUI.contentColor;
            GUI.skin = HudTheme.Skin;
            layout = HudLayout.Current; Styles();
            var construction = controller.construction;
            if (construction)
            {
                if (!construction.SelectedProduction || !construction.SelectedProduction.GetComponent<BuildingHealth>().IsAlive) construction.SelectedProduction = construction.main;
                production = construction.SelectedProduction;
            }
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            DrawHeader(); DrawRoster();
            if (controller.alerts) controller.alerts.DrawPanel();
            DrawRightPanel(construction);
            DrawFooter(construction);
            if (minimap) minimap.Draw();
            if (controller.alerts) controller.alerts.DrawMarkers();
            if (Event.current.type == EventType.Repaint) RepaintCount++;
            GUI.skin = oldSkin; GUI.backgroundColor = oldBackground; GUI.contentColor = oldContent;
        }
        void DrawHeader()
        {
            HudLayout.DrawPanel(layout.Header);
            GUI.Label(layout.Title, "PREHISTORIC FRONTIERS", title);
            GUI.enabled = !controller.MatchOver;
            if (GUI.Button(layout.Pause, controller.IsPaused ? "Продолжить [P]" : "Пауза [P]", button)) controller.SetPaused(!controller.IsPaused);
            GUI.enabled = true;
            if (production) GUI.Label(layout.Resources, "Еда: " + production.wallet.Food + "   Древесина: " + production.wallet.Wood, heading);
            if (controller.playerBase && controller.enemyBase)
            {
                GUI.Label(layout.OwnBase, "Своя база: " + controller.playerBase.CurrentHealth.ToString("0") + "/" + controller.playerBase.maxHealth.ToString("0"), small);
                GUI.Label(layout.EnemyBase, "Вражеская база: " + (controller.CanSee(UnitTeam.Player, controller.enemyBase.transform.position) ? controller.enemyBase.CurrentHealth.ToString("0") + "/" + controller.enemyBase.maxHealth.ToString("0") : "?"), small);
            }
            GUI.Label(layout.Army, "Союзники: " + controller.AlliesAlive + "/20   Враги: " + controller.VisibleEnemies, heading);
            GUI.enabled = CanIssueOrders;
            if (GUI.Button(layout.AutoBattle, controller.AutoBattleEnabled ? "Автобой: ВКЛ [G]" : "Автобой: ВЫКЛ [G]", button)) controller.SetAutoBattle(!controller.AutoBattleEnabled);
            if (GUI.Button(layout.AutoScout, controller.AutoScoutEnabled ? "Разведка: ВКЛ [H]" : "Разведка: ВЫКЛ [H]", button)) controller.SetAutoScout(!controller.AutoScoutEnabled);
            GUI.enabled = true;
        }
        void DrawRoster()
        {
            HudLayout.DrawPanel(layout.Roster);
            GUI.Label(layout.RosterTitle, "ВАШ ОТРЯД · " + controller.AlliesAlive, heading);
            float contentWidth = layout.RosterViewport.width - 22;
            rosterScroll = GUI.BeginScrollView(layout.RosterViewport, rosterScroll, new Rect(0, 0, contentWidth, controller.units.Length * 50 * layout.Scale));
            for (int i = 0; i < controller.units.Length; i++)
            {
                var unit = controller.units[i]; GUI.enabled = unit.IsAvailable;
                string name = (unit.IsAlive ? (unit.IsSelected ? "● " : "") : "× ") + HudTheme.UnitName(unit.stats.displayName) + (unit.IsAlive ? " #" + (i + 1) : " / потерян");
                if(unit.IsInClinic) name += unit.Treatment.WaitingForExit ? " / ждёт выхода" : " / лечение";
                var row = new Rect(0, i * 50 * layout.Scale, contentWidth, 44 * layout.Scale);
                if (GUI.Toggle(row, unit.IsSelected, name, button) != unit.IsSelected) controller.Select(unit, Event.current.shift);
                if (unit.IsAlive)
                {
                    var health = unit.GetComponent<UnitHealth>();
                    HudTheme.Progress(new Rect(8 * layout.Scale, row.yMax - 6 * layout.Scale, contentWidth - 16 * layout.Scale, 3 * layout.Scale), health.CurrentHealth / health.MaxHealth);
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView(); GUI.enabled = true; GUI.contentColor = Color.white;
            GUI.Label(layout.RosterHint, "Shift + ЛКМ: добавить", small);
        }
        void DrawRightPanel(ConstructionSystem construction)
        {
            HudLayout.DrawPanel(layout.ProductionViewport);
            var viewport = layout.ProductionViewport;
            float tabHeight = 46 * layout.Scale;
            if (construction)
            {
                var warriors = new Rect(viewport.x + 12 * layout.Scale, viewport.y + 6 * layout.Scale, 144 * layout.Scale, 34 * layout.Scale);
                var workers = new Rect(viewport.x + 162 * layout.Scale, warriors.y, 144 * layout.Scale, warriors.height);
                if (GUI.Toggle(warriors, construction.WarriorsSelected, "ВОИНЫ", button) != construction.WarriorsSelected) { construction.SelectProduction(true); productionScroll = Vector2.zero; }
                if (GUI.Toggle(workers, !construction.WarriorsSelected, "РАБОЧИЕ", button) == construction.WarriorsSelected) { construction.SelectProduction(false); productionScroll = Vector2.zero; }
                production = construction.WarriorsSelected ? construction.WarriorProduction : construction.main;
            }
            if (production != displayedProduction)
            {
                productionScroll = Vector2.zero;
                displayedProduction = production;
            }
            constructionBuilders.Clear();
            foreach (var unit in controller.units)
            {
                if (!unit || !unit.IsAlive || !unit.stats.isWorker) continue;
                var builder = unit.GetComponent<WorkerBuilder>();
                if (builder && builder.Site) constructionBuilders.Add(builder);
            }
            float productionTop = constructionBuilders.Count > 0 ? ConstructionQueueHeight() + 8 : 0;
            float productionHeight = production ? ProductionHeight() : 194;
            float buildTop = productionTop + productionHeight + 8;
            float enemyTop = buildTop + (construction ? 180 : 0) + 8;
            var scrollViewport = new Rect(viewport.x, viewport.y + tabHeight, viewport.width, viewport.height - tabHeight);
            productionScroll = GUI.BeginScrollView(scrollViewport, productionScroll, R(0, 0, 300, enemyProduction ? enemyTop + 114 : enemyTop));
            if (constructionBuilders.Count > 0) DrawConstructionQueue(0);
            if (production) DrawProduction(productionTop);
            else if (construction)
            {
                GUI.Box(R(0, productionTop, 300, 194), GUIContent.none);
                GUI.Label(R(14, productionTop + 10, 272, 25), "ПРОИЗВОДСТВО ВОИНОВ", heading);
                GUI.Label(R(14, productionTop + 42, 272, 44), "Раптор, трицератопс и мамонт обучаются в готовой казарме.", small);
                GUI.enabled = CanIssueOrders;
                if (GUI.Button(R(14, productionTop + 90, 272, 44), "Построить / достроить казарму", button)) construction.PrepareWarriorProduction();
                GUI.enabled = true;
                GUI.Label(R(14, productionTop + 140, 272, 44), construction.Pending ? construction.Message : "Цена: 150 древесины. Стройка: 12 с.\n" + construction.Message, small);
            }
            if (construction)
            {
                GUI.Box(R(0, buildTop, 300, 180), GUIContent.none);
                GUI.Label(R(14, buildTop + 8, 272, 24), "СТРОИТЕЛЬСТВО", heading);
                GUI.enabled = CanIssueOrders && controller.Selected.Exists(unit => unit && unit.IsAvailable && unit.stats.isWorker);
                if (GUI.Button(R(14, buildTop + 37, 272, 40), "Казарма • 12 с / 150 древесины", button)) construction.Begin(StructureKind.Barracks);
                if (GUI.Button(R(14, buildTop + 82, 272, 40), "Лечебница • 10 с\n100 еды + 120 древесины", button)) construction.Begin(StructureKind.Clinic);
                GUI.enabled = true;
                GUI.Label(R(14, buildTop + 128, 272, 42), construction.Message, small);
            }
            if (enemyProduction) DrawEnemyProduction(enemyTop);
            GUI.EndScrollView();
        }
        float ConstructionQueueHeight()
        {
            float height = 88;
            foreach (var builder in constructionBuilders) height += 38 + (1 + builder.QueuedCount) * 44;
            return height;
        }
        void DrawConstructionQueue(float top)
        {
            GUI.Box(R(0, top, 300, ConstructionQueueHeight()), GUIContent.none);
            GUI.Label(R(14, top + 8, 272, 25), "ОЧЕРЕДЬ СТРОИТЕЛЬСТВА", heading);
            GUI.Label(R(14, top + 36, 272, 44), "↑ ↓ — порядок, X — снять задание.\nПостройка остаётся для достройки.", small);
            float y = top + 88;
            bool editable = CanIssueOrders;
            foreach (var builder in constructionBuilders)
            {
                int number = System.Array.IndexOf(controller.units, builder.GetComponent<RTSUnit>()) + 1;
                GUI.Label(R(14, y, 272, 28), "Рабочий №" + number, heading); y += 30;
                var site = builder.Site;
                string state = builder.State == BuilderState.Blocked ? "Нет прохода" : builder.State == BuilderState.Travelling ? "Идёт к стройке" : "Строит";
                GUI.Label(R(14, y, 224, 40), "Сейчас: " + ConstructionSystem.Name(site.kind) + " " + Mathf.RoundToInt(site.Progress * 100) + "%\n" + state, small);
                GUI.enabled = editable;
                if (GUI.Button(R(249, y + 3, 34, 30), new GUIContent("X", "Прервать текущую стройку и перейти к следующей")))
                { builder.CancelCurrent(); GUI.enabled = true; return; }
                y += 44;
                for (int i = 0; i < builder.QueuedCount; i++)
                {
                    var queued = builder.QueuedSite(i);
                    GUI.enabled = true;
                    GUI.Label(R(14, y, 148, 36), (i + 1) + ". " + (queued ? ConstructionSystem.Name(queued.kind) : "Недоступно"), small);
                    GUI.enabled = editable && i > 0;
                    if (GUI.Button(R(169, y + 3, 34, 30), new GUIContent("↑", "Раньше")))
                    { builder.MoveQueued(i, i - 1); GUI.enabled = true; return; }
                    GUI.enabled = editable && i + 1 < builder.QueuedCount;
                    if (GUI.Button(R(209, y + 3, 34, 30), new GUIContent("↓", "Позже")))
                    { builder.MoveQueued(i, i + 1); GUI.enabled = true; return; }
                    GUI.enabled = editable;
                    if (GUI.Button(R(249, y + 3, 34, 30), new GUIContent("X", "Убрать только это задание")))
                    { builder.CancelQueued(i); GUI.enabled = true; return; }
                    y += 44;
                }
                GUI.enabled = true; y += 8;
            }
        }
        float ProductionHeight()
        {
            int rows = production.workersOnly ? 1 : production.unitPrefabs.Length;
            return 42 + rows * 53 + 60 + production.QueueCount * 46 + 44;
        }
        void DrawFooter(ConstructionSystem construction)
        {
            HudLayout.DrawPanel(layout.Footer);
            var worker = controller.Selected.Count == 1 ? controller.Selected[0].GetComponent<WorkerGatherer>() : null;
            string status = worker && !controller.MatchOver ? "РАБОЧИЙ / " + worker.StatusMessage + " / ГРУЗ " + worker.Carried + "/20" + (worker.Carried > 0 ? worker.CarriedKind == ResourceKind.Food ? " еды" : " дерева" : "") + (worker.AutoGatherActive ? " / Автосбор ВКЛ" : " / Автосбор ВЫКЛ") : controller.Status;
            if (!worker && !controller.MatchOver && controller.Selected.Count == 1 && controller.Selected[0].GetComponent<UnitCombat>().Order == UnitOrder.Scout) status = "РАЗВЕДКА / Поиск противников в неразведанных областях";
            var builder = controller.Selected.Count == 1 ? controller.Selected[0].GetComponent<WorkerBuilder>() : null;
            if (builder && builder.Site) status = builder.Status;
            if (construction && construction.Pending) status = construction.Message;
            if (controller.AttackMovePending) status = "АТАКОВАТЬ ПО ПУТИ / ЛКМ: цель / Esc: отмена";
            else if (!controller.MatchOver && controller.Selected.Count == 1 && controller.Selected[0].GetComponent<UnitCombat>().HasAttackMove) status = "АТАКОВАТЬ ПО ПУТИ / Бой и продолжение маршрута";
            if(!controller.MatchOver && controller.SelectedClinic && controller.Selected.Count==0) status=controller.SelectedClinic.Summary;
            if(!controller.MatchOver && controller.Selected.Count==1 && controller.Selected[0].IsOnClinicOrder) status=controller.Selected[0].Treatment.Status;
            if (controller.IsPreparing) status = "До наступления врага: " + Mathf.CeilToInt(controller.PreparationRemaining) + " с / " + status;
            if (controller.IsPaused) status = "ПАУЗА / Камера и мини-карта доступны / P — продолжить";
            GUI.Label(layout.Status, new GUIContent(status, status), statusStyle);
            GUI.enabled = CanIssueOrders;
            if (GUI.Button(layout.AttackMove, "Атаковать по пути [F]", button)) controller.ArmAttackMove();
            if (GUI.Button(layout.WorkerProtection, controller.WorkerProtectionEnabled ? "Защита рабочих: ВКЛ" : "Защита рабочих: ВЫКЛ", button)) controller.SetWorkerProtection(!controller.WorkerProtectionEnabled);
            GUI.enabled = true;
            GUI.Label(layout.Controls, "WASD — камера • Колесо — масштаб • MMB — сдвиг • Home — обзор", small);
            GUI.Label(layout.Hints, "ЛКМ — выбор • ПКМ — приказ • Shift — добавить • Esc — снять • Пробел — стоп", small);
        }
        void DrawEnemyProduction(float top)
        {
            var factory = enemyProduction.CombatProduction ? enemyProduction.CombatProduction : enemyProduction.production;
            GUI.Box(R(0, top, 300, 114), GUIContent.none);
            GUI.Label(R(14, top + 10, 272, 24), "ПОДКРЕПЛЕНИЯ ВРАГА", heading);
            if (!controller.CanSee(UnitTeam.Player, controller.enemyBase.transform.position))
            {
                GUI.Label(R(14, top + 40, 272, 23), "Скрыто туманом войны", small);
                GUI.Label(R(14, top + 68, 272, 32), "Разведайте вражескую базу", small); return;
            }
            GUI.Label(R(14, top + 36, 272, 23), "Еда: " + factory.wallet.Food + "   Древесина: " + factory.wallet.Wood, small);
            string slots = "Обучение: " + factory.TeamQueueCount + "/3";
            for (int i = 0; i < factory.QueueCount; i++) slots += "  " + Mathf.RoundToInt(factory.SlotProgress(i) * 100) + "%";
            GUI.Label(R(14, top + 60, 272, 23), slots, small);
            GUI.Label(R(14, top + 84, 272, 25), controller.MatchOver ? "Производство остановлено" : enemyProduction.Status, small);
        }
        void DrawProduction(float top)
        {
            GUI.Box(R(0, top, 300, ProductionHeight()), GUIContent.none);
            GUI.Label(R(14, top + 10, 272, 25), production.workersOnly ? "БАЗА / РАБОЧИЕ" : "КАЗАРМА / ВОИНЫ", heading);
            int row = 0;
            for (int i = 0; i < production.unitPrefabs.Length; i++)
            {
                var stats = production.Stats(i); if (production.workersOnly && !stats.isWorker) continue;
                string name = HudTheme.UnitName(stats.displayName);
                string reason = controller.MatchOver ? "Матч завершён" : controller.IsPaused ? "Игра на паузе"
                    : controller.IsPreparingFor(UnitTeam.Player) && !stats.isWorker ? "Подготовка: " + Mathf.CeilToInt(controller.PreparationRemaining) + " с"
                    : production.TeamQueueCount >= ProductionBuilding.QueueLimit ? "Все 3 места обучения заняты"
                    : controller.AlliesAlive + production.TeamQueueCount >= ProductionBuilding.PopulationLimit ? "Лимит отряда: 20"
                    : !production.wallet.CanAfford(stats.foodCost, stats.woodCost) ? "Недостаточно ресурсов" : "";
                GUI.enabled = reason.Length == 0;
                string text = "Обучить: " + name + " • " + stats.productionTime + " с\n" + (reason.Length > 0 ? reason : stats.foodCost + " еды / " + stats.woodCost + " древесины");
                if (GUI.Button(R(14, top + 42 + row++ * 53, 272, 46), new GUIContent(text, reason), button)) production.TryQueue(i);
            }
            GUI.enabled = true;
            float queueTop = top + 42 + row * 53;
            GUI.Label(R(14, queueTop, 272, 24), "Обучение: " + production.TeamQueueCount + "/3   Отряд: " + controller.AlliesAlive + "/20", heading);
            GUI.Label(R(14, queueTop + 26, 272, 26), production.QueueCount == 0 ? "Выберите юнита для обучения" : "В этом здании: " + production.QueueCount + " • одновременно", small);
            for (int i = 0; i < production.QueueCount; i++)
            {
                float y = queueTop + 60 + i * 46;
                GUI.Label(R(14, y, 235, 24), HudTheme.UnitName(production.QueueName(i)) + " " + Mathf.RoundToInt(production.SlotProgress(i) * 100) + "% / " + Mathf.CeilToInt(production.RemainingSeconds(i)) + " с", small);
                HudTheme.Progress(R(14, y + 27, 230, 6), production.SlotProgress(i));
                GUI.enabled = CanIssueOrders;
                if (GUI.Button(R(253, y, 32, 22), "X")) { production.Cancel(i); break; }
                GUI.enabled = true;
            }
            GUI.Label(R(14, queueTop + 60 + production.QueueCount * 46, 272, 38), production.Message, small);
        }
    }
}
