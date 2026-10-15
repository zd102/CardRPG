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
        AddLoadMeter(box, "InventoryWeightMeter", Session.CarriedGrams, Session.CarriedGrams, ChapterSession.WeightLimitGrams);
        box.AddChild(Text(Session.Overweight ? UiCopy.Text("UI_0F204A4762CC") : UiCopy.Text("UI_051EE0746A8E"), 14, Session.Overweight ? Danger : Muted));
        if (Session.State.LegacyOverCapacity)
            box.AddChild(Text(UiCopy.Text("UI_6005B333F24B"), 16, Danger));
    }
    private void StockRow(VBoxContainer box, ChapterItem item, bool ground)
    {
        var def = ChapterContent.Items[item.Kind];
        var row = Row(16); box.AddChild(row);
        var info = Row(8); info.CustomMinimumSize = new Vector2(228, 0); row.AddChild(info);
        if (item.Kind is "U01" or "M08") info.AddChild(Glyph("supply", Accent, 40));
        else Illustration(info, ItemArt(item.Kind), 42, 44, false);
        var words = FlexibleColumn(info);
        words.AddChild(Text($"{def.Name} ×{item.Quantity}" + (item.FreeSupply ? UiCopy.Text("UI_042308E133F8") : item.Calibrated ? UiCopy.Text("UI_916E1FD310AB") : ""), 16, Ink));
        words.AddChild(Text(ContainerName(item.Container) + $" · {def.Grams * item.Quantity / 1000m:0.##} kg" + (item.KeepOnPerson ? UiCopy.Text("UI_CDDBA48664BF") : ""), 14, Muted));
        var forecast = FlexibleColumn(row); forecast.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var count = ChosenQuantity(item);
        var predicted = Session.Preview(copy => StockCommand(copy, item.Id, item.Container, count, true));
        var summary = predicted == null ? QuantityForecast(item, count) : predicted.State.Phase == ChapterPhase.Battle ? "巡逻先拦截 · 本次物品不移动"
            : $"{ContainerName(item.Container)} → {ContainerName(StockDestination(item))} · {count} 件\n推进 {predicted.State.Pressure - Session.State.Pressure} 步";
        _dragForecast = Text(summary, 16, Ink); forecast.AddChild(_dragForecast);
        var actions = Row(5); actions.SizeFlagsVertical = SizeFlags.ShrinkCenter; row.AddChild(actions);
        if (ground)
        {
            actions.AddChild(Button((item.Quantity > 1 ? "pickup_stack_" : "pickup_") + item.Id, UiCopy.Format("UI_3541B21D9AA0", count), () => RequestStockMove(item.Id, item.Container, count), Session.CanPickup(item.Id, count), true));
        }
        else if (Session.State.Phase == ChapterPhase.Camp)
        {
            var destination = item.Container == "carry" ? "warehouse" : "carry";
            var label = destination == "carry" ? "带入" : "存仓";
            actions.AddChild(Button((item.Quantity > 1 ? "transfer_stack_" : "transfer_") + item.Id, label + $" ×{count}", () => RequestStockMove(item.Id, item.Container, count), Session.CanTransfer(item.Id, destination, count), true));
        }
        else if (Session.State.Phase == ChapterPhase.Exploring && item.Container == "carry")
        {
            actions.AddChild(Button((item.Quantity > 1 ? "drop_stack_" : "drop_") + item.Id, UiCopy.Format("UI_0D997F80F7CF", count), () => RequestStockMove(item.Id, item.Container, count)));
        }
        actions.AddChild(Button("stock_quantity", UiCopy.Text("UI_36CD0B67C8B3"), () => OpenQuantity(item), Session.State.Phase is ChapterPhase.Camp or ChapterPhase.Exploring));
        actions.AddChild(Button("stock_more", UiCopy.Text("UI_6B28AAEB0C00"), () => OpenStockMenu(item.Id)));
    }
    private static string ItemArt(string kind) => kind.StartsWith("BP") ? "satchel" : kind switch
    { "M01" => "steel", "M02" => "wire", "M03" => "spring", "M07" => "T14", "M08" or "U01" => "T01", "M09" => "T01", "M10" => "G25", _ => kind };
    private void ConfirmDiscard(ChapterItem item) => Confirm(UiCopy.Text("UI_385C499B4D2E"), UiCopy.Format("UI_4B9EFB955D1E", ChapterContent.ItemName(item.Kind), item.Quantity), () => Change(() => Session.DiscardAtBase(item.Id, item.Quantity)), UiCopy.Text("UI_51D2F573E92D"));
}
