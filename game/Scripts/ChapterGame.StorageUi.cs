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
    private bool _showStockLoadout;
    private string _stockSearch = "", _stockSort = "kind";

    private void PhysicalInventory(VBoxContainer box)
    {
        var s = Session.State;
        LegacyCapacityRecovery(box);
        var bar = Row(6); box.AddChild(bar);
        var local = s.Stock.Where(i => i.Container == "carry" || Session.AtBase && i.Container == "warehouse" || !Session.AtBase && Session.GroundHere.Any(g => g.Key == i.Container)).ToArray();
        var categories = new[] { ("all", "全部"), ("materials", "材料"), ("gear", "装备"), ("quest", "任务物"), ("supply", "补给") };
        var category = new OptionButton { Name = "StockCategory", CustomMinimumSize = new Vector2(120, 40) };
        foreach (var (key, caption) in categories) category.AddItem(caption + " " + local.Count(i => StockCategory(i, key)));
        category.Selected = Array.FindIndex(categories, t => t.Item1 == _stockFilter); category.ItemSelected += index => { _stockFilter = categories[index].Item1; Render(); }; bar.AddChild(category);
        bar.AddChild(Button("storage_view", _stockList ? UiCopy.Text("UI_E41ECC67CA28") : UiCopy.Text("UI_AEDD6814FF8C"), () => { _stockList = !_stockList; Render(); }));
        if (Session.AtBase) bar.AddChild(Button("storage_paired", _stockPaired ? UiCopy.Text("UI_B38AC9937C6C") : UiCopy.Text("UI_9D856267C7BB"), () => { _stockPaired = !_stockPaired; _pairedAwaitBatch = false; Render(); }));
        bar.AddChild(Button("storage_loadout", _showStockLoadout ? UiCopy.Text("UI_97FCCC4EBD9B") : UiCopy.Text("UI_57F68D9E3597"), () => { _showStockLoadout = !_showStockLoadout; Render(); }));
        var find = bar;
        var search = new LineEdit { Name = "StockSearch", PlaceholderText = UiCopy.Text("UI_13E3E9C861F4"), Text = _stockSearch, ClearButtonEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        find.AddChild(search); search.TextSubmitted += value => { _stockSearch = value; Render(); };
        find.AddChild(Button("stock_search", UiCopy.Text("UI_B5F15473FDC0"), () => { _stockSearch = search.Text; Render(); }));
        if (_stockSearch != "" || _stockFilter != "all") find.AddChild(Button("stock_clear", UiCopy.Text("UI_CB5D682BAC3D"), () => { _stockSearch = ""; _stockFilter = "all"; Render(); }));
        find.AddChild(Button("stock_sort", UiCopy.Text("UI_DE3C35C2A69A") + (_stockSort switch { "name" => "名称", "quantity" => "数量", "weight" => "重量", _ => "类型" }), () => { _stockSort = _stockSort switch { "kind" => "name", "name" => "quantity", "quantity" => "weight", _ => "kind" }; Render(); }));
        var visible = local.Where(StockMatches).ToArray();
        _stockSelection.RemoveWhere(id => !local.Any(i => i.Id == id));
        if (s.Phase == ChapterPhase.Camp)
        {
            var batch = new HFlowContainer(); if (_stockSelection.Count > 0) box.AddChild(batch); else find.AddChild(batch);
            batch.AddChild(Button("stock_select_visible", UiCopy.Text("UI_BB7193F0D0B8"), () => { foreach (var i in visible) _stockSelection.Add(i.Id); Render(); }));
            if (_stockSelection.Count > 0) batch.AddChild(Button("stock_clear_selection", UiCopy.Format("UI_3233147FA505", _stockSelection.Count, _stockSelection.Count(id => !visible.Any(i => i.Id == id))), () => { _stockSelection.Clear(); Render(); }));
            if (_stockSelection.Count > 0)
            {
                batch.AddChild(Button("stock_review_selection", UiCopy.Text("UI_DC037F1BDF80"), () => { _detailItemId = 0; _detailTitle = UiCopy.Text("UI_6F948960E5AD"); _detailText = string.Join("\n", local.Where(i => _stockSelection.Contains(i.Id)).Select(i => ChapterContent.ItemName(i.Kind) + $" ×{ChosenQuantity(i)} · {ContainerName(i.Container)}" + (visible.Any(v => v.Id == i.Id) ? "" : " · 筛选外"))); OpenNested("detail"); }));
                batch.AddChild(Button("stock_clear_hidden", UiCopy.Text("UI_126CEAD279D9"), () => { _stockSelection.RemoveWhere(id => visible.All(i => i.Id != id)); Render(); }));
                var chosenItems = local.Where(i => _stockSelection.Contains(i.Id)).ToArray();
                batch.AddChild(Button("stock_discard_batch", UiCopy.Text("UI_D1BCD8DC4ABC"), () => PreviewBatchDiscard(chosenItems), chosenItems.Any(i => !i.KeepOnPerson && !i.FreeSupply && !i.Kind.StartsWith('T'))));
            }
            var protectedBatches = local.Count(i => i.Container == "carry" && i.KeepOnPerson && _stockSelection.Contains(i.Id));
            if (protectedBatches > 0) batch.AddChild(Inline(UiCopy.Format("UI_7E033240C2D0", protectedBatches), 14, Muted));
            foreach (var source in _stockSelection.Count > 0 ? new[] { "carry", "warehouse" } : Array.Empty<string>())
            {
                var chosen = local.Where(i => i.Container == source && _stockSelection.Contains(i.Id) && !(source == "carry" && i.KeepOnPerson)).ToDictionary(i => i.Id, ChosenQuantity);
                batch.AddChild(Button("stock_batch_" + source, source == "carry" ? UiCopy.Text("UI_C828D9BCB2AB") : UiCopy.Text("UI_17B0D5957164"), () => RequestBatchMove(chosen, source, source == "carry" ? "warehouse" : "carry"), chosen.Count > 0));
            }
            if (_stockUndoBefore != null && Session.ToJson() == _stockUndoAfter) batch.AddChild(Button("stock_undo", UiCopy.Text("UI_FFC43852CE29") + _stockUndoLabel, UndoStock));
        }
        var selected = visible.FirstOrDefault(i => i.Id == _selectedStock) ?? visible.FirstOrDefault(i => i.Container.StartsWith("ground:")) ?? visible.FirstOrDefault();
        if (_stockPaired && _pairedAwaitBatch) selected = null;
        _selectedStock = selected?.Id ?? 0;
        if (_clickMoveId != 0) box.AddChild(Button("stock_cancel_target", UiCopy.Text("UI_B8632A0B2CF9"), () => { _clickMoveId = 0; Render(); }));
        if (_showStockLoadout && Size.X < 1200)
        {
            var wear = Row(6); wear.Name = "CompactLoadout"; box.AddChild(wear);
            foreach (var slot in new[] { "weapon", "body", "hands", "backpack" })
            {
                var kind = Session.Equipped.FirstOrDefault(k => ChapterContent.Gear[k].Slot == slot) ?? "";
                wear.AddChild(Button("compact_wear_" + slot, SlotName(slot) + " · " + (kind == "" ? UiCopy.Text("UI_DFEF18791728") : ChapterContent.ItemName(kind)), () => { if (kind != "") OpenGearComparison(kind); }));
            }
        }
        var columns = Row(12); columns.Name = "StorageWorkspace"; columns.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(columns);
        var next = selected == null ? null : Session.Preview(copy => StockCommand(copy, selected.Id, selected.Container, ChosenQuantity(selected), true));
        if (_stockPaired && Session.AtBase) PairedStockTable(columns, local);
        else
        {
        if (_showStockLoadout && Size.X >= 1200) LoadoutColumn(columns);
        var carry = Section(columns, .85f); carry.Name = "CarryContainer";
        var carryHeading = Text(UiCopy.Format("UI_45EF4D017440", Session.CarryUsed, next?.CarryUsed ?? Session.CarryUsed, Session.CarryCapacity, Session.CarriedGrams / 1000m), 20, Session.Overweight ? Danger : Ink);
        carryHeading.TooltipText = UiCopy.Text("UI_9AEEE992067D") + (Session.Overweight ? UiCopy.Text("UI_13E5575CB6B0") : UiCopy.Text("UI_706C0D37535C"));
        carry.AddChild(carryHeading);
        AddLoadMeter(carry, "CarryCapacityMeter", Session.CarryUsed, next?.CarryUsed ?? Session.CarryUsed, Session.CarryCapacity);
        StockTarget(carry, "carry");
        var carryBody = Scroll(carry); StorageGrid(carryBody, Session.CarriedItems, Session.CarryCapacity, "carry");
        BindStockDrop(carry.GetParent<Control>(), "carry");
        var other = Section(columns, 1.15f); other.Name = "ExternalContainer";
        if (Session.AtBase)
        {
            var warehouseHeading = Text(UiCopy.Format("UI_5DFEDBE684F6", Session.WarehouseUsed, next?.WarehouseUsed ?? Session.WarehouseUsed, Session.WarehouseCapacity), 20, Ink);
            warehouseHeading.TooltipText = UiCopy.Text("UI_3BAB0CF9D3DB"); other.AddChild(warehouseHeading);
            AddLoadMeter(other, "WarehouseCapacityMeter", Session.WarehouseUsed, next?.WarehouseUsed ?? Session.WarehouseUsed, Session.WarehouseCapacity);
            StockTarget(other, "warehouse");
            StorageGrid(Scroll(other), Session.StoredStock, Session.WarehouseCapacity, "warehouse");
            BindStockDrop(other.GetParent<Control>(), "warehouse");
            var deposit = Session.MaterialDepositPlan(Session.CarriedItems.Where(StockMatches).Select(i => i.Kind));
            if (Session.CarriedItems.Any(i => ChapterContent.MaterialIds.Contains(i.Kind))) other.AddChild(Button("store_materials", UiCopy.Text("UI_30E438D1EE19"), () => RequestBatchMove(deposit, "carry", "warehouse"), deposit.Count > 0));
        }
        else
        {
            other.AddChild(Text(UiCopy.Text("UI_7EB4223497E3"), 20, Ink)); StockTarget(other, "ground"); var ground = Scroll(other);
            foreach (var pair in Session.GroundHere.ToArray())
            {
                var key = pair.Key; var items = s.Stock.Where(i => i.Container == key).ToArray();
                if (items.Length == 0) continue;
                ground.AddChild(Text(pair.Value.Name + (pair.Value.FreePickup ? UiCopy.Text("UI_7A79030C34BA") : UiCopy.Format("UI_2427A90A37D9", Session.SceneActionCost())), 16, Ink));
                var fitting = Session.FittingPickup(key); var count = fitting.Values.Sum();
                var full = count == items.Sum(i => i.Quantity);
                ground.AddChild(Button("loot_all_" + key.Replace(':', '_'), full ? UiCopy.Text("UI_E0037BA86CEE") : count > 0 ? UiCopy.Format("UI_E3D2F73CFC69", count) : UiCopy.Text("UI_42E649EED1AF"), () =>
                {
                    if (full) Change(() => Session.PickupAll(key));
                    else ConfirmPartialPickup(key, fitting);
                }, count > 0 && s.Phase == ChapterPhase.Exploring));
                StorageGrid(ground, items, 0, key.Replace(':', '_'));
                ground.AddChild(Button("loot_plan_" + key.Replace(':', '_'), UiCopy.Text("UI_0A461F49190A"), () => ConfirmPartialPickup(key, fitting)));
            }
            if (!Session.GroundHere.Any()) ground.AddChild(Text(UiCopy.Text("UI_3BD654AEAB50"), 16, Muted));
            if (Session.GroundHere.Any(g => g.Value.FreePickup)) other.AddChild(Text(UiCopy.Text("UI_AEB3CCEEFF1E"), 14, Muted));
            BindStockDrop(other.GetParent<Control>(), "ground");
        }
        }
        if (_stockPaired && Session.AtBase)
        {
            var capacities = Row(14); box.AddChild(capacities);
            CapacityMeter(FlexibleColumn(capacities), "CarryCapacityMeter", "随身", Session.CarryUsed, next?.CarryUsed ?? Session.CarryUsed, Session.CarryCapacity);
            CapacityMeter(FlexibleColumn(capacities), "WarehouseCapacityMeter", "仓库", Session.WarehouseUsed, next?.WarehouseUsed ?? Session.WarehouseUsed, Session.WarehouseCapacity);
        }
        var actionPanel = FlatPanel(box, 12); actionPanel.Name = "StockActionBar";
        var details = Column(6); actionPanel.AddChild(details);
        if (selected == null) { details.AddChild(Text(_pairedAwaitBatch ? UiCopy.Text("UI_C0B48E021697") : UiCopy.Text("UI_B47E43678B1F"), 16, Muted)); return; }
        StockRow(details, selected, selected.Container.StartsWith("ground:"));
        WeightMeter(details, next?.CarriedGrams ?? Session.CarriedGrams);
    }

    private bool StockMatches(ChapterItem item) => StockCategory(item, _stockFilter) && (_stockSearch == "" || (ChapterContent.ItemName(item.Kind) + ItemPurpose(item.Kind)).Contains(_stockSearch, StringComparison.OrdinalIgnoreCase));
    private static bool StockCategory(ChapterItem item, string category) => category switch
    { "materials" => ChapterContent.MaterialIds.Contains(item.Kind), "gear" => ChapterContent.Gear.ContainsKey(item.Kind), "quest" => item.Kind.StartsWith('T'), "supply" => item.Kind == "U01", _ => true };

    private void StorageGrid(Node parent, IEnumerable<ChapterItem> stock, int capacity, string name)
    {
        var all = stock.ToArray();
        var grid = new ChapterSlotGrid { Name = "StorageGrid_" + name, ReadingScale = ReadingScale, SizeFlagsHorizontal = SizeFlags.ExpandFill, Capacity = capacity, ShowEmpty = false, ListMode = _stockList };
        parent.AddChild(grid);
        var used = ChapterSession.Slots(all);
        grid.SetMeta("occupied", used); grid.SetMeta("capacity", capacity);
        foreach (var first in all.Where(StockMatches).OrderBy(i => _stockSort == "weight" ? -ChapterContent.Items[i.Kind].Grams * i.Quantity : _stockSort == "quantity" ? -i.Quantity : 0).ThenBy(i => _stockSort == "name" ? ChapterContent.ItemName(i.Kind) : i.Kind, StringComparer.CurrentCulture).ThenBy(i => i.Id))
        {
                var def = ChapterContent.Items[first.Kind];
                var caption = $"整批 ×{first.Quantity}" + (first.FreeSupply ? " 配发" : first.Calibrated ? " 校准" : "") + (first.KeepOnPerson ? " 保留" : "") + (_stockArrivals.Contains(first.Id) ? " 新入" : "");
                if (_stockList) caption += $" · {def.Grams * first.Quantity / 1000m:0.##} kg";
                var tile = ItemTile($"stock_{first.Id}_0_0", first.Kind, def.Name, caption, () =>
                {
                    if (_quickMove && Input.IsKeyPressed(Key.Shift) && Session.State.Phase == ChapterPhase.Camp) RequestStockMove(first.Id, first.Container, ChosenQuantity(first));
                    else { _selectedStock = first.Id; Render(); }
                }, first.Id == _selectedStock, _stockList ? 72 : 118);
                tile.SetMeta("slots", 1); tile.SetMeta("item_id", first.Id); tile.SetMeta("quantity", first.Quantity); tile.CustomMinimumSize = new Vector2(0, _stockList ? 72 : 118);
                if (_stockList)
                {
                    Dock(tile.GetChild<Control>(0), 10, 10, 65, 70);
                    var labels = new[]{tile.GetNode<Label>("ItemTitle"),tile.GetNode<Label>("ItemCaption")};
                    if(tile.GetNodeOrNull<Control>("CapacityBadge") is {} badge) {badge.Hide();tile.GetNode<Control>("BagBadge").Hide();labels[1].Text+=UiCopy.Format("UI_73A7582C28F9", 4 + ChapterContent.BagCapacity(first.Kind));}
                    Dock(labels[0], 78, 10, -10, 38, ar: 1); Dock(labels[1], 78, 42, -10, 68, ar: 1);
                    foreach (var label in labels) label.HorizontalAlignment = HorizontalAlignment.Left;
                }
                tile.TooltipText = def.Name + " · " + caption + "\n" + ItemPurpose(first.Kind) + UiCopy.Text("UI_67255B6133F5");
                if (Session.State.Phase == ChapterPhase.Camp)
                {
                    var check = new CheckBox { Name = "SelectBatch_" + first.Id, ButtonPressed = _stockSelection.Contains(first.Id), TooltipText = UiCopy.Text("UI_1DE84B390491") };
                    tile.AddChild(check); Dock(check, 0, 0, 30, 30); check.Toggled += on => { if (on) _stockSelection.Add(first.Id); else _stockSelection.Remove(first.Id); Render(); };
                }
                BindStockPointer(tile, first);
                grid.AddChild(tile);
        }
        if (capacity > 0)
        {
            parent.AddChild(Text(UiCopy.Format("UI_1DDE781E80DC", Math.Max(0, capacity - used), all.Count(StockMatches), all.Length) + (used > capacity ? UiCopy.Text("UI_04B1A04B0391") : "") + UiCopy.Text("UI_43FFDABA27F0"), 14, Muted));
            parent.AddChild(Button("storage_empty_" + name, _showStockEmpty ? UiCopy.Text("UI_491AA759B092") : UiCopy.Text("UI_E32F89EE9F41"), () => { _showStockEmpty = !_showStockEmpty; Render(); }));
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
        return direction + $" · 整批后 {next.CarryUsed}/{next.CarryCapacity} 格 / {next.CarriedGrams / 1000m:0.##} kg · 行动值 +{next.State.Pressure - Session.State.Pressure}";
    }
}
