using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NumericsVector = System.Numerics.Vector2;

namespace CardRPG;

/// <summary>Opt-in end-to-end verification, enabled only by the --verify-demo user argument.</summary>
public partial class DemoVerification : Node
{
    public MicroMaze Game { get; set; } = null!;
    private string? _captureDirectory;

    public override async void _Ready()
    {
        try
        {
            _captureDirectory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?[14..];
            if (_captureDirectory != null) System.IO.Directory.CreateDirectory(_captureDirectory);
            await Frames(12);
            await Capture("01-courtyard.png");
            Require(!Game.Session.IsVisible(new Cell(20, 5)), "Key must be hidden at spawn.");
            var click = Game.GetGlobalTransformWithCanvas() * new Vector2(10.5f * MazeView.TilePixels, 19.5f * MazeView.TilePixels);
            GetViewport().PushInput(new InputEventMouseButton { Position = click, GlobalPosition = click, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = click, GlobalPosition = click, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            Require(Game.Navigation.IsActive, "Mouse click must start navigation before keyboard takeover.");
            var spawn = Game.Session.Position;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
            await PhysicsFrames(12);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
            Require(Game.Session.Position.X > spawn.X + 0.3f, "The D key must move the player through the live input/physics path.");
            Require(!Game.Navigation.IsActive, "Keyboard movement must cancel mouse navigation.");
            await Frames(2);
            Game.Restart();
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.M, Keycode = Key.M, Pressed = true });
            await Frames(2);
            Require(Game.Hud.Mode == OverlayMode.Map, "M must open the exploration map.");
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.M, Keycode = Key.M, Pressed = false });
            var distance = Game.Session.Position;
            var pausedTime = Game.Session.ElapsedSeconds;
            Input.ActionPress("maze_right");
            await PhysicsFrames(5);
            Input.ActionRelease("maze_right");
            Require(Game.Session.Position == distance && Game.Session.ElapsedSeconds == pausedTime, $"The map must pause movement and time: {distance} -> {Game.Session.Position}, {pausedTime} -> {Game.Session.ElapsedSeconds}, {Game.Hud.Mode}.");
            await Capture("02-memory-map.png");
            Game.Hud.ShowOverlay(OverlayMode.None, Game.Session);
            await KeyClick(Key.F1);
            Require(Game.Hud.Mode == OverlayMode.Help, "F1 must open the operation guide.");
            await KeyClick(Key.Escape);
            await KeyClick(Key.Escape);
            Require(Game.Hud.Mode == OverlayMode.Pause, "Escape must open pause.");
            await KeyClick(Key.Escape);
            await KeyClick(Key.R);
            Require(Game.Hud.Mode == OverlayMode.Restart, "R must open restart confirmation.");
            await KeyClick(Key.Escape);
            Require(Game.Hud.Mode == OverlayMode.None, "Escape must cancel restart.");
            WalkTo(new Cell(5, 20));
            await KeyClick(Key.E);
            Require(Game.Session.LastMessage.Contains("余火温暖"), "E must still interact with a nearby object.");
            WalkTo(new Cell(20, 5));
            Game.Interact();
            Require(Game.Session.HasKey, "Key pickup failed.");
            await Capture("03-key-hall.png");
            WalkTo(new Cell(12, 19));
            Game.Interact();
            Require(Game.Session.ShortcutUnlocked, "Shortcut must open from the deep side.");
            WalkTo(new Cell(8, 19));
            WalkTo(new Cell(22, 19));
            Game.Interact();
            Require(Game.Session.TreasureCount == 1, "Optional chest pickup failed.");
            WalkTo(new Cell(30, 19));
            Require(Game.Session.SealUnlocked, "Key door must unlock along the exit route.");
            Game.Interact();
            Require(Game.Session.Won && Game.Hud.Mode == OverlayMode.Complete, "Exit completion failed.");
            await Capture("04-complete.png");
            Game.Restart();
            Require(!Game.Session.Won && !Game.Session.HasKey && !Game.Session.ShortcutUnlocked && Game.Session.TreasureCount == 0, "Restart must reset all progress.");
            Require(Game.Session.Position == Game.Session.Map.Start.Center, "Restart must restore the spawn.");
            Require(!Game.Session.WasSeen(new Cell(30, 19)), "Restart must reset exploration memory.");
            GD.Print("MICRO_MAZE_VERIFIED | keyboard takeover, E/M/Esc/R, map pause, doors, key, shortcut, treasure, exit, restart");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"DEMO_VERIFICATION_FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private void WalkTo(Cell destination)
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
        Require(previous.ContainsKey(destination), $"No route to {destination}.");
        var path = new List<Cell>();
        for (var step = destination; step != start; step = previous[step]) path.Add(step);
        path.Reverse();
        foreach (var step in path)
        {
            if (MazeDefinition.IsDoor(session.Map[step]) && !session.IsDoorOpen(step))
            {
                Require(session.InteractionTarget() == step, $"Door cannot be targeted at {step}.");
                Game.Interact();
                Require(session.IsDoorOpen(step), $"Door remained closed at {step}.");
            }
            var frames = 0;
            while (NumericsVector.Distance(session.Position, step.Center) > 0.015f)
            {
                Require(++frames < 120, $"Movement blocked near {step} at {session.Position}.");
                var difference = step.Center - session.Position;
                session.Tick(difference, Math.Min(1f / 60, difference.Length() / MazeSession.MoveSpeed));
            }
        }
        Game.RefreshPresentation(0);
    }

    private async Task Capture(string name)
    {
        Game.RefreshPresentation(0);
        await Frames(3);
        if (_captureDirectory == null || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(System.IO.Path.Combine(_captureDirectory, name));
        Require(error == Error.Ok, $"Screenshot failed: {error}.");
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task KeyClick(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        await Frames(2);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
