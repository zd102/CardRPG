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

    private void PhysicalInventory(VBoxContainer box)
    {
        var s = Session.State;
        var bar = box.GetChild<HBoxContainer>(0);
        Tabs(bar, "stock_filter_", [("all", "全部"), ("materials", "材料"), ("gear", "装备"), ("quest", "任务物"), ("supply", "补给")], _stockFilter, key => _stockFilter = key);
        bar.AddChild(Button("storage_view", _stockList ? "网格" : "列表", () => { _stockList = !_stockList; Render(); }));
        var visible = s.Stock.Where(i => i.Container == "carry" || Session.AtBase && i.Container == "warehouse" || !Session.AtBase && Session.GroundHere.Any(g => g.Key == i.Container)).Where(StockMatches).ToArray();
        var selected = visible.FirstOrDefault(i => i.Id == _selectedStock) ?? visible.FirstOrDefault(i => i.Container.StartsWith("ground:")) ?? visible.FirstOrDefault();
        _selectedStock = selected?.Id ?? 0;
        var columns = Row(12); columns.Name = "StorageWorkspace"; columns.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(columns);
        LoadoutColumn(columns);
        var carry = Section(columns, .85f); carry.Name = "CarryContainer";
        carry.AddChild(Text($"随身背包   {Session.CarryUsed} / {Session.CarryCapacity}", 20, Session.Overweight ? Danger : Ink));
        carry.AddChild(Inline("带入区域 · 撤离后保留", 14, Muted));
        var carryBody = Scroll(carry); StorageGrid(carryBody, Session.CarriedItems, Session.CarryCapacity, "carry");
        var other = Section(columns, 1.15f); other.Name = "ExternalContainer";
        if (Session.AtBase)
        {
            other.AddChild(Text($"仓库   {Session.WarehouseUsed} / {Session.WarehouseCapacity}", 20, Ink));
            other.AddChild(Inline("安全储存 · 本趟不会丢失", 14, Muted));
            StorageGrid(Scroll(other), Session.StoredStock, Session.WarehouseCapacity, "warehouse");
            var canStore = Session.Preview(x => x.StoreCarriedMaterials()) != null;
            if (Session.CarriedItems.Any(i => ChapterContent.MaterialIds.Contains(i.Kind))) other.AddChild(Button("store_materials", "存入随身全部材料", () => Change(Session.StoreCarriedMaterials), canStore));
            if (!canStore && Session.CarriedItems.Any(i => ChapterContent.MaterialIds.Contains(i.Kind))) other.AddChild(Text("仓库装不下全部材料，请逐项存入。", 16, Danger));
        }
        else
        {
            other.AddChild(Text("当前位置 · 地面物品", 20, Ink)); var ground = Scroll(other);
            foreach (var pair in Session.GroundHere.ToArray())
            {
                var key = pair.Key; var items = s.Stock.Where(i => i.Container == key).ToArray();
                if (items.Length == 0) continue;
                ground.AddChild(Text(pair.Value.Name + (pair.Value.FreePickup ? " · 首次装包免费" : $" · 行动值 +{Session.SceneActionCost()}"), 16, Ink));
                var fitting = Session.FittingPickup(key); var count = fitting.Values.Sum();
                var full = count == items.Sum(i => i.Quantity);
                ground.AddChild(Button("loot_all_" + key.Replace(':', '_'), full ? "全部拾取" : count > 0 ? $"预览可装入 {count} 件…" : "空间不足 · 可先放下物品", () =>
                {
                    if (full) Change(() => Session.PickupAll(key));
                    else ConfirmPartialPickup(key, fitting);
                }, count > 0 && s.Phase == ChapterPhase.Exploring));
                StorageGrid(ground, items, 0, key.Replace(':', '_'));
            }
            if (!Session.GroundHere.Any()) ground.AddChild(Text("此地暂无遗留物。", 16, Muted));
        }
        var actionPanel = FlatPanel(box, 12); actionPanel.Name = "StockActionBar";
        var details = Column(6); actionPanel.AddChild(details);
        if (selected == null) { details.AddChild(Text("选择物品查看详情与取放操作。Shift+Enter 可整批取放。", 16, Muted)); return; }
        var batches = visible.Where(i => i.Container == selected.Container && i.Kind == selected.Kind && i.Registered == selected.Registered && i.Calibrated == selected.Calibrated && i.FreeSupply == selected.FreeSupply).ToArray();
        if (batches.Length > 1)
        {
            var batchRow = new HFlowContainer(); details.AddChild(batchRow);
            for (var index = 0; index < batches.Length; index++)
            { var batch = batches[index]; batchRow.AddChild(Button("batch_" + batch.Id, $"批次 {index + 1} ×{batch.Quantity}" + (batch.Id == selected.Id ? " ◆" : ""), () => { _selectedStock = batch.Id; Render(); })); }
        }
        StockRow(details, selected, selected.Container.StartsWith("ground:"));
    }

    private bool StockMatches(ChapterItem item) => _stockFilter switch
    { "materials" => ChapterContent.MaterialIds.Contains(item.Kind), "gear" => ChapterContent.Gear.ContainsKey(item.Kind), "quest" => item.Kind.StartsWith('T'), "supply" => item.Kind == "U01", _ => true };

    private void StorageGrid(Node parent, IEnumerable<ChapterItem> stock, int capacity, string name)
    {
        var all = stock.ToArray();
        var grid = new ChapterSlotGrid { Name = "StorageGrid_" + name, SizeFlagsHorizontal = SizeFlags.ExpandFill, Capacity = capacity, ShowEmpty = _stockFilter == "all", ListMode = _stockList };
        parent.AddChild(grid);
        var used = ChapterSession.Slots(all);
        grid.SetMeta("occupied", used); grid.SetMeta("capacity", capacity);
        foreach (var group in all.Where(StockMatches).GroupBy(i => (i.Kind, i.FreeSupply, i.Calibrated, i.Registered)))
        {
            var first = group.First(); var def = ChapterContent.Items[first.Kind]; var total = group.Sum(i => i.Quantity);
            for (var offset = 0; offset < total; offset += def.Stack)
            {
                var quantity = Math.Min(def.Stack, total - offset);
                var caption = $"×{quantity}" + (first.FreeSupply ? " 配发" : first.Calibrated ? " 校准" : "") + (def.Slots > 1 ? $" / {def.Slots}格" : "");
                var tile = ItemTile($"stock_{first.Id}_{offset}_0", first.Kind, def.Name, caption, () => { _selectedStock = first.Id; Render(); }, group.Any(i => i.Id == _selectedStock), 106);
                tile.SetMeta("slots", def.Slots); tile.CustomMinimumSize = new Vector2(0, 106);
                if (_stockList)
                {
                    Dock(tile.GetChild<Control>(0), 10, 10, 65, 70);
                    var labels = tile.GetChildren().OfType<Label>().ToArray();
                    Dock(labels[0], 78, 10, -10, 38, ar: 1); Dock(labels[1], 78, 42, -10, 68, ar: 1);
                    foreach (var label in labels) label.HorizontalAlignment = HorizontalAlignment.Left;
                }
                tile.TooltipText = ""; // Selection discloses details in the action bar without covering neighboring slots.
                grid.AddChild(tile);
            }
        }
        if (capacity > 0) parent.AddChild(Text($"空余 {Math.Max(0, capacity - used)} 格" + (used > capacity ? " · 需整理超出部分" : "") + (_stockFilter != "all" ? " · 仅显示筛选结果" : ""), 14, Muted));
    }

    private void ConfirmPartialPickup(string key, Dictionary<long, int> fitting)
    {
        var take = Session.State.Stock.Where(i => i.Container == key).Select(i => (Item: i, Count: fitting.GetValueOrDefault(i.Id))).ToArray();
        var ground = Session.State.Ground[key];
        var copy = "将装入：\n" + string.Join("\n", take.Where(x => x.Count > 0).Select(x => ChapterContent.ItemName(x.Item.Kind) + " ×" + x.Count))
            + "\n\n留在原位：\n" + string.Join("\n", take.Where(x => x.Count < x.Item.Quantity).Select(x => ChapterContent.ItemName(x.Item.Kind) + " ×" + (x.Item.Quantity - x.Count)))
            + (ground.FreePickup ? "\n本次免费。" : $"\n整批只执行一次取放，行动值 +{Session.SceneActionCost()}。")
            + (Session.State.Warned && !ground.FreePickup ? "\n巡逻将先拦截，本次物品不会移动。" : "");
        Confirm("确认部分拾取", copy, () => Change(() => Session.PickupSelected(key, fitting)), "按清单装包");
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
