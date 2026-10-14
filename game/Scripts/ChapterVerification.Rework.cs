using CardRPG.Core;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CardRPG;

public partial class ChapterVerification
{
    private async Task VerifyRework(string completed)
    {
        GetWindow().Size = new Vector2I(1280,800);
        Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames(8);
        await PressKey(Key.Escape); Require(Game.Buttons.ContainsKey("resume"), "Scene Escape opens system menu");
        await Click("setting_motion"); await Click("resume");
        await Click("camp_trial"); var before = Game.Session.ToJson();
        await Click("card_0", false); Require(Game.Session.ToJson() == before, "Selecting a card previews without spending AP, cards or RNG");
        Require(Game.Buttons.ContainsKey("play_selected"), "Selected card has an explicit commit action");
        await Capture("rework-card-preview.png"); await Click("cancel_card"); Require(Game.Session.ToJson() == before, "Cancel card is free");
        await Click("card_0", false); await PressKey(Key.Escape); Require(Game.Session.ToJson() == before && !Game.Buttons.ContainsKey("play_selected"), "Escape cancels targeting before opening settings");
        Game.Buttons["card_0"].GrabFocus(); await PressKey(Key.Space);
        Require(Game.Buttons.ContainsKey("confirm_action") && Game.Session.ToJson() == before, "Space on a focused card requests ending the turn, never plays it");
        await Click("cancel_action"); Require(Game.Session.ToJson() == before, "End-turn reminder can be cancelled");
        await Click("card_0"); await Click("card_0"); await Click("end_trial");
        Require(Game.Buttons.ContainsKey("make_G26"), "Completed trial leads directly to its recipe");
        await Click("close_panel");

        var danger = new ChapterSession(); danger.Depart(); danger.Move("A03"); danger.Act("K02"); danger.State.Health = 2;
        Game.ReplaceSession(danger); await Frames(); before = Game.Session.ToJson();
        await Click("retreat", false); Require(Game.Session.ToJson() == before, "Dangerous retreat opens preview without executing");
        await Capture("rework-lethal-retreat.png"); await Click("cancel_action"); Require(Game.Session.ToJson() == before, "Retreat cancel preserves the exact battle");
        await Click("retreat", false); await Click("confirm_action");
        Require(Game.Session.State.Phase == ChapterPhase.Defeated && Game.Session.State.LastReport != null, "Confirmed lethal retreat reaches detailed defeat report");
        await PressKey(Key.Enter); Require(Game.Session.State.Phase == ChapterPhase.Camp, "Enter returns from settlement to camp");

        var choices = ChapterSession.FromJson(completed);
        choices.State.CrateChoiceBanked = false; choices.State.CrateSelection = ""; choices.State.Done.Remove("S09");
        choices.SetStart("A01"); choices.Depart(); choices.Move("A02"); choices.Move("A07"); choices.Act("train-inspect");
        Game.ReplaceSession(choices); await Frames(); before = Game.Session.ToJson();
        await Click("act_train-wire",false); await Click("choose_train-parcel");
        Require(Game.Session.ToJson() == before, "Comparing exclusive rewards is free"); await Capture("rework-train-choice.png");
        await Click("choice_cancel"); Require(Game.Session.ToJson() == before, "Cancel exclusive reward without pressure or RNG changes");
        await Click("act_train-wire",false); await Click("choice_confirm");
        Require(Game.Session.State.CrateSelection == "wire" && !Game.Session.Actions().Any(a => a.Id == "train-parcel"), "Only the confirmed reward is generated");

        Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames();
        await Click("camp_preparation"); Game.Buttons["prepare_equipment"].GrabFocus(); await Click("prepare_equipment"); await PressKey(Key.Escape);
        Require(GetViewport().GuiGetFocusOwner()?.Name == "prepare_equipment", "Nested return restores the invoking control"); await Click("close_panel");
        await Click("inventory"); await Click("gear_tab_cargo"); var discarded = Game.Session.StoredStock.First(i => !i.Kind.StartsWith('T'));
        before = Game.Session.ToJson(); await Click("discard_" + discarded.Id, false); await Click("cancel_action");
        Require(Game.Session.ToJson() == before, "Discard confirmation cancellation is lossless");
        await Click("discard_" + discarded.Id,false); await Click("confirm_action");
        Require(!Game.Session.State.Stock.Any(i => i.Id == discarded.Id), "Confirmed discard removes only the selected batch");

        Game.ReplaceSession(new ChapterSession()); await Frames(); await Click("camp_preparation");
        before = Game.Session.ToJson(); Game.SimulateSaveFailure = true; await Click("starter_G02");
        Require(Game.Session.ToJson() == before, "Save failure rolls back equipment and inventory atomically"); await Capture("rework-save-failure.png");
        Game.SimulateSaveFailure = false; await Click("starter_G02"); Require(Game.Session.State.Weapon == "G02", "Retry succeeds after save failure");

        var labels = new ChapterSession(); labels.Depart(); labels.Move("A03");
        Game.ReplaceSession(labels); await Frames(); await PressKey(Key.Escape); await Click("setting_labels"); await Click("resume");
        foreach (var size in new[] { new Vector2I(1100,760), new Vector2I(1280,800), new Vector2I(1600,900) })
        {
            GetWindow().Size = size; await Frames(8);
            var pins = Game.Buttons.Values.OfType<ChapterHotspot>().ToArray();
            foreach (var pin in pins) Require(GetViewport().GetVisibleRect().Encloses(pin.LabelRect()),"Full hotspot label remains within viewport: " + pin.Name);
            for (var i = 0; i < pins.Length; i++)
            for (var j = i + 1; j < pins.Length; j++) Require(!pins[i].LabelRect().Intersects(pins[j].LabelRect()),"Full hotspot labels do not overlap: " + pins[i].Name + "/" + pins[j].Name);
            await Capture($"rework-{size.X}-labels.png");
        }
        await PressKey(Key.Escape); await Click("setting_labels"); await Click("resume");

        foreach (var size in new[] { new Vector2I(1100,760), new Vector2I(1280,800), new Vector2I(1600,900) })
        foreach (var scale in new[] { "100", "125", "150" })
        {
            Game.ReplaceSession(ChapterSession.FromJson(completed)); GetWindow().Size = size; await Frames(8);
            await PressKey(Key.Escape); await Click("font_" + scale); AssertManualLayout(); await Click("resume");
            await Click("inventory"); await Click("gear_tab_weapon"); await Click("inspect_G01");
            AssertManualLayout(); AssertVisible("equip_G01"); await Capture($"rework-{size.X}-{scale}-comparison.png");
            await Click("gear_tab_cargo"); AssertManualLayout(); AssertStorageGrid(); await Capture($"rework-{size.X}-{scale}-storage.png");
            await Click("nav_prepare"); AssertManualLayout(); AssertVisible("prepare_depart"); await Capture($"rework-{size.X}-{scale}-prepare.png");
            await Click("nav_craft"); await Click("craft_tab_recipes"); await Click("recipe_G26"); AssertManualLayout(); AssertVisible("make_G26");
            await Capture($"rework-{size.X}-{scale}-recipe.png");
        }
        Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames(); await PressKey(Key.Escape); await Click("font_100"); await Click("resume");
        GD.Print("UI_REWORK_VERIFIED | preview/cancel, Space conflict, lethal retreat, choices, nested focus, discard, save rollback, 3 sizes x 3 reading scales");
    }
}
