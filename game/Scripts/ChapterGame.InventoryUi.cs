using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private void InventoryStatus(VBoxContainer box)
    {
        box.AddChild(Text(Session.LoadDescription, 16, Session.Overweight ? Danger : Ink));
        HealthBar(box, Math.Min(Session.CarriedGrams, ChapterSession.WeightLimitGrams), ChapterSession.WeightLimitGrams, Session.Overweight ? Danger : Success);
        box.AddChild(Text(Session.Overweight ? "超重 · 场景行动值 ×2" : "负重正常 · 场景行动值 ×1", 14, Session.Overweight ? Danger : Muted));
        if (Session.State.LegacyOverCapacity)
            box.AddChild(Text("旧档物品已保留。请整理超出容量的库存后再出发；可存取、制作、扩仓或放弃普通物品。", 16, Danger));
    }
    private void StockRow(VBoxContainer box, ChapterItem item, bool ground)
    {
        var def = ChapterContent.Items[item.Kind];
        var row = Row(16); box.AddChild(row);
        var info = Row(8); info.CustomMinimumSize = new Vector2(228, 0); row.AddChild(info);
        if (item.Kind is "U01" or "M08") info.AddChild(Glyph("supply", Accent, 40));
        else Illustration(info, ItemArt(item.Kind), 42, 44, false);
        var words = FlexibleColumn(info);
        words.AddChild(Text($"{def.Name} ×{item.Quantity}" + (item.FreeSupply ? " · 配发" : item.Calibrated ? " · 校准" : ""), 16, Ink));
        words.AddChild(Text($"每堆 {def.Stack} 件 · {def.Slots} 格 · {def.Grams / 1000m:0.##} kg/件", 14, Muted));
        var forecast = FlexibleColumn(row); forecast.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        forecast.AddChild(Text(StockForecast(item, ground), 16, Ink));
        var actions = new HFlowContainer { CustomMinimumSize = new Vector2(260, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        actions.AddThemeConstantOverride("h_separation", 5); actions.AddThemeConstantOverride("v_separation", 5); row.AddChild(actions);
        if (ground)
        {
            actions.AddChild(Button("pickup_" + item.Id, "拾取 1", () => Change(() => Session.Pickup(item.Id, 1)), Session.CanPickup(item.Id, 1)));
            if (item.Quantity > 1) actions.AddChild(Button("pickup_stack_" + item.Id, "拾取整批", () => Change(() => Session.Pickup(item.Id, item.Quantity)), Session.CanPickup(item.Id, item.Quantity)));
        }
        else if (Session.State.Phase == ChapterPhase.Camp)
        {
            var destination = item.Container == "carry" ? "warehouse" : "carry";
            var label = destination == "carry" ? "带入" : "存仓";
            actions.AddChild(Button("transfer_" + item.Id, label + " 1", () => Change(() => Session.Transfer(item.Id, destination, 1)), Session.CanTransfer(item.Id, destination, 1)));
            if (item.Quantity > 1) actions.AddChild(Button("transfer_stack_" + item.Id, label + "整批", () => Change(() => Session.Transfer(item.Id, destination, item.Quantity)), Session.CanTransfer(item.Id, destination, item.Quantity)));
            if (!item.Kind.StartsWith('T')) actions.AddChild(Button("discard_" + item.Id, "放弃…", () => ConfirmDiscard(item)));
        }
        else if (Session.State.Phase == ChapterPhase.Exploring && item.Container == "carry")
        {
            actions.AddChild(Button("drop_" + item.Id, "放下 1", () => Change(() => Session.Drop(item.Id, 1))));
            if (item.Quantity > 1) actions.AddChild(Button("drop_stack_" + item.Id, "放下整批", () => Change(() => Session.Drop(item.Id, item.Quantity))));
        }
    }
    private static string ItemArt(string kind) => kind.StartsWith("BP") ? "satchel" : kind switch
    { "M01" => "steel", "M02" => "wire", "M03" => "spring", "M07" => "T14", "M08" or "U01" => "T01", "M09" => "T01", "M10" => "G25", _ => kind };
    private void ConfirmDiscard(ChapterItem item) => Confirm("放弃实物", $"永久放弃 {ChapterContent.ItemName(item.Kind)} ×{item.Quantity}？\n此操作不返还材料。", () => Change(() => Session.DiscardAtBase(item.Id, item.Quantity)), "确认放弃");
    private void StorageCrafting(VBoxContainer page)
    {
        foreach (var kind in new[] { "BP01", "BP02", "BP03" })
        {
            var card = CardBox(page); var row = Row(12); card.AddChild(row); Illustration(row, "satchel", 72, 76);
            var copy = FlexibleColumn(row); copy.AddChild(Text(ChapterContent.ItemName(kind), 20, Ink));
            copy.AddChild(Text($"额外 {ChapterContent.BagCapacity(kind)} 格 · 自重 {ChapterContent.Items[kind].Grams / 1000m:0.##} kg · 不供牌", 16, Muted));
            Costs(card, Session.BackpackCost(kind));
            var reason = Session.CraftReason(kind); if (reason != "") card.AddChild(Text(reason, 16, Muted));
            var label = Session.HasPhysical(kind) ? "✓ 已持有" : reason == "" ? "制作背包" : "暂不可制作";
            card.AddChild(Button("make_" + kind, label, () => Change(() => Session.CraftBackpack(kind)), Session.CanCraftBackpack(kind), true));
        }
        var medical = CardBox(page); medical.AddChild(Text("密封补给 ×2", 20, Ink));
        medical.AddChild(Text("织物 1、药剂 1；普通份可留仓，出征免费份在本趟结束清除。", 16, Muted));
        Costs(medical, [0,0,0,1,1,0,0]);
        if (!Session.CanCraftSupplies) medical.AddChild(Text(Session.State.Phase != ChapterPhase.Camp ? "需回到据点" : Session.AvailableMaterial(3) < 1 || Session.AvailableMaterial(4) < 1 ? "请补齐标出的材料" : "仓库需腾出成品空间",16,Danger));
        medical.AddChild(Button("make_U01", "制作补给", () => Change(Session.CraftSupplies), Session.CanCraftSupplies));
        var warehouse = CardBox(page); warehouse.AddChild(Text($"仓库 · {Session.WarehouseUsed}/{Session.WarehouseCapacity} 格", 20, Ink));
        if (Session.State.WarehouseLevel == 3) warehouse.AddChild(Text("✓ 已完成全部扩建。",16,Success));
        else
        {
            Costs(warehouse, Session.WarehouseCost);
            if (!Session.CanUpgradeWarehouse) warehouse.AddChild(Text(Session.State.Phase != ChapterPhase.Camp ? "需回到据点" : Session.State.WarehouseLevel == 0 && !Session.State.Done.Contains("Q02") ? "需完成：有自己的工具" : Session.State.WarehouseLevel > 0 && !Session.SliceComplete ? "需完成外环主线与货站任务" : "请补齐标出的材料",16,Danger));
        }
        warehouse.AddChild(Button("upgrade_warehouse", "扩建仓库", () => Change(Session.UpgradeWarehouse), Session.CanUpgradeWarehouse, true));
    }
}
