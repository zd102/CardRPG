using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private sealed record UiReceipt(string Command, bool Success, string Details, long ItemId, string Kind);
    private readonly List<UiReceipt> _receipts = new();
    private string _commandCaption = "操作";
    private void Receipt(ChapterState before, bool success, string reason = "")
    {
        var changes = new List<string>();
        foreach (var id in before.Stock.Select(i => i.Id).Union(Session.State.Stock.Select(i => i.Id)))
        {
            var old = before.Stock.FirstOrDefault(i => i.Id == id); var next = Session.State.Stock.FirstOrDefault(i => i.Id == id);
            if (old?.Container == next?.Container && old?.Quantity == next?.Quantity) continue;
            changes.Add(ChapterContent.ItemName((next ?? old)!.Kind) + $" · 批次 #{id} · " + (old == null ? "新增" : ContainerName(old.Container) + " ×" + old.Quantity) + " → " + (next == null ? "已消耗 / 移除" : ContainerName(next.Container) + " ×" + next.Quantity));
        }
        var selected = Session.State.Stock.FirstOrDefault(i => i.Id == _selectedStock);
        var detail = reason + (changes.Count == 0 ? "" : "\n" + string.Join("\n", changes));
        if (success && detail == "") detail = "命令已完成。";
        _receipts.Add(new(_commandCaption, success, detail, selected?.Id ?? 0, selected?.Kind ?? ""));
        if (_receipts.Count > 30) _receipts.RemoveAt(0);
    }
    private void RecentReceipts(VBoxContainer box)
    {
        box.AddChild(Text(UiCopy.Text("UI_168C68EDC0DF"), 18, Ink)); var page = Scroll(box);
        foreach (var receipt in _receipts.AsEnumerable().Reverse())
        {
            var entry = Column(6); page.AddChild(entry);
            entry.AddChild(Text((receipt.Success ? UiCopy.Text("UI_40816FB67BB5") : UiCopy.Text("UI_939466C09A16")) + receipt.Command, 18, receipt.Success ? Ink : Danger));
            entry.AddChild(Text(receipt.Details, 16, Ink));
            var item = Session.State.Stock.FirstOrDefault(i => i.Id == receipt.ItemId && i.Kind == receipt.Kind);
            if (item != null && (item.Container == "carry" || Session.AtBase && item.Container == "warehouse" || Session.GroundHere.Any(g => g.Key == item.Container)))
                entry.AddChild(Button("receipt_object_" + page.GetChildCount(), UiCopy.Text("UI_DE5E8CC0267A"), () => OpenStockObject(item.Id)));
            else if (receipt.ItemId != 0) entry.AddChild(Text(UiCopy.Text("UI_C731608BC09C"), 14, Muted));
            Rule(entry);
        }
        if (_receipts.Count == 0) page.AddChild(Text(UiCopy.Text("UI_1F06115AA0DA"), 16, Muted));
        box.AddChild(Button("receipts_return", UiCopy.Text("UI_572CF45BA436"), ClosePanel));
    }
}
