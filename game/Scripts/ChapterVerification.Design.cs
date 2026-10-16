using CardRPG.Core;
using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CardRPG;

public partial class ChapterVerification
{
    private async Task DesignCapture(string name)
    {
        GetViewport().GuiReleaseFocus();
        GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(2, 2), GlobalPosition = new Vector2(2, 2) }, true);
        await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
        await Capture(name);
    }

    private async Task VerifyDesign()
    {
        GetWindow().Size = new Vector2I(1280, 800); Game.ReplaceSession(new ChapterSession()); await Frames(8);
        await DesignCapture("design-01-camp.png");
        await Click("depart", false); await DesignCapture("design-02-departure.png");
        Require(!((Control)Game.GetNode<Control>("ChapterPresentation").FindChild("Tools_prepare", true, false)).IsVisibleInTree(), "Departure starts with focused information");
        await Click("tools_prepare"); await Click("close_panel"); await Click("depart", false);
        Require(((Control)Game.GetNode<Control>("ChapterPresentation").FindChild("Tools_prepare", true, false)).IsVisibleInTree(), "Expanded preparation tools survive navigation");
        await Click("tools_prepare"); await Click("prepare_depart"); await Click("move_A03");
        await DesignCapture("design-03-exploration.png");
        await Click("act_relay");
        Require(Game.Buttons.ContainsKey("loot_manage") && !Game.Buttons.ContainsKey("stock_search"), "World pickup opens focused loot, not inventory management");
        await DesignCapture("design-04-pickup.png");
        var item = Game.Session.State.Stock.Single(i => i.Kind == "T01" && i.Container.StartsWith("ground:"));
        var pressure = Game.Session.State.Pressure;
        await Click("pickup_" + item.Id);
        Require(Game.Session.CarriedItems.Any(i => i.Kind == "T01") && Game.Session.State.Pressure == pressure, "Direct pickup honors the free initial collection");
        await Click("loot_return");
        await Click("act_rescue"); await Click("story_continue");
        await Click("move_A01"); await Click("act_extract"); await DesignCapture("design-05-return.png");
        await Click("camp"); await Click("camp_quests"); await Click("deliver_Q01");
        await DesignCapture("design-06-story.png"); await Click("story_continue"); await Click("close_panel");

        var full = new ChapterSession(); full.Depart(); full.Move("A03"); full.Act("relay");
        full.Pickup(full.State.Stock.Single(i => i.Kind == "T01").Id, 1); full.Act("scrap");
        foreach (var material in full.State.Stock.Where(i => i.Kind is "M01" or "M02").ToArray()) full.Pickup(material.Id, material.Quantity);
        full.Act("cloth"); Game.ReplaceSession(full); await Frames(); await Click("field_loot");
        var cloth = Game.Session.State.Stock.Single(i => i.Kind == "M07");
        await Click("pickup_stack_" + cloth.Id);
        Require(Game.Buttons.ContainsKey("space_pickup"), "Full pockets lead directly to a space decision");
        await DesignCapture("design-19-space-choice.png");
        var supply = Game.Session.CarriedItems.Single(i => i.FreeSupply);
        await Click("space_drop_" + supply.Id);
        if (Game.Buttons.ContainsKey("confirm_action")) await Click("confirm_action");
        Require(Game.Buttons.ContainsKey("space_pickup"), "Dropping an item keeps the current pickup decision");
        await Click("space_pickup");
        if (Game.Buttons.ContainsKey("confirm_action")) await Click("confirm_action");
        Require(Game.Session.CarriedItems.Any(i => i.Kind == "M07"), "Space decision completes with the requested material");

        var veteran = ChapterSession.FromJson(JsonSerializer.Serialize(new ChapterState
        {
            ContentVersion = "chapter1-outer-ring-v1", Extractions = 2, Owned = ChapterContent.Gear.Keys.ToHashSet(),
            Bank = [20, 20, 20], Cargo = [0, 0, 0], Done = ["Q01", "Q02", "S01"], World = ["station", "trial-complete"]
        }));
        veteran.State.WarehouseLevel = 3; Game.ReplaceSession(veteran); await Frames();
        await Click("inventory"); await DesignCapture("design-07-inventory.png");
        await Click("tools_stock"); await Click("workspace_density");
        await DesignCapture("design-08-inventory-compact.png");
        await Click("stock_select_visible");
        Require(Game.Buttons.ContainsKey("stock_clear_selection"), "Batch management remains directly usable in expanded layout");
        await Click("stock_clear_selection"); await Click("workspace_density");
        await Click("nav_character"); await DesignCapture("design-09-character.png");
        await Click("tools_deck"); await DesignCapture("design-10-deck-analysis.png");
        await Click("nav_craft"); await Click("craft_tab_storage"); await DesignCapture("design-11-backpacks.png");
        await Click("nav_gear"); await Click("gear_tab_weapon"); await Click("inspect_G02"); await DesignCapture("design-12-equipment.png");
        await Click("nav_craft"); await Click("craft_tab_mods"); await DesignCapture("design-13-mods.png");
        await Click("nav_journal"); await DesignCapture("design-14-journal.png"); await Click("close_panel");

        var crafting = new ChapterSession(); crafting.Depart(); crafting.Move("A03"); crafting.Act("cloth");
        foreach (var ground in crafting.GroundHere.ToArray()) crafting.PickupAll(ground.Key);
        crafting.Act("scrap");
        foreach (var ground in crafting.GroundHere.ToArray()) crafting.PickupAll(ground.Key);
        crafting.Move("A01"); crafting.Act("extract"); crafting.ReturnToCamp();
        Game.ReplaceSession(crafting); await Frames();
        await Click("craft"); await Click("craft_tab_storage"); await Click("make_BP01");
        await Click("crafted_equip_BP01");
        Require(Game.Session.CarryCapacity == 12 && Game.Session.Equipped.Contains("BP01"), "Crafted backpack is equippable without leaving its crafting page");
        await DesignCapture("design-15-crafted-equipped.png");

        foreach (var size in new[] { new Vector2I(1280,800), new Vector2I(1024,720), new Vector2I(1600,900) })
        {
            var combat = new ChapterSession(); combat.Depart(); combat.Move("A03"); combat.Act("K02");
            Game.ReplaceSession(combat); GetWindow().Size = size; await Frames(8);
            AssertBattleLayout(); await DesignCapture($"design-16-battle-{size.X}.png");
            var before = Game.Session.ToJson(); var battle = Game.Session.State.Battle!;
            var index = Enumerable.Range(0, battle.Hand.Count).First(i => Game.Session.CanPlay(i, 0));
            await Click("card_" + index, false);
            Require(Game.Session.ToJson() == before, "Selecting a card previews without committing");
            AssertHandVisible(); await DesignCapture($"design-17-preview-{size.X}.png");
            await Click("play_selected"); Require(Game.Session.ToJson() != before, "Confirm executes the selected card");
        }
        var routing = new ChapterSession(); routing.Depart(); Game.ReplaceSession(routing); await Frames();
        await PressKey(Key.M); await Click("map_place_A04");
        var route = Game.Session.KnownRoute("A04"); var startPressure = Game.Session.State.Pressure;
        await DesignCapture("design-18-map.png"); await Click("map_move_next");
        Require(Game.Session.State.Location == route[1] && Game.Session.State.Pressure == startPressure + 1, "Map navigation executes exactly one real scene step");
        await VerifyRefinement(veteran.ToJson());
        GD.Print("UI_DESIGN_VERIFIED | focused pickup, remembered disclosure, veteran batch actions, direct crafted equip, card preview/commit, 3 battle resolutions, one-step map travel");
    }

    private async Task VerifyRefinement(string veteran)
    {
        GetWindow().Size = new Vector2I(1280, 800); Game.ReplaceSession(ChapterSession.FromJson(veteran)); await Frames(8);
        await Click("inventory");
        var root = Game.GetNode<Control>("ChapterPresentation");
        if (root.FindChild("Tools_stock", true, false) is Control { Visible: true }) await Click("tools_stock");
        if (Game.Buttons["workspace_density"].Text.Contains("紧凑")) await Click("workspace_density");
        root = Game.GetNode<Control>("ChapterPresentation");
        var grid = (Control)root.FindChild("StorageGrid_warehouse", true, false);
        var viewport = grid.GetParent().GetParent<ScrollContainer>();
        Require(grid.GetChildren().OfType<Control>().Count(c => viewport.GetGlobalRect().Encloses(c.GetGlobalRect())) >= 8, "Standard inventory shows at least eight complete physical batches at 1280x800");
        var toolbarHeight = ((Control)root.FindChild("StockToolbar", true, false)).Size.Y;
        await Click("stock_select_visible");
        root = Game.GetNode<Control>("ChapterPresentation");
        Require(Math.Abs(((Control)root.FindChild("StockToolbar", true, false)).Size.Y - toolbarHeight) < 1, "Batch selection does not grow the toolbar");
        await DesignCapture("refine-01-batch.png"); await Click("stock_clear_selection");
        await Click("storage_paired");
        root = Game.GetNode<Control>("ChapterPresentation");
        var pairedHeader = (Control)root.FindChild("PairedStockHeader", true, false);
        Require(pairedHeader.GetParent() is not ScrollContainer && !pairedHeader.GetParent().GetParent().GetChildren().OfType<ScrollContainer>().Any(s => s.IsAncestorOf(pairedHeader)), "Comparison headings remain outside the scrolling list");
        await DesignCapture("refine-02-paired.png");
        await Click("storage_paired"); await Click("workspace_density"); await Click("nav_character");
        Require(Game.Buttons["workspace_density"].Text.Contains("舒展"), "Inventory density does not silently change another page");
        await Click("nav_gear"); Require(Game.Buttons["workspace_density"].Text.Contains("紧凑"), "Inventory remembers its own density");
        await Click("workspace_density"); await Click("close_panel");

        foreach (var handCount in new[] { 7, 10 })
        {
            var combat = new ChapterSession(); combat.Depart(); combat.Move("A03"); combat.Act("K02");
            while (combat.State.Battle!.Hand.Count < handCount) combat.State.Battle.Hand.Add("C01");
            Game.ReplaceSession(combat); GetWindow().Size = new Vector2I(1024, 720); await Frames(8);
            var last = Game.Buttons["card_" + (handCount - 1)]; last.GrabFocus(); await Frames(8);
            var handViewport = (ScrollContainer)Game.GetNode<Control>("ChapterPresentation").FindChild("HandViewport", true, false);
            Require(handViewport.GetGlobalRect().Encloses(last.GetGlobalRect()) && last.Size.X >= 118, "Large hands scroll to focused cards without reducing readability");
            AssertHandVisible(); await DesignCapture($"refine-03-hand-{handCount}.png");
        }
        var forecast = new ChapterSession(); forecast.Depart(); forecast.Move("A03"); forecast.Act("K02");
        Game.ReplaceSession(forecast); GetWindow().Size = new Vector2I(1280,800); await Frames(8);
        var state = Game.Session.ToJson(); var first = ChapterContent.Cards[forecast.State.Battle!.Hand[0]];
        var direct = forecast.Preview(s => s.EndTurn())!;
        var played = forecast.Preview(s => s.PlayCard(0,0))!;
        var sequential = played.State.Phase == ChapterPhase.Battle ? played.Preview(s => s.EndTurn())! : played;
        await Click("card_0", false);
        root = Game.GetNode<Control>("ChapterPresentation");
        Require(((Label)root.FindChild("CurrentEndHealth", true, false)).Text.Contains("→ " + direct.State.Health), "Direct ending preview matches the current state");
        Require(((Label)root.FindChild("SelectedEndHealth", true, false)).Text.Contains("→ " + sequential.State.Health), "Selected-card ending preview includes card and enemy resolution");
        Require(Game.Session.ToJson() == state, "Both forecasts preserve health, RNG and card order");
        Require(Game.Buttons["end_turn"].Text.Contains("当前状态"), "Ending while a card is selected explicitly excludes the unplayed preview");
        await Click("cancel_card"); Require(Game.Session.ToJson() == state, "Cancelling a forecast has no gameplay effects");
        forecast.State.Battle!.Enemies[0].Health = first.Damage; Game.ReplaceSession(forecast); await Frames(8); await Click("card_0", false);
        Require(((Label)Game.GetNode<Control>("ChapterPresentation").FindChild("SelectedEndHealth", true, false)).Text.Contains("战斗结束"), "Lethal preview never invents a subsequent enemy turn");
        await DesignCapture("refine-04-lethal-preview.png");

        var report = new ChapterSession(); report.Depart(); report.Move("A03"); report.Act("relay"); report.PickupAll(report.GroundHere.First().Key); report.Move("A01"); report.Act("extract");
        report.State.LastReport!.Gained["M07"] = 12; report.State.LastReport.Gained["G26"] = 1;
        Game.ReplaceSession(report); GetWindow().Size = new Vector2I(1024,720); await Frames(8);
        foreach (var kind in report.State.LastReport.Gained.Keys)
        {
            var label = (Label)Game.GetNode<Control>("ChapterPresentation").FindChild("ReportItem_" + kind, true, false);
            Require(label.Size.X > 300 && label.GetLineCount() == 1, "Debrief rewards retain horizontal reading width");
        }
        await DesignCapture("refine-05-multiple-rewards.png");
        var largeBattle = new ChapterSession(); largeBattle.Depart(); largeBattle.Move("A03"); largeBattle.Act("K02");
        Game.ReplaceSession(largeBattle); await Frames(8); await PressKey(Key.Escape); await Click("font_150"); await Click("resume");
        await Click("card_0", false); AssertBattleLayout();
        var turnPanel = (Control)Game.GetNode<Control>("ChapterPresentation").FindChild("EndTurnForecast", true, false);
        Require(GetViewport().GetVisibleRect().Encloses(turnPanel.GetGlobalRect()), "Large-font battle forecast and ending actions stay inside the window");
        await DesignCapture("refine-06-large-font-battle.png");
        await Click("cancel_card"); await PressKey(Key.Escape); await Click("font_100"); await Click("resume");
        GD.Print("UI_REFINEMENT_VERIFIED | eight rows, stable batch toolbar, sticky headings, per-page density, 7/10 readable cards, sequential and lethal forecasts, multi-item debrief");
    }
}
