using CardRPG.Core;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CardRPG;

/// <summary>Opt-in UI verification. Dispatches real pointer/keyboard input and never touches user saves.</summary>
public partial class ExpeditionVerification : Node
{
    public ExpeditionDemo Game { get; set; } = null!;
    private string? _captures;
    public override async void _Ready()
    {
        try
        {
            _captures = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--capture-dir="))?[14..];
            if (_captures != null) System.IO.Directory.CreateDirectory(_captures);
            await Frames(8); await Capture("01-camp.png");
            await Click("depart"); Require(Game.Session.State.Phase == ExpeditionPhase.Exploring, "Depart button");
            await Click("move_square"); await Capture("02-square.png");
            var step = Game.Session.State.Steps;
            await Click("inventory"); await Click("close_inventory"); await Frames(6);
            Require(Game.Session.State.Steps == step, "Browsing must not advance world");
            await Click("act_safe"); Require(Game.Session.State.Locked, "Search warning");
            await Capture("03-warning.png"); await Click("act_safe");
            Require(Game.Session.State.Phase == ExpeditionPhase.Battle && Game.Session.State.Encounter == "patrol", "Interruption");
            await Capture("04-battle.png"); await Fight();
            await Click("act_safe"); Require(Game.Session.State.Flags.Contains("safe"), "Resume interrupted search");
            await Click("move_gate"); await Click("act_extract");
            Require(Game.Session.State.Phase == ExpeditionPhase.Extracted, "Extraction");
            await Capture("05-extracted.png"); await Click("camp");
            Require(Game.Session.State.Owned.Count > 3, "Loot banked");
            // Keyboard navigation must operate the real focused button.
            Game.Buttons["depart"].GrabFocus();
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false });
            await Frames(3); Require(Game.Session.State.Phase == ExpeditionPhase.Exploring, "Keyboard depart");
            // Direct encounter, construction, blessing, boss and alternate exit in another live run.
            await Click("move_square"); await Click("act_convoy"); await Fight();
            await Click("move_scrap"); await Click("act_salvage"); await Click("act_power");
            if (Game.Session.State.Health < ExpeditionSession.MaxHealth) await Click("act_heal");
            await Click("move_workshop"); await Click("act_workshop");
            await Click("field_equipment");
            if (!Game.Buttons["equip_breaker"].Disabled) await Click("equip_breaker");
            await Click("close_inventory"); await Click("act_blessing");
            await Click("bless_" + Game.Session.State.BlessingOffers[0]);
            await Click("move_furnace"); await Click("act_alarm"); await Click("act_boss"); await Fight();
            await Click("move_exit"); await Click("act_extract");
            Require(Game.Session.State.Owned.Contains("bracer"), "Boss loot and secondary exit");
            GD.Print("EXPEDITION_VERIFIED | real mouse, keyboard, warning, interruption, cards, gear, blessing, boss, both exits, banking");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError("EXPEDITION_VERIFICATION_FAILED: " + e); GetTree().Quit(1); }
    }

    private async Task Fight()
    {
        for (var turn = 0; Game.Session.State.Phase == ExpeditionPhase.Battle && turn < 50; turn++)
        {
            for (var play = 0; play < 20 && Game.Session.State.Phase == ExpeditionPhase.Battle; play++)
            {
                var s = Game.Session.State;
                var index = s.Hand.Select((id, i) => (Card: ExpeditionContent.Cards[id], Index: i))
                    .Where(x => x.Card.ActionCost <= s.ActionPoints && x.Card.ManaCost <= s.Mana)
                    .OrderByDescending(x => x.Card.Damage).Select(x => x.Index).DefaultIfEmpty(-1).First();
                if (index < 0) break;
                await Click("card_" + index);
            }
            if (Game.Session.State.Phase == ExpeditionPhase.Battle) await Click("end_turn");
        }
        Require(Game.Session.State.Phase == ExpeditionPhase.Exploring, "Win battle using actual card controls");
    }
    private async Task Click(string id)
    {
        await Frames(2);
        if (!Game.Buttons.TryGetValue(id, out var button)) throw new Exception("Missing button " + id);
        Require(!button.Disabled, "Enabled button " + id);
        var parent = button.GetParent();
        while (parent != null) { if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(button); parent = parent.GetParent(); }
        await Frames(2);
        var point = button.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Frames(2);
    }
    private async Task Capture(string name)
    {
        if (_captures == null || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(System.IO.Path.Combine(_captures, name));
        Require(error == Error.Ok, "Capture " + name);
    }
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
}
