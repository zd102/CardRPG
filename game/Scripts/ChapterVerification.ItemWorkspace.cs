using CardRPG.Core;
using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CardRPG;

public partial class ChapterVerification
{
    private async Task VerifyInventoryWorkspace()
    {
        GetWindow().Size = new Vector2I(1280, 800);
        Game.ReplaceSession(new ChapterSession()); await Frames(8); await RawClick("inventory");
        await DesignCapture("inventory-00-first-play.png");
        var fixture = ChapterSession.FromJson(JsonSerializer.Serialize(new ChapterState
        {
            ContentVersion = "chapter1-outer-ring-v1", Extractions = 2, Owned = ChapterContent.Gear.Keys.ToHashSet(),
            Bank = [20,20,20], Cargo = [0,0,0], Done = ["Q01", "Q02", "S01"], World = ["station", "trial-complete"]
        }));
        fixture.State.WarehouseLevel = 3; Game.ReplaceSession(fixture); await Frames(8);
        await RawClick("inventory"); await DesignCapture("inventory-01-workspace.png"); await VerifyRenderedContrast();
        var bag = Game.Session.State.Stock.First(i => i.Kind == "BP01");
        await RawClick($"stock_{bag.Id}_0_0");
        Require(Game.Buttons.ContainsKey("equip_BP01") && Game.Buttons.ContainsKey("wear_backpack"), "Backpack equips in the same inventory workspace");
        await RawClick("equip_BP01");
        Require(Game.Session.CarryCapacity == 12 && Game.Buttons.ContainsKey("stock_search"), "Equip applies capacity without leaving stock");
        await RawClick("wear_weapon");
        Require(Game.Buttons.ContainsKey("stock_clear"), "Wear slot filters candidates in place");
        await DesignCapture("inventory-02-slot-filter.png");
        await RawClick("stock_clear");
        var weapon = Game.Session.State.Stock.First(i => i.Kind == "G02" && i.Container == "warehouse");
        await RawClick($"stock_{weapon.Id}_0_0"); await DesignCapture("inventory-03-equip.png");
        var beforeEquip = Game.Session.ToJson();
        await RawClick("stock_compare"); Require(Game.Session.ToJson() == beforeEquip, "Detailed comparison is read only");
        await PressKey(Key.Escape); Require(Game.Buttons.ContainsKey("stock_search"), "Comparison returns to the same item workspace");
        await RawClick("equip_G02"); Require(Game.Session.Equipped.Contains("G02"), "Direct inventory equip commits selected candidate");
        await RawClick("tools_stock"); await RawClick("stock_batch_mode"); await RawClick("stock_select_visible");
        await DesignCapture("inventory-04-batch.png"); await RawClick("stock_batch_mode");
        await RawClick("tools_stock");
        await RawClick("storage_view"); await DesignCapture("inventory-05-list.png"); await RawClick("storage_view");
        foreach (var size in new[] { new Vector2I(1024,720), new Vector2I(1600,900) })
        {
            GetWindow().Size = size; await Frames(8); AssertManualLayout(); await DesignCapture($"inventory-06-{size.X}.png");
        }
        GetWindow().Size = new Vector2I(1280,800); await Frames(8);
        var grouped = ChapterSession.FromJson(fixture.ToJson());
        var steel = grouped.StoredStock.First(i => i.Kind == "M01");
        Require(grouped.Transfer(steel.Id, "carry", 2), "Protected group fixture transfer");
        var protectedSteel = grouped.CarriedItems.First(i => i.Kind == "M01");
        Require(grouped.SetCarryProtection(protectedSteel.Id, true), "Protected group fixture flag");
        Require(grouped.Transfer(steel.Id, "carry", 3), "Second source fixture transfer");
        var split = grouped.CarriedItems.First(i => i.Kind == "M01" && !i.KeepOnPerson);
        Require(grouped.Transfer(split.Id, "warehouse", 3), "Separate physical batches return to warehouse");
        Game.ReplaceSession(grouped); await Frames(8); await RawClick("inventory");
        var search = (LineEdit)Game.GetNode("ChapterPresentation").FindChild("StockSearch", true, false);
        search.Text = "废钢"; await RawClick("stock_search");
        var groupedTile = Game.Buttons[$"stock_{steel.Id}_0_0"];
        Require(groupedTile.GetMeta("quantity").AsInt32() == 18 && groupedTile.GetMeta("item_ids").AsString().Split(',').Length == 2, "Two source batches display their summed quantity");
        await RawClick($"stock_{steel.Id}_0_0"); await DesignCapture("inventory-07-grouped.png");
        var groupedBefore = Game.Session.ToJson(); await RawClick("stock_more");
        Require(Game.Buttons.Keys.Count(k => k.StartsWith("stock_batch_detail_")) == 2, "Source disclosure retains both exact identities");
        await DesignCapture("inventory-08-sources.png"); await RawClick("stock_batch_detail_" + split.Id);
        Require(Game.Buttons.ContainsKey("transfer_stack_" + split.Id) && Game.Session.ToJson() == groupedBefore, "Selecting a source returns to that exact batch without moving it");
        if (!Game.Buttons.ContainsKey("storage_batches")) await RawClick("tools_stock");
        await Click("storage_batches"); await RawClick($"stock_{steel.Id}_0_0");
        if (Game.Buttons["tools_stock"].Text.Contains("收起")) await RawClick("tools_stock");
        await RawClick("stock_quantity");
        var quantity = (SpinBox)Game.GetNode("ChapterPresentation").FindChild("StockQuantity", true, false);
        Require(quantity.MaxValue == 18, "Group quantity dialog supports the full aggregate"); quantity.Value = 16; await Frames(3);
        await DesignCapture("inventory-09-quantity.png"); await RawClick("quantity_commit");
        Require(Game.Session.StoredStock.Where(i => i.Kind == "M01").Sum(i => i.Quantity) == 2, "Quantity transfer spans source boundaries exactly once");
        Require(Game.Session.CarriedItems.Single(i => i.Id == protectedSteel.Id).Quantity == 2 && Game.Session.CarriedItems.Single(i => i.Id == protectedSteel.Id).KeepOnPerson, "Grouped movement preserves the separate protected batch");
        await RawClick("stock_undo"); Require(Game.Session.ToJson() == groupedBefore, "Grouped undo restores all source identities and quantities");
        var carry = ((Control)Game.GetNode("ChapterPresentation").FindChild("CarryContainer", true, false)).GetGlobalRect();
        await Drag(steel.Id, new Vector2(carry.GetCenter().X, carry.End.Y - 15));
        Require(Game.Session.StoredStock.All(i => i.Kind != "M01"), "Dragging a merged item transfers every displayed unit");
        await RawClick("stock_undo"); Require(Game.Session.ToJson() == groupedBefore, "Merged drag has a single atomic undo");
        await RawClick($"stock_{protectedSteel.Id}_0_0"); await RawClick("transfer_stack_" + protectedSteel.Id);
        Require(Game.Buttons.ContainsKey("cancel_action"), "Protected item still requires explicit override"); await RawClick("cancel_action");
        await RawClick("stock_clear"); await RawClick("workspace_density"); await DesignCapture("inventory-10-compact.png");
        await RawClick("workspace_density"); await RawClick("close_panel");
        GetWindow().Size = new Vector2I(1024,720); await Frames(8);
        await PressKey(Key.Escape); await Click("font_150"); await Click("resume"); await Click("inventory");
        AssertManualLayout(); AssertStorageGrid(); await DesignCapture("inventory-11-large-type.png");
        await Click("close_panel"); await PressKey(Key.Escape); await Click("font_100"); await Click("resume");
        var field = new ChapterSession(); field.Depart(); field.Move("A03"); field.Act("scrap"); Game.ReplaceSession(field); await Frames(8);
        await Click("inventory"); await DesignCapture("inventory-12-field.png");
        var ground = field.State.Stock.First(i => i.Container.StartsWith("ground:")); var beforeField = field.State.Pressure;
        await Click($"stock_{ground.Id}_0_0"); await Click((ground.Quantity > 1 ? "pickup_stack_" : "pickup_") + ground.Id);
        Require(Game.Session.CarriedItems.Any(i => i.Kind == ground.Kind) && Game.Session.State.Pressure >= beforeField, "Field pickup uses the real source and pressure rules");
        GD.Print("UI_INVENTORY_VERIFIED | direct equip, slot filter, comparison return, grid/list, batch selection, grouping identities, quantity across sources, protected separation, merged drag/undo, 3 resolutions, 150% type, field pickup");
    }
}
