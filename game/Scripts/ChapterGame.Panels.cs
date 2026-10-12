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
        var shade = new ColorRect { Name = "ModalShade", Color = new Color("03090ca6"), MouseFilter = MouseFilterEnum.Stop };
        _root.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = Panel(_root, new Color("101b20f2")); panel.Name = "CenteredJournal";
        Dock(panel, 0, 0, 0, 0, .15f, .15f, .85f, .9f);
        var layout = Column(14); panel.AddChild(layout); var head = Row(); layout.AddChild(head);
        var name = Text(title, 26, Ink); name.SizeFlagsHorizontal = SizeFlags.ExpandFill; head.AddChild(name);
        head.AddChild(Button("close_panel", "返回场景  Esc", () => { _panel = ""; Render(); }));
        return Scroll(layout);
    }
    private void ShowPanel()
    {
        if (_panel == "map") { Map(); return; }
        var box = Overlay(_panel switch { "gear" => "装备与随身物品", "craft" => "岑砧的工坊", "journal" => "任务与发现", "prepare" => "行前整备", _ => "远征手册" });
        switch (_panel)
        {
            case "prepare": Preparation(box); break;
            case "gear": Inventory(box); break;
            case "craft": Crafting(box); break;
            case "journal": Journal(box); break;
            default:
                Heading(box, "带回第一盏灯", "不用先打完地图。拿到继电器后，随时能回入口撤离。");
                box.AddChild(Text("点击场景出口移动，每次 1 步。搜物、设施与补给通常也用 1 步。阅读、看地图、比较装备和战斗思考免费。\n\n巡逻先预警，抵达后还能移动离开；继续停留才会被拦截。固定任务物与出口不会因压力消失。", 18, Ink));
                Heading(box, "任务物、装备与制作", "新收获必须撤离登记；永久修复和救援会立即保存。");
                box.AddChild(Text("据点可交付任务、换装和制作。出发最多选 2 件备用；A04 启用后只能换随身或新获得的装备。\n\nQ02 开放手部、制作与改装。外环实现了两项主线及相关支线，完成后可继续回访；水务区尚未开放。", 18, Ink));
                Heading(box, "战斗的每一回合", "每回合 3 行动、抽 5 张，魔法恢复 1，上限 3；额外抽牌最多 2 张。");
                box.AddChild(Text("先选敌人，再使用手牌。结束回合弃置剩余牌，敌人执行公开意图。护盾下回合清除，稳固可保留最多 4。\n\n盾压真正消耗护盾；穿盾直接扣生命。战术撤退仍承受本轮意图，存活后退回进入位置。敌人和牌区暂停，重返战斗不会刷新开场。", 18, Ink));
                break;
        }
    }
    private void Inventory(VBoxContainer box)
    {
        var s = Session.State;
        box.AddChild(Text(Session.CanEquip ? "此处可换装。武器与身体必备，手部可卸下。" : "当前只可查看；据点与启用后的 A04 可换装。", 16, Accent));
        box.AddChild(Text($"当前卡组 {Session.Deck().Count} 张  /  备用 {s.Backups.Count}/2\n" + string.Join("、", Session.Deck().GroupBy(c => c).Select(g => ChapterContent.Cards[g.Key].Name + " ×" + g.Count())), 16, Ink));
        foreach (var id in s.Owned.Concat(s.Loot).Distinct().Order())
        {
            var gear = ChapterContent.Gear[id]; var wearing = Session.Equipped.Contains(id);
            var body = CardBox(box, wearing ? new Color("243a34") : Surface);
            var row = Row(12); body.AddChild(row); var name = Text(gear.Name + (s.Loot.Contains(id) ? " · 未登记" : ""), 20, wearing ? Accent : Ink);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(name);
            var carried = s.Phase == ChapterPhase.Camp || wearing || s.Backups.Contains(id) || s.Loot.Contains(id) || s.DepartureGear.ContainsValue(id);
            row.AddChild(Button("equip_" + id, wearing ? gear.Slot == "hands" ? "卸下" : "已穿戴" : "穿戴", () => Change(() => Session.Equip(id)), Session.CanEquip && carried && (!wearing || gear.Slot == "hands") && (gear.Slot != "hands" || s.Done.Contains("Q02"))));
            if (s.Phase == ChapterPhase.Camp && !wearing && s.Owned.Contains(id))
                row.AddChild(Button("backup_" + id, s.Backups.Contains(id) ? "取消备用" : "带作备用", () => Change(() => Session.ToggleBackup(id)), s.Backups.Contains(id) || s.Backups.Count < 2));
            body.AddChild(Text(Session.CardList(id), 16, Ink)); body.AddChild(Text(gear.Hint, 14, Muted));
            if (s.Phase == ChapterPhase.Exploring && !carried) body.AddChild(Text("存放在仓库，本次未携带。", 14, Muted));
        }
        Heading(box, "携带品与仓库", "未入库物品在战败时丢失，已登记装备保留。");
        box.AddChild(Text("未入库：" + Materials(s.Cargo) + "\n任务物：" + (s.Items.Count == 0 ? "无" : string.Join("、", s.Items.Select(ChapterContent.ItemName)))
            + "\n仓库：" + Materials(s.Bank) + "\n已存任务物：" + (s.StoredItems.Count == 0 ? "无" : string.Join("、", s.StoredItems.Select(ChapterContent.ItemName))), 16, Ink));
        if (s.Phase == ChapterPhase.Camp && s.StoredItems.Contains("T02") && !s.StoredCalibration)
            box.AddChild(Button("carry_T02", s.CarryItems.Contains("T02") ? "取消携带待校准夹具" : "将待校准夹具带入下一次远征", () => Change(() => Session.ToggleCarryItem("T02"))));
    }
    private void Crafting(VBoxContainer box)
    {
        var s = Session.State; var open = s.Done.Contains("Q02"); var camp = s.Phase == ChapterPhase.Camp;
        box.AddChild(Text(open ? "每件装备固定供牌，制作没有随机失败。已登记底材不重复制作。" : "先完成“有自己的工具”，开放制作、改装与台架试用。", 17, Accent));
        box.AddChild(Text("仓库 · " + Materials(s.Bank), 17, Ink));
        box.AddChild(Button("trial", "借用检修手套，进入台架试用", () => { _panel = ""; Change(Session.StartTrial); }, open && camp, true));
        box.AddChild(Text(s.World.Contains("trial-complete") ? "试用已完成，G26 配方开放；剧场后续尚未开放。" : "试用中用预备工具后再攻击。免费退出，不影响真实生命、装备和补给。", 15, Muted));
        foreach (var recipe in ChapterContent.Recipes.Values)
        {
            var id = recipe.Gear; var body = CardBox(box); var row = Row(); body.AddChild(row);
            var title = Text(ChapterContent.Gear[id].Name, 20, Ink); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(title);
            var caption = s.Owned.Contains(id) ? "已登记" : !Session.RecipeOpen(id) ? "配方未开放" : !camp ? "需回据点" : Session.CanCraft(id) ? "制作" : "材料不足";
            row.AddChild(Button("make_" + id, caption, () => Change(() => Session.Craft(id)), Session.CanCraft(id)));
            body.AddChild(Text("消耗 · " + Materials([recipe.Steel, recipe.Wire, recipe.Spring]) + "\n供牌 · " + Session.CardList(id), 16, Muted));
        }
        Heading(box, "一个改装位", "每次替换一张真实副本，费用为废钢 2＋精密弹簧 1。已购方案可免费切回。");
        foreach (var gear in s.Owned.Order())
        foreach (var mod in new[] { "D01", "D08" }.Where(m => ChapterSession.CompatibleMod(gear, m)))
        {
            var body = CardBox(box); var bought = s.BoughtMods.Contains(gear + ":" + mod); var active = s.Mods.GetValueOrDefault(gear) == mod;
            body.AddChild(Text(ChapterContent.Gear[gear].Name + " / " + (mod == "D01" ? "护盾导轨：锤击 → 盾刃" : "应急工具袋：坚守 → 应急加固"), 17, Ink));
            body.AddChild(Button("mod_" + gear + "_" + mod, bought ? active ? "切回基础版（免费）" : "安装已购方案（免费）" : "购买并安装 · 废钢 2 / 弹簧 1",
                () => Change(() => bought ? Session.SetMod(gear, active ? "" : mod) : Session.BuyMod(gear, mod)), open && camp && (bought || s.Bank[0] >= 2 && s.Bank[2] >= 1)));
        }
    }
    private void Journal(VBoxContainer box)
    {
        box.AddChild(Text(Session.Objective, 19, Accent));
        foreach (var quest in ChapterContent.Quests)
        {
            var q = CardBox(box); var row = Row(); q.AddChild(row);
            var title = Text(quest.Name + " · " + Session.QuestStatus(quest.Id), 19, Ink); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(title);
            if (Session.CanTurnIn(quest.Id)) row.AddChild(Button("deliver_" + quest.Id, "交付", () => Change(() => Session.TurnIn(quest.Id)), true, true));
            q.AddChild(Text(quest.Hint, 16, Muted));
        }
        Heading(box, "路上的记录", "最近的记录在前，查看不推进时间。");
        foreach (var note in Session.State.Log.TakeLast(18).Reverse()) box.AddChild(Text("· " + note, 16, Muted));
    }
    private void Map()
    {
        var title = Column(6); _root.AddChild(title); Dock(title, 40, 160, -40, 245, ar: 1);
        title.AddChild(Text("外环路线图", 30, Ink)); title.AddChild(Text("只读 · 返回场景，通过出口移动 · 已发现的支路永久记录", 16, Muted));
        foreach (var (id, point) in ChapterScene.MapPoints)
        {
            if (!Session.IsRevealed(id)) continue;
            var current = Session.State.Location == id;
            var p = Panel(_root, current ? new Color("314a40ef") : new Color("10212be8"), current ? Accent : Outline);
            p.Name = "MapLabel_" + id; p.MouseFilter = MouseFilterEnum.Ignore;
            Dock(p, -83, 8, 83, 78, point.X, point.Y, point.X, point.Y);
            var text = Text((current ? "● " : "") + ChapterContent.Places[id].Name + "\n" + (id == "A01" || id == "A05" && Session.State.World.Contains("station") ? "安全撤离" : "探索地点"), 16, current ? Accent : Ink);
            text.HorizontalAlignment = HorizontalAlignment.Center; p.AddChild(text);
        }
        var back = Button("close_panel", "返回场景 [Esc]", () => { _panel = ""; Render(); }, true, true);
        _root.AddChild(back); Dock(back, -240, 134, -36, 182, al: 1, ar: 1);
    }
}
