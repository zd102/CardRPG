using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private string _stockFilter = "all";
    private bool _stockList;
    private bool _showStockEmpty;
    private bool _showStockLoadout = true;
    private string _stockSearch = "", _stockSort = "kind";

    private bool StockMatches(ChapterItem item) => StockCategory(item, _stockFilter) && (_stockGearSlot == "" || ChapterContent.Gear.TryGetValue(item.Kind, out var filterGear) && filterGear.Slot == _stockGearSlot) && (_stockSearch == "" || (ChapterContent.ItemName(item.Kind) + ItemPurpose(item.Kind)).Contains(_stockSearch, StringComparison.OrdinalIgnoreCase));
    private static bool StockCategory(ChapterItem item, string category) => category switch
    { "materials" => ChapterContent.MaterialIds.Contains(item.Kind), "gear" => ChapterContent.Gear.ContainsKey(item.Kind), "quest" => item.Kind.StartsWith('T'), "supply" => item.Kind == "U01", _ => true };

    private void StorageGrid(Node parent, IEnumerable<ChapterItem> stock, int capacity, string name)
    {
        var all = stock.ToArray();
        var list = _stockList || name == "carry";
        var grid = new ChapterSlotGrid { Name = "StorageGrid_" + name, ReadingScale = ReadingScale, Compact = CompactDensity, SizeFlagsHorizontal = SizeFlags.ExpandFill, Capacity = capacity, ShowEmpty = false, ListMode = list };
        parent.AddChild(grid);
        var used = ChapterSession.Slots(all);
        grid.SetMeta("occupied", used); grid.SetMeta("capacity", capacity);
        var groups = all.Where(StockMatches).GroupBy(StockGroupKey).Select(g => g.OrderBy(i => i.Id).ToArray())
            .OrderBy(g => _stockSort == "weight" ? -ChapterContent.Items[g[0].Kind].Grams * g.Sum(i => i.Quantity) : _stockSort == "quantity" ? -g.Sum(i => i.Quantity) : 0)
            .ThenBy(g => _stockSort == "name" ? ChapterContent.ItemName(g[0].Kind) : g[0].Kind, StringComparer.CurrentCulture).ThenBy(g => g[0].Id).ToArray();
        foreach (var bundle in groups)
        {
                var first = bundle[0]; var total = bundle.Sum(i => i.Quantity);
                var def = ChapterContent.Items[first.Kind];
                var caption = $"×{total}" + (first.FreeSupply ? " · 配发" : first.Calibrated ? " · 校准" : "") + (first.KeepOnPerson ? " · 保留" : "") + (bundle.Any(i => _stockArrivals.Contains(i.Id)) ? " · 新入" : "");
                if (list) caption += $" · {def.Grams * total / 1000m:0.##} kg";
                var tile = ItemTile($"stock_{first.Id}_0_0", first.Kind, def.Name, caption, () =>
                {
                    if (_quickMove && Input.IsKeyPressed(Key.Shift) && Session.State.Phase == ChapterPhase.Camp) RequestBundleMove(first, bundle.Length == 1 ? ChosenQuantity(first) : total);
                    else SelectStock(first);
                }, bundle.Any(i => i.Id == _selectedStock), list ? 50 : CompactDensity ? 110 : 126);
                tile.SetMeta("slots", 1); tile.SetMeta("item_id", first.Id); tile.SetMeta("item_ids", string.Join(",", bundle.Select(i => i.Id))); tile.SetMeta("quantity", total); tile.CustomMinimumSize = new Vector2(0, list ? CompactDensity ? 48 : 50 : CompactDensity ? 110 : 126);
                if (list)
                {
                    Dock(tile.GetChild<Control>(0), 12, 4, 54, 50);
                    var labels = new[]{tile.GetNode<Label>("ItemTitle"),tile.GetNode<Label>("ItemCaption")};
                    if(tile.GetNodeOrNull<Control>("CapacityBadge") is {} badge) {badge.Hide();tile.GetNode<Control>("BagBadge").Hide();labels[1].Text+=UiCopy.Format("UI_73A7582C28F9", 4 + ChapterContent.BagCapacity(first.Kind));}
                    Dock(labels[0], 62, 0, -8, 24 * ReadingScale, ar: 1);
                    Dock(labels[1], 62, 25 * ReadingScale, -8, (CompactDensity ? 47 : 49) * ReadingScale, ar: 1);
                    foreach (var label in labels) { label.HorizontalAlignment = HorizontalAlignment.Left; label.AutowrapMode = TextServer.AutowrapMode.Off; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; }
                    if (CompactDensity)
                    {
                        Dock(tile.GetChild<Control>(0), 12, 4, 54, 48);
                    }
                    if (_stockBatchMode)
                    {
                        Dock(tile.GetChild<Control>(0), 34, 4, 76, CompactDensity ? 48 : 50);
                        labels[0].OffsetLeft = labels[1].OffsetLeft = 84;
                    }
                }
                tile.TooltipText = def.Name + " · " + caption + "\n" + ItemPurpose(first.Kind) + UiCopy.Text("UI_67255B6133F5");
                if (Session.State.Phase == ChapterPhase.Camp && _stockBatchMode)
                {
                    tile.GetNodeOrNull<Control>("BagBadge")?.Hide();
                    var check = new CheckBox { Name = "SelectBatch_" + first.Id, ButtonPressed = bundle.All(i => _stockSelection.Contains(i.Id)), TooltipText = "选择此物品的全部同状态批次" };
                    tile.AddChild(check); Dock(check, 0, 0, 30, 30); check.Toggled += on => { foreach (var item in bundle) { if (on) _stockSelection.Add(item.Id); else _stockSelection.Remove(item.Id); } Render(); };
                }
                BindStockPointer(tile, first);
                grid.AddChild(tile);
        }
        if (capacity > 0)
        {
            if (groups.Length == 0) parent.AddChild(Text(all.Length == 0 ? name == "carry" ? "背包是空的\n选中物品后带入，穿戴背包可扩容。" : "仓库是空的" : "没有符合筛选的物品", 16, Muted));
            if (_expandedTools.Contains("stock")) parent.AddChild(Text($"显示 {groups.Length} 项 · {_stockBatches switch { true => "按批次", false => "同状态合并" }}\n容量按物品实际堆叠计算，图块不代表一格。", 14, Muted));
            var capacityToggle = QuietButton("storage_empty_" + name, _showStockEmpty ? "收起容量示意" : "查看容量示意", () => { _showStockEmpty = !_showStockEmpty; Render(); });
            capacityToggle.Visible = _expandedTools.Contains("stock"); parent.AddChild(capacityToggle);
            if (_showStockEmpty && capacity > used)
            {
                parent.AddChild(Text(UiCopy.Text("UI_268B3A9E15A4"), 14, Muted));
                parent.AddChild(new ChapterSlotGrid { Name = "EmptyCapacity_" + name, Capacity = capacity - used, ShowEmpty = true, ReadingScale = ReadingScale, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            }
        }
    }

    private void ConfirmPartialPickup(string key, Dictionary<long, int> fitting)
    {
        _pickupContainer = key; _pickupCounts = new(fitting); _pickupOrder.Clear();
        _pickupOrder.AddRange(Session.State.Stock.Where(i => i.Container == key).OrderByDescending(i => i.Kind.StartsWith('T')).ThenByDescending(i => i.KeepOnPerson).ThenBy(i => i.Id).Select(i => i.Id));
        OpenNested("pickup-plan");
    }
    private string StockForecast(ChapterItem item, bool ground)
    {
        if (Session.State.Phase is not (ChapterPhase.Camp or ChapterPhase.Exploring)) return "当前仅可查看物品，回营后可取放。";
        var next = Session.Preview(x => ground ? x.Pickup(item.Id, item.Quantity) : Session.State.Phase == ChapterPhase.Camp
            ? x.Transfer(item.Id, item.Container == "carry" ? "warehouse" : "carry", item.Quantity) : x.Drop(item.Id, item.Quantity));
        if (next == null) return "整批空间不足，可尝试单件；未移动物品保持原位。";
        if (next.State.Phase == ChapterPhase.Battle) return "巡逻将打断取放 · 本次物品不移动";
        var direction = ground ? "地面 → 随身" : Session.State.Phase == ChapterPhase.Camp ? item.Container == "carry" ? "随身 → 仓库" : "仓库 → 随身" : "随身 → 地面";
        return direction + $" · 整批后 {CapacityText(next.CarryUsed, next.CarryCapacity)} / {next.CarriedGrams / 1000m:0.##} kg · 区域压力 +{next.State.Pressure - Session.State.Pressure}";
    }
}
