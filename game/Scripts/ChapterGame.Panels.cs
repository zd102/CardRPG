using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private VBoxContainer Overlay(string title)
    {
        if (_hud != null)
        {
            void DisableFocus(Node node)
            {
                if (node is Control control) control.FocusMode = FocusModeEnum.None;
                foreach (var child in node.GetChildren()) DisableFocus(child);
            }
            DisableFocus(_hud);
            _hud.Modulate = new Color(.55f, .55f, .55f);
        }
        var shade = new ColorRect { Name = "ModalShade", Color = new Color("030b09b8"), MouseFilter = MouseFilterEnum.Stop };
        _root.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = ChapterSkin.Panel(_root, "ledger-paper", 18); panel.Name = "FieldManual";
        Dock(panel, 24, 108, -24, -20, ar: 1, ab: 1);
        var layout = Column(10); panel.AddChild(layout);
        var head = Row(18); layout.AddChild(head);
        var titles = FlexibleColumn(head); titles.AddThemeConstantOverride("separation", 2);
        var titleRow=Row(14); titles.AddChild(titleRow); Illustration(titleRow,_panel switch {"gear"=>"G01","craft"=>"G26","journal"=>"T11","prepare"=>"satchel",_=>"crest"},48,46,false);
        titleRow.AddChild(Inline(title, 28, Ink));
        var folio = Inline(_panel switch { "character" => "01 / 远征档案", "prepare" => "02 / 出发检查", "gear" => "03 / 装备物资", "craft" => "04 / 制作改装", "journal" => "05 / 行程记录", "map" => "06 / 区域路线", _ => "07 / 现场指南" }, 14, Muted);
        folio.SizeFlagsHorizontal = SizeFlags.ExpandFill; folio.HorizontalAlignment = HorizontalAlignment.Right;
        folio.SizeFlagsVertical = SizeFlags.ShrinkCenter; head.AddChild(folio);
        var close = IconButton("close_panel", "close", _panelHistory.Count > 0 ? "返回上页 · Esc" : "返回场景 · Esc", ClosePanel);
        close.SizeFlagsVertical = SizeFlags.ShrinkCenter; head.AddChild(close);
        Rule(layout);
        var content = Row(12); content.SizeFlagsVertical = SizeFlags.ExpandFill; layout.AddChild(content);
        var settlement = Session.State.Phase is ChapterPhase.Extracted or ChapterPhase.Defeated;
        if (!settlement)
        {
            var rail = Column(8); rail.CustomMinimumSize = new Vector2(98, 0); content.AddChild(rail);
            foreach (var (key, caption) in new[] { ("character", "角色"), ("prepare", "整备"), ("gear", "装备"), ("craft", "工坊"), ("journal", "任务"), ("map", "地图"), ("help", "指引") })
            {
                var target = key;
                var nav = Button("nav_" + key, "     " + caption, () => { if (_panel != target) Toggle(target); }, true, _panel == key);
                nav.CustomMinimumSize = new Vector2(98, 48);
                var icon = Glyph(key=="gear"?"bag":key, _panel == key ? new Color("f1e7ce") : Ink, 22); nav.AddChild(icon); Dock(icon,8,-11,30,11,at:.5f,ab:.5f);
                rail.AddChild(nav);
            }
            Fill(rail); rail.AddChild(Text("余火站\n外环 · 第一章", 14, Muted));
        }
        var body = FlexibleColumn(content); body.Name = "WindowContent";
        if (_actionWarning != "") body.AddChild(Text(_actionWarning, 16, Danger));
        Rule(layout);
        var footer = Row(); layout.AddChild(footer);
        var state = Text(settlement ? "余火未熄" : Session.State.Phase == ChapterPhase.Camp ? "安全据点" : "撤离后入库", 13, Muted);
        state.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(state);
        footer.AddChild(Inline("Tab 切换  ·  Enter 确认  ·  Esc 返回", 13, Muted));
        return body;
    }

    private void ShowPanel()
    {
        var box = Overlay(_panel switch { "character" => "角色全览", "gear" => "装备与背包", "craft" => "岑砧的工坊", "journal" => "任务与发现", "prepare" => "行前整备", "map" => "外环路线图", _ => "远征指引" });
        switch (_panel)
        {
            case "prepare": Preparation(box); break;
            case "character": CharacterOverview(box); break;
            case "gear": Inventory(box); break;
            case "craft": Crafting(box); break;
            case "journal": Journal(box); break;
            case "map": Map(box); break;
            default: Handbook(box); break;
        }
    }

    private void Tabs(Node parent, string prefix, (string Id, string Label)[] tabs, string selected, Action<string> select)
    {
        var row = new HFlowContainer(); row.AddThemeConstantOverride("h_separation", 6); row.AddThemeConstantOverride("v_separation", 6); parent.AddChild(row);
        foreach (var (id, caption) in tabs)
        {
            var key = id;
            row.AddChild(Button(prefix + id, caption, () => { select(key); _scroll = null; Render(); }, true, id == selected));
        }
    }

    private void Inventory(VBoxContainer box)
    {
        var s = Session.State;
        var status = Row(16); box.AddChild(status);
        var load = Text(Session.LoadDescription, 16, Session.Overweight ? Danger : Ink);
        load.SizeFlagsHorizontal = SizeFlags.ExpandFill; status.AddChild(load);
        status.AddChild(Inline(Session.CanEquip ? "可换装" : "据点 / 台架可换装", 14, Muted));
        Tabs(box, "gear_tab_", [("all", "全部装备"), ("weapon", "武器"), ("body", "身体"), ("hands", "手部"), ("backpack", "背包"), ("cargo", "物品与仓库")], _gearFilter, key => _gearFilter = key);
        if (_gearFilter == "cargo") { PhysicalInventory(box); return; }
        var split = Row(14); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var wearing = Section(split, .95f);
        wearing.AddChild(Text("当前穿戴", 20, Ink));
        var board = Scroll(wearing); EquipmentBoard(board, Session.Equipped.ToArray(), "wear", false);
        board.AddChild(Text($"提供 {Session.Deck().Count} 张牌 · 备用 {s.Backups.Count} 件", 14, Muted));
        board.AddChild(Button("gear_overview", "属性与完整卡组 →", () => OpenNested("character")));

        var choices = Section(split, 1);
        choices.AddChild(Text("可选装备", 20, Ink));
        var shelf = Scroll(choices);
        var gearIds = s.Owned.Concat(s.Loot).Distinct().Order().Where(id => _gearFilter == "all" || ChapterContent.Gear[id].Slot == _gearFilter).ToArray();
        if (!gearIds.Contains(_selectedGear)) _selectedGear = gearIds.FirstOrDefault(id => Session.Equipped.Contains(id)) ?? gearIds.FirstOrDefault() ?? "";
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); shelf.AddChild(grid);
        foreach (var id in gearIds)
        {
            var gear = ChapterContent.Gear[id]; var equipped = Session.Equipped.Contains(id);
            var tile = ItemTile("inspect_" + id, id, gear.Name, equipped ? "已穿戴" : s.Backups.Contains(id) ? "随身备用" : Session.IsOnPerson(id) ? "随身" : "未携带",
                () => { _selectedGear = id; Render(); }, _selectedGear == id, 118);
            tile.SizeFlagsHorizontal = SizeFlags.ExpandFill; grid.AddChild(tile);
        }
        if (gearIds.Length == 0) shelf.AddChild(Text("此部位还没有可选装备。", 16, Muted));
        var details = Section(split, 1.2f); details.AddChild(Text("装备详情", 20, Ink)); var detail = Scroll(details);
        if (_selectedGear == "") { detail.AddChild(Text("选择装备查看供牌与穿戴选项。", 16, Muted)); return; }
        var selected = ChapterContent.Gear[_selectedGear]; var kind = _selectedGear;
        var hero = Row(10); detail.AddChild(hero); Illustration(hero, ItemArt(kind), 64, 64);
        var name = FlexibleColumn(hero); name.AddChild(Text(selected.Name, 20, Ink));
        name.AddChild(Text(SlotName(selected.Slot) + $" · {ChapterContent.Items[kind].Grams / 1000m:0.##} kg", 14, Muted));
        var actions = new HFlowContainer(); actions.AddThemeConstantOverride("h_separation", 6); actions.AddThemeConstantOverride("v_separation", 6); detail.AddChild(actions);
        var isWearing = Session.Equipped.Contains(kind);
        actions.AddChild(Button("equip_" + kind, isWearing ? selected.Slot is "hands" or "backpack" ? "卸下" : "已穿戴" : "穿戴", () => Change(() => Session.Equip(kind)), Session.CanEquipItem(kind), !isWearing));
        if (s.Phase == ChapterPhase.Camp && !isWearing && s.Owned.Contains(kind))
            actions.AddChild(Button("backup_" + kind, s.Backups.Contains(kind) ? "取消备用" : "带作备用", () => Change(() => Session.ToggleBackup(kind)),
                s.Stock.Any(i => i.Kind == kind && Session.CanTransfer(i.Id, i.Container == "carry" ? "warehouse" : "carry", 1))));
        var physical = s.Stock.First(i => i.Kind == kind);
        detail.AddChild(Text(isWearing ? "装备栏 · 不占储物格" : physical.Container == "carry" ? "随身物品 · 占用背包格位" : s.Ground.TryGetValue(physical.Container, out var ground) ? "遗留于" + ChapterContent.Places[ground.Place].Name : "基地仓库", 14, Muted));
        if (selected.Slot == "backpack")
        {
            detail.AddChild(Text($"口袋 4 + 背包 {ChapterContent.BagCapacity(kind)} = {4 + ChapterContent.BagCapacity(kind)} 格", 20, Accent));
            detail.AddChild(Text("背包也是装备。更换或卸下时，剩余格位必须容纳随身物品。", 16, Ink));
        }
        else { detail.AddChild(Text($"装备供牌 · {Session.GearCards(kind).Length} 张", 16, Accent)); SupplyCards(detail, kind, true); }
        Rule(detail); detail.AddChild(Text(selected.Hint, 14, Muted));
    }

    private void Crafting(VBoxContainer box)
    {
        var s = Session.State; var open = s.Done.Contains("Q02"); var camp = s.Phase == ChapterPhase.Camp;
        IllustratedMaterials(box,s.Bank);
        if (!open) box.AddChild(Text("装备制作需完成“有自己的工具”；背包与基础补给的简易制作现在可用。", 16, Muted));
        Tabs(box, "craft_tab_", [("recipes", "装备制作"), ("mods", "供牌改装"), ("storage", "背包与仓储")], _craftTab, key => _craftTab = key);
        var page = Scroll(box);
        if (_craftTab == "storage") { StorageCrafting(page); return; }
        if (_craftTab == "mods")
        {
            Heading(page, "一个改装位", "每次替换一张真实副本；购买需废钢 2 / 弹簧 1，已购方案可免费切换。");
            foreach (var gear in s.Owned.Order())
            foreach (var mod in new[] { "D01", "D08" }.Where(m => ChapterSession.CompatibleMod(gear, m)))
            {
                var card = CardBox(page); var bought = s.BoughtMods.Contains(gear + ":" + mod); var active = s.Mods.GetValueOrDefault(gear) == mod;
                var work=Row(18); card.AddChild(work); Illustration(work,gear,104,104);
                var change=FlexibleColumn(work); change.AddChild(Text(ChapterContent.Gear[gear].Name,20,Ink));
                ProcessTrail(change,[(gear,mod=="D01"?"锤击":"坚守",false),(mod=="D01"?"G15":"G26",mod=="D01"?"盾刃":"应急加固",active)]);
                card.AddChild(Button("mod_" + gear + "_" + mod, bought ? active ? "切回基础版（免费）" : "安装已购方案（免费）" : "购买并安装 · 废钢 2 / 弹簧 1",
                    () => Change(() => bought ? Session.SetMod(gear, active ? "" : mod) : Session.BuyMod(gear, mod)), open && camp && (bought || s.Bank[0] >= 2 && s.Bank[2] >= 1)));
            }
            return;
        }
        var note = Paper(page, 16); var trial = Row(16); note.AddChild(trial); var copy = FlexibleColumn(trial);
        Illustration(trial,"enemy-dummy",80,88,false);
        copy.AddChild(Text("试验台", 20, ChapterSkin.PaperInk));
        copy.AddChild(Text(s.World.Contains("trial-complete") ? "试用已完成，检修工具手套配方已开放。" : "免费借用检修手套，用预备工具后攻击；不消耗真实生命与补给。", 16, ChapterSkin.PaperMuted));
        var start = Button("trial", "台架试用  →", () => { _panel = ""; _panelHistory.Clear(); Change(Session.StartTrial); }, open && camp, true);
        start.SizeFlagsVertical = SizeFlags.ShrinkCenter; trial.AddChild(start);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 12); page.AddChild(grid);
        foreach (var recipe in ChapterContent.Recipes.Values)
        {
            var id = recipe.Gear; var card = CardBox(grid); ((Control)card.GetParent()).SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var work=Row(12); card.AddChild(work); Illustration(work,id,110,116);
            var specification=FlexibleColumn(work); specification.AddChild(Text(ChapterContent.Gear[id].Name,20,Ink));
            SymbolValue(specification,"draw",Session.GearCards(id).Count().ToString(),Muted,"制作后获得的供牌张数");
            var cost = new[] { recipe.Steel, recipe.Wire, recipe.Spring };
            IllustratedMaterials(card,cost,cost.Where((n,i)=>n>s.Bank[i]).Any() ? Danger : Ink);
            var caption = s.Owned.Contains(id) ? "已登记" : !Session.RecipeOpen(id) ? "配方未开放" : !camp ? "需回据点" : Session.CanCraft(id) ? "制作装备" : "材料或成品空间不足";
            card.AddChild(Button("make_" + id, caption, () => Change(() => Session.Craft(id)), Session.CanCraft(id), true));
        }
    }

    private void Journal(VBoxContainer box)
    {
        var s = Session.State; box.AddChild(Text(Session.Objective, 20, Accent));
        Tabs(box, "quest_tab_", [("all", "全部记录"), ("main", "主线"), ("side", "支线"), ("done", "已完成")], _questFilter, key => _questFilter = key);
        var split = Row(16); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var list = Section(split, 1.35f); list.AddChild(Text("委托档案",20,Ink)); var quests = Scroll(list);
        foreach (var quest in ChapterContent.Quests.Where(q => _questFilter switch { "main" => q.Id.StartsWith("Q"), "side" => q.Id.StartsWith("S"), "done" => s.Done.Contains(q.Id), _ => true }))
        {
            var q = CardBox(quests); var heading = Row(12); q.AddChild(heading);
            Illustration(heading,quest.Id switch {"Q01"=>"T01","Q02"=>"T02","S01"=>"T11","S07"=>"repair-robot","S08"=>"G25","S09"=>"T14",_=>"G26"},76,80);
            var questCopy=FlexibleColumn(heading); questCopy.AddChild(Text(quest.Name,20,Ink));
            SymbolValue(questCopy,s.Done.Contains(quest.Id)?"check":Session.CanTurnIn(quest.Id)?"home":"journal",Session.QuestStatus(quest.Id),Session.CanTurnIn(quest.Id)?Success:Muted,"",14);
            var hint = Text(quest.Hint,16,Muted); hint.Visible = false; q.AddChild(hint);
            var detail = IconButton("quest_detail_"+quest.Id,"help",quest.Name+"\n"+quest.Hint,()=>{ hint.Visible=!hint.Visible; });
            detail.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            heading.AddChild(detail);
            if (Session.CanTurnIn(quest.Id)) q.AddChild(Button("deliver_" + quest.Id, "交付任务  →", () => Change(() => Session.TurnIn(quest.Id)), true, true));
        }
        var log = Paper(split); ((Control)log.GetParent()).SizeFlagsStretchRatio = .85f;
        log.AddChild(Text("路上的记录", 24, ChapterSkin.PaperInk));
        var panorama=ChapterArt.Picture("camp"); panorama.CustomMinimumSize=new Vector2(0,98); log.AddChild(panorama); Rule(log);
        var notes = Scroll(log);
        if (s.Log.Count == 0) notes.AddChild(Text("新的旅程，从余火站出发。", 16, ChapterSkin.PaperMuted));
        foreach (var note in s.Log.TakeLast(18).Reverse()) notes.AddChild(Text("· " + note, 16, ChapterSkin.PaperInk));
    }

    private void Map(VBoxContainer box)
    {
        box.AddChild(Text("从场景中的出口移动；地图仅用于查看路线。", 16, Muted));
        box.AddChild(new ChapterRouteMap
        {
            Name = "RouteMap", Current = Session.State.Phase == ChapterPhase.Camp ? "" : Session.State.Location,
            Revealed = ChapterContent.Places.Keys.Where(Session.IsRevealed).ToHashSet(), StationPowered = Session.State.World.Contains("station"),
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        foreach (var group in Session.State.Ground.Where(p => Session.State.Stock.Any(i => i.Container == p.Key)).GroupBy(p => p.Value.Place))
            box.AddChild(Text(ChapterContent.Places[group.Key].Name + $"：{group.Count()} 处遗留物（仅本次远征）", 14, Accent));
        box.AddChild(Text("● 当前位置    ◇ 安全撤离    ┄ 已发现路线", 14, Accent));
    }

    private void Handbook(VBoxContainer box)
    {
        var page = Scroll(box); var lead = Paper(page);
        lead.AddChild(Text("把收获带回来。", 28, ChapterSkin.PaperInk));
        lead.AddChild(Text("首轮只需从废料场取回继电器。无需清空地图，随时可以回入口撤离。", 18, ChapterSkin.PaperMuted));
        ProcessTrail(lead,[("T01","取得继电器",false),("satchel","带回收获",false),("crest","点亮余火站",false)]);
        var legend = new GridContainer { Columns = 4 }; legend.AddThemeConstantOverride("h_separation",22); legend.AddThemeConstantOverride("v_separation",12); page.AddChild(legend);
        foreach(var (symbol,label) in new[] { ("heart","生命"),("supply","补给"),("bag","携带物"),("exit","出口 / 撤离"),
            ("action","行动"),("mana","魔法"),("hammer","伤害"),("guard","护盾"),("draw","抽牌"),("eye","区域压力"),("all","全体 / 增援"),("round","回合 / 蓄力"),
            ("steel","废钢"),("wire","导能线"),("spring","精密弹簧"),("lock","资源不足") })
            SymbolValue(legend,symbol,label,Ink,"",16);
        page.AddChild(Text("圆形标记是物件，菱形标记是出口。停留鼠标或用 Tab 聚焦，可查看完整规则；小亮点标记关键物件。",16,Muted));
        foreach (var (number, title, copy) in new[]
        {
            ("01", "探索与巡逻", "通过场景出口移动，每次 1 步。搜物、设施与补给通常也用 1 步。负重超过 20 kg 时场景行动值翻倍。巡逻先预警，抵达后仍可离开；继续停留才会被拦截。阅读与思考免费。"),
            ("02", "装备决定卡组", "据点可换装和制作，备用件数由背包格位决定，穿戴与随身实物都计入负重。启动维修工坊的台架后可换随身装备。新物品需要撤离登记；永久修复与救援立即保存。"),
            ("03", "每回合做出选择", "每回合 3 行动，抽 5 张牌，恢复 1 魔法（上限 3）。先选敌人再出牌；结束回合弃牌，敌人执行公开意图。额外抽牌最多 2 张。"),
            ("04", "护盾与撤退", "护盾下回合清除，稳固最多保留 4。盾压消耗实际护盾，穿盾直接扣生命。战术撤退仍承受本轮意图，存活后退回进入位置，敌人状态保留。")
        })
        {
            var card = CardBox(page); var lesson=Row(18); card.AddChild(lesson);
            Illustration(lesson,number switch {"01"=>"T11","02"=>"G01","03"=>"enemy-E01",_=>"G09"},96,98);
            var copyBox=FlexibleColumn(lesson); copyBox.AddChild(Text(number + "  /  " + title,20,Accent)); copyBox.AddChild(Text(copy,16,Ink));
        }
    }
}
