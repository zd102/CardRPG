using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private string _selectedRecipe = "G10", _selectedQuest = "Q01", _logFilter = "all", _mapPlace = "";

    private VBoxContainer Directory(Node parent)
    {
        var column = Column(8); column.CustomMinimumSize = new Vector2(Size.X < 1200 ? 220 : 260, 0);
        parent.AddChild(column); return column;
    }

    private void Inventory(VBoxContainer box)
    {
        var s = Session.State;
        var toolbar = Row(8); box.AddChild(toolbar);
        Tabs(toolbar, "gear_tab_", [("cargo", "物资管理"), ("all", "更换装备"), ("weapon", "武器"), ("body", "身体"), ("hands", "手部"), ("backpack", "背包")], _gearFilter, key => _gearFilter = key);
        if (_gearFilter == "cargo") { PhysicalInventory(box); return; }
        var split = Row(12); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        LoadoutColumn(split);
        var choices = Section(split, .8f); choices.AddChild(Text("可用装备", 20, Ink)); var shelf = Scroll(choices);
        var ids = s.Owned.Concat(s.Loot).Distinct().Order().Where(id => _gearFilter == "all" || ChapterContent.Gear[id].Slot == _gearFilter).ToArray();
        if (!ids.Contains(_selectedGear)) _selectedGear = ids.FirstOrDefault() ?? "";
        foreach (var id in ids)
        {
            var kind = id; var worn = Session.Equipped.Contains(id);
            var button = EquipmentTile("inspect_" + id, kind, worn ? "已穿戴" : SlotName(ChapterContent.Gear[id].Slot), () => { _selectedGear = kind; Render(); }, _selectedGear == id);
            shelf.AddChild(button);
        }
        choices.AddChild(Button("gear_overview", "属性与完整卡组 →", () => OpenNested("character")));
        var details = Section(split, 1.3f); details.AddChild(Text(_selectedGear == "" ? "此部位暂无装备" : ChapterContent.ItemName(_selectedGear), 20, Ink));
        if (_selectedGear == "") return;
        var selected = _selectedGear; var gear = ChapterContent.Gear[selected];
        var detail = Scroll(details); EquipmentComparison(detail, selected);
        var current = Session.Equipped.Contains(selected);
        var allowed = Session.CanEquipItem(selected);
        var preview = allowed ? Session.Preview(x => x.Equip(selected)) : null;
        details.AddChild(Text(preview == null ? current ? "当前已穿戴" : "需在据点或启用的台架换装，并为旧装备留出空间。" : "换装后 · " + preview.LoadDescription + $" · 卡组 {preview.Deck().Count} 张", 16, allowed ? Ink : Muted));
        var actions = new HFlowContainer(); actions.AddThemeConstantOverride("h_separation", 8); details.AddChild(actions);
        actions.AddChild(Button("equip_" + selected, current ? gear.Slot is "hands" or "backpack" ? "卸下" : "已穿戴" : "穿戴此装备", () => Change(() => Session.Equip(selected)), allowed, !current));
        if (s.Phase == ChapterPhase.Camp && !current && s.Owned.Contains(selected))
            actions.AddChild(Button("backup_" + selected, s.Backups.Contains(selected) ? "存回仓库" : "带作备用", () => Change(() => Session.ToggleBackup(selected)), s.Stock.Any(i => i.Kind == selected && Session.CanTransfer(i.Id, i.Container == "carry" ? "warehouse" : "carry", 1))));
    }

    private void EquipmentComparison(VBoxContainer parent, string kind)
    {
        var gear = ChapterContent.Gear[kind];
        var old = Session.Equipped.FirstOrDefault(g => ChapterContent.Gear[g].Slot == gear.Slot);
        var hero = Row(16); parent.AddChild(hero); Illustration(hero, ItemArt(kind), 76, 76, false);
        var words = FlexibleColumn(hero); words.AddChild(Text("当前：" + (old == null ? "未穿戴" : ChapterContent.ItemName(old)), 16, Muted));
        words.AddChild(Text(old != null && old != kind ? $"部位重量 {ChapterContent.Items[old].Grams / 1000m:0.##} → {ChapterContent.Items[kind].Grams / 1000m:0.##} kg" : $"部位重量 {ChapterContent.Items[kind].Grams / 1000m:0.##} kg", 16, Ink));
        if (gear.Slot == "backpack")
        {
            parent.AddChild(Text($"携带容量 {Session.CarryCapacity} → {4 + ChapterContent.BagCapacity(kind)} 格 · 当前使用 {Session.CarryUsed} 格", 20, Ink));
            parent.AddChild(Text("背包不供牌。更换时必须容纳现有随身物品及换下装备。", 16, Muted)); return;
        }
        if (old != null && old != kind)
        {
            var from = Session.GearCards(old).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var to = Session.GearCards(kind).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            parent.AddChild(Text("供牌变化 · 当前 → 候选", 20, Ink));
            foreach (var id in from.Keys.Union(to.Keys).Where(c => from.GetValueOrDefault(c) != to.GetValueOrDefault(c)))
            {
                var card = ChapterContent.Cards[id]; var delta = to.GetValueOrDefault(id) - from.GetValueOrDefault(id);
                parent.AddChild(Text((delta > 0 ? "加入 " : "移除 ") + card.Name + " ×" + Math.Abs(delta) + $" · {card.Action} 行动 / {card.Mana} 魔法\n" + card.Text, 16, delta > 0 ? Success : Danger));
            }
            Rule(parent);
        }
        parent.AddChild(Text("候选供牌 · " + Session.GearCards(kind).Length + " 张", 20, Ink));
        SupplyCards(parent, kind); parent.AddChild(Text(gear.Hint, 16, Muted));
    }

    private void Costs(Node parent, int[] cost)
    {
        var row = new HFlowContainer(); row.AddThemeConstantOverride("h_separation", 16); parent.AddChild(row);
        for (var i = 0; i < cost.Length; i++)
            if (cost[i] > 0) row.AddChild(Inline(ChapterContent.MaterialName(i) + $" {Session.AvailableMaterial(i)}/{cost[i]}", 16, Session.AvailableMaterial(i) < cost[i] ? Danger : Ink));
    }

    private void Crafting(VBoxContainer box)
    {
        var s = Session.State;
        Tabs(box, "craft_tab_", [("recipes", "装备制作"), ("mods", "供牌改装"), ("storage", "背包与仓储")], _craftTab, key => _craftTab = key);
        if (_craftTab == "storage") { StorageCrafting(Scroll(box)); return; }
        if (_craftTab == "mods") { Modifications(Scroll(box)); return; }
        var split = Row(16); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var directory = Directory(split); var list = Scroll(directory);
        foreach (var recipe in ChapterContent.Recipes.Values)
        {
            var kind = recipe.Gear;
            list.AddChild(Button("recipe_" + kind, (_selectedRecipe == kind ? "◆ " : "") + ChapterContent.ItemName(kind), () => { _selectedRecipe = kind; Render(); }));
        }
        directory.AddChild(Text(s.World.Contains("trial-complete") ? "✓ 台架试用已完成" : "免费试用可解锁检修手套", 16, Muted));
        directory.AddChild(Button("trial", "台架试用 →", () => { _panel = ""; _panelHistory.Clear(); Change(Session.StartTrial); }, s.Phase == ChapterPhase.Camp && s.Done.Contains("Q02")));
        var detail = Section(split); var kindId = _selectedRecipe;
        if (!ChapterContent.Recipes.ContainsKey(kindId)) kindId = _selectedRecipe = ChapterContent.Recipes.Keys.First();
        detail.AddChild(Text(ChapterContent.ItemName(kindId), 20, Ink));
        EquipmentComparison(Scroll(detail), kindId);
        var selected = ChapterContent.Recipes[kindId]; Costs(detail, [selected.Steel, selected.Wire, selected.Spring]);
        var reason = Session.CraftReason(kindId);
        detail.AddChild(Text(reason == "" ? "材料按仓库与随身合计，成品存入仓库。" : reason, 16, Session.HasPhysical(kindId) ? Success : reason == "" ? Muted : Danger));
        if (Session.HasPhysical(kindId))
            detail.AddChild(Button("crafted_view", "查看成品与穿戴 →", () => { _selectedGear = kindId; _gearFilter = ChapterContent.Gear[kindId].Slot; OpenNested("gear"); }));
        detail.AddChild(Button("make_" + kindId, Session.HasPhysical(kindId) ? "✓ 已制作 / 已持有" : "制作 " + ChapterContent.ItemName(kindId), () => Change(() => Session.Craft(kindId)), Session.CanCraft(kindId), true));
    }

    private void Modifications(VBoxContainer page)
    {
        page.AddChild(Text("每件装备一个改装位 · 每次只替换一张副本 · 已购方案可免费切换", 16, Muted));
        foreach (var gear in Session.State.Owned.Order())
        foreach (var mod in new[] { "D01", "D08" }.Where(m => ChapterSession.CompatibleMod(gear, m)))
        {
            var box = CardBox(page); var s = Session.State;
            var bought = s.BoughtMods.Contains(gear + ":" + mod); var active = s.Mods.GetValueOrDefault(gear) == mod;
            box.AddChild(Text(ChapterContent.ItemName(gear) + (active ? " · 已安装" : ""), 20, Ink));
            var row = Row(16); box.AddChild(row);
            foreach (var (id, label) in new[] { (mod == "D01" ? "C01" : "C03", "基础副本"), (mod == "D01" ? "C19" : "C26", "改装副本") })
            {
                var card = ChapterContent.Cards[id]; var column = FlexibleColumn(row);
                column.AddChild(Text(label + " · " + card.Name, 20, Ink));
                column.AddChild(Text($"{card.Action} 行动 / {card.Mana} 魔法", 16, Muted)); column.AddChild(Text(card.Text, 16, Ink));
            }
            if (!bought) Costs(box, [2, 0, 1]);
            var available = s.Phase == ChapterPhase.Camp && s.Done.Contains("Q02") && (bought || Session.AvailableMaterial(0) >= 2 && Session.AvailableMaterial(2) >= 1);
            if (!available) box.AddChild(Text(s.Phase != ChapterPhase.Camp ? "需回到据点" : !s.Done.Contains("Q02") ? "需完成：有自己的工具" : "请补齐上方标出的材料", 16, Danger));
            box.AddChild(Button("mod_" + gear + "_" + mod, bought ? active ? "切回基础版（免费）" : "安装已购方案（免费）" : "购买并安装", () => Change(() => bought ? Session.SetMod(gear, active ? "" : mod) : Session.BuyMod(gear, mod)), available, !active));
        }
    }

    private void Journal(VBoxContainer box)
    {
        box.AddChild(Text(Session.NextObjective, 20, Accent));
        Tabs(box, "quest_tab_", [("active", "进行中"), ("main", "主线"), ("side", "支线"), ("done", "已完成"), ("log", "行程日志"), ("discoveries", "发现记录")], _questFilter, key => _questFilter = key);
        if (_questFilter == "log")
        {
            Tabs(box, "log_", [("all", "全部"), ("battle", "战斗"), ("stock", "物资"), ("world", "探索")], _logFilter, key => _logFilter = key);
            var notes = Scroll(box);
            foreach (var note in Session.State.Log.AsEnumerable().Reverse().Where(n => _logFilter == "all" || LogCategory(n) == _logFilter)) notes.AddChild(Text("· " + note, 16, Ink));
            if (_lastFeedback.Count > 0) { notes.AddChild(Text("最近一次战斗结算", 20, Ink)); foreach (var note in _lastFeedback) notes.AddChild(Text(note, 16, Ink)); }
            return;
        }
        if (_questFilter == "discoveries")
        {
            var page = Scroll(box);
            foreach (var key in Session.State.World.Concat(Session.State.Done).Where(Stories.ContainsKey))
            { var story = Stories[key]; page.AddChild(Button("recall_" + key, story.Title + " · " + story.Person, () => { _story = key; OpenNested("story"); })); }
            foreach (var clue in Session.State.Intel) page.AddChild(Text(clue switch { "relay-seen" => "发现：工坊墙后有荧光痕迹", "glowing-manifest" => "发现：夹层中的货单与工具", _ => "已记录的区域线索" }, 16, Ink));
            return;
        }
        var quests = ChapterContent.Quests.Where(q => Session.QuestOpen(q.Id) && (_questFilter switch
        { "main" => q.Id.StartsWith('Q'), "side" => q.Id.StartsWith('S'), "done" => Session.State.Done.Contains(q.Id), _ => !Session.State.Done.Contains(q.Id) }))
            .OrderByDescending(q => Session.CanTurnIn(q.Id)).ToArray();
        var split = Row(16); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var directory = Directory(split); var list = Scroll(directory);
        if (!quests.Any(q => q.Id == _selectedQuest)) _selectedQuest = quests.FirstOrDefault()?.Id ?? "";
        foreach (var quest in quests)
        {
            var id = quest.Id; list.AddChild(Button("quest_detail_" + id, (_selectedQuest == id ? "◆ " : "") + quest.Name + (Session.CanTurnIn(id) ? " · 可交付" : ""), () => { _selectedQuest = id; Render(); }));
        }
        var detail = Section(split);
        if (_selectedQuest == "") { detail.AddChild(Text("此分类暂无任务。", 16, Muted)); return; }
        var selected = quests.Single(q => q.Id == _selectedQuest); var body = Scroll(detail);
        body.AddChild(Text(selected.Name, 28, Ink)); body.AddChild(Text(Session.QuestStatus(selected.Id), 20, Success));
        body.AddChild(Text(selected.Hint, 18, Ink));
        if (!Session.State.Done.Contains(selected.Id)) body.AddChild(Text(Session.CanTurnIn(selected.Id) ? "下一步：在此交付，领取奖励。" : "下一步：" + QuestNext(selected.Id), 16, Ink));
        if (Session.CanTurnIn(selected.Id)) detail.AddChild(Button("deliver_" + selected.Id, "交付 " + selected.Name, () => Change(() => Session.TurnIn(selected.Id)), primary: true));
    }

    private static string LogCategory(string note) => new[] { "战斗", "攻击", "护盾", "回合", "傀儡", "试用" }.Any(note.Contains) ? "battle" : new[] { "拾取", "物品", "装包", "仓库", "制作", "材料", "装备" }.Any(note.Contains) ? "stock" : "world";
    private string QuestNext(string id) => id switch
    {
        "Q01" => Session.NextObjective,
        "Q02" => Session.State.Calibrated || Session.State.StoredCalibration ? "将校准夹具安全带回并交付。" : "到广场取夹具，再携带到工坊启动台架并校准。",
        "S01" => "在货站修复线路、领取路签，安全带回交付。",
        "S07" => "在废料场救出维修偶，然后安全撤离。",
        "S08" => "沿已发现的荧光线索探索工坊，带回工具。",
        "S09" => "探索无人车厢，取回居民包裹并安全撤离。",
        _ => "在工坊完成台架试用，按任务说明准备物品。"
    };

    private void Map(VBoxContainer box)
    {
        box.AddChild(Text(Session.NextObjective + " · 地图查看免费，从场景出口移动", 16, Ink));
        var split = Row(16); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var map = new ChapterRouteMap { Name = "RouteMap", Current = Session.AtBase ? "" : Session.State.Location, Revealed = ChapterContent.Places.Keys.Where(Session.IsRevealed).ToHashSet(), StationPowered = Session.State.World.Contains("station"), Route = Session.AtBase ? [] : Session.ExitRoute(), Selected = _mapPlace, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddChild(map); var side = Directory(split); var list = Scroll(side);
        foreach (var place in ChapterContent.Places.Values.Where(p => Session.IsRevealed(p.Id)))
        {
            var id = place.Id; list.AddChild(Button("map_place_" + id, (_mapPlace == id ? "◆ " : "") + place.Name, () => { _mapPlace = id; Render(); }));
        }
        if (_mapPlace == "" || !Session.IsRevealed(_mapPlace)) _mapPlace = Session.State.Location;
        list.AddChild(Text(ChapterContent.Places[_mapPlace].Name, 20, Ink));
        list.AddChild(Text(_mapPlace == "A01" || _mapPlace == "A05" && Session.State.World.Contains("station") ? "安全撤离点" : "已发现地点", 16, Muted));
        if (_mapPlace == Session.State.PatrolPlace) list.AddChild(Text("巡逻当前所在", 16, Danger));
        foreach (var item in Session.State.Stock.Where(i => Session.State.Ground.TryGetValue(i.Container, out var g) && g.Place == _mapPlace)) list.AddChild(Text("遗留：" + ChapterContent.ItemName(item.Kind) + " ×" + item.Quantity, 16, Accent));
        string[] local = _mapPlace switch { "A02" => ["crate", "transfer"], "A03" => ["scrap", "supplies", "cloth"], "A04" => ["bench"], "A05" => ["wire", "medicine", "frame"], "A07" => ["train-inspect"], _ => [] };
        var searched = local.Where(Session.State.Run.Contains).Select(k => ActionPins[k].Label).ToArray();
        list.AddChild(Text("本地已搜索：" + (searched.Length == 0 ? "暂无记录" : string.Join("、", searched)), 14, Muted));
        if (!Session.AtBase) box.AddChild(Text("撤离路线：" + string.Join(" → ", Session.ExitRoute().Select(id => ChapterContent.Places[id].Name)), 16, Success));
    }
}
