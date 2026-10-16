using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private long _clickMoveId, _spacePlanItem;
    private string _clickMoveSource = "", _pickupContainer = "";
    private readonly List<long> _pickupOrder = new();
    private Dictionary<long, int> _pickupCounts = new();
    private readonly HashSet<long> _stockArrivals = new();
    private void StockTarget(Node parent, string destination)
    {
        var item = Session.State.Stock.FirstOrDefault(i => i.Id == _clickMoveId && i.Container == _clickMoveSource);
        if (item == null) return;
        var legal = destination == StockDestination(item);
        parent.AddChild(Button("stock_target_" + destination, UiCopy.Text("UI_05C2E26D15DD") + ChapterContent.ItemName(item.Kind) + " ×" + ChosenQuantity(item), () =>
        { _clickMoveId = 0; RequestStockMove(item.Id, item.Container, ChosenQuantity(item), true); }, legal, legal));
    }
    private void PreviewBatchDiscard(ChapterItem[] items)
    {
        var eligible = items.Where(i => !i.KeepOnPerson && !i.FreeSupply && !i.Kind.StartsWith('T')).ToDictionary(i => i.Id, ChosenQuantity);
        var copy = "永久放弃（不返还材料）：\n" + string.Join("\n", items.Where(i => eligible.ContainsKey(i.Id)).Select(i => ChapterContent.ItemName(i.Kind) + " ×" + eligible[i.Id]))
            + "\n\n跳过：\n" + string.Join("\n", items.Where(i => !eligible.ContainsKey(i.Id)).Select(i => ChapterContent.ItemName(i.Kind) + (i.KeepOnPerson ? " · 保留随身" : i.FreeSupply ? " · 配发物品" : " · 任务物")));
        Confirm(UiCopy.Text("UI_2FA0F23870AE"), copy, () => Change(() => Session.DiscardBatch(eligible)), UiCopy.Text("UI_07314583B0BF"));
    }
    private void PickupPlan(VBoxContainer box)
    {
        if (!Session.State.Ground.TryGetValue(_pickupContainer, out var ground) || ground.Place != Session.State.Location) { box.AddChild(Text(UiCopy.Text("UI_015B2DA0F3C6"), 18, Muted)); return; }
        var items = Session.State.Stock.Where(i => i.Container == _pickupContainer).ToArray();
        _pickupOrder.RemoveAll(id => items.All(i => i.Id != id)); foreach (var item in items) if (!_pickupOrder.Contains(item.Id)) _pickupOrder.Add(item.Id);
        box.AddChild(Text(ground.Name + UiCopy.Text("UI_B20900D3CE2B"), 18, Ink));
        box.AddChild(Button("pickup_suggest", UiCopy.Text("UI_85AE2CA604DF"), () => { _pickupCounts = Session.FittingPickup(_pickupContainer, _pickupOrder); Render(); }));
        var split = Row(12); split.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(split);
        var take = Section(split, 1.5f); take.AddChild(Text("装入背包", 20, Ink)); var left = Section(split, .7f); left.AddChild(Text("留在原地", 20, Ink));
        var takeList = Scroll(take); var leftList = Scroll(left);
        foreach (var id in _pickupOrder)
        {
            var item = items.Single(i => i.Id == id); var count = Math.Clamp(_pickupCounts.GetValueOrDefault(id), 0, item.Quantity);
            var row = Row(8); takeList.AddChild(row);
            var itemName = Text(ChapterContent.ItemName(item.Kind) + " ×" + item.Quantity, 16, Ink);
            itemName.SizeFlagsHorizontal = SizeFlags.ExpandFill; itemName.CustomMinimumSize = new Vector2(140, 0); itemName.SizeFlagsVertical = SizeFlags.ShrinkCenter; row.AddChild(itemName);
            var amount = new SpinBox { Name = "PickupCount_" + id, MinValue = 0, MaxValue = item.Quantity, Step = 1, Value = count, CustomMinimumSize = new Vector2(100, 40) }; row.AddChild(amount);
            amount.ValueChanged += v => { if (v == 0) _pickupCounts.Remove(id); else _pickupCounts[id] = (int)v; };
            row.AddChild(Button("pickup_up_" + id, "↑", () => { var index = _pickupOrder.IndexOf(id); if (index > 0) { _pickupOrder.RemoveAt(index); _pickupOrder.Insert(index - 1, id); } Render(); }, _pickupOrder.IndexOf(id) > 0));
            Buttons["pickup_up_" + id].TooltipText = "提高自动装包时的优先顺序";
            if (count < item.Quantity) leftList.AddChild(Text(ChapterContent.ItemName(item.Kind) + " ×" + (item.Quantity - count), 16, Muted));
        }
        var summary = Text("", 16, Ink); box.AddChild(summary);
        var actions = Row(); box.AddChild(actions); actions.AddChild(Button("pickup_plan_return", UiCopy.Text("UI_CF5BE93DB83B"), ClosePanel));
        var commit = Button("confirm_action", UiCopy.Text("UI_8211EBE3B592"), () => { var plan = new Dictionary<long, int>(_pickupCounts); ClosePanel(); Change(() => Session.PickupSelected(_pickupContainer, plan)); }, primary: true); actions.AddChild(commit);
        void Update()
        {
            var preview = Session.Preview(s => s.PickupSelected(_pickupContainer, _pickupCounts));
            commit.Disabled = preview == null;
            summary.Text = preview == null ? UiCopy.Text("UI_F6F870BFF961")
                : preview.State.Phase == ChapterPhase.Battle ? UiCopy.Text("UI_4CCC3A3B1BBF")
                : UiCopy.Format("UI_D77D5FE606C1", _pickupCounts.Values.Sum(), Session.CarryUsed, preview.CarryUsed, preview.CarryCapacity, Session.CarriedGrams / 1000m, preview.CarriedGrams / 1000m, preview.State.Pressure - Session.State.Pressure);
            foreach (var child in leftList.GetChildren()) { leftList.RemoveChild(child); child.QueueFree(); }
            foreach (var item in items) if (_pickupCounts.GetValueOrDefault(item.Id) < item.Quantity) leftList.AddChild(Text(ChapterContent.ItemName(item.Kind) + " ×" + (item.Quantity - _pickupCounts.GetValueOrDefault(item.Id)), 16, Muted));
        }
        foreach (var amount in takeList.GetChildren().SelectMany(n => n.GetChildren()).OfType<SpinBox>()) amount.ValueChanged += _ => Update();
        Update();
    }
    private void SpacePlan(VBoxContainer box)
    {
        var target = Session.State.Stock.FirstOrDefault(i => i.Id == _spacePlanItem && Session.State.Ground.ContainsKey(i.Container));
        if (target == null)
        {
            box.AddChild(Text("物品已离开地面，空间取舍已完成。", 20, Success));
            box.AddChild(Button("space_return", "返回物资", ClosePanel, primary: true)); return;
        }
        var count = ChosenQuantity(target); var space = Session.PickupSpace(target.Id, count);
        var targetRow = Row(14); box.AddChild(targetRow); ItemVisual(targetRow, target.Kind, 52);
        var targetCopy = FlexibleColumn(targetRow);
        targetCopy.AddChild(Text("准备拿取 · " + ChapterContent.ItemName(target.Kind) + " ×" + count, 20, Ink));
        targetCopy.AddChild(Text(CapacityText(Session.CarryUsed, Session.CarryCapacity) + $" · 还需腾出 {Math.Max(0, space.Required - space.Capacity)} 格", 16, Accent));
        targetCopy.AddChild(Text(ItemPurpose(target.Kind), 16, Muted));
        var page = Scroll(box);
        foreach (var item in Session.CarriedItems)
        {
            var afterDrop = Session.Preview(s => s.Drop(item.Id, ChosenQuantity(item))); if (afterDrop == null) continue;
            var afterTake = afterDrop.State.Phase == ChapterPhase.Exploring ? afterDrop.Preview(s => s.Pickup(target.Id, count)) : null;
            var row = Row(12); page.AddChild(row); ItemVisual(row, item.Kind, 44);
            var copy = FlexibleColumn(row); var amount = ChosenQuantity(item);
            copy.AddChild(Text(ChapterContent.ItemName(item.Kind) + " ×" + amount + (item.Kind.StartsWith('T') ? " · 任务物" : "") + (item.KeepOnPerson ? " · 保留随身" : ""), 16, Ink));
            copy.AddChild(Text(afterDrop.State.Phase == ChapterPhase.Battle ? "巡逻会先拦截，本次不会放下物品" : $"腾出 {Session.CarryUsed - afterDrop.CarryUsed} 格 · 放下后 {afterDrop.CarriedGrams / 1000m:0.##} kg\n" + (afterTake == null ? "仍不能装入目标物品" : afterTake.State.Phase == ChapterPhase.Battle ? "随后拿取会遇到巡逻拦截" : $"再拿取后：{afterTake.CarryUsed}/{afterTake.CarryCapacity} 格 · {afterTake.CarriedGrams / 1000m:0.##} kg · 拿取压力 +{afterTake.State.Pressure - afterDrop.State.Pressure}"), 14, afterTake == null || afterTake.State.Phase == ChapterPhase.Battle ? Muted : Success));
            row.AddChild(Button("space_drop_" + item.Id, $"放下{ChapterContent.ItemName(item.Kind)} ×{amount}\n压力 +{afterDrop.State.Pressure - Session.State.Pressure}", () => RequestStockMove(item.Id, item.Container, amount, true)));
            Rule(page);
        }
        box.AddChild(Text("先放下，再拿取；每一步单独结算区域压力和巡逻。放下的物品留在地面。", 16, Muted));
        var pickup = Session.Preview(s => s.Pickup(target.Id, count));
        box.AddChild(Button("space_pickup", "拿取 " + ChapterContent.ItemName(target.Kind) + " ×" + count + (pickup == null ? " · 空间不足" : pickup.State.Phase == ChapterPhase.Battle ? " · 将被巡逻拦截" : $" · 压力 +{pickup.State.Pressure - Session.State.Pressure}"), () => RequestStockMove(target.Id, target.Container, count, true), Session.CanPickup(target.Id, count), true));
        box.AddChild(Button("space_return", UiCopy.Text("UI_4315003F98F1"), ClosePanel));
    }
}
