using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private bool _stockPaired;
    private bool _pairedAwaitBatch;
    private string _pairedIdentity = "", _pairedSource = "";
    private static string StockIdentity(ChapterItem item) => $"{item.Kind}_{item.FreeSupply}_{item.Calibrated}_{item.Registered}";

    private void CapacityMeter(Node parent, string id, string title, int current, int predicted, int capacity)
    {
        parent.AddChild(Text(UiCopy.Format("UI_2AB391448B7D", title, current, predicted, capacity) + (predicted > capacity ? UiCopy.Format("UI_626EE0D8E0E9", predicted - capacity) : UiCopy.Format("UI_A3C3F394DC76", capacity - predicted)), 14, predicted > capacity ? Danger : Ink));
        AddLoadMeter(parent, id, current, predicted, capacity);
    }
    private void AddLoadMeter(Node parent, string id, int current, int predicted, int limit)
    {
        var meter = new ChapterLoadMeter { Name = id, Current = current, Predicted = predicted, Limit = limit, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        meter.SetMeta("current", current); meter.SetMeta("predicted", predicted); meter.SetMeta("limit", limit); parent.AddChild(meter);
    }
    private void WeightMeter(Node parent, int predicted)
    {
        parent.AddChild(Text(UiCopy.Format("UI_4AB383363BBD", Session.CarriedGrams / 1000m, predicted / 1000m) + (predicted > ChapterSession.WeightLimitGrams ? UiCopy.Format("UI_42EED919022C", (predicted - ChapterSession.WeightLimitGrams) / 1000m) : UiCopy.Text("UI_88989631843C")), 14, predicted > ChapterSession.WeightLimitGrams ? Danger : Ink));
        AddLoadMeter(parent, "StockWeightMeter", Session.CarriedGrams, predicted, ChapterSession.WeightLimitGrams);
    }

    private void PairedStockTable(Node parent, ChapterItem[] local)
    {
        var container = Section(parent); container.Name = "PairedStockWorkspace";
        container.AddChild(Text(UiCopy.Text("UI_DE49E69A94E9"), 16, Ink));
        var page = Scroll(container, "paired_stock");
        var table = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        table.AddThemeConstantOverride("h_separation", 10); table.AddThemeConstantOverride("v_separation", 6); page.AddChild(table);
        table.AddChild(Text(UiCopy.Text("UI_359B34B424EA"), 16, Ink)); table.AddChild(Text(UiCopy.Text("UI_B4666BF14344"), 16, Ink)); table.AddChild(Text(UiCopy.Text("UI_89B2ECBE1F09"), 16, Ink));
        foreach (var group in local.Where(StockMatches).GroupBy(StockIdentity).OrderBy(g => _stockSort == "weight" ? -g.Sum(i => ChapterContent.Items[i.Kind].Grams * i.Quantity) : _stockSort == "quantity" ? -g.Sum(i => i.Quantity) : 0).ThenBy(g => _stockSort == "name" ? ChapterContent.ItemName(g.First().Kind) : g.Key, StringComparer.CurrentCulture))
        {
            var item = group.First(); var key = group.Key;
            table.AddChild(Text(ChapterContent.ItemName(item.Kind) + (item.FreeSupply ? UiCopy.Text("UI_042308E133F8") : "") + (item.Calibrated ? UiCopy.Text("UI_15E142EA28E2") : "")
                + (ChapterContent.Gear.ContainsKey(item.Kind) ? item.Registered ? UiCopy.Text("UI_0FE06A373E0B") : UiCopy.Text("UI_53641F86EF59") : ""), 16, Ink));
            foreach (var source in new[] { "carry", "warehouse" })
            {
                var batches = group.Where(i => i.Container == source).ToArray(); var count = batches.Sum(i => i.Quantity);
                var button = Button("paired_" + key + "_" + source, UiCopy.Format("UI_BE0211EB1631", count, batches.Length) + (_pairedIdentity == key && _pairedSource == source ? " ◀" : ""), () => { _pairedIdentity = key; _pairedSource = source; _pairedAwaitBatch = true; Render(); }, count > 0);
                button.SetMeta("quantity", count); button.SetMeta("source", source); table.AddChild(button);
            }
            if (_pairedIdentity != key) continue;
            var batchesInSource = group.Where(i => i.Container == _pairedSource).OrderBy(i => i.Id).ToArray();
            foreach (var batch in batchesInSource)
            {
                table.AddChild(Text(UiCopy.Format("UI_6238EDE8BE6C", batch.Id) + (batch.KeepOnPerson ? UiCopy.Text("UI_CDDBA48664BF") : ""), 14, Muted));
                foreach (var source in new[] { "carry", "warehouse" })
                {
                    if (source != batch.Container) { table.AddChild(Text("—", 14, Muted)); continue; }
                    var button = Button("paired_batch_" + batch.Id, (!_pairedAwaitBatch && _selectedStock == batch.Id ? "◆ " : "") + UiCopy.Format("UI_76F7D5E12F41", batch.Quantity), () => { _selectedStock = batch.Id; _pairedAwaitBatch = false; Render(); });
                    button.SetMeta("item_id", batch.Id); BindStockPointer(button, batch); table.AddChild(button);
                }
            }
        }
        if (!local.Any(StockMatches)) page.AddChild(Text(UiCopy.Text("UI_03078FD783FB"), 16, Muted));
        var index = 0;
        foreach (var cell in table.GetChildren().OfType<Control>())
        {
            cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            cell.CustomMinimumSize = new Vector2(index++ % 3 == 0 ? 220 : 140, 0);
        }
    }
}
