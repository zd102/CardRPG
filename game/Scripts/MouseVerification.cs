using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CardRPG;

/// <summary>Drives the shipped mouse event/GUI/physics path; never calls movement or interaction methods directly.</summary>
public partial class MouseVerification : Node
{
    public MicroMaze Game { get; set; } = null!;
    private string? _captureDirectory;

    public override async void _Ready()
    {
        try
        {
            Engine.TimeScale = 4;
            _captureDirectory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?[14..];
            if (_captureDirectory != null) System.IO.Directory.CreateDirectory(_captureDirectory);
            await Frames(10);
            Require(!Game.Hud.BlocksWorldPointer(new Vector2(180, 720)) && !Game.Hud.BlocksWorldPointer(new Vector2(1120, 400)), "Bottom and right play areas must be free of permanent panels.");
            Require(Game.Hud.Root.FindChild("NearbyInteraction", true, false) == null, "The old footer action panel must be removed.");
            await Capture("01-mouse-controls.png");
            var preciseTarget = new System.Numerics.Vector2(8.23f, 20.17f);
            await ClickWorldPoint(preciseTarget);
            Require(Game.Navigation.Destination is System.Numerics.Vector2 requested &&
                System.Numerics.Vector2.Distance(requested, preciseTarget) < 0.0001f, "World input must retain the exact click position.");
            await Capture("06-free-movement.png");
            await FinishNavigation();
            Require(System.Numerics.Vector2.Distance(Game.Session.Position, preciseTarget) < 0.0001f, "Mouse must stop at a non-centred position.");
            var shortTarget = Game.Session.Position + new System.Numerics.Vector2(0.006f, -0.004f);
            await ClickWorldPoint(shortTarget);
            await FinishNavigation();
            Require(System.Numerics.Vector2.Distance(Game.Session.Position, shortTarget) < 0.0001f, "Sub-pixel movement must not stall or snap to a tile.");
            await ClickWorldPoint(Game.Session.Map.Start.Center);
            await FinishNavigation();
            await ClickWorld(new Cell(10, 20));
            Require(Game.Navigation.IsActive, "Clicking ground must create a route.");
            await Capture("02-mouse-route.png");
            await ClickWorld(Cell.At(Game.Session.Position), MouseButton.Right);
            Require(!Game.Navigation.IsActive, "Right click must stop the route.");
            var stopped = Game.Session.Position;
            await PhysicsFrames(4);
            Require(Game.Session.Position == stopped, "Stopped movement must remain stopped.");

            await ClickWorld(new Cell(10, 20));
            var destination = Game.Navigation.Destination;
            var mapButton = (Button)Game.Hud.Root.FindChild("MapButton", true, false);
            await Click(new Vector2(mapButton.GetGlobalRect().End.X + 4, mapButton.GetGlobalRect().GetCenter().Y));
            Require(Game.Navigation.Destination == destination, "HUD clicks must not replace world routes.");
            await ClickControl("MapButton");
            Require(Game.Hud.Mode == OverlayMode.Map && !Game.Navigation.IsActive, "Map button must pause and cancel navigation.");
            stopped = Game.Session.Position;
            var time = Game.Session.ElapsedSeconds;
            await Click(new Vector2(60, 300)); // Modal backdrop must not click through.
            await PhysicsFrames(4);
            Require(Game.Session.Position == stopped && Game.Session.ElapsedSeconds == time, "Map must freeze simulation.");
            await ClickControl("ModalPrimary");
            Require(Game.Hud.Mode == OverlayMode.None, "Map must close by mouse.");
            await PhysicsFrames(3);
            Require(Game.Session.Position == stopped, "Closing map must not resume a stale route.");
            await ClickControl("HelpButton");
            Require(Game.Hud.Mode == OverlayMode.Help, "HUD help must open by mouse.");
            await Capture("04-hud-help.png");
            await ClickControl("ModalPrimary");
            Require(Game.Hud.Mode == OverlayMode.None, "Help must close by mouse.");

            await ClickControl("PauseButton");
            await ClickControl("ModalRestart");
            Require(Game.Hud.Mode == OverlayMode.Restart, "Restart must show confirmation.");
            await ClickControl("ModalCancel");
            Require(Game.Hud.Mode == OverlayMode.None && Game.Session.Position == stopped, "Mouse cancellation must preserve the run.");
            await ClickControl("PauseButton");
            await ClickControl("ModalRestart");
            await ClickControl("ModalPrimary");
            Require(Game.Session.Position == Game.Session.Map.Start.Center, "Mouse confirmation must restart.");

            await WalkTo(new Cell(5, 20));
            var hint = (Control)Game.Hud.Root.FindChild("ContextHint", true, false);
            Require(hint.IsVisibleInTree() && !Game.Hud.BlocksWorldPointer(hint.GetGlobalRect().GetCenter()), "Scene cues must be visible without blocking movement clicks.");
            await Capture("05-context-cue.png");
            await ClickWorld(new Cell(5, 21));
            Require(Game.Session.LastMessage.Contains("余火温暖"), "Direct world interaction must work without a footer button.");
            await WalkTo(new Cell(3, 17));
            Require(Game.Session.LastMessage.StartsWith("石碑"), "Stone tablet mouse interaction failed.");
            await WalkTo(new Cell(5, 14));
            await ClickWorld(new Cell(5, 14));
            Require(!Game.Session.IsDoorOpen(new Cell(5, 14)) && !Game.Session.IsVisible(new Cell(5, 12)), "Mouse must close a door and restore occlusion.");
            await WalkTo(new Cell(20, 5));
            Require(Game.Session.HasKey, "Mouse route must acquire the key.");
            await WalkTo(new Cell(11, 19));
            Require(Game.Session.ShortcutUnlocked, "Mouse must open the deep-side shortcut.");
            await WalkTo(new Cell(8, 19));
            await WalkTo(new Cell(22, 19));
            Require(Game.Session.TreasureCount == 1, "Mouse must collect the optional chest.");
            await WalkTo(new Cell(29, 11));
            Require(Game.Session.SealUnlocked, "Mouse must unlock the copper door.");
            await WalkTo(new Cell(30, 19));
            Require(Game.Session.Won && Game.Hud.Mode == OverlayMode.Complete, "Mouse must finish the maze.");
            await Capture("03-mouse-complete.png");
            await ClickControl("ModalPrimary");
            Require(!Game.Session.Won && !Game.Session.HasKey && !Game.Session.ShortcutUnlocked &&
                Game.Session.TreasureCount == 0 && !Game.Session.WasSeen(new Cell(30, 19)), "Replay must reset all progress.");
            await ClickControl("PauseButton");
            GD.Print("MICRO_MAZE_MOUSE_VERIFIED | precise arbitrary-angle movement, short distances, targeting, stop, UI isolation, map, help, pause, cancel, restart, key, shortcut, chest, exit, replay");
            await ClickControl("ModalQuit");
            throw new InvalidOperationException("Quit button did not terminate the game.");
        }
        catch (Exception exception)
        {
            GD.PushError($"MOUSE_VERIFICATION_FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task WalkTo(Cell destination)
    {
        var session = Game.Session;
        var start = Cell.At(session.Position);
        var queue = new Queue<Cell>();
        var previous = new Dictionary<Cell, Cell> { [start] = start };
        queue.Enqueue(start);
        while (queue.TryDequeue(out var current) && !previous.ContainsKey(destination))
        {
            foreach (var next in new[] { new Cell(current.X + 1, current.Y), new Cell(current.X - 1, current.Y), new Cell(current.X, current.Y + 1), new Cell(current.X, current.Y - 1) })
            {
                var tile = session.Map[next];
                if (tile is '#' or 'G' || previous.ContainsKey(next)) continue;
                if (tile == 'L' && !session.HasKey) continue;
                if (tile == 'S' && !session.ShortcutUnlocked && current.X <= next.X) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        Require(previous.ContainsKey(destination), $"No verification itinerary to {destination}.");
        var path = new List<Cell>();
        for (var step = destination; step != start; step = previous[step]) path.Add(step);
        path.Reverse();
        // The itinerary knows the fixture, but each actual click must address a visible/observed on-screen tile.
        foreach (var step in path)
        {
            var tile = session.Map[step];
            if (MazeDefinition.IsDoor(tile))
            {
                if (!session.IsDoorOpen(step))
                {
                    await ClickWorld(step);
                    await FinishNavigation();
                    Require(session.IsDoorOpen(step), $"Mouse did not open door {step}.");
                }
                continue; // Walk through an open doorway by clicking the ground beyond it.
            }
            if (MazeDefinition.IsObject(tile) && step != destination) continue;
            await ClickWorld(step);
            await FinishNavigation();
        }
    }

    private async Task FinishNavigation()
    {
        for (var frame = 0; frame < 240 && Game.Navigation.IsActive; frame++) await PhysicsFrames(1);
        Require(!Game.Navigation.IsActive && Game.Navigation.Feedback == null, $"Mouse route failed: {Game.Navigation.Feedback}");
        await Frames(2);
    }

    private Task ClickWorld(Cell cell, MouseButton button = MouseButton.Left) => ClickWorldPoint(cell.Center, button);

    private Task ClickWorldPoint(System.Numerics.Vector2 point, MouseButton button = MouseButton.Left)
    {
        var screen = Game.GetGlobalTransformWithCanvas() * (new Vector2(point.X, point.Y) * MazeView.TilePixels);
        Require(GetViewport().GetVisibleRect().HasPoint(screen) && !Game.Hud.BlocksWorldPointer(screen), $"Verification target is covered or off-screen: {point} at {screen}.");
        return Click(screen, button);
    }

    private Task ClickControl(string name)
    {
        var button = Game.Hud.Root.FindChild(name, true, false) as Button;
        Require(button != null && button.IsVisibleInTree() && !button.Disabled, $"Button {name} is not available.");
        return Click(button!.GetGlobalRect().GetCenter());
    }

    private async Task Click(Vector2 position, MouseButton button = MouseButton.Left)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = button, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = button, Pressed = false }, true);
        await Frames(2);
    }

    private async Task Capture(string name)
    {
        if (_captureDirectory == null || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(System.IO.Path.Combine(_captureDirectory, name)) == Error.Ok, "Screenshot failed.");
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
