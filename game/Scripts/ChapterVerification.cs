using CardRPG.Core;
using Godot;
using System;
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
            await Frames(8); AssertArtAssets(); AssertImmersiveScene(); AssertSceneArt("camp"); await Capture("01-camp.png");
            await Click("camp_preparation"); await Click("starter_G02"); await Click("starter_G01");
            await Capture("01-preparation.png"); await Click("close_panel");
            await Click("help"); await Capture("01-help.png"); await Click("close_panel");
            await PressKey(Key.M); var unchanged = Game.Session.ToJson();
            Require(Game.Buttons.ContainsKey("close_panel"), "Map opens with M");
            await Capture("02-map.png"); await PressKey(Key.Escape); Require(unchanged == Game.Session.ToJson(), "Map is read only");
            await PressKey(Key.I); await PressKey(Key.Escape); await Click("depart");
            AssertImmersiveScene(); await Capture("02-gate.png"); await Click("move_A03");
            await Capture("03-scrap.png"); await Click("act_rescue"); AssertSceneArt("A03-rescued");
            await Capture("03-scrap-rescued.png"); await Click("act_relay"); await Click("act_scrap");
            await Click("act_K02"); await Capture("04-battle.png"); await Fight();
            await Click("move_A04"); await Capture("04-workshop.png"); await Click("act_bench"); await Click("act_dark");
            AssertSceneArt("A04-dark"); await Capture("04-workshop-dark.png"); await Click("move_A06");
            await Capture("05-hidden-room.png"); await Click("act_shovel");
            await Click("move_A04"); await Click("field_equipment"); await Click("equip_G25"); await Click("close_panel");
            await Click("move_A02"); await Capture("05-square.png"); await Click("act_crate"); await Click("act_crate");
            await Click("move_A01"); await Click("act_extract"); await Capture("06-extraction.png"); await Click("camp");
            await Click("camp_quests"); await Click("deliver_Q01"); await Click("deliver_S07"); await Click("deliver_S08");
            await Click("close_panel"); AssertSceneArt("camp-lit"); await Capture("07-lit-home.png");
            await Click("inventory"); await Click("equip_G10"); await Click("backup_G02"); await Click("close_panel");
            Game.Buttons["depart"].GrabFocus(); await PressKey(Key.Enter);
            Require(Game.Session.State.Phase == ChapterPhase.Exploring, "Keyboard departure");
            await Click("move_A02"); await Click("act_clamp"); await Click("move_A04");
            await Click("act_bench"); await Click("act_calibrate"); await Click("move_A05");
            await Capture("07-station.png"); await Click("act_repair"); AssertSceneArt("A05-powered"); await Capture("07-station-powered.png");
            await Click("act_sign"); await Click("act_wire"); await Click("act_extract");
            await Click("camp"); await Click("camp_quests"); await Click("deliver_Q02"); await Click("deliver_S01"); await Click("close_panel");
            Require(Game.Session.SliceComplete, "Main loop completed with real input");
            var saved = Game.Session.ToJson(); Game.ReplaceSession(ChapterSession.FromJson(saved));
            await Frames(); Require(Game.Session.ToJson() == saved, "Chapter save restore");
            await Click("camp_trial"); await Capture("08-trial.png"); await Click("card_0"); await Click("card_0");
            Require(Game.Session.State.World.Contains("trial-complete"), "Trial actually played");
            await Click("end_trial"); await Click("craft"); await Click("make_G26"); await Capture("09-crafting.png");
            Require(Game.Session.State.Owned.Contains("G26"), "Crafting registers gear");
            await Click("close_panel"); await Click("inventory"); await Click("equip_G26"); await Capture("10-equipment.png"); await Click("close_panel");
            await Click("camp_preparation"); await Click("start_A05"); await Click("prepare_depart"); Require(Game.Session.State.Location == "A05", "Forward station departure");
            await Click("move_A04"); await Click("move_A02"); await Click("move_A07");
            await Capture("11-train.png"); await Click("act_train-inspect"); await Capture("11-train-choices.png"); await Click("act_train-parcel");
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
                AssertVisible("end_turn"); AssertVisible("end_trial"); await Click("end_trial");
            }
            Game.ReplaceSession(ChapterSession.FromJson(completed)); await Frames(); await Capture("chapter-cover.png");
            await VerifyPatrolAndDefeat();
            GD.Print("CHAPTER_VERIFIED | real input, 7 places, 23 art assets, state variants, rescue, hidden room, combat, calibration, exits, quests, trial, crafting, parcel, save restore, responsive layouts, three enemies, defeat recovery");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError("CHAPTER_VERIFICATION_FAILED: " + e); GetTree().Quit(1); }
    }
    private async Task VerifyPatrolAndDefeat()
    {
        GetWindow().Size = new Vector2I(1280, 800);
        Game.ReplaceSession(new ChapterSession()); await Frames(8);
        await Click("depart"); await Click("move_A03"); await Click("act_rescue"); await Click("act_relay");
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
    }
    private static void AssertArtAssets()
    {
        string[] opaque = ["camp", "camp-lit", "A01", "A02", "A03", "A03-rescued", "A04", "A04-dark", "A05", "A05-powered", "A06", "A07", "card-strike", "card-ward", "card-arc"];
        string[] transparent = ["enemy-E01", "enemy-E02", "enemy-E03", "enemy-E13", "enemy-dummy", "repair-robot", "crest", "clothes-rack"];
        foreach (var name in opaque.Concat(transparent))
        {
            var texture = ChapterArt.Load(name);
            Require(texture != null && texture.GetWidth() > 0 && texture.GetHeight() > 0, "Imported art missing: " + name);
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
        Require(!presentation.GetChildren().OfType<PanelContainer>().Any(p => p.Name != "TransientNotice"), "No persistent side or bottom panels");
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
    private async Task Click(string id)
    {
        await Frames(); Require(Game.Buttons.TryGetValue(id, out var button), "Missing button " + id);
        Require(!button!.Disabled, "Disabled button " + id);
        for (Node? p = button.GetParent(); p != null; p = p.GetParent())
            if (p is ScrollContainer scroll) { scroll.EnsureControlVisible(button); await Frames(3); }
        AssertVisible(id); var position = button.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Frames(4);
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
        await Frames(4); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(Path.Combine(_captures, file)); Require(error == Error.Ok, "Screenshot " + file);
    }
}
