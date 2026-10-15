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
        var take = Section(split); take.AddChild(Text(UiCopy.Text("UI_87E813FCA206"), 20, Ink)); var left = Section(split); left.AddChild(Text(UiCopy.Text("UI_4EBFB013B6A0"), 20, Ink));
        var takeList = Scroll(take); var leftList = Scroll(left);
        foreach (var id in _pickupOrder)
        {
            var item = items.Single(i => i.Id == id); var count = Math.Clamp(_pickupCounts.GetValueOrDefault(id), 0, item.Quantity);
            var row = Row(8); takeList.AddChild(row);
            row.AddChild(Text(ChapterContent.ItemName(item.Kind), 16, Ink));
            var amount = new SpinBox { Name = "PickupCount_" + id, MinValue = 0, MaxValue = item.Quantity, Step = 1, Value = count, CustomMinimumSize = new Vector2(100, 40) }; row.AddChild(amount);
            amount.ValueChanged += v => { if (v == 0) _pickupCounts.Remove(id); else _pickupCounts[id] = (int)v; };
            row.AddChild(Button("pickup_up_" + id, UiCopy.Text("UI_7657191DAB75"), () => { var index = _pickupOrder.IndexOf(id); if (index > 0) { _pickupOrder.RemoveAt(index); _pickupOrder.Insert(index - 1, id); } Render(); }, _pickupOrder.IndexOf(id) > 0));
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
        if (target == null) { box.AddChild(Text(UiCopy.Text("UI_01FABC89DC36"), 18, Muted)); return; }
        var count = ChosenQuantity(target); var space = Session.PickupSpace(target.Id, count);
        box.AddChild(Text(UiCopy.Format("UI_3A08AE5A5280", ChapterContent.ItemName(target.Kind), count, space.Required, space.Capacity, Math.Max(0, space.Required - space.Capacity)), 18, Ink));
        var page = Scroll(box);
        foreach (var item in Session.CarriedItems)
        {
            var afterDrop = Session.Preview(s => s.Drop(item.Id, ChosenQuantity(item))); if (afterDrop == null) continue;
            var afterTake = afterDrop.State.Phase == ChapterPhase.Exploring ? afterDrop.Preview(s => s.Pickup(target.Id, count)) : null;
            page.AddChild(Text(UiCopy.Format("UI_A9CF50EC0F96", ChapterContent.ItemName(item.Kind), ChosenQuantity(item), afterDrop.State.Pressure - Session.State.Pressure) + (afterDrop.State.Phase == ChapterPhase.Battle ? UiCopy.Text("UI_3345ED11D5DA") : afterTake == null ? UiCopy.Text("UI_636A22ADC195") : UiCopy.Format("UI_A8520325B2F4", afterTake.State.Pressure - afterDrop.State.Pressure) + (afterTake.State.Phase == ChapterPhase.Battle ? UiCopy.Text("UI_09F6DD13AD1F") : UiCopy.Format("UI_4F5A172A0874", afterTake.CarryUsed, afterTake.CarryCapacity, afterTake.CarriedGrams / 1000m))), 16, Ink));
            page.AddChild(Button("space_drop_" + item.Id, UiCopy.Text("UI_8AF784339D06"), () => RequestStockMove(item.Id, item.Container, ChosenQuantity(item), true)));
        }
        box.AddChild(Text(UiCopy.Text("UI_04109121F0E5"), 16, Muted));
        box.AddChild(Button("space_pickup", UiCopy.Text("UI_00002E9859FD"), () => RequestStockMove(target.Id, target.Container, count, true), Session.CanPickup(target.Id, count)));
        box.AddChild(Button("space_return", UiCopy.Text("UI_4315003F98F1"), ClosePanel));
    }
}
