using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CardRPG;

/// <summary>Real GUI input against the chapter entry point. Never touches player saves.</summary>
public partial class ChapterVerification : Node
{
    public ChapterGame Game { get; set; } = null!;
    private string? _captures;
    private async Task Frames(int count = 3) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
    public override async void _Ready()
    {
        try
        {
            _captures = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?[14..];
            if (_captures != null) Directory.CreateDirectory(_captures);
            if (OS.GetCmdlineUserArgs().Contains("--verify-management-only") || OS.GetCmdlineUserArgs().Contains("--verify-completion-only") || OS.GetCmdlineUserArgs().Contains("--verify-layout-only"))
            {
                var state = new ChapterState { ContentVersion = "chapter1-outer-ring-v1", Extractions = 1, Owned = ChapterContent.Gear.Keys.ToHashSet(), Bank = [20,20,20], Cargo = [0,0,0], Done = ["Q01", "Q02", "S01"], World = ["station"] };
                var fixture = ChapterSession.FromJson(System.Text.Json.JsonSerializer.Serialize(state)); fixture.State.WarehouseLevel = 3; fixture.Equip("BP01");
                if(OS.GetCmdlineUserArgs().Contains("--verify-layout-only")){await VerifyManual(fixture.ToJson());GD.Print("UI_LAYOUT_VERIFIED");}else if(OS.GetCmdlineUserArgs().Contains("--verify-completion-only"))await VerifyCompletion(fixture.ToJson());else await VerifyManagement(fixture.ToJson()); GetTree().Quit(0); return;
            }
            await Frames(8); AssertArtAssets(); AssertImmersiveScene(); AssertSceneArt("camp"); await Capture("01-camp.png");
            await VerifyGraphicInspection();
            await Click("depart", false); await Click("starter_G02"); await Click("starter_G01");
            await Capture("01-preparation.png"); await Click("close_panel");
            await Click("help"); await Capture("01-help.png"); await Click("close_panel");
            await PressKey(Key.M); var unchanged = Game.Session.ToJson();
            Require(Game.Buttons.ContainsKey("close_panel"), "Map opens with M");
            await Capture("02-map.png"); await PressKey(Key.Escape); Require(unchanged == Game.Session.ToJson(), "Map is read only");
            await PressKey(Key.I); await PressKey(Key.Escape); await Click("depart");
            AssertImmersiveScene(); await Capture("02-gate.png"); await Click("move_A03");
            await Capture("03-scrap.png"); await Click("act_rescue"); AssertSceneArt("A03-rescued");
            await Capture("03-scrap-rescued.png");
            await Click("act_relay"); await CollectLoot(); await Click("act_cloth"); await CollectLoot();
            await Click("act_scrap"); await CollectLoot();
            Require(Game.Session.CarryUsed == 4, "Bare pockets filled without overflowing");
            Require(Game.Session.State.Stock.Any(i => i.Kind == "M02" && i.Container.StartsWith("ground:")), "Full pockets leave wire at its source");
            await Click("field_loot"); await Capture("inventory-full-pockets.png"); await Click("close_panel");
            await Click("move_A01"); await Click("act_extract"); await Capture("06-extraction.png"); await Click("camp");
            await Click("camp_quests"); await Click("deliver_Q01"); await Click("deliver_S07"); await Click("close_panel");
            AssertSceneArt("camp-lit"); await Capture("07-lit-home.png");
            await Click("craft"); await Click("craft_tab_storage"); await Click("make_BP01"); await Capture("inventory-backpack-crafting.png");
            await Click("nav_gear"); await Click("gear_tab_backpack"); await Click("equip_BP01");
            Require(Game.Session.CarryCapacity == 12, "Backpack equips and expands carrying capacity");
            await Click("gear_tab_body"); await Click("equip_G10"); await Click("close_panel");
            Game.Buttons["depart"].GrabFocus(); await PressKey(Key.Enter);
            await Click("prepare_depart");
            Require(Game.Session.State.Phase == ChapterPhase.Exploring, "Keyboard departure");
            await Click("move_A03"); await Click("act_scrap"); await CollectLoot();
            await Click("act_K02"); await Capture("04-battle.png");
            var battleBefore = Game.Session.ToJson(); await PressKey(Key.C); AssertOverview(); await Capture("character-battle.png"); await PressKey(Key.Escape);
            Require(battleBefore == Game.Session.ToJson(), "Battle overview preserves action, card order and RNG");
            Game.Buttons["card_0"].GrabFocus(); await Frames(); AssertInspection(ChapterContent.Cards[Game.Session.State.Battle!.Hand[0]].Name);
            await Capture("04-card-inspection.png"); await Fight(); await CollectLoot();
            await Click("move_A04"); await Capture("04-workshop.png"); await Click("act_bench"); await Click("act_dark");
            AssertSceneArt("A04-dark"); await Capture("04-workshop-dark.png"); await Click("move_A06");
            await Capture("05-hidden-room.png"); await Click("act_shovel"); await CollectLoot();
            await Click("move_A04"); await Click("field_equipment"); await Click("gear_tab_weapon"); await Click("equip_G25"); await Click("close_panel");
            await Click("move_A02"); await Capture("05-square.png"); await Click("act_crate"); await Click("act_crate"); await CollectLoot();
            await Click("act_clamp"); await CollectLoot(); await Click("move_A04");
            if(!Game.Buttons.ContainsKey("act_calibrate"))GD.Print("CALIBRATION_DIAGNOSTIC "+string.Join(";",Game.Session.State.Stock.Select(i=>$"{i.Kind}:{i.Container}:{i.Quantity}:{i.Calibrated}"))+" LOG="+string.Join(";",Game.Session.State.Log.TakeLast(14)));
            await Click("act_calibrate"); await Click("move_A05");
            await Capture("07-station.png"); await Click("act_repair"); AssertSceneArt("A05-powered"); await Capture("07-station-powered.png");
            await Click("act_sign"); await CollectLoot(); await Click("act_wire"); await CollectLoot(); await Click("act_extract");
            await Click("camp"); await Click("camp_quests"); await Click("deliver_Q02"); await Click("deliver_S01"); await Click("deliver_S08"); await Click("close_panel");
            Require(Game.Session.SliceComplete, "Main loop completed with real input");
            var saved = Game.Session.ToJson(); Game.ReplaceSession(ChapterSession.FromJson(saved));
            await Frames(); Require(Game.Session.ToJson() == saved, "Chapter save restore");
            await Click("camp_trial"); await Capture("08-trial.png"); await Click("card_0"); await Click("card_0");
            Require(Game.Session.State.World.Contains("trial-complete"), "Trial actually played");
            await Click("end_trial"); await Click("craft_tab_recipes"); await Click("make_G26"); await Capture("09-crafting.png");
            Require(Game.Session.State.Owned.Contains("G26"), "Crafting registers gear");
            await Click("close_panel"); await Click("inventory"); await Click("gear_tab_hands"); await Click("equip_G26"); await Capture("10-equipment.png"); await Click("close_panel");
            await Click("depart", false); await Click("start_A05"); await Click("prepare_depart"); Require(Game.Session.State.Location == "A05", "Forward station departure");
            await Click("move_A04"); await Click("move_A02"); await Click("move_A07");
            await Capture("11-train.png"); await Click("act_train-inspect"); await Capture("11-train-choices.png"); await Click("act_train-parcel"); await CollectLoot();
            await Click("move_A02"); await Click("move_A01"); await Click("act_extract"); await Click("camp");
            await Click("camp_quests"); await Click("deliver_S09"); await Click("close_panel"); await Capture("11-lived-in-home.png");
            await Click("journal"); await Capture("12-journal.png"); await Click("close_panel");
            var completed = Game.Session.ToJson();
            foreach (var size in new[] { new Vector2I(1100, 760), new Vector2I(1600, 900) })
            {
                GetWindow().Size = size; await Frames(8); AssertImmersiveScene();
                Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames();
                AssertImmersiveScene(); await Capture($"{size.X}-camp.png"); AssertVisible("depart"); await Click("depart");
                await Capture($"{size.X}-exploration.png"); AssertVisible("move_A03");
                await PressKey(Key.M); await Capture($"{size.X}-map.png"); await PressKey(Key.Escape);
                Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames();
                await Click("camp_trial"); await Click("card_0"); AssertHandVisible(); await Capture($"{size.X}-extra-cards.png");
                AssertVisible("end_turn"); AssertVisible("end_trial"); await Click("end_trial"); await Click("close_panel");
            }
            Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames(); await Capture("chapter-cover.png");
            await VerifyManual(completed);
            await VerifyInventory(completed);
            await VerifyUnavailableCards();
            await VerifyPatrolAndDefeat();
            await VerifyRework(completed);
            await VerifyManagement(completed);
            await VerifyCompletion(completed);
            GD.Print("CHAPTER_VERIFIED | real input, 7 places, print palette, graphic HUD, focus inspection, modal focus, nested return, filters, three window sizes, rescue, hidden room, combat, calibration, exits, quests, trial, crafting, parcel, save restore, three enemies, defeat recovery, physical inventory, full pockets, ground pickup, backpack crafting");
            GetTree().Quit(0);
        }
        catch (Exception e) { await Capture("failure.png");GD.PushError("CHAPTER_VERIFICATION_FAILED: " + e); GetTree().Quit(1); }
    }
    private async Task CollectLoot()
    {
        if (Game.Buttons.ContainsKey("loot_manage")) await Click("loot_manage");
        if (Game.Session.State.Phase != ChapterPhase.Exploring || !Game.Buttons.ContainsKey("close_panel")) return;
        foreach (var item in Game.Session.State.Stock.Where(i => Game.Session.State.Ground.TryGetValue(i.Container, out var ground) && ground.Place == Game.Session.State.Location).ToArray())
        {
            if (!Game.Session.CanPickup(item.Id, item.Quantity)) continue;
            await Click(item.Quantity == 1 ? "pickup_" + item.Id : "pickup_stack_" + item.Id);
        }
        await Click("close_panel");
    }

