using CardRPG.Core;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CardRPG;

public partial class ChapterVerification
{
    private async Task Pointer(Vector2 position, MouseButton button, bool pressed)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = button, Pressed = pressed }, true);
        await Frames(2);
    }

    private async Task Drag(long id, Vector2 target, bool cancel = false)
    {
        var tile = Game.Buttons[$"stock_{id}_0_0"];
        for (var ancestor = tile.GetParent(); ancestor != null; ancestor = ancestor.GetParent()) if (ancestor is ScrollContainer scroll) scroll.EnsureControlVisible(tile);
        await Frames(4); var source = tile.GetGlobalRect().GetCenter();
        await Pointer(source, MouseButton.Left, true);
        foreach (var position in new[] { source + new Vector2(25, 10), (source + target) / 2, target })
        { GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position, Relative = new Vector2(25, 10), ButtonMask = MouseButtonMask.Left }, true); await Frames(3); }
        Require(GetViewport().GuiIsDragging(), "Natural pointer movement starts inventory drag");
        if (cancel)
        {
            await Pointer(target, MouseButton.Right, true); await Pointer(target, MouseButton.Right, false);
            Require(!GetViewport().GuiIsDragging(), "Right click cancels drag");
            Require(!Game.Buttons.ContainsKey("menu_move"), "Cancelling drag does not open context menu");
        }
        await Pointer(target, MouseButton.Left, false); await Frames(5);
        GD.Print($"MANAGEMENT_DRAG id={id} target={target} cancelled={cancel} success={GetViewport().GuiIsDragSuccessful()} items=" + string.Join(";", Game.Session.State.Stock.Where(i => i.Id == id).Select(i => $"{i.Id}:{i.Container}:{i.Quantity}")));
    }

    private async Task VerifyManagement(string completed)
    {
        GetWindow().Size = new Vector2I(1280, 800);
        var fixture = ChapterSession.FromJson(completed);
        var steel = fixture.StoredStock.First(i => i.Kind == "M01");
        Require(fixture.Transfer(steel.Id, "carry", 2), "Prepare an independent two-item batch");
        Game.ReplaceSession(fixture); await Frames(8); await Click("inventory");
        var batch = Game.Session.CarriedItems.First(i => i.Kind == "M01" && i.Quantity == 2);
        await Click($"stock_{batch.Id}_0_0");
        var unchanged = Game.Session.ToJson(); var tile = Game.Buttons[$"stock_{batch.Id}_0_0"];
        await Pointer(tile.GetGlobalRect().GetCenter(), MouseButton.Right, true); await Pointer(tile.GetGlobalRect().GetCenter(), MouseButton.Right, false);
        Require(Game.Buttons.ContainsKey("menu_quantity") && Game.Session.ToJson() == unchanged, "Right click opens actions without moving a batch");
        await Capture("management-context-menu.png"); await PressKey(Key.Escape);
        Require(!Game.Buttons.ContainsKey("menu_quantity") && Game.Buttons.ContainsKey("stock_more"), "Escape dismisses menu before leaving inventory");
        await Click("stock_quantity"); var quantity = Game.GetNode<Control>("ChapterPresentation").FindChild("StockQuantity", true, false) as SpinBox;
        Require(quantity != null, "Quantity dialog uses a real numeric input");
        quantity!.Value = 1;
        await Frames(3);
        var edit = quantity.GetLineEdit(); edit.GrabFocus();
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, ShiftPressed = true, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, ShiftPressed = true, Pressed = false }); await Frames();
        Require(Game.Session.ToJson() == unchanged, "Shift Enter in quantity input cannot trigger inventory shortcut");
        await PressKey(Key.M); Require(Game.Buttons.ContainsKey("quantity_commit"), "Quantity dialog owns page shortcuts");
        await Click("quantity_set");
        await Click("stock_more"); await Click("menu_detail");
        Require(Game.Session.ToJson() == unchanged && Game.Buttons.ContainsKey("detail_return"), "Pinned detail is read only");
        await Click("detail_return");
        var outside = new Vector2(8, 8); await Drag(batch.Id, outside);
        Require(Game.Session.ToJson() == unchanged, "Dropping outside a container never discards or moves items");
        var target = ((Control)Game.GetNode<Control>("ChapterPresentation").FindChild("ExternalContainer", true, false)).GetGlobalRect().GetCenter();
        await Drag(batch.Id, target, true); Require(Game.Session.ToJson() == unchanged, "Cancelled drag preserves exact state");
        await Drag(batch.Id, target);
        await Capture("management-drag-result.png");
        Require(Game.Session.State.Stock.Single(i => i.Id == batch.Id).Quantity == 1, "Drag moves chosen quantity instead of silently moving the entire batch");
        Require(Game.Buttons.ContainsKey("stock_undo"), "Camp transfer exposes a named undo");
        await Click("stock_undo"); Require(Game.Session.ToJson() == unchanged, "Undo restores only the most recent camp transfer");
        await Click($"stock_{batch.Id}_0_0"); await Click("stock_more"); await Click("menu_keep");
        Require(Game.Session.State.Stock.Single(i => i.Id == batch.Id).KeepOnPerson, "Keep flag applies to the actual selected identity");
        await Click("stock_more"); await Click("menu_move");
        Require(Game.Buttons.ContainsKey("confirm_action"), "Explicit movement of protected stock asks to override");
        var protectedState = Game.Session.ToJson(); await PressKey(Key.M);
        Require(Game.Buttons.ContainsKey("confirm_action") && Game.Session.ToJson() == protectedState, "Confirmation cannot be lost through a page shortcut");
        await Click("cancel_action");
        var search = (LineEdit)Game.GetNode<Control>("ChapterPresentation").FindChild("StockSearch", true, false);
        search.GrabFocus(); await PressKey(Key.I);
        Require(Game.Buttons.ContainsKey("stock_more") && Game.Session.ToJson() == protectedState, "Typing inventory key in search does not toggle the page");
        await Capture("management-storage.png");
        await VerifyConvenience();
        Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames();
        await VerifyPlanningInterface(completed);
        GD.Print("UI_MANAGEMENT_VERIFIED | real right click, quantity input scope, pinned details, natural drag/drop/cancel, protected stock, named undo, text input scope");
    }

    private async Task VerifyPlanningInterface(string completed)
    {
        var fixture = ChapterSession.FromJson(completed); fixture.State.World.Add("trial-complete"); fixture.State.World.Add("dark-door");
        var gloves = fixture.State.Stock.FirstOrDefault(i => i.Kind == "G26");
        if (gloves?.Container == "hands") { Require(fixture.Equip("G26"), "Remove test gloves before planning fixture"); gloves = fixture.State.Stock.First(i => i.Kind == "G26"); }
        if (gloves != null && gloves.Container is "warehouse" or "carry") fixture.DiscardAtBase(gloves.Id, 1);
        Require(!fixture.HasPhysical("G26"), "Planning fixture requires an unowned recipe");
        Game.ReplaceSession(fixture); await Frames();
        await Click("depart", false); await Click("prepare_presets");
        var name = (LineEdit)Game.GetNode<Control>("ChapterPresentation").FindChild("PresetName", true, false); name.Text = "验证整备";
        await Click("preset_save"); Require(Game.Session.State.PreparationPresets.Any(p => p.Name == "验证整备"), "Named preparation is saved separately from applying it");
        var presetButton = Game.Buttons.First(p => p.Key.StartsWith("preset_select_") && p.Value.Text.Contains("验证整备")).Key;
        await Click(presetButton); var before = Game.Session.ToJson(); await Click("preset_apply", false);
        Require(Game.Session.ToJson() == before, "Preset confirmation is pure"); await Capture("management-preset-preview.png"); await Click("cancel_action");
        await Click("nav_craft"); await Click("craft_tab_recipes"); await Click("recipe_G26"); await Click("recipe_plan_toggle");
        Require(Game.Session.State.RecipePlan.Contains("G26"), "Recipe tracking changes only its explicit planning state");
        before = Game.Session.ToJson(); await Click("recipe_plan"); await Capture("management-material-plan.png");
        Require(Game.Session.ToJson() == before, "Combined material plan is read only"); await Click("plan_return");
        await Click("craft_tab_mods"); before = Game.Session.ToJson(); await Click("mod_deck_preview");
        Require(Game.Session.ToJson() == before && Game.Buttons.ContainsKey("detail_return"), "Modification deck preview neither buys nor installs");
        await Click("detail_return"); await Capture("management-modifications.png");
        await Click("craft_tab_storage"); await Click("storage_craft_bags");
        Require(new[] { "BP01", "BP02", "BP03" }.All(k => Game.Buttons.ContainsKey("bag_spec_" + k)), "All three bag specifications can be compared together");
        await Capture("management-bag-comparison.png");
        await Click("storage_craft_supplies"); Require(Game.Buttons.ContainsKey("make_U01"), "Repeatable supplies have a dedicated quantity workspace");
        await Capture("management-supply-quantity.png");
        await Click("nav_journal"); await Click("quest_tab_log"); await Capture("management-structured-log.png");
        Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames();
        await PressKey(Key.Escape); await Click("system_keys"); await Click("bind_map"); await PressKey(Key.I);
        Require(Game.Buttons.ContainsKey("close_panel") && !Game.Buttons.ContainsKey("bind_map"), "Conflicting key remains in rebinding dialog");
        await PressKey(Key.F9); Require(Game.Buttons["bind_map"].Text.Contains("F9"), "Successful binding updates its visible label");
        await Click("keys_return"); await Click("resume"); await PressKey(Key.F9);
        Require(Game.Buttons.ContainsKey("map_place_A01"), "Rebound key opens the intended page"); await PressKey(Key.Escape);
        await PressKey(Key.Escape); await Click("system_keys"); await Click("keys_reset"); await Click("keys_return"); await Click("resume");
        GetWindow().Size = new Vector2I(1100, 760); await Frames(8);
        await PressKey(Key.Escape); await Click("font_150"); await Click("resume"); await Click("inventory"); await Click("gear_tab_cargo");
        await Capture("management-large-type.png"); AssertManualLayout(); AssertStorageGrid();
        await Click("storage_paired"); AssertManualLayout();
        Require(Game.Buttons.Where(p => p.Key.StartsWith("paired_")).All(p => p.Value.Size.X >= 140), "Paired quantity columns retain usable widths at large type");
        await Capture("management-paired-large-type.png"); await Click("storage_paired");
        await Click("nav_map"); await Click("map_place_A05"); AssertManualLayout(); await Capture("management-map-large-type.png");
        await Click("nav_prepare"); AssertManualLayout(); await Capture("management-prepare-large-type.png");
        await Click("close_panel"); await PressKey(Key.Escape); await Click("font_100"); await Click("resume");
        GD.Print("UI_PLANNING_VERIFIED | named preset preview/cancel, combined material plan, modification deck preview, bag specification table, supply quantity, structured log");
    }

    private async Task VerifyConvenience()
    {
        var before = Game.Session.ToJson(); var material = Game.Session.CarriedItems.First(i => i.Kind == "M01");
        await Click($"stock_{material.Id}_0_0");
        await PressKey(Key.F6);
        var warehouse = Game.GetNode<Control>("ChapterPresentation").FindChild("ExternalContainer", true, false);
        Require(warehouse.IsAncestorOf(GetViewport().GuiGetFocusOwner()), "F6 moves from carry list into warehouse without walking every item");
        await Click("storage_paired");
        var cells = Game.Buttons.Where(p => p.Key.StartsWith("paired_M01_") && p.Value.HasMeta("source")).ToArray();
        foreach (var source in new[] { "carry", "warehouse" })
            Require(cells.Where(p => p.Value.GetMeta("source").AsString() == source).Sum(p => p.Value.GetMeta("quantity").AsInt32()) == Game.Session.State.Stock.Where(i => i.Kind == "M01" && i.Container == source).Sum(i => i.Quantity), "Paired count equals physical quantities in " + source);
        await Click(cells.First(p => p.Key.EndsWith("_carry")).Key);
        Require(!Game.Buttons.ContainsKey("stock_more"), "Aggregate source cell cannot leave a previous batch action active");
        Require(Game.Session.ToJson() == before, "Quantity cell expands source batches without transferring or guessing a batch");
        await Click("paired_batch_" + material.Id); await Capture("management-paired-table.png");
        await Click("stock_more"); await Click("menu_detail");
        var recipe = Game.Buttons.Keys.First(k => k.StartsWith("material_recipe_")); await Click(recipe);
        Require(Game.Session.ToJson() == before, "Material recipe reverse lookup is read only"); await Capture("management-material-recipe.png");
        await Click("material_recipe_return"); await Click("detail_return");
        Require(Game.Buttons.ContainsKey("paired_batch_" + material.Id), "Reverse lookup returns to the same table source and batch");
        await Click("storage_paired");
        var edit = (LineEdit)Game.GetNode<Control>("ChapterPresentation").FindChild("StockSearch", true, false); edit.GrabFocus();
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true }); await Frames();
        await Click("stock_more"); await Click("menu_move");
        Require(GetViewport().GuiGetFocusOwner()?.Name == "cancel_action", "Dangerous confirmation starts on cancel");
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true, Echo = true });
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }); await Frames();
        Require(Game.Buttons.ContainsKey("confirm_action") && Game.Session.ToJson() == before, "Held confirmation and old release cannot advance the new dialog");
        await PressKey(Key.Enter); Require(!Game.Buttons.ContainsKey("confirm_action"), "A fresh confirmation key works immediately on cancel");
        var armor = Game.Session.StoredStock.First(i => ChapterContent.Gear.TryGetValue(i.Kind, out var gear) && gear.Slot == "body");
        if (!Game.Buttons.ContainsKey("wear_body")) await Click("storage_loadout");
        await Click($"stock_{armor.Id}_0_0");
        await Drag(armor.Id, Game.Buttons["wear_weapon"].GetGlobalRect().GetCenter());
        Require(Game.Session.ToJson() == before && Game.Buttons.ContainsKey("stock_more"), "Wrong equipment slot rejects drag without changing inventory");
        await Drag(armor.Id, Game.Buttons["wear_body"].GetGlobalRect().GetCenter());
        Require(Game.Session.ToJson() == before && Game.Buttons.ContainsKey("equip_" + armor.Kind), "Equipment drop opens comparison and requires a separate equip action");
        await Capture("management-equipment-drop.png"); await PressKey(Key.Escape);
        Require(Game.Buttons.ContainsKey("stock_more"), "Equipment comparison returns to original inventory mode");
        await Click("nav_map"); Require(Game.GetNode<Control>("ChapterPresentation").FindChild("RouteMap", true, false) is ChapterRouteMap, "Route map available");
        await Click("map_place_A05"); await Click("map_waypoint");
        var map = (ChapterRouteMap)Game.GetNode<Control>("ChapterPresentation").FindChild("RouteMap", true, false);
        Require(map.Selected == "A05" && map.Waypoint == "A05" && map.Route.SequenceEqual(Game.Session.KnownRoute("A05")), "Selected marker and route refer to the same place");
        Require(Game.Session.ToJson() == before, "Setting map target never moves, deploys, or advances pressure");
        await Capture("management-map-target.png"); await Click("map_current");
        map = (ChapterRouteMap)Game.GetNode<Control>("ChapterPresentation").FindChild("RouteMap", true, false);
        Require(map.Selected == Game.Session.State.Start && map.Waypoint == "A05", "Recenter preserves independent temporary target");
        await Click("map_clear_waypoint"); await Click("nav_prepare");
        Require(Game.GetNode<Control>("ChapterPresentation").FindChild("DeploymentMap", true, false) == null, "Preparation starts with compact deployment cards");
        await Click("prepare_map"); Require(Game.GetNode<Control>("ChapterPresentation").FindChild("DeploymentMap", true, false) != null, "Full deployment map remains explicitly available");
        await Click("prepare_map"); Require(Game.Session.ToJson() == before, "Expanding preparation map preserves chosen deployment and inventory");
        await Click("nav_character"); await Click("deck_sources");
        var matrix = (GridContainer)Game.GetNode<Control>("ChapterPresentation").FindChild("DeckSourceMatrix", true, false);
        var cellsByCard = matrix.GetChildren().OfType<Control>().Where(c => c.HasMeta("source_card")).GroupBy(c => c.GetMeta("source_card").AsString());
        foreach (var group in cellsByCard)
            Require(group.Sum(c => c.GetMeta("copies").AsInt32()) == Game.Session.Deck().Count(id => id == group.Key), "Matrix row matches actual deck multiplicity: " + group.Key);
        Require(Game.Session.ToJson() == before, "Deck source matrix leaves cards and inventory unchanged"); await Capture("management-deck-sources.png");
        await Click("deck_sources");
        GD.Print("UI_CONVENIENCE_VERIFIED | paired quantities and batch selection, reverse recipes, focus region, held-input boundary, equipment slot drop, waypoint, recenter, compact preparation");
    }
}
