using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private string _deckFilter = "all", _deckSort = "cost", _selectedOverviewCard = "";
    private bool _prepareMapExpanded;

    private void LootSummary(VBoxContainer box)
    {
        var items = Session.State.Stock.Where(i => Session.State.Ground.TryGetValue(i.Container, out var ground) && ground.Place == Session.State.Location).ToArray();
        var status = Row(28); box.AddChild(status);
        Metric(status, "随身容量", CapacityText(Session.CarryUsed, Session.CarryCapacity), Session.CarryUsed >= Session.CarryCapacity ? Danger : Ink, "bag");
        Metric(status, "当前负重", $"{Session.CarriedGrams / 1000m:0.#} / 20 kg", Session.Overweight ? Danger : Ink);
        var workspace = Row(18); workspace.Name = "PickupWorkspace"; workspace.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(workspace);
        var carry = Section(workspace, .7f); carry.AddChild(Text("随身携带", 20, Ink));
        var carryPage = Scroll(carry, "pickup_carry");
        foreach (var held in Session.CarriedItems)
        {
            var row = Row(8); carryPage.AddChild(row); ItemVisual(row, held.Kind, 36);
            var copy = FlexibleColumn(row); copy.AddChild(Text(ChapterContent.ItemName(held.Kind) + " ×" + held.Quantity, 16, Ink));
            copy.AddChild(Text((held.Kind.StartsWith('T') ? "任务物 · " : "") + (held.KeepOnPerson ? "保留随身 · " : "") + $"{ChapterContent.Items[held.Kind].Grams * held.Quantity / 1000m:0.##} kg", 14, Muted));
        }
        if (!Session.CarriedItems.Any()) carryPage.AddChild(Text("背包为空", 16, Muted));
        var found = Section(workspace, 1.3f); found.AddChild(Text("本地搜获", 20, Ink));
        var page = Scroll(found, "pickup");
        if (items.Length == 0)
        {
            page.AddChild(Text("已收入背包", 28, Success));
            page.AddChild(Text("继续探索，或带着收获安全撤离。", 18, Muted));
        }
        foreach (var pair in Session.GroundHere.ToArray())
        {
            var key = pair.Key; var sourceItems = items.Where(i => i.Container == key).ToArray();
            if (sourceItems.Length == 0) continue;
            var source = Column(10); page.AddChild(source);
            var title = Row(12); source.AddChild(title);
            var caption = Text(pair.Value.Name, 20, Ink); caption.SizeFlagsHorizontal = SizeFlags.ExpandFill; title.AddChild(caption);
            var takeAll = Session.Preview(s => s.PickupAll(key));
            if (sourceItems.Length > 1) title.AddChild(Button("loot_take_" + key.Replace(':', '_'), "全部拿取", () => Change(() => Session.PickupAll(key)), takeAll != null, true));
            source.AddChild(Text(pair.Value.FreePickup ? "本次免费装包 · 关闭后再次拿取会增加区域压力" : "每次拿取增加区域压力 · 全部拿取合为一次", 14, Muted));
            foreach (var item in sourceItems)
            {
                var row = Row(10); source.AddChild(row); ItemVisual(row, item.Kind, 48);
                var info = FlexibleColumn(row); info.AddChild(Text(ChapterContent.ItemName(item.Kind) + " ×" + item.Quantity, 20, Ink));
                info.AddChild(Text(ItemPurpose(item.Kind), 16, Muted));
                var count = ChosenQuantity(item);
                var can = Session.CanPickup(item.Id, count);
                var prediction = Session.Preview(s => s.Pickup(item.Id, count));
                var cost = prediction == null ? "" : prediction.State.Phase == ChapterPhase.Battle ? " · 巡逻拦截" : prediction.State.Pressure == Session.State.Pressure ? " · 免费" : $" · 压力 +{prediction.State.Pressure - Session.State.Pressure}";
                if (prediction != null && prediction.State.Phase != ChapterPhase.Battle) info.AddChild(Text("拿取后：" + CapacityText(prediction.CarryUsed, prediction.CarryCapacity) + $"\n负重 {prediction.CarriedGrams / 1000m:0.##}/20 kg", 14, prediction.Overweight ? Danger : Muted));
                var controls = Row(6); source.AddChild(controls);
                controls.AddChild(Button((item.Quantity > 1 ? "pickup_stack_" : "pickup_") + item.Id, can ? $"拿取 ×{count}" + cost : "腾出空间…", () =>
                {
                    if (can) RequestStockMove(item.Id, item.Container, count);
                    else { _spacePlanItem = item.Id; OpenNested("space-plan"); }
                }, primary: sourceItems.Length == 1));
                if (item.Quantity > 1) controls.AddChild(QuietButton("pickup_quantity_" + item.Id, "选择数量", () => OpenQuantity(item)));
                BindStockPointer(controls.GetChild<Button>(0), item);
            }
            var fitting = Session.FittingPickup(key);
            if (sourceItems.Length > 1) source.AddChild(QuietButton("loot_plan_" + key.Replace(':', '_'), takeAll == null ? "选择数量 / 安排装包顺序" : "自选数量与顺序", () => ConfirmPartialPickup(key, fitting)));
        }
        if (Session.State.Warned && items.Length > 0) box.AddChild(Text("巡逻已抵达：消耗行动的拿取会先触发拦截。", 16, Danger));
        var actions = Row(12); box.AddChild(actions);
        actions.AddChild(Button("loot_manage", "整理背包 / 腾出空间", () => { _panel = "gear"; _gearFilter = "cargo"; Render(); }));
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        actions.AddChild(Button("loot_return", items.Length == 0 ? "继续探索 →" : "留在原地 · 返回", ClosePanel, primary: items.Length == 0));
    }

    private void Preparation(VBoxContainer box)
    {
        var s = Session.State; var camp = s.Phase == ChapterPhase.Camp;
        if (_prepareMapExpanded) { PreparationRoute(box); return; }
        var split = Row(24); split.Name = "PreparationWorkspace"; split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var route = Section(split, .85f); var routeBody = Scroll(route);
        var picture = ChapterArt.Picture(s.Start); picture.CustomMinimumSize = new Vector2(0, CompactDensity ? 130 : 180); routeBody.AddChild(picture);
        Eyebrow(routeBody, "部署起点"); routeBody.AddChild(Text(ChapterContent.Places[s.Start].Name, 28, Ink));
        if (ChapterContent.Places.TryGetValue(Session.QuestPlace(Session.ObjectiveQuest), out var destination)) routeBody.AddChild(Text("任务目的地 · " + destination.Name, 16, Accent));
        routeBody.AddChild(Text(Session.NextObjective, 18, Ink));
        foreach (var id in new[] { "A01", "A05" })
        {
            var place = id;
            routeBody.AddChild(NavigationButton("start_" + id, (s.Start == id ? "✓  " : "") + ChapterContent.Places[id].Name + (id == "A05" && !s.Done.Contains("S01") ? " · 尚未开放" : ""), s.Start == id, () => Change(() => Session.SetStart(place))));
            Buttons["start_" + id].Disabled = !camp || id == "A05" && !s.Done.Contains("S01");
        }
        var routeActions = Row(8); route.AddChild(routeActions);
        routeActions.AddChild(QuietButton("prepare_map", "查看路线", () => { _prepareMapExpanded = true; Render(); }));

        var ready = Section(split, 1.15f); var manifest = Scroll(ready);
        manifest.AddChild(Text("带上装备，准备出发", 20, Ink));
        manifest.AddChild(Text($"生命 {s.Health}/42 · 卡组 {Session.Deck().Count} 张\n" + string.Join(" · ", new[] { "body", "hands", "backpack" }.Select(slot => SlotName(slot) + "：" + (Session.Equipped.FirstOrDefault(id => ChapterContent.Gear[id].Slot == slot) is { } worn ? ChapterContent.ItemName(worn) : "空槽"))), 16, Ink));
        if (s.Extractions == 0)
        {
            manifest.AddChild(Text("选择一种起始工具。首趟可以避开战斗。", 16, Muted));
            var starters = Row(10); manifest.AddChild(starters);
            foreach (var id in new[] { "G01", "G02" })
            {
                var kind = id;
                var button = EquipmentTile("starter_" + id, id, id == "G01" ? "攻击 · 使用行动" : "导能 · 使用魔法", () => Change(() => Session.SelectStarter(kind)), s.Weapon == id);
                button.Disabled = !camp; starters.AddChild(button);
            }
            var starterCard = ChapterContent.Cards[Session.GearCards(s.Weapon).First()];
            manifest.AddChild(Text("代表卡 · " + starterCard.Name + $"  {starterCard.Action} 行动点 / {starterCard.Mana} 魔法\n" + CardCoreEffect(starterCard), 16, Ink));
        }
        else
        {
            manifest.AddChild(Text(string.Join(" · ", Session.Equipped.Select(ChapterContent.ItemName)), 18, Ink));
            manifest.AddChild(Text($"装备提供 {Session.Deck().Count} 张卡牌", 16, Muted));
        }
        var changes = Row(8); manifest.AddChild(changes);
        changes.AddChild(QuietButton("prepare_presets", "整备预设", () => OpenNested("presets")));
        changes.AddChild(Button("prepare_equipment", "调整装备", () => { _gearFilter = "all"; OpenNested("gear"); }));
        changes.AddChild(Button("prepare_storage", "整理背包", () => { OpenNested("gear"); _gearFilter = "cargo"; Render(); }));
        changes.AddChild(QuietButton("nav_craft", "工坊", () => Toggle("craft")));
        Rule(manifest);
        var free = new CheckBox { Name = "free_supplies", Text = "领取免费补给 ×2", ButtonPressed = s.FreeSupplyAllowance == 2, Disabled = !camp, CustomMinimumSize = new Vector2(0, 44) };
        free.AddThemeFontSizeOverride("font_size", (int)(16 * ReadingScale));
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) free.AddThemeColorOverride(color, Ink);
        free.Toggled += on => Change(() => Session.SetFreeSupplies(on ? 2 : 0)); manifest.AddChild(free); Buttons["free_supplies"] = free;
        manifest.AddChild(Text("占用 1 格，探索中可恢复生命；本趟结束后清除。", 16, Muted));
        var load = Session.DepartureLoad();
        var metrics = Row(26); manifest.AddChild(metrics);
        Metric(metrics, "出发容量", CapacityText(load.Slots, load.Capacity), load.Slots > load.Capacity ? Danger : Ink);
        Metric(metrics, "出发负重", $"{load.Grams / 1000m:0.#} / 20 kg", load.Grams > ChapterSession.WeightLimitGrams ? Danger : Ink);
        if (load.Grams > ChapterSession.WeightLimitGrams) manifest.AddChild(Text("负重超限：探索行动消耗翻倍。", 16, Danger));
        var advanced = ToolsSection(manifest, "prepare", "随身清单、任务与仓储明细");
        LastPreparationSummary(advanced); PreparationTaskManifest(advanced);
        foreach (var item in Session.CarriedItems) advanced.AddChild(Text(ChapterContent.ItemName(item.Kind) + " ×" + item.Quantity, 16, Ink));
        if (s.StoredItems.Contains("T02") && !s.StoredCalibration) advanced.AddChild(Button("carry_T02", "携带工坊夹具", () => Change(() => Session.ToggleCarryItem("T02")), camp));
        var details = Text($"生命 {s.Health}/42 · 配发 {s.FreeSupplyAllowance} 份 · 仓库预留 {load.Reserved} 格", 16, Muted); details.Name = "DepartureManifest"; advanced.AddChild(details);
        if (!Session.CanDepart)
        {
            ready.AddChild(Text(!camp ? "返回余火站后出发。" : load.Slots > load.Capacity ? $"背包超出 {load.Slots - load.Capacity} 格，请先整理。" : $"仓库需要预留 {load.Reserved} 格，请先腾出空间。", 16, Danger));
        }
        ready.AddChild(Button("prepare_depart", "出发 · " + ChapterContent.Places[s.Start].Name + "  →", () => { _panel = ""; _panelHistory.Clear(); Change(Session.Depart); }, camp && Session.CanDepart, true));
    }

    private void PreparationRoute(VBoxContainer box)
    {
        box.AddChild(Button("prepare_map", UiCopy.Text("UI_8EA5B70593B8"), () => { _prepareMapExpanded = false; Render(); }));
        box.AddChild(Text(Session.NextObjective, 16, Ink));
        var split = Row(14); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var map = new ChapterRouteMap { Name = "DeploymentMap", Selected = Session.State.Start, Deployment = true, ReadingScale = ReadingScale,
            Revealed = ChapterContent.Places.Keys.Where(Session.IsRevealed).ToHashSet(), StationPowered = Session.State.World.Contains("station"),
            Objective = Session.QuestPlace(Session.ObjectiveQuest), Route = Session.KnownRoute(Session.QuestPlace(Session.ObjectiveQuest)),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddChild(map);
        var side = Directory(split); var page = Scroll(side);
        page.AddChild(Text(UiCopy.Text("UI_7DFFCE28F99C") + ChapterContent.Places[Session.State.Start].Name, 20, Ink));
        foreach (var id in new[] { "A01", "A05" })
            page.AddChild(Button("start_" + id, (Session.State.Start == id ? "◆ " : "") + ChapterContent.Places[id].Name, () => Change(() => Session.SetStart(id)), Session.State.Phase == ChapterPhase.Camp && (id == "A01" || Session.State.Done.Contains("S01"))));
        var cost = Session.RouteCost(map.Route, true);
        page.AddChild(Text(map.Route.Count == 0 ? UiCopy.Text("UI_DD0A4346F79D") : UiCopy.Format("UI_0821CD915E3E", cost.Steps, cost.Pressure), 16, Ink));
        page.AddChild(Text(UiCopy.Text("UI_4E490D238C40"), 16, Muted));
        map.PlaceSelected += id => { if (Session.State.Phase == ChapterPhase.Camp && (id == "A01" || id == "A05" && Session.State.Done.Contains("S01"))) Change(() => Session.SetStart(id)); };
    }


    private void CharacterOverview(VBoxContainer box)
    {
        var s = Session.State; var battle = s.Battle;
        var equipment = (battle?.Equipment ?? Session.Equipped.ToList()).ToArray();
        var cards = battle == null ? Session.Deck() : battle.Hand.Concat(battle.Draw).Concat(battle.Discard).ToList();
        var split = Row(20); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var wear = Directory(split); var board = Scroll(wear);
        var hp = Text($"生命  {s.Health} / {ChapterSession.MaxHealth}", 20, Ink); hp.Name = "OverviewHealth"; board.AddChild(hp);
        HealthBar(board, s.Health, ChapterSession.MaxHealth, Success);
        board.AddChild(Text("穿戴装备 → 获得对应卡牌", 16, Accent));
        EquipmentBoard(board, equipment, "overview", true);
        board.AddChild(Text(Session.LoadDescription, 16, Muted));
        if (s.Resonance != "") board.AddChild(Text(s.Resonance == "R01" ? "余烬护层：首回合 +5 护盾" : "厚壁余温：每回合首次获得至少 7 护盾时额外 +2", 16, Muted));
        wear.AddChild(Button("overview_inventory", "打开背包", () => { OpenNested("gear"); _gearFilter = "cargo"; Render(); }));
        var deck = Section(split);
        var title = Text($"当前卡组 · {cards.Count} 张", 20, Ink); title.Name = "OverviewDeckCount"; deck.AddChild(title);
        deck.AddChild(Text(battle?.Id == "trial" ? "当前为台架借用装备，结束试用后恢复。" : "每张牌下方显示装备来源；更换装备会同步改变卡组。", 16, Muted));
        Tabs(deck, "deck_filter_", [("all", "全部"), ("attack", "攻击"), ("guard", "防护"), ("draw", "抽牌")], _deckFilter, value => _deckFilter = value);
        var analysis = ToolsSection(deck, "deck", "卡组分析、来源筛选与排序");
        CostDistribution(analysis, cards);
        var tools = new HFlowContainer(); tools.AddThemeConstantOverride("h_separation", 8); analysis.AddChild(tools);
        var sourceFilter = new OptionButton { Name = "DeckSourceFilter", CustomMinimumSize = new Vector2(180, 40) }; sourceFilter.AddItem("全部装备来源");
        foreach (var source in equipment) sourceFilter.AddItem(ChapterContent.ItemName(source));
        sourceFilter.Selected = Array.IndexOf(equipment, _deckSource) + 1;
        sourceFilter.ItemSelected += index => { _deckSource = index == 0 ? "" : equipment[index - 1]; Render(); }; tools.AddChild(sourceFilter);
        var sortKeys = new[] { "cost", "function", "source", "name" };
        tools.AddChild(Button("deck_sort", "排序：" + (_deckSort switch { "function" => "功能", "source" => "装备来源", "name" => "名称", _ => "费用" }), () => { _deckSort = sortKeys[(Array.IndexOf(sortKeys, _deckSort) + 1) % sortKeys.Length]; Render(); }));
        tools.AddChild(Button("deck_sources", _deckMatrix ? "返回卡牌清单" : "来源矩阵", () => { _deckMatrix = !_deckMatrix; Render(); }));
        if (_deckSource != "") deck.AddChild(QuietButton("deck_clear_source", "仅看 " + ChapterContent.ItemName(_deckSource) + " · 清除筛选", () => { _deckSource = ""; Render(); }));
        var list = Scroll(deck); list.Name = "OverviewDeck";
        var groups = cards.GroupBy(c => c).Where(g => _deckFilter switch { "attack" => ChapterContent.Cards[g.Key].Damage > 0, "guard" => ChapterContent.Cards[g.Key].Shield > 0, "draw" => g.Key is "C26" or "C36", _ => true });
        if (_deckSource != "" && equipment.Contains(_deckSource)) groups = groups.Where(g => (battle?.Id == "trial" ? ChapterContent.Gear[_deckSource].Cards : Session.GearCards(_deckSource)).Contains(g.Key));
        if (_deckMatrix) { DeckSourceMatrix(list, equipment, groups, battle?.Id == "trial"); return; }
        string SourceKey(string card) => string.Join("/", equipment.Where(id => (battle?.Id == "trial" ? ChapterContent.Gear[id].Cards : Session.GearCards(id)).Contains(card)).Select(ChapterContent.ItemName).Order());
        foreach (var group in groups.OrderBy(g => _deckSort == "cost" ? ChapterContent.Cards[g.Key].Action * 10 + ChapterContent.Cards[g.Key].Mana : _deckSort == "function" ? g.Key is "C26" or "C36" ? 0 : ChapterContent.Cards[g.Key].Damage > 0 ? 1 : 2 : 0).ThenBy(g => _deckSort == "source" ? SourceKey(g.Key) : "").ThenBy(g => ChapterContent.Cards[g.Key].Name))
        {
            var card = ChapterContent.Cards[group.Key]; var row = Row(14); list.AddChild(row);
            if (!CompactDensity)
            {
                var art = ActionArt(card); art.CustomMinimumSize = new Vector2(94, 94); row.AddChild(art);
            }
            var description = FlexibleColumn(row);
            var sources = string.Join("、", equipment.Select(id => (Id: id, Count: (battle?.Id == "trial" ? ChapterContent.Gear[id].Cards : Session.GearCards(id)).Count(c => c == card.Id))).Where(p => p.Count > 0).Select(p => ChapterContent.ItemName(p.Id) + " ×" + p.Count));
            var line = Row(10); description.AddChild(line);
            var button = QuietButton("overview_card_" + card.Id, card.Name + $" ×{group.Count()}", () => { _selectedOverviewCard = _selectedOverviewCard == card.Id ? "" : card.Id; Render(); });
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.Alignment = HorizontalAlignment.Left;
            button.SetMeta("copies", group.Count()); button.TooltipText = card.Name + "\n" + card.Text + "\n来自：" + sources; line.AddChild(button);
            var cost = Inline($"{card.Action} 行动 / {card.Mana} 魔法", 14, Muted); cost.SizeFlagsVertical = SizeFlags.ShrinkCenter; line.AddChild(cost);
            description.AddChild(Text(CardCoreEffect(card), 16, Ink));
            description.AddChild(Text("来自 " + sources, 14, Muted));
            if (_selectedOverviewCard == card.Id)
            {
                description.AddChild(Text(card.Text, 16, Ink));
                description.AddChild(QuietButton("overview_detail", "完整规则", () => { _detailItemId = 0; _detailTitle = card.Name; _detailText = CardRules(card.Id); OpenNested("detail"); }));
            }
            Rule(list);
        }
    }

    private void Debrief()
    {
        var s = Session.State; var ok = s.Phase == ChapterPhase.Extracted;
        var body = Overlay(ok ? "撤离成功 · 本趟收获" : "紧急回收 · 本趟损失"); Buttons["close_panel"].Visible = false;
        var page = Scroll(body); var report = s.LastReport;
        if (report == null) page.AddChild(Text(s.Summary, 18, Ink));
        else
        {
            void Items(string title, Dictionary<string, int> items, Color color)
            {
                page.AddChild(Text(title, 20, color));
                if (items.Count == 0) page.AddChild(Text(UiCopy.Text("UI_484D55613910"), 16, Muted));
                foreach (var (kind, count) in items)
                {
                    var reward = Row(14); page.AddChild(reward); ItemVisual(reward, kind, 64);
                    var rewardName = Text(ChapterContent.ItemName(kind) + " ×" + count, 20, Ink);
                    rewardName.Name = "ReportItem_" + kind; rewardName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    rewardName.SizeFlagsVertical = SizeFlags.ShrinkCenter; reward.AddChild(rewardName);
                }
            }
            if (ok)
            {
                if (report.BaselineKnown) Items("本趟净增加", report.Gained, Success);
                else page.AddChild(Text(UiCopy.Text("UI_81CB617E52EE"),16,Muted));
                var brought = Column(6); brought.Visible = false; page.AddChild(Button("report_brought", UiCopy.Text("UI_52F64E2A95D8"), () => brought.Visible = !brought.Visible)); page.AddChild(brought);
                foreach (var (kind, count) in report.Brought) brought.AddChild(Text(ChapterContent.ItemName(kind) + " ×" + count, 16, Ink));
                if (report.NewGear.Count > 0) page.AddChild(Text(UiCopy.Text("UI_8FA09105FC9E") + string.Join("、",report.NewGear.Select(ChapterContent.ItemName)),16,Success));
                var ready = Session.PreviewCopy(); ready.ReturnToCamp();
                page.AddChild(Text(UiCopy.Text("UI_278B2BB4014E"), 20, Ink));
                var quests = ChapterContent.Quests.Where(q => ready.CanTurnIn(q.Id)).ToArray();
                page.AddChild(Text(quests.Length == 0 ? UiCopy.Text("UI_5DDF104D1DF6") : string.Join("、", quests.Select(q => q.Name)), 16, Accent));
            }
            else
            {
                page.AddChild(Text(UiCopy.Text("UI_5ECDC0EC7AB6"), 20, Danger));
                if (_lastFeedback.Count > 0) page.AddChild(Text(UiCopy.Text("UI_ABDB062FBB1C") + string.Join(" · ", _lastFeedback.TakeLast(2)), 16, Muted));
                Items("实际丢失", report.Lost, Danger);
                page.AddChild(Text(UiCopy.Text("UI_E309F2263E75"), 20, Success));
                page.AddChild(Text(UiCopy.Text("UI_0154D5EE94F8") + string.Join("、", report.Preserved), 16, Ink));
                page.AddChild(Text(UiCopy.Text("UI_2620CD30991F"), 16, Muted));
            }
            if (report.Changes.Count > 0)
            {
                page.AddChild(Text(UiCopy.Text("UI_E48EB232BAA9"), 20, Ink));
                foreach (var change in report.Changes) page.AddChild(Text(WorldChange(change), 16, Success));
            }
            if (report.Left.Count > 0)
            { page.AddChild(Text(UiCopy.Text("UI_62973FA95193"), 20, Muted)); foreach (var left in report.Left) page.AddChild(Text(left, 16, Muted)); }
        }
        page.AddChild(Text(UiCopy.Text("UI_9163A1CB0C4F"),14,Muted));
        Rule(page); page.AddChild(Text(UiCopy.Format("UI_29390557D71B", Session.WarehouseUsed, Session.WarehouseCapacity, Session.CarryUsed, Session.CarryCapacity), 14, Muted));
        var returning = Session.Preview(s => s.ReturnToCamp());
        var canDeliver = returning != null && ChapterContent.Quests.Any(q => returning.CanTurnIn(q.Id));
        if (canDeliver) body.AddChild(Button("report_quests", "返回据点 · 交付任务 →", () => { Change(Session.ReturnToCamp); if (Session.State.Phase == ChapterPhase.Camp) { _questFilter = "active"; Toggle("journal"); } }, primary: true));
        body.AddChild(Button("camp", "返回余火站", () => Change(Session.ReturnToCamp), primary: !canDeliver)); FocusLater(Buttons[canDeliver ? "report_quests" : "camp"]);
    }

    private static string WorldChange(string key) => key switch
    { "robot-rescued" => "维修偶已获救", "robot-arrived" => "维修偶抵达余火站", "station" => "货站线路永久修复", "dark-door" => "发现荧光夹层入口", "shovel-inspected" => "发现清障铲与货单", "leader-won" => "军需领班首胜已记录", "trial-complete" => "检修手套试用完成", _ => "新的区域发现已保存" };
}