    private async Task VerifyInventory(string completed)
    {
        Game.ReplaceSession(ChapterSession.FromJson(completed)); GetWindow().Size = new Vector2I(1100, 760); await Frames(8);
        await Click("inventory"); await Click("gear_tab_all");
        foreach (var kind in new[] { "G01", "G02", "G09" }) await Click("backup_" + kind);
        Require(Game.Session.State.Backups.Count == 3, "UI carries more than two backup items");
        await Click("gear_tab_cargo"); AssertManualLayout(); await Capture("inventory-three-backups.png"); await Click("close_panel");
        await Click("depart", false); await Click("start_A01"); await Click("prepare_depart");
        Require(Game.Session.Overweight, "Worn and packed gear contribute to weight");
        var pressure = Game.Session.State.Pressure; await Click("move_A03");
        Require(Game.Session.State.Pressure == pressure + 2, "Overweight movement costs two action values");
        await Capture("inventory-overweight.png");
        await Click("act_cloth"); await CollectLoot(); await Click("inventory"); await Click("gear_tab_cargo");
        var armor = Game.Session.CarriedItems.Single(i => i.Kind == "G09"); pressure = Game.Session.State.Pressure;
        await Click("drop_" + armor.Id);
        if(Game.Session.State.Pressure!=pressure+2||Game.Session.Overweight)GD.Print("DROP_DIAGNOSTIC expectedPressure="+(pressure+2)+" actual="+Game.Session.State.Pressure+" grams="+Game.Session.CarriedGrams+" item="+Game.Session.State.Stock.FirstOrDefault(i=>i.Id==armor.Id)?.Container+" focus="+GetViewport().GuiGetFocusOwner()?.Name+" drop="+Game.Buttons["drop_"+armor.Id].GetGlobalRect()+" inspection="+Game.GetNode<Control>("ChapterPresentation/Inspection").GetGlobalRect()+" hovered="+GetViewport().GuiGetHoveredControl()?.Name+" log="+string.Join(";",Game.Session.State.Log.TakeLast(4)));
        Require(Game.Session.State.Pressure == pressure + 2 && !Game.Session.Overweight, "Dropping pays starting surcharge and removes overweight");
        await Capture("inventory-ground-return.png"); await Click("close_panel");
        var saved = Game.Session.ToJson(); Game.ReplaceSession(ChapterSession.FromJson(saved)); await Frames();
        Require(Game.Session.GroundHere.Any(), "Ground objects survive save restore");
        await Click("move_A05"); await Click("act_frame"); await CollectLoot(); await Click("act_medicine"); await CollectLoot();
        await Click("act_extract"); await Click("camp"); await Click("craft"); await Click("craft_tab_storage");
        await Click("upgrade_warehouse"); Require(Game.Session.WarehouseCapacity == 72, "Warehouse expansion pays materials");
        await Click("make_U01"); await Click("nav_gear"); await Click("gear_tab_cargo");
        var supplies = Game.Session.StoredStock.First(i => i.Kind == "U01"); await Click("transfer_stack_" + supplies.Id);
        Require(Game.Session.CarriedItems.Any(i => i.Kind == "U01" && !i.FreeSupply), "Crafted supplies can be selected for next expedition");
        await Capture("inventory-warehouse.png");
        Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames();
    }

