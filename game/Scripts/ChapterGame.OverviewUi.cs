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
        var page = Scroll(box); page.AddChild(Text(UiCopy.Text("UI_9C3B6012486E"), 18, Ink));
        var items = Session.State.Stock.Where(i => Session.State.Ground.TryGetValue(i.Container, out var ground) && ground.Place == Session.State.Location).ToArray();
        foreach (var group in items.GroupBy(i => _recentLoot.Contains(i.Id)).OrderByDescending(g => g.Key))
        {
            page.AddChild(Text(group.Key ? UiCopy.Text("UI_2A6C929F7701") : UiCopy.Text("UI_00AE799F3AB4"), 20, Ink));
            foreach (var item in group) page.AddChild(Text(ChapterContent.ItemName(item.Kind) + " ×" + item.Quantity + " · " + Session.State.Ground[item.Container].Name, 16, Ink));
        }
        foreach (var pair in Session.GroundHere.ToArray())
        {
            var key = pair.Key; var next = Session.Preview(s => s.PickupAll(key));
            page.AddChild(Button("loot_take_" + key.Replace(':', '_'), UiCopy.Text("UI_12C024A193B4") + pair.Value.Name + (pair.Value.FreePickup ? UiCopy.Text("UI_705FC6E91775") : UiCopy.Format("UI_D266454ACC02", Session.SceneActionCost())), () => Change(() => Session.PickupAll(key)), next != null, true));
            if (next?.State.Phase == ChapterPhase.Battle) page.AddChild(Text(UiCopy.Text("UI_D4799862BA2B"), 16, Danger));
        }
        if (items.Length == 0) page.AddChild(Text(UiCopy.Text("UI_FD82A3676DBB"), 18, Success));
        else page.AddChild(Text(UiCopy.Text("UI_4F6B5AFB275A"), 16, Muted));
        page.AddChild(Text(Session.LoadDescription, 16, Muted));
        box.AddChild(Button("loot_manage", UiCopy.Text("UI_23BCA41DE06A"), () => { _panel = "gear"; _gearFilter = "cargo"; Render(); }, primary: true));
        box.AddChild(Button("loot_return", UiCopy.Text("UI_A8AB98831F5C"), ClosePanel));
    }

    private void Preparation(VBoxContainer box)
    {
        var s = Session.State; var camp = s.Phase == ChapterPhase.Camp;
        if (_prepareMapExpanded) { PreparationRoute(box); return; }
        var split = Row(12); split.Name = "PreparationWorkspace"; split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        LoadoutColumn(split);
        var route = Section(split, 1.2f);
        route.AddChild(Text(UiCopy.Text("UI_A3487362C507"), 20, Ink));
        route.AddChild(Button("prepare_map", _prepareMapExpanded ? UiCopy.Text("UI_E9478C9F038B") : UiCopy.Text("UI_11D640133504"), () => { _prepareMapExpanded = !_prepareMapExpanded; Render(); }));
        route.AddChild(Text(UiCopy.Text("UI_50C50999461F"), 14, Muted));
        route.AddChild(Text(Session.NextObjective, 16, Ink));
        route.AddChild(Button("prepare_presets", Session.State.LastPreparation == null ? UiCopy.Text("UI_E7138FEBD5FD") : UiCopy.Text("UI_59187622893F"), () => OpenNested("presets")));
        foreach (var id in new[] { "A01", "A05" })
        {
            var place = id;
            route.AddChild(Button("start_" + id, (s.Start == id ? UiCopy.Text("UI_1AFA2E65E8D5") : "") + ChapterContent.Places[id].Name + (id == "A05" && !s.Done.Contains("S01") ? UiCopy.Text("UI_6AD6E33D33A2") : ""),
                () => Change(() => Session.SetStart(place)), camp && (id == "A01" || s.Done.Contains("S01"))));
        }
        var right = Section(split); var manifest = Scroll(right);
        manifest.AddChild(Text(UiCopy.Text("UI_E432AF181280"), 20, Ink));
        LastPreparationSummary(manifest);
        PreparationTaskManifest(manifest);
        if (s.Extractions == 0)
        {
            manifest.AddChild(Text(UiCopy.Text("UI_B48A85B1C2E0"), 16, Ink));
            foreach (var id in new[] { "G01", "G02" })
            {
                var kind = id;
                manifest.AddChild(Button("starter_" + id, (s.Weapon == id ? UiCopy.Text("UI_1AFA2E65E8D5") : "") + ChapterContent.ItemName(id), () => Change(() => Session.SelectStarter(kind)), camp));
                var example = ChapterContent.Cards[ChapterContent.Gear[id].Cards[0]];
                manifest.AddChild(Text(ChapterContent.Gear[id].Hint + UiCopy.Format("UI_30829EE49194", example.Name, example.Action, example.Mana, example.Text), 16, Muted));
            }
            manifest.AddChild(Text(UiCopy.Text("UI_5155189C64C2"), 16, Muted));
        }
        var free = new CheckBox { Name = "free_supplies", Text = UiCopy.Text("UI_769AD1429B30"), ButtonPressed = s.FreeSupplyAllowance == 2, Disabled = !camp };
        free.AddThemeFontSizeOverride("font_size", (int)(16 * ReadingScale));
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) free.AddThemeColorOverride(color, Ink);
        free.Toggled += on => Change(() => Session.SetFreeSupplies(on ? 2 : 0)); manifest.AddChild(free); Buttons["free_supplies"] = free;
        manifest.AddChild(Text(UiCopy.Text("UI_CCF39CA33921"), 14, Muted));
        foreach (var item in Session.CarriedItems) manifest.AddChild(Text(ChapterContent.ItemName(item.Kind) + " ×" + item.Quantity, 16, Ink));
        if (s.StoredItems.Contains("T02") && !s.StoredCalibration) manifest.AddChild(Button("carry_T02", UiCopy.Text("UI_9FB6B2D02BC4"), () => Change(() => Session.ToggleCarryItem("T02")), camp));
        var load = Session.DepartureLoad();
        Rule(right);
        right.AddChild(Text(UiCopy.Format("UI_61F183655F85", Math.Max(0, load.Capacity - load.Slots), load.Slots, load.Capacity), 20, Ink));
        right.AddChild(Text(UiCopy.Format("UI_3B67A2C20355", Session.CarriedGrams / 1000m, load.Grams / 1000m), 20, load.Grams > ChapterSession.WeightLimitGrams ? Danger : Ink));
        var finalState = Text(UiCopy.Format("UI_126A14DFC5BF", s.FreeSupplyAllowance, load.Reserved), 16, Muted); finalState.Name = "DepartureManifest"; right.AddChild(finalState);
        if (load.Grams > ChapterSession.WeightLimitGrams) right.AddChild(Text(UiCopy.Text("UI_71392C7F540A"), 16, Danger));
        right.AddChild(Button("prepare_equipment", UiCopy.Text("UI_E5A826863B87"), () => { _gearFilter = "all"; OpenNested("gear"); }));
        right.AddChild(Button("prepare_storage", UiCopy.Text("UI_D39C3868F6FD"), () => { OpenNested("gear"); _gearFilter = "cargo"; Render(); }));
        if (!Session.CanDepart && camp)
        {
            foreach(var (container,shortage,items) in new[]{("carry",Math.Max(0,load.Slots-load.Capacity),Session.CarriedItems),("warehouse",Math.Max(0,Session.WarehouseUsed+load.Reserved-Session.WarehouseCapacity),Session.StoredStock)})
            {
                if(shortage==0)continue;var culprit=items.OrderByDescending(i=>ChapterContent.Items[i.Kind].Slots*i.Quantity).FirstOrDefault();
                if(culprit!=null)manifest.AddChild(Button("prepare_blocker_"+container,ContainerName(container)+UiCopy.Text("UI_8C78227ABA0B")+shortage+UiCopy.Text("UI_A1A88CD59435"),()=>OpenStockObject(culprit.Id)));
            }
        }
        if (!Session.CanDepart) right.AddChild(Text(camp ? load.Slots > load.Capacity ? UiCopy.Format("UI_E6DCD225F717", load.Slots - load.Capacity) : UiCopy.Format("UI_B539D6B5BBDE", load.Reserved, Session.WarehouseCapacity - Session.WarehouseUsed) : UiCopy.Text("UI_2B84B0F842EC"), 16, Danger));
        right.AddChild(Button("prepare_depart", UiCopy.Text("UI_144E31E3C50C") + ChapterContent.Places[s.Start].Name + " →", () => { _panel = ""; _panelHistory.Clear(); Change(Session.Depart); }, camp && Session.CanDepart, true));
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
        var summary = Row(20); box.AddChild(summary);
        var hp = Inline($"{s.Health} / {ChapterSession.MaxHealth}", 28, Ink); hp.Name = "OverviewHealth"; summary.AddChild(hp);
        summary.AddChild(Inline(UiCopy.Text("UI_45BC77BC1C58"), 16, Muted));
        summary.AddChild(Inline(UiCopy.Format("UI_45D55566C47D", battle?.Action ?? 3, battle?.Mana ?? 3, battle?.Shield ?? 0), 16, Ink));
        box.AddChild(Text((battle?.Id == "trial" ? UiCopy.Text("UI_1ACBFF88D150") : UiCopy.Text("UI_8036F7C90D5B")) + UiCopy.Format("UI_650CE0C73167", cards.Count, cards.Count(id => ChapterContent.Cards[id].Damage > 0), cards.Count(id => ChapterContent.Cards[id].Shield > 0), cards.Count(id => id is "C26" or "C36")), 16, Ink));
        var split = Row(16); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var wear = Directory(split); var board = Scroll(wear); EquipmentBoard(board, equipment, "overview", true);
        board.AddChild(Text(Session.LoadDescription, 16, Ink));
        board.AddChild(Text(s.Resonance switch { "R01" => "余烬护层：首回合 +5 护盾", "R07" => "厚壁余温：每回合首次获得至少 7 护盾时额外 +2", _ => "无临时共鸣" }, 16, Muted));
        wear.AddChild(Button("overview_inventory", UiCopy.Text("UI_0B9B670A717D"), () => { OpenNested("gear"); _gearFilter = "cargo"; Render(); }));
        var deck = Section(split); var title = Text(UiCopy.Format("UI_FDF5FE3921B9", cards.Count), 20, Ink); title.Name = "OverviewDeckCount"; deck.AddChild(title);
        deck.AddChild(Text(battle?.Id == "trial" ? UiCopy.Text("UI_775EAC4A49CA") : battle != null ? UiCopy.Text("UI_FD4EAEDC73B9") : UiCopy.Text("UI_E37F4D078556"), 14, Muted));
        CostDistribution(deck, cards);
        Tabs(deck, "deck_filter_", [("all", "全部"), ("attack", "攻击"), ("guard", "防护"), ("draw", "抽牌")], _deckFilter, value => _deckFilter = value);
        var tools = new HFlowContainer(); tools.AddThemeConstantOverride("h_separation", 8); deck.AddChild(tools);
        var sourceFilter = new OptionButton { Name = "DeckSourceFilter", CustomMinimumSize = new Vector2(180, 40) }; sourceFilter.AddItem("全部装备来源");
        foreach (var source in equipment) sourceFilter.AddItem(ChapterContent.ItemName(source));
        sourceFilter.Selected = Array.IndexOf(equipment, _deckSource) + 1;
        sourceFilter.ItemSelected += index => { _deckSource = index == 0 ? "" : equipment[index - 1]; Render(); }; tools.AddChild(sourceFilter);
        var sortKeys=new[]{"cost","function","source","name"};
        tools.AddChild(Button("deck_sort", UiCopy.Text("UI_DE3C35C2A69A")+(_deckSort switch {"function"=>"功能","source"=>"装备来源","name"=>"名称",_=>"行动 / 魔法费用"}), () => { _deckSort=sortKeys[(Array.IndexOf(sortKeys,_deckSort)+1)%sortKeys.Length]; Render(); }));
        tools.AddChild(Button("deck_sources", _deckMatrix ? UiCopy.Text("UI_1CB44F18C1F2") : UiCopy.Text("UI_BAC0D7C71275"), () => { _deckMatrix = !_deckMatrix; Render(); }));
        var list = Scroll(deck); list.Name = "OverviewDeck";
        var groups = cards.GroupBy(c => c).Where(g => _deckFilter switch { "attack" => ChapterContent.Cards[g.Key].Damage > 0, "guard" => ChapterContent.Cards[g.Key].Shield > 0, "draw" => g.Key is "C26" or "C36", _ => true });
        if (_deckSource != "" && equipment.Contains(_deckSource)) groups = groups.Where(g => (battle?.Id == "trial" ? ChapterContent.Gear[_deckSource].Cards : Session.GearCards(_deckSource)).Contains(g.Key));
        if (_deckMatrix) { DeckSourceMatrix(list, equipment, groups, battle?.Id == "trial"); return; }
        string SourceKey(string card)=>string.Join("/",equipment.Where(id=>(battle?.Id=="trial"?ChapterContent.Gear[id].Cards:Session.GearCards(id)).Contains(card)).Select(ChapterContent.ItemName).Order());
        foreach (var group in groups.OrderBy(g => _deckSort == "cost" ? ChapterContent.Cards[g.Key].Action * 10 + ChapterContent.Cards[g.Key].Mana : _deckSort=="function"?g.Key is "C26" or "C36"?0:ChapterContent.Cards[g.Key].Damage>0?1:2:0).ThenBy(g=>_deckSort=="source"?SourceKey(g.Key):"").ThenBy(g => ChapterContent.Cards[g.Key].Name))
        {
            var card = ChapterContent.Cards[group.Key]; var row = Column(4); list.AddChild(row);
            var sources = string.Join("、", equipment.Select(id => (Id: id, Count: (battle?.Id == "trial" ? ChapterContent.Gear[id].Cards : Session.GearCards(id)).Count(c => c == card.Id))).Where(p => p.Count > 0).Select(p => ChapterContent.ItemName(p.Id) + " ×" + p.Count));
            var line = Row(10); row.AddChild(line);
            var button = Button("overview_card_" + card.Id, (_selectedOverviewCard == card.Id ? "◆ " : "") + card.Name + $" ×{group.Count()}", () => { _selectedOverviewCard = card.Id; Render(); });
            button.CustomMinimumSize = new Vector2(200, 40);
            button.SetMeta("copies", group.Count()); button.Alignment = HorizontalAlignment.Left;
            button.TooltipText = card.Name + "\n" + card.Text + UiCopy.Text("UI_0C72AA33E7B3") + sources; line.AddChild(button);
            var cost = Text($"{card.Action} 行动\n{card.Mana} 魔法", 16, Ink); cost.CustomMinimumSize = new Vector2(90, 0); cost.SizeFlagsVertical = SizeFlags.ShrinkCenter; line.AddChild(cost);
            var core = Text(CardCoreEffect(card), 16, Ink); core.SizeFlagsHorizontal = SizeFlags.ExpandFill; core.SizeFlagsVertical = SizeFlags.ShrinkCenter; line.AddChild(core);
            if (_selectedOverviewCard != card.Id) continue;
            var effect = Row(8); row.AddChild(effect); effect.AddChild(Glyph(CardSymbol(card), Ink, 24));
            var copy = Text(card.Text, 16, Ink); copy.SizeFlagsHorizontal = SizeFlags.ExpandFill; effect.AddChild(copy);
            row.AddChild(Text(UiCopy.Text("UI_3C7FDE7B25EE") + sources, 14, Muted)); Rule(row);
            row.AddChild(Button("overview_detail", UiCopy.Text("UI_06BF8093448E"), () => { _detailItemId = 0; _detailTitle = card.Name; _detailText = CardRules(card.Id); OpenNested("detail"); }));
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
                foreach (var (kind, count) in items) page.AddChild(Text(ChapterContent.ItemName(kind) + " ×" + count, 16, Ink));
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
        body.AddChild(Button("camp", UiCopy.Text("UI_1658F952FA07"), () => Change(Session.ReturnToCamp), primary: true)); FocusLater(Buttons["camp"]);
        var returning = Session.Preview(s => s.ReturnToCamp());
        if (returning != null && ChapterContent.Quests.Any(q => returning.CanTurnIn(q.Id))) body.AddChild(Button("report_quests", UiCopy.Text("UI_D489913ABD56"), () => { Change(Session.ReturnToCamp); if (Session.State.Phase == ChapterPhase.Camp) { _questFilter = "active"; Toggle("journal"); } }));
    }

    private static string WorldChange(string key) => key switch
    { "robot-rescued" => "维修偶已获救", "robot-arrived" => "维修偶抵达余火站", "station" => "货站线路永久修复", "dark-door" => "发现荧光夹层入口", "shovel-inspected" => "发现清障铲与货单", "leader-won" => "军需领班首胜已记录", "trial-complete" => "检修手套试用完成", _ => "新的区域发现已保存" };
}
