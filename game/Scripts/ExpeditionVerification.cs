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
            await Click("inventory"); await Capture("06-inventory.png");
            await PressKey(Key.Escape); Require(Game.Buttons.ContainsKey("depart"), "Escape closes equipment");
            await PressKey(Key.F1); await Capture("07-help.png");
            await PressKey(Key.Escape); Require(Game.Buttons.ContainsKey("depart"), "Escape closes guide");
            Game.Buttons["inventory"].GrabFocus(); await PressKey(Key.Tab);
            Require(GetViewport().GuiGetFocusOwner() == Game.Buttons["help"], "Header Tab order");
            await PressKey(Key.Enter); Require(Game.Buttons.ContainsKey("close_help"), "Keyboard opens guide");
            await PressKey(Key.Escape);
            await Click("depart"); Require(Game.Session.State.Phase == ExpeditionPhase.Exploring, "Depart button");
            await Click("move_square"); await Capture("02-square.png");
            Require(Game.Session.State.Steps == 1, "Scene exit moves exactly one step");
            await VerifyMap("10-map.png");
            var step = Game.Session.State.Steps;
            await Click("inventory"); await Click("close_inventory"); await Frames(6);
            Require(Game.Session.State.Steps == step, "Browsing must not advance world");
            await Click("act_safe"); Require(Game.Session.State.Locked, "Search warning");
            await Capture("03-warning.png"); await Click("act_safe");
            Require(Game.Session.State.Phase == ExpeditionPhase.Battle && Game.Session.State.Encounter == "patrol", "Interruption");
            await Capture("04-battle.png"); await VerifyMap("11-battle-map.png"); await Fight();
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
            await Capture("08-blessing.png");
            await Click("bless_" + Game.Session.State.BlessingOffers[0]);
            await Click("move_furnace"); await Click("act_alarm"); await Click("act_boss"); await Fight();
            await Click("move_exit"); await Click("act_extract");
            Require(Game.Session.State.Owned.Contains("bracer"), "Boss loot and secondary exit");
            // Check the read-only end state and every responsive page at supported window sizes.
            Game.ReplaceSession(new ExpeditionSession(new ExpeditionState { Phase = ExpeditionPhase.Defeated, Health = 0, Summary = "战败：失去本次未撤离装备与材料。已有绑定装备与情报保留。" }));
            await Frames(4); await Capture("09-defeated.png");
            await VerifyPlaces("1280");
            if (_captures != null && DisplayServer.GetName() != "headless")
            {
                var sizes = new[] { new Vector2I(1100, 760), new Vector2I(1600, 900) };
                foreach (var size in sizes)
                {
                    GetWindow().Size = size;
                    Game.ReplaceSession(new ExpeditionSession());
                    await Frames(6); await Capture($"{size.X}-camp.png");
                    await Click("inventory"); await Capture($"{size.X}-inventory.png"); await Click("close_inventory");
                    await Click("help"); await Capture($"{size.X}-help.png"); await Click("close_help");
                    await Click("depart"); await Click("move_square"); await Capture($"{size.X}-explore.png");
                    await Click("act_convoy"); await Capture($"{size.X}-battle.png");
                    var boss = new ExpeditionSession(); boss.Equip("staff"); boss.Depart(); boss.Move("scrap"); boss.Move("workshop"); boss.Move("furnace"); boss.Act("boss");
                    Game.ReplaceSession(boss); await Frames(4);
                    await PointerClick(Game.GetNode<Control>("Presentation/Enemy_1/Actor"));
                    Require(Game.Buttons["target_1"].Text.Contains("◎"), "Clicking the world actor selects it");
                    await Capture($"{size.X}-boss.png");
                    var supportHealth = boss.State.Enemies[1].Health;
                    var attack = boss.State.Hand.FindIndex(id => ExpeditionContent.Cards[id].Damage > 0);
                    await Click("card_" + attack);
                    Require(boss.State.Enemies[1].Health < supportHealth && Game.Buttons["target_1"].Text.Contains("◎"), "Selected enemy persists after playing a card");
                    var collection = new ExpeditionSession(new ExpeditionState { Owned = ["hammer", "staff", "armor", "breaker", "bracer"], Weapon = "staff", WearBracer = true });
                    Game.ReplaceSession(collection); await Frames(4); await Click("inventory"); await Capture($"{size.X}-collection.png");
                    // Use real generated offers so validation stays independent of catalogue names.
                    var reward = new ExpeditionSession(); reward.Depart(); reward.Move("scrap"); reward.Move("workshop"); reward.Act("blessing");
                    Game.ReplaceSession(reward); await Frames(4); await Capture($"{size.X}-blessing.png");
                    Game.ReplaceSession(new ExpeditionSession(new ExpeditionState { Phase = ExpeditionPhase.Extracted, Summary = "安全撤离，全部战利品已入库。" }));
                    await Frames(4); await Capture($"{size.X}-extracted.png");
                    Game.ReplaceSession(new ExpeditionSession(new ExpeditionState { Phase = ExpeditionPhase.Defeated, Health = 0, Summary = "战败，未入库收获已丢失。已有绑定装备与情报保留。" }));
                    await Frames(4); await Capture($"{size.X}-defeated.png");
                    await VerifyPlaces(size.X.ToString());
                }
            }
            GD.Print("EXPEDITION_VERIFIED | local scenes, scene exits, read-only map, real mouse, keyboard, warning, interruption, cards, gear, blessing, boss, both exits, banking");
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
    private async Task VerifyMap(string screenshot)
    {
        var snapshot = Game.Session.ToJson();
        await PressKey(Key.M);
        Require(Game.GetNode<Control>("Presentation/WorldScene") is ExpeditionRegion, "Map uses the region overview");
        Require(Game.Buttons.Keys.All(id => id is "map" or "inventory" or "help" or "quit" or "close_map"), "Map exposes no game actions or movement");
        foreach (var id in ExpeditionContent.Places.Keys)
            await PointerClick(Game.GetNode<Control>("Presentation/MapLabels/MapLabel_" + id));
        Require(Game.Session.ToJson() == snapshot, "Clicking every map label leaves the entire session unchanged");
        await Capture(screenshot);
        await Click("close_map"); Require(Game.Session.ToJson() == snapshot, "Closing map preserves session");
        await Click("map"); await PressKey(Key.Escape);
        Require(!Game.Buttons.ContainsKey("close_map") && Game.Session.ToJson() == snapshot, "Escape closes the read-only map");
    }
    private async Task VerifyPlaces(string prefix)
    {
        foreach (var id in ExpeditionContent.Places.Keys)
        {
            var session = new ExpeditionSession(new ExpeditionState { Phase = ExpeditionPhase.Exploring, Location = id });
            Game.ReplaceSession(session); await Frames(4);
            Require(Game.GetNode<Control>("Presentation/WorldScene") is ExpeditionLocalScene scene && scene.Location == id, "Only the current place is shown");
            var exits = Game.Buttons.Keys.Where(k => k.StartsWith("move_")).Select(k => k[5..]).ToHashSet();
            Require(exits.SetEquals(ExpeditionContent.Places[id].Neighbors), "Only neighboring scene exits are present");
            Require(Game.GetNodeOrNull<Control>("Presentation/SceneActions") == null, "No action sidebar");
            var snapshot = session.ToJson();
            var action = Game.Buttons.First(p => p.Key.StartsWith("act_") && !p.Value.Disabled).Value;
            action.GrabFocus(); await Frames(2);
            Require(session.ToJson() == snapshot, "Reading focused action details is free");
            await Capture($"{prefix}-place-{id}.png");
        }
        await VerifyMap($"{prefix}-map.png");
    }
    private async Task Click(string id)
    {
        await Frames(2);
        if (!Game.Buttons.TryGetValue(id, out var button)) throw new Exception("Missing button " + id);
        Require(!button.Disabled, "Enabled button " + id);
        var parent = button.GetParent();
        while (parent != null) { if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(button); parent = parent.GetParent(); }
        await Frames(2);
        await PointerClick(button);
    }
    private async Task PointerClick(Control control)
    {
        var point = control.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Frames(2);
    }
    private async Task Capture(string name)
    {
        await Frames(3);
        CheckHorizontalLayout(Game);
        var viewport = GetViewport().GetVisibleRect();
        var world = Game.GetNode<Control>("Presentation/WorldScene");
        Require(world.GetGlobalRect().IsEqualApprox(viewport), "World scene fills the viewport");
        var hand = Game.GetNodeOrNull<Control>("Presentation/HandDock");
        foreach (var button in Game.Buttons.Where(p => p.Key.StartsWith("move_") || p.Key.StartsWith("target_")).Select(p => p.Value))
        {
            Require(viewport.Encloses(button.GetGlobalRect()), "World interaction stays visible: " + button.Name);
            if (hand != null) Require(!hand.GetGlobalRect().Intersects(button.GetGlobalRect()), "Hand does not cover enemy targets");
        }
        var interactions = Game.Buttons.Where(p => p.Key.StartsWith("act_") || p.Key.StartsWith("move_") || p.Key == "field_equipment").Select(p => p.Value).ToArray();
        var hint = Game.GetNodeOrNull<Control>("Presentation/InteractionHint");
        for (var i = 0; i < interactions.Length; i++)
        {
            var rect = interactions[i].GetGlobalRect();
            Require(viewport.Encloses(rect), "Scene interaction visible without scrolling: " + interactions[i].Name);
            if (hint != null) Require(!hint.GetGlobalRect().Intersects(rect), "Details do not cover an interaction");
            for (var j = 0; j < i; j++) Require(!rect.Intersects(interactions[j].GetGlobalRect()), "Scene interactions do not overlap");
        }
        if (Game.Buttons.ContainsKey("end_turn"))
        {
            foreach (var button in Game.Buttons.Where(pair => pair.Key.StartsWith("card_") || pair.Key is "end_turn" or "retreat").Select(pair => pair.Value))
            {
                var rect = button.GetGlobalRect();
                Require(GetViewport().GetVisibleRect().Encloses(rect), "Battle action on screen: " + button.Name);
                for (var parent = button.GetParent(); parent != null; parent = parent.GetParent())
                    if (parent is ScrollContainer scroller) Require(scroller.GetGlobalRect().Encloses(rect), "Card usable without scrolling: " + button.Name);
            }
        }
        if (_captures == null || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(System.IO.Path.Combine(_captures, name));
        Require(error == Error.Ok, "Capture " + name);
    }
    private void CheckHorizontalLayout(Node node)
    {
        if (node is Control control && control.IsVisibleInTree() && control is not ScrollBar)
        {
            var rect = control.GetGlobalRect();
            Require(rect.Position.X >= -1 && rect.End.X <= GetViewport().GetVisibleRect().Size.X + 1, "Horizontal layout: " + control.GetPath());
        }
        foreach (var child in node.GetChildren()) CheckHorizontalLayout(child);
    }
    private async Task PressKey(Key key)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Frames(3);
    }
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
}