    private async Task VerifyGraphicInspection()
    {
        var before = Game.Session.ToJson();
        Game.Buttons["inventory"].GrabFocus(); await Frames(); AssertInspection("装备与背包");
        Game.Buttons["objective"].GrabFocus(); await Frames(); AssertInspection(Game.Session.Objective);
        Game.Buttons["camp_equipment"].GrabFocus(); await Frames(); AssertInspection("调整装备");
        Require(Game.Session.ToJson()==before,"Inspecting HUD symbols never advances the game");
    }
    private void AssertInspection(string expected)
    {
        var panel=Game.GetNode<PanelContainer>("ChapterPresentation/Inspection");
        if(!panel.Visible || panel.FindChild("InspectionCopy",true,false) is Label diagnostic && !diagnostic.Text.Contains(expected))GD.Print("INSPECTION_DIAGNOSTIC visible="+panel.Visible+" focus="+GetViewport().GuiGetFocusOwner()?.Name+" text="+(panel.FindChild("InspectionCopy",true,false) as Label)?.Text);
        Require(panel.Visible && panel.FindChild("InspectionCopy",true,false) is Label label && label.Text.Contains(expected),"Keyboard reveals icon meaning: "+expected);
        Require(GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect()),"Inspection stays inside viewport");
    }
    private async Task VerifyUnavailableCards()
    {
        var saved=Game.Session.ToJson();
        Require(Game.Session.StartTrial(),"Start isolated card inspection trial");
        Game.Session.State.Battle!.Action=0; Game.Render(); await Frames();
        var before=Game.Session.ToJson();
        Require(!Game.Buttons["card_0"].Disabled,"Unavailable cards stay keyboard accessible");
        Game.Buttons["card_0"].GrabFocus(); await Frames(); AssertInspection("行动不足");
        await Capture("15-unavailable-card.png"); await PressKey(Key.Enter);
        Require(Game.Session.ToJson()==before,"Inspecting or activating an unavailable card spends nothing");
        Game.ReplaceSession(ChapterSession.FromJson(saved)); await Frames();
    }
    private async Task VerifyManual(string completed)
    {
        foreach (var size in new[] { new Vector2I(1100, 760), new Vector2I(1280, 800), new Vector2I(1600, 900) })
        {
            GetWindow().Size = size; Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames(8);
            foreach (var id in new[] { "map", "inventory", "journal", "craft", "help", "quit" }) AssertVisible(id);
            var before = Game.Session.ToJson();
            await Click("inventory"); AssertManualLayout();
            Require(Game.Buttons.ContainsKey("storage_loadout"), "Inventory exposes a visible loadout disclosure control");
            await Click("gear_tab_weapon"); await Click("inspect_G25");
            Require(!Game.Buttons.ContainsKey("equip_G10"), "Weapon filter excludes body equipment");
            await AssertSupplyCards("G25");
            await Capture($"{size.X}-equipment-detail.png");
            await Click("gear_tab_body"); await Click("inspect_G10"); await AssertSupplyCards("G10");
            await Click("gear_tab_cargo"); AssertManualLayout(); AssertStorageGrid(); await Capture($"{size.X}-cargo.png");
            await Click("nav_character"); AssertManualLayout(); AssertOverview(); await Capture($"{size.X}-character.png");
            foreach (var card in Game.Buttons.Keys.Where(k => k.StartsWith("overview_card_")).ToArray())
            { await Click(card); AssertInspection(ChapterContent.Cards[card[14..]].Text); }
            await Click("overview_inventory"); await PressKey(Key.Escape); Require(Game.Buttons.ContainsKey("overview_inventory"), "Nested inventory returns to character overview");
            await Click("nav_craft"); await Click("craft_tab_mods"); AssertManualLayout(); await Capture($"{size.X}-mods.png");
            await Click("craft_tab_recipes"); await Capture($"{size.X}-crafting.png");
            await Click("nav_journal"); await Click("quest_tab_main"); AssertManualLayout(); await Capture($"{size.X}-journal.png");
            await Click("nav_map"); AssertManualLayout();
            var map = Game.GetNode<Control>("ChapterPresentation/FieldManual").FindChild("RouteMap", true, false) as Control;
            Require(map != null && map.Size.Y > 200, "Route map has a usable viewport");
            var point = map!.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            await Frames(); await Capture($"{size.X}-route-map.png");
            await Click("nav_prepare"); await Click("prepare_equipment");
            await PressKey(Key.Escape); Require(Game.Buttons.ContainsKey("prepare_depart"), "Escape returns from equipment to preparation");
            for (var step = 0; step < 48; step++) { await PressKey(Key.Tab); AssertManualLayout(); }
            await Capture($"{size.X}-preparation.png");
            await PressKey(Key.Escape); Require(!Game.Buttons.ContainsKey("close_panel"), "Escape closes the top-level manual");
            Require(Game.Session.ToJson() == before, "Browsing, filtering and map clicks never mutate game state");
        }
    }
    private async Task AssertSupplyCards(string gear)
    {
        var before = Game.Session.ToJson();
        foreach (var id in Game.Session.GearCards(gear).Distinct())
        {
            var button = Game.Buttons["supply_" + gear + "_" + id];
            button.GrabFocus(); await Frames();
            for (Node? parent = button.GetParent(); parent != null; parent = parent.GetParent())
                if (parent is ScrollContainer scroll)
                    Require(scroll.GetGlobalRect().Encloses(button.GetGlobalRect()), "Supplied card stays inside its detail viewport: " + id);
            AssertInspection(ChapterContent.Cards[id].Text);
        }
        Require(Game.Session.ToJson() == before, "Reading equipment card effects changes no state");
    }
    private void AssertOverview()
    {
        var s = Game.Session.State; var b = s.Battle;
        var cards = b == null ? Game.Session.Deck() : b.Hand.Concat(b.Draw).Concat(b.Discard).ToList();
        var shown = Game.Buttons.Where(p => p.Key.StartsWith("overview_card_")).ToDictionary(p => p.Key[14..], p => p.Value.GetMeta("copies").AsInt32());
        Require(cards.GroupBy(id => id).All(g => shown.GetValueOrDefault(g.Key) == g.Count()) && shown.Values.Sum() == cards.Count, "Overview shows every actual card copy");
        var hp = Game.GetNode<Control>("ChapterPresentation/FieldManual").FindChild("OverviewHealth", true, false) as Label;
        Require(hp != null && hp.Text == $"{s.Health} / {ChapterSession.MaxHealth}", "Overview displays current health");
    }
    private void AssertStorageGrid()
    {
        var panel = Game.GetNode<Control>("ChapterPresentation/FieldManual");
        foreach (var (container, used, capacity) in new[] { ("carry", Game.Session.CarryUsed, Game.Session.CarryCapacity), ("warehouse", Game.Session.WarehouseUsed, Game.Session.WarehouseCapacity) })
        {
            var grid = panel.FindChild("StorageGrid_" + container, true, false) as ChapterSlotGrid;
            Require(grid != null && grid.GetMeta("occupied").AsInt32() == used && grid.GetMeta("capacity").AsInt32() == capacity, "Capacity summary matches real stacking rules: " + container);
            var shown = grid!.GetChildren().OfType<Control>().Select(n => (Id: n.GetMeta("item_id").AsInt64(), Quantity: n.GetMeta("quantity").AsInt32())).ToArray();
            Require(shown.Select(i => i.Id).Distinct().Count() == shown.Length, "Each real batch has exactly one tile");
            foreach (var batch in shown) Require(Game.Session.State.Stock.Any(i => i.Id == batch.Id && i.Container == container && i.Quantity == batch.Quantity), "Displayed batch quantity and command identity agree");
            Require(grid!.Columns >= 2, "Container retains at least two columns at large reading sizes: " + container);
            var items = grid.GetChildren().OfType<Control>().ToArray();
            foreach (var item in items) Require(item.Position.X >= 0 && item.GetRect().End.X <= grid.Size.X + 1, "Item fits container width");
            foreach (var item in items)
            {
                var art = item.GetChild<Control>(0); var title = item.GetNode<Label>("ItemTitle");
                Require(!art.GetRect().Intersects(title.GetRect()), "Item illustration does not overlap its scaled name");
            }
            for (var i = 0; i < items.Length; i++)
            for (var j = i + 1; j < items.Length; j++) Require(!items[i].GetRect().Intersects(items[j].GetRect()), "Storage items do not overlap");
        }
        var workspace = (Control)panel.FindChild("StorageWorkspace", true, false);
        var expandedSummary = panel.FindChild("CompactLoadout", true, false) != null;
        Require(workspace.Size.Y >= GetViewport().GetVisibleRect().Size.Y * (expandedSummary ? .3f : .4f), "Inventory retains browsing space, including explicitly expanded wear summary");
        foreach (var grid in new[] { panel.FindChild("StorageGrid_carry", true, false), panel.FindChild("StorageGrid_warehouse", true, false) }.OfType<Control>())
            if (grid.GetChildCount() > 0 && grid.GetParent()?.GetParent() is ScrollContainer viewport) Require(viewport.Size.Y >= grid.GetChild<Control>(0).Size.Y, "At least one complete item row fits the container viewport");
        var actionBar = (Control)panel.FindChild("StockActionBar", true, false);
        Require(actionBar.Size.Y <= 210, "Selected item forecast and actions stay bounded at larger reading sizes");
        foreach (var slot in new[] { "weapon", "body", "hands", "backpack" })
        {
            if (!Game.Buttons.ContainsKey("wear_" + slot)) continue;
            var tile = Game.Buttons["wear_" + slot]; AssertVisible("wear_" + slot);
            Require(tile.Size.X >= 170, "Worn item name has sufficient reading width");
            for (Node? ancestor = tile.GetParent(); ancestor != null; ancestor = ancestor.GetParent())
                if (ancestor is ScrollContainer scroll) Require(scroll.GetGlobalRect().Encloses(tile.GetGlobalRect()), "All four worn items remain visible without scrolling");
        }
    }
    private void AssertManualLayout()
    {
        var panel = Game.GetNode<Control>("ChapterPresentation/FieldManual");
        Require(GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect()), "Manual remains inside viewport");
        var focus = GetViewport().GuiGetFocusOwner();
        Require(focus != null && panel.IsAncestorOf(focus), "Keyboard focus stays inside the manual");
        AssertVisible("close_panel");
        foreach (var button in Game.Buttons.Where(pair => pair.Key.StartsWith("nav_"))) AssertVisible(button.Key);
    }
    private async Task VerifyPatrolAndDefeat()
    {
        GetWindow().Size = new Vector2I(1280, 800);
        Game.ReplaceSession(new ChapterSession()); await Frames(8);
        await Click("depart"); await Click("move_A03"); await Click("act_rescue"); await Click("act_relay"); await CollectLoot();
        for (var step = 0; step < 40 && !Game.Session.State.Warned; step++)
            await Click(Game.Session.State.Location == "A03" ? "move_A02" : "move_A03");
        Require(Game.Session.State.Warned, "Patrol reaches the player through normal movement");
        foreach (var size in new[] { new Vector2I(1280, 800), new Vector2I(1100, 760), new Vector2I(1600, 900) })
        {
            GetWindow().Size = size; await Frames(8); AssertImmersiveScene();
            await Capture($"{size.X}-patrol-warning.png");
            await Click("move_A03"); Require(Game.Session.State.Warned, "Patrol follows to the scrap yard");
            await Click("move_A02"); Require(Game.Session.State.Warned, "Patrol returns to the square");
        }
        GetWindow().Size = new Vector2I(1280, 800); await Frames(8);
        await Click("act_K09"); AssertBattleLayout(); await Capture("13-patrol.png");
        for (var round = 0; round < 3; round++) await Click("end_turn");
        Require(Game.Session.State.Battle?.Enemies.Count == 3, "Patrol summons a third enemy");
        foreach (var size in new[] { new Vector2I(1280, 800), new Vector2I(1100, 760), new Vector2I(1600, 900) })
        {
            GetWindow().Size = size; await Frames(8); AssertBattleLayout();
            await Click("target_2"); await Capture($"{size.X}-three-enemies.png");
        }
        GetWindow().Size = new Vector2I(1280, 800); await Frames(8);
        for (var round = 0; round < 12 && Game.Session.State.Phase == ChapterPhase.Battle; round++)
            await Click("end_turn");
        Require(Game.Session.State.Phase == ChapterPhase.Defeated, "Enemy attacks reach defeat settlement");
        AssertVisible("camp"); await Capture("14-defeated.png"); await Click("camp");
        Require(Game.Session.State.Phase == ChapterPhase.Camp, "Defeat returns to camp");
        Require(Game.Session.State.World.Contains("robot-rescued"), "Rescue survives defeat");
        await Click("depart");
        Require(Game.Session.State.Health == 42, "Next expedition restores health after defeat");
    }
    private void AssertBattleLayout()
    {
        AssertHandVisible(); AssertVisible("end_turn"); AssertVisible("retreat");
        var targets = Game.Buttons.Where(p => p.Key.StartsWith("target_")).Select(p => p.Value).ToArray();
        foreach (var target in targets) AssertVisible(target.Name.ToString());
        for (var i = 1; i < targets.Length; i++)
            Require(!targets[i - 1].GetGlobalRect().Intersects(targets[i].GetGlobalRect()), "Enemy targets overlap");
        var feedback = Game.GetNode<Control>("ChapterPresentation").FindChild("BattleFeedback", true, false) as Control;
        if (feedback is { Visible: true })
            foreach (var target in targets) Require(!feedback.GetGlobalRect().Intersects(target.GetGlobalRect()), "Battle feedback preserves enemy names and intent");
    }
    private static void AssertArtAssets()
    {
        string[] opaque = ["camp", "camp-lit", "A01", "A02", "A03", "A03-rescued", "A04", "A04-dark", "A05", "A05-powered", "A06", "A07"];
        string[] transparent = ["enemy-E01", "enemy-E02", "enemy-E03", "enemy-E13", "enemy-dummy", "repair-robot", "crest", "clothes-rack",
            "G01", "G02", "G03", "G09", "G10", "G15", "G25", "G26", "T01", "T02", "T11", "T14", "satchel", "steel", "wire", "spring", "card-strike", "card-ward", "card-arc"];
        foreach (var name in opaque.Concat(transparent))
        {
            var texture = ChapterArt.Load(name);
            Require(texture != null && texture.GetWidth() > 0 && texture.GetHeight() > 0, "Imported art missing: " + name);
            var source = texture is AtlasTexture atlas ? atlas.Atlas.ResourcePath : texture!.ResourcePath;
            Require(source.StartsWith("res://Art/Print/"), "Chapter uses the redesigned illustration: " + name);
            if (!transparent.Contains(name)) continue;
            using var image = texture!.GetImage();
            Require(image.GetPixel(0, 0).A < .01f && image.GetPixel(image.GetWidth() - 1, image.GetHeight() - 1).A < .01f,
                "Sprite has an opaque background: " + name);
        }
    }
    private void AssertSceneArt(string name) => Require(Game.GetNode<ChapterScene>("ChapterPresentation/World").ArtName == name, "Wrong world variant: " + name);
    private void AssertHandVisible()
    {
        var cards = Game.Buttons.Where(p => p.Key.StartsWith("card_")).Select(p => p.Value).ToArray();
        foreach (var card in cards) AssertVisible(card.Name.ToString());
        for (var i = 1; i < cards.Length; i++)
            Require(!cards[i - 1].GetGlobalRect().Intersects(cards[i].GetGlobalRect()), "Hand cards overlap");
    }
    private void AssertImmersiveScene()
    {
        var presentation = Game.GetNode<Control>("ChapterPresentation");
        var world = presentation.GetNode<ChapterScene>("World");
        Require(world.GetGlobalRect().IsEqualApprox(presentation.GetGlobalRect()), "Scene fills the entire presentation");
        Require(!presentation.GetChildren().OfType<PanelContainer>().Any(p => p.Name != "TransientNotice" && p.Name != "Inspection" && p.Name != "BattleFeedback" && p.Name != "CardPreview" && p.Name != "CardTravel"), "No persistent side or bottom panels");
        var pins = Game.Buttons.Values.OfType<ChapterHotspot>().Where(p => !p.Disabled).ToArray();
        foreach (var pin in pins) AssertVisible(pin.Name.ToString());
        for (var i = 0; i < pins.Length; i++)
        for (var j = i + 1; j < pins.Length; j++)
            Require(!pins[i].GetGlobalRect().Intersects(pins[j].GetGlobalRect()), "Overlapping world controls: " + pins[i].Name + " / " + pins[j].Name);
    }
    private void AssertVisible(string id)
    {
        var button = Game.Buttons[id]; var rect = button.GetGlobalRect(); var viewport = GetViewport().GetVisibleRect();
        Require(viewport.Encloses(rect), "Button outside viewport: " + id + " " + rect);
    }
    private async Task Click(string id, bool completeFlow = true)
    {
        while (Game.IsFeedbackPlaying) await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        if (Game.Buttons.ContainsKey("story_continue") && id != "story_continue") await Click("story_continue");
        if (id == "gear_tab_cargo") id = "gear_mode_cargo";
        if (!Game.Buttons.ContainsKey(id) && id.StartsWith("gear_tab_") && Game.Buttons.ContainsKey("gear_mode_equipment")) await Click("gear_mode_equipment");
        if (!Game.Buttons.ContainsKey(id) && id.StartsWith("deliver_")) { if (!Game.Buttons.ContainsKey("quest_detail_" + id[8..])) await Click("quest_tab_active"); await Click("quest_detail_" + id[8..]); }
        if (!Game.Buttons.ContainsKey(id) && id.StartsWith("make_") && Game.Buttons.ContainsKey("recipe_" + id[5..])) await Click("recipe_" + id[5..]);
        if (!Game.Buttons.ContainsKey(id) && id.StartsWith("make_BP") && Game.Buttons.ContainsKey("storage_craft_bags")) { await Click("storage_craft_bags"); await Click("bag_spec_" + id[5..]); }
        if (!Game.Buttons.ContainsKey(id) && id == "make_U01" && Game.Buttons.ContainsKey("storage_craft_supplies")) await Click("storage_craft_supplies");
        if (!Game.Buttons.ContainsKey(id) && id == "upgrade_warehouse" && Game.Buttons.ContainsKey("storage_craft_warehouse")) await Click("storage_craft_warehouse");
        if (!Game.Buttons.ContainsKey(id) && id.StartsWith("mod_") && Game.Buttons.ContainsKey("mod_choice_" + id[4..])) await Click("mod_choice_" + id[4..]);
        // Management actions live in the selected item's detail pane.
        if (!Game.Buttons.ContainsKey(id) && (id.StartsWith("equip_") || id.StartsWith("backup_")))
            await Click("inspect_" + id[(id.IndexOf('_') + 1)..]);
        if (!Game.Buttons.ContainsKey(id) && new[] { "pickup_", "transfer_", "drop_", "discard_" }.Any(id.StartsWith))
        {
            var item = Game.Session.State.Stock.Single(i => i.Id == long.Parse(id[(id.LastIndexOf('_') + 1)..]));
            await Click($"stock_{item.Id}_0_0");
            if (id.StartsWith("discard_")) { await Click("stock_more"); id = "menu_discard"; }
        }
        Button? button = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Frames();
            if (!Game.Buttons.ContainsKey(id)) { await Capture("failure-" + id + ".png"); GD.Print("BUTTONS: " + string.Join(",", Game.Buttons.Keys)); }
            Require(Game.Buttons.TryGetValue(id, out button), "Missing button " + id);
            Require(!button!.Disabled, "Disabled button " + id);
            // Scrolling may trigger a deferred layout refresh. Never keep a parent across an await.
            for (Node? parent = button.GetParent(); parent != null; parent = parent.GetParent())
                if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(button);
            await Frames(3);
            if (GodotObject.IsInstanceValid(button) && Game.Buttons.TryGetValue(id, out var current) && current == button) break;
            button = null;
        }
        Require(button != null && GodotObject.IsInstanceValid(button), "Button layout did not settle: " + id);
        AssertVisible(id); var position = button!.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Frames(4);
        if (completeFlow)
        {
            if (id == "depart") await Click("prepare_depart");
            if (id is "close_panel" or "loot_return" && Game.Buttons.TryGetValue("confirm_action", out var closePickup) && closePickup.Text == "结束并返回场景") await Click("confirm_action");
            if (id.StartsWith("card_") && Game.Buttons.ContainsKey("play_selected")) await Click("play_selected");
            if (id == "end_turn" && Game.Buttons.ContainsKey("confirm_action")) await Click("confirm_action");
            if (id is "act_train-wire" or "act_train-supply" or "act_train-parcel" or "act_R01" or "act_R07") await Click("choice_confirm");
            if (id == "act_extract" && Game.Buttons.ContainsKey("confirm_action")) await Click("confirm_action");
        }
        if (!Game.Buttons.ContainsKey("close_panel")) AssertImmersiveScene();
    }
    private async Task PressKey(Key key)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }); await Frames(4);
    }
    private async Task Fight()
    {
        for (var turn = 0; Game.Session.State.Phase == ChapterPhase.Battle && turn < 25; turn++)
        {
            for (var play = 0; play < 10 && Game.Session.State.Phase == ChapterPhase.Battle; play++)
            {
                var b = Game.Session.State.Battle!; var target = b.Enemies.FindIndex(e => e.Health > 0);
                var index = b.Hand.Select((c, i) => (Card: ChapterContent.Cards[c], Index: i))
                    .Where(x => Game.Session.CanPlay(x.Index, target)).OrderByDescending(x => x.Card.Damage).Select(x => x.Index).DefaultIfEmpty(-1).First();
                if (index < 0) break;
                await Click("target_" + target); await Click("card_" + index);
            }
            if (Game.Session.State.Phase == ChapterPhase.Battle) await Click("end_turn");
        }
        Require(Game.Session.State.Phase == ChapterPhase.Exploring, "Won encounter with normal equipment");
    }
    private async Task Capture(string file)
    {
        if (_captures == null || DisplayServer.GetName() == "headless") return;
        await Frames(4);
        // A static or occluded window may not emit another automatic draw signal.
        // Render the settled layout explicitly before reading the viewport texture.
        RenderingServer.ForceDraw(false);
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(Path.Combine(_captures, file)); Require(error == Error.Ok, "Screenshot " + file);
    }
}
