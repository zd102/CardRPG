using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private string _selectedRecipe = "G10", _selectedQuest = "Q01", _logFilter = "all", _mapPlace = "";
    private string _selectedModGear = "", _selectedMod = "";
    private string _lastEquipmentFilter = "all";
    private string _waypoint = "";
    private string _lastEquippedKind = "";
    private ulong _lastEquipmentChange;
    private string _pinnedGear = "", _lastEquipReceipt = "";
    private bool _expandCraftConditions;
    private string NavigationTarget => Session.IsRevealed(_waypoint) ? _waypoint : Session.QuestPlace(Session.ObjectiveQuest);

    private VBoxContainer Directory(Node parent)
    {
        var column = Column(8); column.CustomMinimumSize = new Vector2(Size.X < 1200 ? 220 : 260, 0);
        parent.AddChild(column); return column;
    }

    private void Inventory(VBoxContainer box)
    {
        var s = Session.State;
        Tabs(box, "gear_mode_", [("cargo", "物资管理"), ("equipment", "装备比较")], _gearFilter == "cargo" ? "cargo" : "equipment", key => { if (_gearFilter != "cargo") _lastEquipmentFilter = _gearFilter; _gearFilter = key == "cargo" ? "cargo" : _lastEquipmentFilter; });
        if (_gearFilter == "cargo") { PhysicalInventory(box); return; }
        Tabs(box, "gear_tab_", [("all", "全部部位"), ("weapon", "武器"), ("body", "身体"), ("hands", "手部"), ("backpack", "背包")], _gearFilter, key => _gearFilter = _lastEquipmentFilter = key);
        var split = Row(12); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        LoadoutColumn(split);
        var choices = Section(split, .8f); choices.AddChild(Text(UiCopy.Text("UI_750E7C92F33F"), 20, Ink)); var shelf = Scroll(choices, "equipment_directory", _gearFilter);
        var ids = s.Owned.Concat(s.Loot).Distinct().Order().Where(id => _gearFilter == "all" || ChapterContent.Gear[id].Slot == _gearFilter).ToArray();
        if (!ids.Contains(_selectedGear)) _selectedGear = ids.FirstOrDefault() ?? "";
        if (_pinnedGear != "" && _selectedGear != "" && ChapterContent.Gear[_pinnedGear].Slot != ChapterContent.Gear[_selectedGear].Slot) _pinnedGear = "";
        foreach (var id in ids)
        {
            var kind = id; var worn = Session.Equipped.Contains(id);
            var physical = s.Stock.FirstOrDefault(i => i.Kind == kind);
            var source = worn ? "已穿戴" : physical == null ? "无实物" : ContainerName(physical.Container) + (physical.Registered ? " · 已登记" : " · 未登记");
            var button = EquipmentTile("inspect_" + id, kind, source, () => { _selectedGear = kind; Render(); }, _selectedGear == id);
            shelf.AddChild(button);
        }
        choices.AddChild(Button("gear_overview", UiCopy.Text("UI_6BF897D22B18"), () => OpenNested("character")));
        var details = Section(split, 1.3f); details.AddChild(Text(_selectedGear == "" ? UiCopy.Text("UI_0C680DD5F980") : ChapterContent.ItemName(_selectedGear), 20, Ink));
        if (_selectedGear == "") return;
        var selected = _selectedGear; var gear = ChapterContent.Gear[selected];
        var equippedKind = Session.Equipped.FirstOrDefault(g => ChapterContent.Gear[g].Slot == gear.Slot);
        details.AddChild(Text(UiCopy.Text("UI_B841058CF971") + (equippedKind == null ? UiCopy.Text("UI_7EEA754DDA60") : ChapterContent.ItemName(equippedKind)) + UiCopy.Text("UI_A08355D8D2D9") + ChapterContent.ItemName(selected), 16, Ink));
        details.AddChild(Button("equipment_pin", _pinnedGear == "" ? UiCopy.Text("UI_EA56569BE18F") : UiCopy.Text("UI_459EBEFF1562") + ChapterContent.ItemName(_pinnedGear), () => { _pinnedGear = _pinnedGear == "" ? selected : ""; Render(); }));
        if (_lastEquippedKind == selected && Time.GetTicksMsec() - _lastEquipmentChange < 12000) details.AddChild(Text(UiCopy.Text("UI_C3090C690995"), 16, Success));
        var detail = Scroll(details, "equipment_detail", selected); EquipmentComparison(detail, selected);
        var current = Session.Equipped.Contains(selected);
        var allowed = Session.CanEquipItem(selected);
        var preview = allowed ? Session.Preview(x => x.Equip(selected)) : null;
        details.AddChild(Text(preview == null ? Session.EquipBlocker(selected) : UiCopy.Text("UI_6E4CA00270FE") + preview.LoadDescription + UiCopy.Format("UI_09AA562C32EC", preview.Deck().Count), 16, allowed ? Ink : Muted));
        if (!allowed && !current) details.AddChild(Button("equip_resolve", s.Phase == ChapterPhase.Exploring && !Session.CanEquip ? UiCopy.Text("UI_0FCBBBB02D20") : UiCopy.Text("UI_F32349186F1D"), () =>
        {
            if (s.Phase == ChapterPhase.Exploring && !Session.CanEquip) { _mapPlace = "A04"; OpenNested("map"); }
            else { _gearFilter = "cargo"; _stockFilter = "all"; _stockSearch = ""; _selectedStock = s.Stock.FirstOrDefault(i => i.Kind == selected)?.Id ?? 0; Render(); }
        }));
        var actions = new HFlowContainer(); actions.AddThemeConstantOverride("h_separation", 8); details.AddChild(actions);
        actions.AddChild(Button("equip_" + selected, current ? gear.Slot is "hands" or "backpack" ? UiCopy.Text("UI_048E6CEA31C1") : UiCopy.Text("UI_AD93D8439182") : UiCopy.Text("UI_E7D9D62416F6"), () => ChangeEquipment(selected), allowed, !current));
        if (s.Phase == ChapterPhase.Camp && !current && s.Owned.Contains(selected))
            actions.AddChild(Button("backup_" + selected, s.Backups.Contains(selected) ? UiCopy.Text("UI_A69D8FC4D94B") : UiCopy.Text("UI_6A2A469A7E23"), () => Change(() => Session.ToggleBackup(selected)), s.Stock.Any(i => i.Kind == selected && Session.CanTransfer(i.Id, i.Container == "carry" ? "warehouse" : "carry", 1))));
    }

    private void ChangeEquipment(string kind)
    {
        var before = Session.ToJson(); var oldDeck = Session.Deck().GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var oldLoad = Session.LoadDescription; Change(() => Session.Equip(kind));
        if (_saveWarning == "" && Session.ToJson() != before)
        {
            _lastEquippedKind = kind; _lastEquipmentChange = Time.GetTicksMsec();
            if(_craftedResult==kind)_craftedResult="";
            var nextDeck = Session.Deck().GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            _lastEquipReceipt = "本次换装已完成\n" + oldLoad + " → " + Session.LoadDescription + "\n" + string.Join("；", oldDeck.Keys.Union(nextDeck.Keys).Where(id => oldDeck.GetValueOrDefault(id) != nextDeck.GetValueOrDefault(id)).Select(id => ChapterContent.Cards[id].Name + $" {oldDeck.GetValueOrDefault(id)} → {nextDeck.GetValueOrDefault(id)}")); Render();
        }
    }

    private void EquipmentComparison(VBoxContainer parent, string kind)
    {
        var gear = ChapterContent.Gear[kind];
        var old = Session.Equipped.FirstOrDefault(g => ChapterContent.Gear[g].Slot == gear.Slot);
        var pinned = _panel == "gear" && _pinnedGear != "" && _pinnedGear != kind && ChapterContent.Gear[_pinnedGear].Slot == gear.Slot;
        if (pinned) old = _pinnedGear;
        if (_lastEquippedKind == kind && Time.GetTicksMsec() - _lastEquipmentChange < 12000) parent.AddChild(Text(_lastEquipReceipt, 16, Success));
        var hero = Row(16); parent.AddChild(hero); Illustration(hero, ItemArt(kind), 76, 76, false);
        var words = FlexibleColumn(hero); words.AddChild(Text((pinned ? UiCopy.Text("UI_7E9C653B7403") : UiCopy.Text("UI_660648805666")) + (old == null ? UiCopy.Text("UI_7EEA754DDA60") : ChapterContent.ItemName(old)), 16, Muted));
        words.AddChild(Text(old != null && old != kind ? UiCopy.Format("UI_AD532FBBFC73", ChapterContent.Items[old].Grams / 1000m, ChapterContent.Items[kind].Grams / 1000m) : UiCopy.Format("UI_42439FB7CB6B", ChapterContent.Items[kind].Grams / 1000m), 16, Ink));
        if (gear.Slot == "backpack")
        {
            parent.AddChild(Text(UiCopy.Format("UI_1307211637F8", Session.CarryCapacity, 4 + ChapterContent.BagCapacity(kind), Session.CarryUsed), 20, Ink));
            parent.AddChild(Text(UiCopy.Text("UI_55D0D1E6AF2A"), 16, Muted)); return;
        }
        if (old != null && old != kind)
        {
            var from = Session.GearCards(old).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var to = Session.GearCards(kind).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            parent.AddChild(Text(UiCopy.Text("UI_FE25322E4DDA"), 20, Ink));
            int Count(System.Collections.Generic.Dictionary<string, int> cards, Func<ChapterCard, bool> match) => cards.Where(p => match(ChapterContent.Cards[p.Key])).Sum(p => p.Value);
            parent.AddChild(Text(UiCopy.Format("UI_74D529CBFE49", from.Values.Sum(), to.Values.Sum(), from.GetValueOrDefault("C18"), to.GetValueOrDefault("C18"), Count(from, c => c.Action == 0), Count(to, c => c.Action == 0)), 16, Ink));
            var diff = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; diff.AddThemeConstantOverride("h_separation", 10); parent.AddChild(diff);
            diff.AddChild(Text(UiCopy.Text("UI_BDE12DD10076"), 16, Muted)); diff.AddChild(Text(UiCopy.Text("UI_39646A3C59BB"), 16, Muted)); diff.AddChild(Text(UiCopy.Text("UI_14EFF928A8A3"), 16, Muted));
            foreach (var id in from.Keys.Union(to.Keys).Where(c => from.GetValueOrDefault(c) != to.GetValueOrDefault(c)))
            {
                var card = ChapterContent.Cards[id]; var delta = to.GetValueOrDefault(id) - from.GetValueOrDefault(id);
                diff.AddChild(Text(card.Name, 16, Ink));
                diff.AddChild(Text($"{from.GetValueOrDefault(id)} → {to.GetValueOrDefault(id)}" + (delta > 0 ? UiCopy.Text("UI_ED0E94549364") : UiCopy.Text("UI_6135D4159E89")), 16, delta > 0 ? Success : Danger));
                diff.AddChild(Text(UiCopy.Format("UI_285FE4CE7E60", card.Action, card.Mana), 14, Muted));
            }
            foreach (var cell in diff.GetChildren().OfType<Control>()) cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            Rule(parent);
        }
        parent.AddChild(Text(UiCopy.Text("UI_3A5C958469CF") + Session.GearCards(kind).Length + UiCopy.Text("UI_E9E0FDC242B4"), 20, Ink));
        SupplyCards(parent, kind); parent.AddChild(Text(gear.Hint, 16, Muted));
    }

    private void Costs(Node parent, int[] cost, bool emphasizeShortage=true)
    {
        var table=new GridContainer {Columns=4,SizeFlagsHorizontal=SizeFlags.ExpandFill};table.AddThemeConstantOverride("h_separation",12);table.AddThemeConstantOverride("v_separation",6);parent.AddChild(table);
        foreach(var heading in new[]{"材料 / 来源","拥有","消耗","剩余"})table.AddChild(Text(heading,14,Muted));
        foreach (var budget in Session.MaterialBudgetFor(cost))
        {
            var material=Button("material_info_"+budget.Kind,ChapterContent.ItemName(budget.Kind),()=>{_materialInfoKind=budget.Kind;OpenNested("material-info");});material.Alignment=HorizontalAlignment.Left;table.AddChild(material);
            material.TooltipText=UiCopy.Format("UI_DEB2C7B0F80F",budget.FromWarehouse,budget.FromCarry)+(budget.ProtectedConsumption>0?UiCopy.Format("UI_FBFE4C8E2B9E",budget.ProtectedConsumption):"");
            foreach(var value in new[]{budget.Warehouse+budget.Carry,budget.Required,budget.Remaining}) {var number=Text(value.ToString(),16,budget.Missing>0&&emphasizeShortage?Danger:Ink);number.HorizontalAlignment=HorizontalAlignment.Right;number.SizeFlagsHorizontal=SizeFlags.ExpandFill;number.AddThemeFontOverride("font",ChapterSkin.NumberFont);table.AddChild(number);}
            if(budget.Missing>0||budget.ProtectedConsumption>0)parent.AddChild(Text(ChapterContent.ItemName(budget.Kind)+(budget.Missing>0?UiCopy.Format("UI_DC9CA0D895FA",budget.Missing):"")+(budget.ProtectedConsumption>0?UiCopy.Format("UI_FBFE4C8E2B9E",budget.ProtectedConsumption):""),14,emphasizeShortage?Danger:Muted));
        }
    }

    private void Crafting(VBoxContainer box)
    {
        var s = Session.State;
        Tabs(box, "craft_tab_", [("recipes", "装备制作"), ("mods", "供牌改装"), ("storage", "背包与仓储")], _craftTab, key => _craftTab = key);
        if (_craftTab == "storage") { StorageCraftingWorkspace(box); return; }
        if (_craftTab == "mods") { ModificationsWorkspace(box); return; }
        var split = Row(16); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var directory = Directory(split); var list = Scroll(directory, "recipe_directory");
        foreach (var recipe in ChapterContent.Recipes.Values)
        {
            var kind = recipe.Gear;
            var status = Session.HasPhysical(kind) ? "已持有" : Session.CanCraft(kind) ? "可制作" : !Session.RecipeOpen(kind) ? "未解锁" : "缺材料 / 空间";
            list.AddChild(Button("recipe_" + kind, (_selectedRecipe == kind ? "◆ " : "") + ChapterContent.ItemName(kind) + "\n" + status, () => { _selectedRecipe = kind; Render(); }));
            BindObjectMenu(Buttons["recipe_"+kind],()=>RecipeMenu(kind));
        }
        directory.AddChild(Text(s.World.Contains("trial-complete") ? UiCopy.Text("UI_D6867C651AF7") : UiCopy.Text("UI_9268E2D31304"), 16, Muted));
        directory.AddChild(Button("trial", UiCopy.Text("UI_8CD95787F228"), () => { _panel = ""; _panelHistory.Clear(); Change(Session.StartTrial); }, s.Phase == ChapterPhase.Camp && s.Done.Contains("Q02")));
        directory.AddChild(Button("recipe_plan", UiCopy.Format("UI_C54E6452321A", s.RecipePlan.Count), () => OpenNested("plan")));
        var detail = Section(split); var kindId = _selectedRecipe;
        if (!ChapterContent.Recipes.ContainsKey(kindId)) kindId = _selectedRecipe = ChapterContent.Recipes.Keys.First();
        var owned = Session.HasPhysical(kindId);
        detail.AddChild(Text((owned ? UiCopy.Text("UI_CDA027749CBD") : UiCopy.Text("UI_B19E1EDDC2E8")) + ChapterContent.ItemName(kindId), 20, Ink));
        var recipeBody = Scroll(detail, "recipe_detail", kindId); EquipmentComparison(recipeBody, kindId);
        var selected = ChapterContent.Recipes[kindId];
        if (!Session.HasPhysical(kindId)) Costs(recipeBody, [selected.Steel, selected.Wire, selected.Spring],Session.RecipeOpen(kindId));
        else detail.AddChild(Text(UiCopy.Text("UI_646EB8EF8622") + string.Join("、", s.Stock.Where(i => i.Kind == kindId).Select(i => (i.Container is "carry" or "warehouse" ? ContainerName(i.Container) : ChapterContent.Gear[kindId].Slot == i.Container ? "当前穿戴" : "地面") + " ×" + i.Quantity)), 16, Success));
        var blockers = Session.CraftBlockers(kindId); var reason = blockers.FirstOrDefault() ?? "";
        detail.AddChild(Text(reason == "" ? UiCopy.Text("UI_21B85EF5DCD4") + Materials([selected.Steel, selected.Wire, selected.Spring]) + UiCopy.Text("UI_E4317BA26AEF") : reason, 16, owned ? Success : !Session.RecipeOpen(kindId) || reason == "" ? Muted : Danger));
        if (blockers.Length > 1) recipeBody.AddChild(Button("craft_conditions", _expandCraftConditions ? UiCopy.Text("UI_8959A40CE1B3") : UiCopy.Format("UI_1CA74F9B74A1", blockers.Length - 1), () => { _expandCraftConditions = !_expandCraftConditions; Render(); }));
        if (_expandCraftConditions) foreach (var blocker in blockers.Skip(1)) recipeBody.AddChild(Text(blocker, 16, Muted));
        if (Session.HasPhysical(kindId))
        {
            detail.AddChild(Button("crafted_view", UiCopy.Text("UI_B56343BE40C8"), () => { _selectedGear = kindId; _gearFilter = ChapterContent.Gear[kindId].Slot; OpenNested("gear"); }));
            recipeBody.AddChild(Button("owned_recipe_cost", _showOwnedRecipeCost ? UiCopy.Text("UI_1C47F09E3AB2") : UiCopy.Text("UI_65033023A183"), () => { _showOwnedRecipeCost = !_showOwnedRecipeCost; Render(); }));
            if (_showOwnedRecipeCost) Costs(recipeBody, [selected.Steel, selected.Wire, selected.Spring]);
        }
        if (_craftedResult == kindId) detail.AddChild(Button("crafted_keep", UiCopy.Text("UI_5B4B4E8816A9"), () => { _craftedResult = ""; Render(); }));
        if (kindId == "G26") recipeBody.AddChild(Button("recipe_trial", UiCopy.Text("UI_79D997001877"), () => { _trialRecipeReturn = kindId; _panel = ""; _panelHistory.Clear(); Change(Session.StartTrial); }, s.Phase == ChapterPhase.Camp && s.Done.Contains("Q02")));
        PinTaskButton(recipeBody, "recipe:" + kindId);
        recipeBody.AddChild(Button("recipe_more",UiCopy.Text("UI_747EE88D6FDB"),()=>RecipeMenu(kindId)));
        recipeBody.AddChild(Button("recipe_plan_toggle", s.RecipePlan.Contains(kindId) ? UiCopy.Text("UI_4A7606707123") : UiCopy.Text("UI_DEC27655F1F0"), () => ToggleRecipePlan(kindId), s.RecipePlan.Contains(kindId) || !Session.HasPhysical(kindId) && s.RecipePlan.Count < 4));
        if (!owned) detail.AddChild(Button("make_" + kindId, UiCopy.Text("UI_0C775C37A53D") + ChapterContent.ItemName(kindId), () => RequestCraft(kindId, session => session.Craft(kindId)), Session.CanCraft(kindId), true));
    }

    private void Journal(VBoxContainer box)
    {
        box.AddChild(Text(Session.NextObjective, 20, Accent));
        Tabs(box, "quest_tab_", [("active", "进行中"), ("main", "主线"), ("side", "支线"), ("done", "已完成"), ("log", "行程日志"), ("discoveries", "发现记录")], _questFilter, key => _questFilter = key);
        if (_questFilter == "log")
        {
            JournalLog(box);
            return;
        }
        if (_questFilter == "discoveries")
        {
            DiscoveryJournal(box);
            return;
        }
        var quests = ChapterContent.Quests.Where(q => Session.QuestOpen(q.Id) && (_questFilter switch
        { "main" => q.Id.StartsWith('Q'), "side" => q.Id.StartsWith('S'), "done" => Session.State.Done.Contains(q.Id), _ => !Session.State.Done.Contains(q.Id) }))
            .OrderByDescending(q => Session.CanTurnIn(q.Id)).ToArray();
        var split = Row(16); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var directory = Directory(split); var list = Scroll(directory);
        if (ChapterContent.Quests.Count(q => Session.CanTurnIn(q.Id)) > 1) directory.AddChild(Button("quest_batch", UiCopy.Text("UI_5C87B02E7277"), RequestQuestBatch));
        if (!quests.Any(q => q.Id == _selectedQuest)) _selectedQuest = quests.FirstOrDefault()?.Id ?? "";
        foreach (var quest in quests)
        {
            var id = quest.Id; list.AddChild(Button("quest_detail_" + id, (_selectedQuest == id ? "◆ " : "") + quest.Name + (Session.CanTurnIn(id) ? UiCopy.Text("UI_590B7151F815") : ""), () => { _selectedQuest = id; Render(); }));
            BindObjectMenu(Buttons["quest_detail_"+id],()=>QuestMenu(id));
        }
        var detail = Section(split);
        if (_questFilter == "done") { var plate=detail.GetParent<Control>(); plate.SizeFlagsVertical = SizeFlags.ShrinkBegin; plate.CustomMinimumSize=new Vector2(0,Mathf.Min(420,Size.Y-270)); }
        if (_selectedQuest == "") { detail.AddChild(Text(UiCopy.Text("UI_8F910BE1B1F7"), 16, Muted)); return; }
        var selected = quests.Single(q => q.Id == _selectedQuest); var body = Scroll(detail, "quest_detail", _selectedQuest);
        body.AddChild(Text(selected.Name, 28, Ink)); body.AddChild(Text(Session.QuestStatus(selected.Id), 20, Success));
        body.AddChild(Text(selected.Hint, 18, Ink));
        PinTaskButton(detail, "quest:" + selected.Id);
        body.AddChild(Button("quest_more",UiCopy.Text("UI_747EE88D6FDB"),()=>QuestMenu(selected.Id)));
        if (Session.State.QuestReceipts.TryGetValue(selected.Id, out var receipt)) body.AddChild(Text(UiCopy.Text("UI_3EDDBC5BDB66") + ReportItems(receipt.Consumed) + UiCopy.Text("UI_27F146A833F3") + ReportItems(receipt.Gained), 18, Ink));
        foreach (var step in Session.QuestSteps(selected.Id)) body.AddChild(Text(step, 16, Ink));
        if (!Session.State.Done.Contains(selected.Id))
        {
            var rewards = Session.QuestRewardPreview(selected.Id);
            if (rewards.Gained.Count > 0) body.AddChild(Text(UiCopy.Text("UI_9260E7FD8068") + ReportItems(rewards.Consumed) + UiCopy.Text("UI_217397E556B0") + string.Join("、", rewards.Gained.Select(p => ChapterContent.ItemName(p.Key) + " ×" + p.Value)) + UiCopy.Format("UI_223999D528BB", Session.WarehouseUsed, rewards.Used, Session.WarehouseCapacity), 16, rewards.Used > Session.WarehouseCapacity ? Danger : Ink));
            detail.AddChild(Button("track_quest", Session.State.TrackedQuest == selected.Id ? UiCopy.Text("UI_A561BCBF823F") : UiCopy.Text("UI_BDFCEC1A7793"), () => Change(() => Session.TrackQuest(Session.State.TrackedQuest == selected.Id ? "" : selected.Id))));
            var destination = Session.QuestPlace(selected.Id);
            if (destination != "") detail.AddChild(Button("quest_map", UiCopy.Text("UI_74C85FFA033D") + ChapterContent.Places[destination].Name, () => { _mapPlace = destination; OpenNested("map"); }));
            var kind = ChapterSession.QuestItem(selected.Id); var item = Session.State.Stock.FirstOrDefault(i => i.Kind == kind && (i.Container == "carry" || Session.AtBase && i.Container == "warehouse"));
            if (item != null) detail.AddChild(Button("quest_item", UiCopy.Text("UI_CA34D354AE46") + ChapterContent.ItemName(kind), () => OpenStockObject(item.Id)));
        }
        if (!Session.State.Done.Contains(selected.Id)) body.AddChild(Text(Session.CanTurnIn(selected.Id) ? UiCopy.Text("UI_87C7ECAE5C1A") : UiCopy.Text("UI_2425E9E5EAA4") + QuestNext(selected.Id), 16, Ink));
        if (Session.CanTurnIn(selected.Id)) detail.AddChild(Button("deliver_" + selected.Id, UiCopy.Text("UI_975BE3695001") + selected.Name, () => DeliverQuest(selected.Id), primary: true));
    }

    private static string LogCategory(string note) => new[] { "战斗", "攻击", "护盾", "回合", "傀儡", "试用" }.Any(note.Contains) ? "battle" : new[] { "拾取", "物品", "装包", "仓库", "制作", "材料", "装备" }.Any(note.Contains) ? "stock" : "world";
    private string QuestNext(string id) => Session.NextQuestStep(id);

    private void Map(VBoxContainer box)
    {
        if (_mapPlace == "" || !Session.IsRevealed(_mapPlace)) _mapPlace = Session.AtBase ? Session.State.Start : Session.State.Location;
        box.AddChild(Text(Session.NextObjective + UiCopy.Text("UI_8ED27B3D03F2"), 16, Ink));
        var controls = new HFlowContainer(); box.AddChild(controls);
        controls.AddChild(Button("map_current", Session.AtBase ? UiCopy.Text("UI_A86CB8FEF7D6") : UiCopy.Text("UI_6C06C6C2569D"), () => { _mapPlace = Session.AtBase ? Session.State.Start : Session.State.Location; Render(); }));
        controls.AddChild(Button("map_waypoint", UiCopy.Text("UI_564FEFC4B58F"), () => { _waypoint = _mapPlace; Render(); }));
        if (_waypoint != "") controls.AddChild(Button("map_clear_waypoint", UiCopy.Text("UI_173A111152C6"), () => { _waypoint = ""; Render(); }));
        var split = Row(16); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var path = Session.KnownRoute(_mapPlace);
        var map = new ChapterRouteMap { Name = "RouteMap", Current = Session.AtBase ? "" : Session.State.Location, ReadingScale = ReadingScale, Revealed = ChapterContent.Places.Keys.Where(Session.IsRevealed).ToHashSet(), StationPowered = Session.State.World.Contains("station"), Route = path, Selected = _mapPlace,
            Objective = Session.QuestPlace(Session.ObjectiveQuest), Waypoint = _waypoint, Patrol = Session.State.PatrolPlace, GroundQuantities = ChapterContent.Places.Keys.Where(Session.IsRevealed).ToDictionary(id => id, Session.GroundQuantityAt), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        map.PlaceSelected += id => { _mapPlace = id; Render(); };
        split.AddChild(map); var side = Directory(split); var list = Scroll(side);
        foreach (var place in ChapterContent.Places.Values.Where(p => Session.IsRevealed(p.Id)))
        {
            var id = place.Id; list.AddChild(Button("map_place_" + id, (_mapPlace == id ? "◆ " : "") + place.Name, () => { _mapPlace = id; Render(); }));
        }
        list.AddChild(Text(ChapterContent.Places[_mapPlace].Name, 20, Ink));
        list.AddChild(Text(_mapPlace == "A01" || _mapPlace == "A05" && Session.State.World.Contains("station") ? UiCopy.Text("UI_7D93A450EEE2") : UiCopy.Text("UI_48AF0B150BB2"), 16, Muted));
        if (_mapPlace == Session.State.PatrolPlace) list.AddChild(Text(UiCopy.Text("UI_611457013A45"), 16, Danger));
        foreach (var item in Session.State.Stock.Where(i => Session.State.Ground.TryGetValue(i.Container, out var g) && g.Place == _mapPlace)) list.AddChild(Text(UiCopy.Text("UI_86225436AC2D") + ChapterContent.ItemName(item.Kind) + " ×" + item.Quantity, 16, Accent));
        string[] local = _mapPlace switch { "A02" => ["crate", "transfer"], "A03" => ["scrap", "supplies", "cloth"], "A04" => ["bench"], "A05" => ["wire", "medicine", "frame"], "A07" => ["train-inspect"], _ => [] };
        var searched = local.Where(Session.State.Run.Contains).Select(k => ActionPins[k].Label).ToArray();
        list.AddChild(Text(UiCopy.Text("UI_32F466F8918A") + (searched.Length == 0 ? UiCopy.Text("UI_46CA86DA52B3") : string.Join("、", searched)), 14, Muted));
        var cost = Session.RouteCost(path, Session.AtBase);
        list.AddChild(Text(path.Count == 0 ? UiCopy.Text("UI_6B397EAB757C") : UiCopy.Text("UI_F346E0B68FF4") + string.Join(" → ", path.Select(id => ChapterContent.Places[id].Name)) + UiCopy.Format("UI_46963490A112", cost.Steps, cost.Pressure) + (Session.AtBase ? UiCopy.Text("UI_1374AA244BB6") : UiCopy.Text("UI_B9C058BF2D01")), 16, Ink));
        list.AddChild(Text(UiCopy.Text("UI_9DFD3E897E05"), 14, Muted));
        if (NavigationTarget != "") list.AddChild(Text(UiCopy.Text("UI_9BD864CE0A12") + ChapterContent.Places[NavigationTarget].Name + (_waypoint == "" ? UiCopy.Text("UI_7501F5127A47") : UiCopy.Text("UI_894E5AF0F802")), 16, Accent));
        if (!Session.AtBase)
        {
            var exit = Session.ExitRoute(); var exitCost = Session.RouteCost(exit);
            box.AddChild(Text(UiCopy.Text("UI_628183536105") + string.Join(" → ", exit.Select(id => ChapterContent.Places[id].Name)) + UiCopy.Format("UI_AF5CB68769B8", exitCost.Steps, exitCost.Pressure), 16, Success));
        }
    }
}
