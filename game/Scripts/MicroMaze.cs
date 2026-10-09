using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class MicroMaze : Node2D
{
    public MazeSession Session { get; private set; } = null!;
    public MazeView World { get; private set; } = null!;
    public MazeHud Hud { get; private set; } = null!;
    public MouseNavigator Navigation { get; private set; } = null!;
    private MazeDefinition _definition = null!;
    private Camera2D _camera = null!;
    private bool _verifying;

    public override void _Ready()
    {
        try
        {
            _verifying = OS.GetCmdlineUserArgs().Any(a => a is "--verify-demo" or "--verify-mouse");
            _definition = MazeDefinition.FromJson(FileAccess.GetFileAsString("res://Data/micro-maze.json"));
            Session = new MazeSession(_definition);
            Navigation = new MouseNavigator(Session);
            ConfigureInput();
            World = new MazeView();
            AddChild(World);
            World.Initialize(Session);
            _camera = new Camera2D { PositionSmoothingEnabled = false };
            AddChild(_camera);
            Hud = new MazeHud();
            AddChild(Hud);
            Hud.MapRequested += ToggleMap;
            Hud.PauseRequested += () => ShowOverlay(OverlayMode.Pause);
            Hud.HelpRequested += ToggleHelp;
            Hud.ResumeRequested += () => Hud.ShowOverlay(OverlayMode.None, Session);
            Hud.RestartRequested += Restart;
            Hud.RestartConfirmationRequested += () => ShowOverlay(OverlayMode.Restart);
            Hud.QuitRequested += () => GetTree().Quit();
            GetWindow().MinSize = new Vector2I(960, 600);
            GetWindow().FocusExited += () =>
            {
                if (!_verifying && !Session.Won && Hud.Mode == OverlayMode.None)
                    ShowOverlay(OverlayMode.Pause);
            };
            RefreshPresentation(0);
            GD.Print("MICRO_MAZE_READY | Godot C# | 35x25 | ANGLE compatible");
            if (OS.GetCmdlineUserArgs().Contains("--verify-demo")) AddChild(new DemoVerification { Game = this });
            if (OS.GetCmdlineUserArgs().Contains("--verify-mouse")) AddChild(new MouseVerification { Game = this });
        }
        catch (Exception exception)
        {
            GD.PushError($"Micro maze initialization failed: {exception}");
            GetTree().Quit(1);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Session == null || Hud == null) return;
        if (Hud.Mode == OverlayMode.None)
        {
            var input = GetViewport().GuiGetFocusOwner() == null
                ? Input.GetVector("maze_left", "maze_right", "maze_up", "maze_down") : Vector2.Zero;
            if (input != Vector2.Zero)
            {
                Navigation.Cancel();
                Session.Tick(new System.Numerics.Vector2(input.X, input.Y), (float)delta);
            }
            else Navigation.Tick((float)delta);
            if (Session.Won) ShowOverlay(OverlayMode.Complete);
        }
        RefreshPresentation(Hud.Mode == OverlayMode.None ? (float)delta : 0);
    }

    public override void _Input(InputEvent input)
    {
        if (Hud == null || input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (Hud.Mode == OverlayMode.None && key.PhysicalKeycode is Key.W or Key.A or Key.S or Key.D or Key.Up or Key.Down or Key.Left or Key.Right)
        {
            Navigation.Cancel();
            GetViewport().GuiReleaseFocus();
        }
        if (key.PhysicalKeycode == Key.Escape)
        {
            if (Hud.Mode == OverlayMode.Complete) return;
            ShowOverlay(Hud.Mode == OverlayMode.None ? OverlayMode.Pause : OverlayMode.None);
        }
        else if (key.PhysicalKeycode == Key.M) ToggleMap();
        else if (key.PhysicalKeycode == Key.F1) ToggleHelp();
        else if (key.PhysicalKeycode == Key.R && Hud.Mode != OverlayMode.Complete)
            ShowOverlay(OverlayMode.Restart);
        else if (key.PhysicalKeycode == Key.E && Hud.Mode == OverlayMode.None)
            Interact();
        else return;
        GetViewport().SetInputAsHandled();
    }

    public void Interact()
    {
        Navigation.Cancel();
        if (Session.Interact()) Hud.ShowMessage(Session.LastMessage);
        RefreshPresentation(0);
        if (Session.Won) ShowOverlay(OverlayMode.Complete);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (Hud == null || Hud.Mode != OverlayMode.None || Session.Won ||
            input is not InputEventMouseButton mouse || !mouse.Pressed) return;
        if (mouse.ButtonIndex == MouseButton.Right) Navigation.Cancel();
        else if (mouse.ButtonIndex == MouseButton.Left)
        {
            var world = GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position;
            var target = new System.Numerics.Vector2(world.X, world.Y) / MazeView.TilePixels;
            RequestMouseTarget(target);
        }
        else return;
        RefreshPresentation(0);
        GetViewport().SetInputAsHandled();
    }

    public void Restart()
    {
        Session = new MazeSession(_definition);
        Navigation = new MouseNavigator(Session);
        World.ReplaceSession(Session);
        Hud.ShowOverlay(OverlayMode.None, Session);
        RefreshPresentation(0);
    }

    public void RefreshPresentation(float delta)
    {
        World.Refresh(delta);
        // The whole viewport is now the play area; the HUD floats above it.
        var target = new Vector2(Session.Position.X, Session.Position.Y) * MazeView.TilePixels;
        _camera.Position = target.Round();
        _camera.ForceUpdateScroll();
        World.Navigation = Navigation;
        World.HoveredCell = null;
        if (!Hud.BlocksWorldPointer(GetViewport().GetMousePosition()))
        {
            var pointer = GetGlobalMousePosition() / MazeView.TilePixels;
            var cell = Cell.At(new System.Numerics.Vector2(pointer.X, pointer.Y));
            if (Session.IsVisible(cell) && MazeDefinition.IsObject(Session.Map[cell]) && !Session.IsCollected(cell))
                World.HoveredCell = cell;
        }
        Input.SetDefaultCursorShape(World.HoveredCell.HasValue ? Input.CursorShape.PointingHand : Input.CursorShape.Arrow);
        Hud.Update(Session, GetGlobalTransformWithCanvas(), delta, World.HoveredCell, Navigation.Feedback);
    }

    private void RequestMouseTarget(System.Numerics.Vector2 target)
    {
        GetViewport().GuiReleaseFocus();
        Navigation.Request(target);
        if (!Navigation.IsActive) Hud.ShowMessage(Navigation.Feedback ?? Session.LastMessage);
        if (Session.Won) ShowOverlay(OverlayMode.Complete);
        RefreshPresentation(0);
    }

    private void ToggleMap()
    {
        if (Session.Won) return;
        ShowOverlay(Hud.Mode == OverlayMode.Map ? OverlayMode.None : OverlayMode.Map);
    }

    private void ShowOverlay(OverlayMode mode)
    {
        Navigation.Cancel();
        Hud.ShowOverlay(mode, Session);
    }

    private void ToggleHelp()
    {
        if (!Session.Won) ShowOverlay(Hud.Mode == OverlayMode.Help ? OverlayMode.None : OverlayMode.Help);
    }

    private static void ConfigureInput()
    {
        AddAction("maze_up", Key.W, Key.Up);
        AddAction("maze_down", Key.S, Key.Down);
        AddAction("maze_left", Key.A, Key.Left);
        AddAction("maze_right", Key.D, Key.Right);
    }

    private static void AddAction(string name, params Key[] keys)
    {
        if (!InputMap.HasAction(name)) InputMap.AddAction(name);
        foreach (var key in keys) InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key });
    }
}
