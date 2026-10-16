using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private string _storageCraftTab = "bags", _selectedBag = "BP01";
    private int _supplyTimes = 1;
    private void StorageCraftingWorkspace(VBoxContainer box)
    {
        Tabs(box, "storage_craft_", [("bags", "背包规格"), ("supplies", "普通补给"), ("warehouse", "仓库扩建")], _storageCraftTab, key => _storageCraftTab = key);
        if (_storageCraftTab == "supplies") { SupplyCrafting(box); return; }
        if (_storageCraftTab == "warehouse")
        {
            var page = Scroll(box); page.AddChild(Text(UiCopy.Format("UI_21995C297255", Session.WarehouseUsed, Session.WarehouseCapacity, Session.WarehouseReserved), 20, Ink));
            if (Session.State.WarehouseLevel == 3) page.AddChild(Text(UiCopy.Text("UI_5580B0D73332"), 20, Success));
            else
            {
                var preview = Session.Preview(s => s.UpgradeWarehouse());
                var capacity = new[] { 48, 72, 96, 128 }[Session.State.WarehouseLevel + 1];
                page.AddChild(Text(UiCopy.Format("UI_1857568936EB", Session.WarehouseCapacity, capacity, capacity - Session.WarehouseCapacity) + (preview == null ? UiCopy.Text("UI_5425DF4F4EAC") : UiCopy.Format("UI_D70FD4674A73", preview.WarehouseCapacity - preview.WarehouseUsed)), 20, Ink));
                Costs(page, Session.WarehouseCost,Session.State.WarehouseLevel==0?Session.State.Done.Contains("Q02"):Session.SliceComplete);
                if (Session.State.Phase != ChapterPhase.Camp) page.AddChild(Text(UiCopy.Text("UI_251FEEFCD521"), 18, Danger));
                if (Session.State.WarehouseLevel == 0 && !Session.State.Done.Contains("Q02") || Session.State.WarehouseLevel > 0 && !Session.SliceComplete) page.AddChild(Text(UiCopy.Text("UI_BC93F281FF6E"), 18, Danger));
            }
            box.AddChild(Button("upgrade_warehouse", UiCopy.Text("UI_1EF4DFCDBDAB"), () => Change(Session.UpgradeWarehouse), Session.CanUpgradeWarehouse, true)); return;
        }
        var bagChoices = Row(10); box.AddChild(bagChoices);
        foreach (var kind in new[] { "BP01", "BP02", "BP03" })
        {
            var status = Session.HasPhysical(kind) ? "已持有" : Session.BackpackRecipeOpen(kind) ? "配方已解锁" : "尚未解锁";
            var choice = Button("bag_spec_" + kind, (_selectedBag == kind ? "✓ " : "") + ChapterContent.ItemName(kind) + $"\n{4 + ChapterContent.BagCapacity(kind)} 格 · {status}", () => { _selectedBag = kind; Render(); });
            choice.SizeFlagsHorizontal = SizeFlags.ExpandFill; choice.CustomMinimumSize = new Vector2(0, 74); bagChoices.AddChild(choice);
        }
        var body = Scroll(box);
        var selected = _selectedBag;
        EquipmentComparison(body, selected);
        if (!Session.HasPhysical(selected)) Costs(body, Session.BackpackCost(selected),Session.BackpackRecipeOpen(selected));
        var blockers = Session.CraftBlockers(selected); if (!Session.HasPhysical(selected) && blockers.Length > 0) body.AddChild(Text(string.Join(" · ", blockers), 16, Muted));
        var actions = Row(12); box.AddChild(actions);
        if (Session.HasPhysical(selected)) CraftedEquipAction(box, selected);
        if (Session.HasPhysical(selected) && !Session.Equipped.Contains(selected)) actions.AddChild(QuietButton("bag_equip_view", "比较穿戴", () => { _selectedGear = selected; _gearFilter = "backpack"; OpenNested("gear"); }));
        if (!Session.HasPhysical(selected)) actions.AddChild(Button("make_" + selected, UiCopy.Text("UI_0C775C37A53D") + ChapterContent.ItemName(selected), () => RequestCraft(selected, s => s.CraftBackpack(selected)), Session.CanCraftBackpack(selected), true));
        if (_craftedResult == selected) actions.AddChild(Button("crafted_keep", UiCopy.Text("UI_1A934B955342"), () => { _craftedResult = ""; Render(); }));
        if (!Session.HasPhysical(selected) || Session.State.RecipePlan.Contains(selected)) actions.AddChild(QuietButton("bag_plan_toggle", Session.State.RecipePlan.Contains(selected) ? "移出制作计划" : "加入制作计划", () => ToggleRecipePlan(selected)));
        actions.AddChild(QuietButton("bag_plan", "制作计划", () => OpenNested("plan")));
    }

    private void SupplyCrafting(VBoxContainer box)
    {
        box.AddChild(Text(UiCopy.Text("UI_E8E095114A87"), 20, Ink));
        box.AddChild(Text(UiCopy.Format("UI_237F8B8E8C7C", Session.CarriedItems.Where(i => i.Kind == "U01" && !i.FreeSupply).Sum(i => i.Quantity), Session.StoredStock.Where(i => i.Kind == "U01").Sum(i => i.Quantity)), 18, Ink));
        var row = Row(10); box.AddChild(row); row.AddChild(Inline(UiCopy.Text("UI_6C269E68C2BC"), 16, Ink));
        var times = new SpinBox { Name = "SupplyCraftTimes", MinValue = 1, MaxValue = 50000, Step = 1, Value = _supplyTimes, CustomMinimumSize = new Vector2(160, 44) }; row.AddChild(times);
        var maximum = Math.Min(50000, Math.Min(Session.AvailableMaterial(3), Session.AvailableMaterial(4)));
        maximum = Math.Min(maximum, Session.WarehouseCapacity * ChapterContent.Items["U01"].Stack / 2);
        while (maximum > 0 && Session.Preview(s => s.CraftSupplies(maximum)) == null) maximum--;
        row.AddChild(Button("supplies_max", UiCopy.Text("UI_5D98011C2217") + maximum + UiCopy.Text("UI_F81526EFCE19"), () => times.Value = maximum, maximum > 0));
        var preview = Text("", 18, Ink); box.AddChild(preview); Fill(box);
        var commit = Button("make_U01", UiCopy.Text("UI_451BD2DA5A02"), () => RequestCraft("U01", s => s.CraftSupplies((int)times.Value)), primary: true); box.AddChild(commit);
        void Update()
        {
            _supplyTimes = (int)times.Value;
            var next = Session.Preview(s => s.CraftSupplies(_supplyTimes));
            int[] cost = [0, 0, 0, _supplyTimes, _supplyTimes, 0, 0];
            preview.Text = UiCopy.Format("UI_E8ABE7EF3C34", _supplyTimes, _supplyTimes * 2) + string.Join("\n", Session.MaterialBudgetFor(cost).Select(b => ChapterContent.ItemName(b.Kind) + $" {b.Warehouse + b.Carry} − {b.Required} → {b.Remaining}；扣仓库 {b.FromWarehouse} / 随身 {b.FromCarry}"))
                + (next == null ? UiCopy.Text("UI_C4907794A5FF") : UiCopy.Format("UI_876AA7DBC108", Session.WarehouseUsed, next.WarehouseUsed, next.WarehouseCapacity));
            commit.Text = UiCopy.Format("UI_E2889E5FF2DE", _supplyTimes, _supplyTimes * 2); commit.Disabled = next == null;
        }
        times.ValueChanged += _ => Update(); Update();
    }
}
