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
    private void PhysicalInventory(VBoxContainer box)
    {
        var s = Session.State;
        var visible = s.Stock.Where(i => i.Container == "carry" || Session.AtBase && i.Container == "warehouse" ||
            !Session.AtBase && Session.GroundHere.Any(p => p.Key == i.Container)).ToArray();
        var selected = visible.FirstOrDefault(i => i.Id == _selectedStock);
        if (selected == null)
        {
            selected = visible.FirstOrDefault(i => i.Container.StartsWith("ground:")) ?? visible.FirstOrDefault();
            _selectedStock = selected?.Id ?? 0;
        }
        var columns = Row(14); columns.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(columns);
        var carrying = FlexibleColumn(columns, 1);
        carrying.AddChild(Text($"随身 · {Session.CarryUsed}/{Session.CarryCapacity} 格", 20, Ink));
        carrying.AddChild(Text(s.Backpack == "" ? "口袋 4 格 · 尚未穿戴背包" : "口袋 4 + 背包 " + ChapterContent.BagCapacity(s.Backpack) + " 格", 14, Muted));
        var carryGrid = Scroll(carrying);
        StorageGrid(carryGrid, Session.CarriedItems, Session.CarryCapacity, "carry");
        carrying.AddChild(Text("点击物品查看取放操作\n双格装备的续格标有“占位”", 14, Muted));
        var other = FlexibleColumn(columns, 1.1f);
        if (Session.AtBase)
        {
            other.AddChild(Text($"仓库 · {Session.WarehouseUsed}/{Session.WarehouseCapacity} 格", 20, Ink));
            other.AddChild(Text("穿戴不占格 · 仓储物品不计负重", 14, Muted));
            StorageGrid(Scroll(other), Session.StoredStock, Session.WarehouseCapacity, "warehouse");
        }
        else
        {
            other.AddChild(Text("当前位置 · 遗留物", 20, Ink));
            other.AddChild(Text("同趟可回取 · 未带走物品下趟重置", 14, Muted));
            var ground = Scroll(other);
            foreach (var pair in Session.GroundHere.ToArray())
            {
                ground.AddChild(Text(pair.Value.Name, 16, Accent));
                ground.AddChild(Text(pair.Value.FreePickup ? "本次首次装包免费" : $"每次取放行动值 +{Session.SceneActionCost()}", 14, Muted));
                var key = pair.Key;
                ground.AddChild(Button("loot_all_" + key.Replace(':', '_'), "全部拾取", () => Change(() => Session.PickupAll(key)), s.Phase == ChapterPhase.Exploring));
                StorageGrid(ground, s.Stock.Where(i => i.Container == key), 0, key.Replace(':', '_'));
            }
            if (!Session.GroundHere.Any()) ground.AddChild(Text("这里没有已发现的遗留物。", 16, Muted));
            other.AddChild(Text($"基地仓库 {Session.WarehouseUsed}/{Session.WarehouseCapacity} 格\n返还预留 {Session.WarehouseReserved} 格", 14, Muted));
        }
        var details = FlexibleColumn(columns, 1.05f); details.AddChild(Text("物品详情", 20, Ink));
        var detail = Scroll(details);
        if (selected == null)
        {
            Illustration(detail, "satchel", 120, 120); detail.AddChild(Text("暂无物品。搜索区域或在工坊制作，获得物品后可在这里整理。", 16, Muted)); return;
        }
        var batch = visible.Where(i => i.Container == selected.Container && i.Kind == selected.Kind && i.FreeSupply == selected.FreeSupply && i.Calibrated == selected.Calibrated).ToArray();
        if (batch.Length > 1) detail.AddChild(Text($"同类共 {batch.Sum(i => i.Quantity)} 件，按来源批次取放。", 14, Muted));
        foreach (var item in batch) StockRow(detail, item, item.Container.StartsWith("ground:"));
    }

    private void StorageGrid(Node parent, IEnumerable<ChapterItem> stock, int capacity, string name)
    {
        var grid = new GridContainer { Name = "StorageGrid_" + name, Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 4); grid.AddThemeConstantOverride("v_separation", 4); parent.AddChild(grid);
        var used = 0;
        foreach (var group in stock.GroupBy(i => (i.Kind, i.FreeSupply, i.Calibrated)))
        {
            var first = group.First(); var def = ChapterContent.Items[first.Kind];
            var selected = group.Any(i => i.Id == _selectedStock);
            var total = group.Sum(i => i.Quantity);
            for (var offset = 0; offset < total; offset += def.Stack)
            for (var part = 0; part < def.Slots; part++)
            {
                var quantity = Math.Min(def.Stack, total - offset);
                var tile = ItemTile($"stock_{first.Id}_{offset}_{part}", part == 0 ? first.Kind : "", part == 0 ? "×" + quantity : "↳ 占位", part == 0 ? def.Name : def.Name,
                    () => { _selectedStock = first.Id; Render(); }, selected, 88);
                tile.CustomMinimumSize = new Vector2(42, 88); tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                if (part == 0)
                {
                    Dock(tile.GetChild<Control>(0), 8, 6, -8, -25, ar: 1, ab: 1);
                    var count = tile.GetChildren().OfType<Label>().First(); count.HorizontalAlignment = HorizontalAlignment.Right;
                    Dock(count, -36, 2, -4, 23, al: 1, ar: 1);
                }
                else if (tile.GetChild(0) is ExpeditionGlyph continuation) continuation.Symbol = "arrow";
                tile.TooltipText = def.Name + $" · 同类 {total} 件 / {(total + def.Stack - 1) / def.Stack * def.Slots} 格" +
                    (part > 0 ? "\n同一装备占用的续格" : "") + "\n" + (first.FreeSupply ? "本趟配发" : first.Registered ? "已登记装备" : "点击查看详情");
                grid.AddChild(tile); used++;
            }
        }
        for (var n = used; n < capacity; n++)
        {
            var cell = Panel(grid, new Color("d4d3bd"), new Color("a4ad94"));
            cell.Name = "Empty_" + n; cell.CustomMinimumSize = new Vector2(42, 88); cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            cell.MouseFilter = MouseFilterEnum.Ignore;
            var label = Text((n + 1).ToString("00"), 14, Muted); label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center;
            cell.AddChild(label);
        }
        grid.SetMeta("occupied", used); grid.SetMeta("capacity", capacity);
    }
    private void StockRow(VBoxContainer box, ChapterItem item, bool ground)
    {
        var def = ChapterContent.Items[item.Kind]; var card = CardBox(box);
        var info = Row(8); card.AddChild(info);
        if (item.Kind is "U01" or "M08") info.AddChild(Glyph("supply", Accent, 40));
        else Illustration(info, ItemArt(item.Kind), 42, 44, false);
        var words = FlexibleColumn(info);
        words.AddChild(Text($"{def.Name} ×{item.Quantity}" + (item.FreeSupply ? " · 本趟配发" : item.Calibrated ? " · 已校准" : ""), 16, Ink));
        words.AddChild(Text($"每堆 {def.Stack} 件／{def.Slots} 格 · 单件 {def.Grams / 1000m:0.##} kg", 14, Muted));
        if (ground) words.AddChild(Text("地面 · " + Session.State.Ground[item.Container].Name, 14, Muted));
        else words.AddChild(Text(item.Container == "carry" ? "随身物品" : "基地仓库", 14, Muted));
        if (item.Registered) words.AddChild(Text("已登记装备", 14, Success));
        var actions = new HFlowContainer(); actions.AddThemeConstantOverride("h_separation", 5); actions.AddThemeConstantOverride("v_separation", 5); card.AddChild(actions);
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
    private void ConfirmDiscard(ChapterItem item)
    {
        var dialog = new ConfirmationDialog { Title = "放弃实物", DialogText = $"永久放弃 {ChapterContent.ItemName(item.Kind)} ×{item.Quantity}？\n此操作不返还材料。", OkButtonText = "确认放弃", CancelButtonText = "保留" };
        AddChild(dialog); dialog.Confirmed += () => Change(() => Session.DiscardAtBase(item.Id, item.Quantity));
        dialog.Canceled += () => dialog.QueueFree(); dialog.Confirmed += () => dialog.QueueFree();
        dialog.PopupCentered(new Vector2I(420, 180));
    }
    private void StorageCrafting(VBoxContainer page)
    {
        foreach (var kind in new[] { "BP01", "BP02", "BP03" })
        {
            var card = CardBox(page); var row = Row(12); card.AddChild(row); Illustration(row, "satchel", 72, 76);
            var copy = FlexibleColumn(row); copy.AddChild(Text(ChapterContent.ItemName(kind), 20, Ink));
            copy.AddChild(Text($"额外 {ChapterContent.BagCapacity(kind)} 格 · 自重 {ChapterContent.Items[kind].Grams / 1000m:0.##} kg · 不供牌", 16, Muted));
            card.AddChild(Text(Materials(Session.BackpackCost(kind)), 14, Muted));
            var label = Session.HasPhysical(kind) ? "已经持有" : !Session.BackpackRecipeOpen(kind) ? "需完成后续整备任务" : "制作背包";
            card.AddChild(Button("make_" + kind, label, () => Change(() => Session.CraftBackpack(kind)), Session.CanCraftBackpack(kind), true));
        }
        var medical = CardBox(page); medical.AddChild(Text("密封补给 ×2", 20, Ink));
        medical.AddChild(Text("织物 1、药剂 1；普通份可留仓，出征免费份在本趟结束清除。", 16, Muted));
        medical.AddChild(Button("make_U01", "制作补给", () => Change(Session.CraftSupplies), Session.CanCraftSupplies));
        var warehouse = CardBox(page); warehouse.AddChild(Text($"仓库 · {Session.WarehouseUsed}/{Session.WarehouseCapacity} 格", 20, Ink));
        warehouse.AddChild(Text(Session.State.WarehouseLevel == 3 ? "已完成全部扩建。" : Materials(Session.WarehouseCost), 16, Muted));
        warehouse.AddChild(Button("upgrade_warehouse", "扩建仓库", () => Change(Session.UpgradeWarehouse), Session.CanUpgradeWarehouse, true));
    }
}
