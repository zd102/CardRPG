using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private void LootSummary(VBoxContainer box)
    {
        var page = Scroll(box); page.AddChild(Text("敌人已被击败。选择装包后，物品才会随你撤离。", 18, Ink));
        foreach (var item in Session.State.Stock.Where(i => Session.State.Ground.TryGetValue(i.Container, out var ground) && ground.Place == Session.State.Location))
            page.AddChild(Text(ChapterContent.ItemName(item.Kind) + " ×" + item.Quantity + " · 地面", 20, Ink));
        page.AddChild(Text(Session.LoadDescription, 16, Muted));
        box.AddChild(Button("loot_manage", "整理战利品 →", () => { _panel = "gear"; _gearFilter = "cargo"; Render(); }, primary: true));
    }

    private void Preparation(VBoxContainer box)
    {
        var s = Session.State; var camp = s.Phase == ChapterPhase.Camp;
        var split = Row(12); split.Name = "PreparationWorkspace"; split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        LoadoutColumn(split);
        var route = Section(split, 1.2f);
        route.AddChild(Text("外环 · 部署地点", 20, Ink));
        var map = new ChapterRouteMap { Name = "DeploymentMap", Current = s.Start, Selected = s.Start,
            Revealed = ChapterContent.Places.Keys.Where(Session.IsRevealed).ToHashSet(), StationPowered = s.World.Contains("station"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 220) };
        route.AddChild(map);
        route.AddChild(Text(Session.NextObjective, 16, Ink));
        foreach (var id in new[] { "A01", "A05" })
        {
            var place = id;
            route.AddChild(Button("start_" + id, (s.Start == id ? "已选 · " : "") + ChapterContent.Places[id].Name + (id == "A05" && !s.Done.Contains("S01") ? " · 完成货站任务解锁" : ""),
                () => Change(() => Session.SetStart(place)), camp && (id == "A01" || s.Done.Contains("S01"))));
        }
        var right = Section(split); var manifest = Scroll(right);
        manifest.AddChild(Text("出发清单", 20, Ink));
        if (s.Extractions == 0)
        {
            manifest.AddChild(Text("选择初始工具", 16, Ink));
            foreach (var id in new[] { "G01", "G02" })
            {
                var kind = id;
                manifest.AddChild(Button("starter_" + id, (s.Weapon == id ? "已选 · " : "") + ChapterContent.ItemName(id), () => Change(() => Session.SelectStarter(kind)), camp));
            }
            manifest.AddChild(Text("首趟可以不战斗，两种工具都能完成任务。", 16, Muted));
        }
        manifest.AddChild(Button("free_supplies", s.FreeSupplyAllowance == 2 ? "✓ 配发补给 ×2 · 取消" : "领取免费补给 ×2", () => Change(() => Session.SetFreeSupplies(s.FreeSupplyAllowance == 2 ? 0 : 2)), camp));
        manifest.AddChild(Text("补给合占 1 格，本趟结束清除。", 14, Muted));
        foreach (var item in Session.CarriedItems) manifest.AddChild(Text(ChapterContent.ItemName(item.Kind) + " ×" + item.Quantity, 16, Ink));
        if (s.StoredItems.Contains("T02") && !s.StoredCalibration) manifest.AddChild(Button("carry_T02", "带上夹具，出征校准", () => Change(() => Session.ToggleCarryItem("T02")), camp));
        var load = Session.DepartureLoad();
        Rule(right);
        right.AddChild(Text($"携带 {load.Slots} / {load.Capacity} 格", 20, Ink));
        right.AddChild(Text($"出发负重 {load.Grams / 1000m:0.##} / 20 kg", 20, load.Grams > ChapterSession.WeightLimitGrams ? Danger : Ink));
        var finalState = Text($"生命 42/42 · 配发 {s.FreeSupplyAllowance} 份\n仓库返还预留 {load.Reserved} 格", 16, Muted); finalState.Name = "DepartureManifest"; right.AddChild(finalState);
        if (load.Grams > ChapterSession.WeightLimitGrams) right.AddChild(Text("超重：场景行动值 ×2。", 16, Danger));
        right.AddChild(Button("prepare_equipment", "调整穿戴与备用", () => { _gearFilter = "all"; OpenNested("gear"); }));
        right.AddChild(Button("prepare_storage", "整理随身与仓库", () => { OpenNested("gear"); _gearFilter = "cargo"; Render(); }));
        if (!Session.CanDepart) right.AddChild(Text(camp ? load.Slots > load.Capacity ? $"需腾出 {load.Slots - load.Capacity} 格" : $"仓库需预留 {load.Reserved} 格，现空余 {Session.WarehouseCapacity - Session.WarehouseUsed} 格" : "回到据点后可以出发。", 16, Danger));
        right.AddChild(Button("prepare_depart", "部署 · " + ChapterContent.Places[s.Start].Name + " →", () => { _panel = ""; _panelHistory.Clear(); Change(Session.Depart); }, camp && Session.CanDepart, true));
    }

    private void CharacterOverview(VBoxContainer box)
    {
        var s = Session.State; var battle = s.Battle;
        var equipment = (battle?.Equipment ?? Session.Equipped.ToList()).ToArray();
        var cards = battle == null ? Session.Deck() : battle.Hand.Concat(battle.Draw).Concat(battle.Discard).ToList();
        var summary = Row(20); box.AddChild(summary);
        var hp = Inline($"{s.Health} / {ChapterSession.MaxHealth}", 28, Ink); hp.Name = "OverviewHealth"; summary.AddChild(hp);
        summary.AddChild(Inline("生命", 16, Muted));
        summary.AddChild(Inline($"行动 {battle?.Action ?? 3} · 魔法 {battle?.Mana ?? 3} · 护盾 {battle?.Shield ?? 0}", 16, Ink));
        box.AddChild(Text((battle?.Id == "trial" ? "借用构筑 · 不改变真实穿戴" : "当前构筑") + $" · 总牌数 {cards.Count} · 攻击 {cards.Count(id => ChapterContent.Cards[id].Damage > 0)} / 防护 {cards.Count(id => ChapterContent.Cards[id].Shield > 0)} / 抽牌 {cards.Count(id => id is "C26" or "C36")}（分类可重叠）", 16, Ink));
        var split = Row(16); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var wear = Directory(split); var board = Scroll(wear); EquipmentBoard(board, equipment, "overview", true);
        board.AddChild(Text(Session.LoadDescription, 16, Ink));
        board.AddChild(Text(s.Resonance switch { "R01" => "余烬护层：首回合 +5 护盾", "R07" => "厚壁余温：每回合首次获得至少 7 护盾时额外 +2", _ => "无临时共鸣" }, 16, Muted));
        wear.AddChild(Button("overview_inventory", "管理随身与仓库 →", () => { OpenNested("gear"); _gearFilter = "cargo"; Render(); }));
        var deck = Section(split); var title = Text($"完整卡组 · {cards.Count} 张", 20, Ink); title.Name = "OverviewDeckCount"; deck.AddChild(title);
        var list = Scroll(deck); list.Name = "OverviewDeck";
        foreach (var group in cards.GroupBy(c => c).OrderBy(g => g.Key))
        {
            var card = ChapterContent.Cards[group.Key]; var row = Column(4); list.AddChild(row);
            var sources = string.Join("、", equipment.Where(id => (battle?.Id == "trial" ? ChapterContent.Gear[id].Cards : Session.GearCards(id)).Contains(card.Id)).Select(ChapterContent.ItemName));
            var button = Button("overview_card_" + card.Id, card.Name + $" ×{group.Count()} · {card.Action} 行动 / {card.Mana} 魔法", () => { });
            button.SetMeta("copies", group.Count()); button.Alignment = HorizontalAlignment.Left;
            button.TooltipText = card.Name + "\n" + card.Text + "\n来源：" + sources; row.AddChild(button);
            var effect = Row(8); row.AddChild(effect); effect.AddChild(Glyph(CardSymbol(card), Ink, 24));
            var copy = Text(card.Text, 16, Ink); copy.SizeFlagsHorizontal = SizeFlags.ExpandFill; effect.AddChild(copy);
            row.AddChild(Text("来源 · " + sources, 14, Muted)); Rule(row);
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
                if (items.Count == 0) page.AddChild(Text("无", 16, Muted));
                foreach (var (kind, count) in items) page.AddChild(Text(ChapterContent.ItemName(kind) + " ×" + count, 16, Ink));
            }
            if (ok)
            {
                Items("安全带回（含出发时携带）", report.Brought, Success);
                if (report.BaselineKnown) Items("本趟净增加", report.Gained, Success);
                else page.AddChild(Text("此趟从旧存档继续，未记录出发清单，无法回溯净增加。",16,Muted));
                if (report.NewGear.Count > 0) page.AddChild(Text("新登记装备：" + string.Join("、",report.NewGear.Select(ChapterContent.ItemName)),16,Success));
                var ready = Session.PreviewCopy(); ready.ReturnToCamp();
                page.AddChild(Text("接下来可以交付", 20, Ink));
                var quests = ChapterContent.Quests.Where(q => ready.CanTurnIn(q.Id)).ToArray();
                page.AddChild(Text(quests.Length == 0 ? "暂无可交付委托" : string.Join("、", quests.Select(q => q.Name)), 16, Accent));
            }
            else
            {
                Items("实际丢失", report.Lost, Danger);
                page.AddChild(Text("已保留", 20, Success));
                page.AddChild(Text("仓库物品、已完成的救援和修复。\n出发装备：" + string.Join("、", report.Preserved), 16, Ink));
                page.AddChild(Text("回营恢复生命；下趟可重新搜索任务物。已记录的领班首胜允许重新取回工具。", 16, Muted));
            }
            if (report.Changes.Count > 0)
            {
                page.AddChild(Text("永久变化", 20, Ink));
                foreach (var change in report.Changes) page.AddChild(Text(WorldChange(change), 16, Success));
            }
            if (report.Left.Count > 0)
            { page.AddChild(Text("本趟未带走（区域已重置）", 20, Muted)); foreach (var left in report.Left) page.AddChild(Text(left, 16, Muted)); }
        }
        page.AddChild(Text("配发补给在本趟结束时清除，不计入收获或普通物品损失。",14,Muted));
        Rule(page); page.AddChild(Text($"仓库总量 {Session.WarehouseUsed}/{Session.WarehouseCapacity} 格 · 仓满余物仍安全随身 {Session.CarryUsed}/{Session.CarryCapacity} 格", 14, Muted));
        body.AddChild(Button("camp", "回到余火站 →", () => Change(Session.ReturnToCamp), primary: true)); FocusLater(Buttons["camp"]);
    }

    private static string WorldChange(string key) => key switch
    { "robot-rescued" => "维修偶已获救", "robot-arrived" => "维修偶抵达余火站", "station" => "货站线路永久修复", "dark-door" => "发现荧光夹层入口", "shovel-inspected" => "发现清障铲与货单", "leader-won" => "军需领班首胜已记录", "trial-complete" => "检修手套试用完成", _ => "新的区域发现已保存" };
}
