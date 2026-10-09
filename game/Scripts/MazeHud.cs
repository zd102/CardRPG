using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public enum OverlayMode { None, Map, Pause, Restart, Complete, Help }

public partial class MazeHud : CanvasLayer
{
    private static readonly Color Text = new("e1e7de");
    private static readonly Color Muted = new("a0b0ad");
    private static readonly Color Gold = new("e9b96a");
    private Label _objective = null!;
    private Label _location = null!;
    private Label _key = null!;
    private Label _treasure = null!;
    private Label _walkHint = null!;
    private PanelContainer _toast = null!;
    private Label _message = null!;
    private Label _context = null!;
    private Cell? _contextTarget;
    private MazeSession? _lastSession;
    private string? _lastMessage;
    private string? _lastFeedback;
    private float _toastRemaining;
    private bool _hasMoved;
    private Control _overlay = null!;
    private PanelContainer _modal = null!;
    private Label _modalTitle = null!;
    private Label _modalBody = null!;
    private Button _primary = null!;
    private Button _restart = null!;
    private Button _cancel = null!;
    private ExplorationMap _map = null!;
    private readonly List<Control> _worldBlockers = [];
    public OverlayMode Mode { get; private set; }
    public Control Root { get; private set; } = null!;
    public event Action? MapRequested;
    public event Action? PauseRequested;
    public event Action? HelpRequested;
    public event Action? ResumeRequested;
    public event Action? RestartRequested;
    public event Action? RestartConfirmationRequested;
    public event Action? QuitRequested;

    public override void _Ready()
    {
        Root = new Control { Name = "Interface", MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(Root);
        Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var font = new SystemFont { FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "sans-serif"] };
        Root.Theme = new Theme { DefaultFont = font, DefaultFontSize = 18 };

        // Read-only HUD text never consumes clicks on the world beneath it.
        var heading = new VBoxContainer { Position = new Vector2(28, 24), MouseFilter = Control.MouseFilterEnum.Ignore };
        heading.AddThemeConstantOverride("separation", 5);
        Root.AddChild(heading);
        _location = Label("灰烬回廊 / 入口庭院", 21, Text);
        heading.AddChild(_location);
        _objective = Label("◇  寻找铜钥匙", 16, Gold);
        heading.AddChild(_objective);

        var tools = new HBoxContainer { Name = "HudTools", AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -184, OffsetRight = -28, OffsetTop = 24, OffsetBottom = 68, MouseFilter = Control.MouseFilterEnum.Stop };
        tools.AddThemeConstantOverride("separation", 10);
        Root.AddChild(tools);
        tools.AddChild(IconButton("MapButton", "map", "探索地图 · M", () => MapRequested?.Invoke()));
        tools.AddChild(IconButton("PauseButton", "pause", "暂停 · Esc", () => PauseRequested?.Invoke()));
        tools.AddChild(IconButton("HelpButton", "help", "操作指引 · F1", () => HelpRequested?.Invoke()));
        _worldBlockers.Add(tools);

        var inventory = new HBoxContainer { Name = "InventoryHud", AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -270, OffsetRight = -28, OffsetTop = 82, OffsetBottom = 106, Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
        inventory.AddThemeConstantOverride("separation", 20);
        Root.AddChild(inventory);
        _key = Label("◆ 铜钥匙", 16, Gold);
        _treasure = Label("", 16, Muted);
        inventory.AddChild(_key);
        inventory.AddChild(_treasure);

        _walkHint = Label("点击地面前进  /  WASD 移动", 16, Muted);
        _walkHint.Name = "WalkHint";
        Root.AddChild(_walkHint);

        _context = Label("", 16, Gold);
        _context.Name = "ContextHint";
        _context.Visible = false;
        Root.AddChild(_context);

        _toast = new PanelContainer { Name = "SceneMessage", AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -240, OffsetRight = 240, OffsetTop = 94, OffsetBottom = 94, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _toast.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.04f, 0.07f, 0.08f, 0.9f), new Color(0, 0, 0, 0), 10));
        Root.AddChild(_toast);
        _message = Label("", 16, Text, true);
        _message.HorizontalAlignment = HorizontalAlignment.Center;
        _toast.AddChild(_message);
        BuildOverlay();
    }

    public void Update(MazeSession session, Transform2D worldToScreen, float delta, Cell? hovered = null, string? feedback = null)
    {
        if (!ReferenceEquals(session, _lastSession))
        {
            _lastSession = session;
            _lastMessage = session.LastMessage;
            _lastFeedback = null;
            _toastRemaining = 0;
            _hasMoved = false;
            _contextTarget = null;
        }
        _location.Text = "灰烬回廊 / " + Region(session);
        _objective.Text = "◇  " + session.Objective;
        _key.Visible = session.HasKey;
        _treasure.Visible = session.TreasureCount > 0;
        _treasure.Text = $"银币 {session.TreasureCount}";
        if (session.LastMessage != _lastMessage)
        {
            _lastMessage = session.LastMessage;
            ShowMessage(session.LastMessage);
        }
        if (feedback != _lastFeedback)
        {
            _lastFeedback = feedback;
            if (feedback != null) ShowMessage(feedback);
        }
        _toastRemaining = Math.Max(0, _toastRemaining - delta);
        _toast.Visible = Mode == OverlayMode.None && _toastRemaining > 0;
        _toast.Modulate = new Color(1, 1, 1, Math.Min(1, _toastRemaining / 0.5f));

        var player = worldToScreen * (new Vector2(session.Position.X, session.Position.Y) * MazeView.TilePixels);
        _hasMoved |= System.Numerics.Vector2.Distance(session.Position, session.Map.Start.Center) > 0.45f;
        _walkHint.Visible = Mode == OverlayMode.None && !_hasMoved && session.ElapsedSeconds < 8;
        _walkHint.Size = _walkHint.GetCombinedMinimumSize();
        _walkHint.Position = player + new Vector2(-_walkHint.Size.X / 2, 48);

        // Contextual text is click-through; only the world object owns the interaction.
        _contextTarget = hovered ?? session.InteractionTarget();
        _context.Visible = Mode == OverlayMode.None && !session.Won && _contextTarget.HasValue;
        if (_contextTarget is Cell target)
        {
            var keyboardTarget = session.InteractionTarget() == target;
            _context.Text = keyboardTarget ? $"{session.InteractionLabel(target)}  [E]" : session.InteractionLabel(target);
            _context.Size = _context.GetCombinedMinimumSize();
            var anchor = worldToScreen * (new Vector2(target.Center.X, target.Center.Y) * MazeView.TilePixels);
            var candidates = new[]
            {
                anchor + new Vector2(-_context.Size.X / 2, -_context.Size.Y - 32),
                anchor + new Vector2(-_context.Size.X / 2, 32),
                anchor + new Vector2(32, -_context.Size.Y / 2),
                anchor + new Vector2(-_context.Size.X - 32, -_context.Size.Y / 2)
            };
            var clearance = new Rect2(player - new Vector2(58, 58), new Vector2(116, 116));
            var bounds = new Rect2(new Vector2(12, 116), Root.Size - new Vector2(24, 128));
            var position = candidates.FirstOrDefault(candidate =>
                bounds.Encloses(new Rect2(candidate, _context.Size)) && !clearance.Intersects(new Rect2(candidate, _context.Size)),
                player + new Vector2(-_context.Size.X / 2, -_context.Size.Y - 76));
            _context.Position = new Vector2(
                Mathf.Clamp(position.X, 12, Root.Size.X - _context.Size.X - 12),
                Mathf.Clamp(position.Y, 12, Root.Size.Y - _context.Size.Y - 12));
        }
        _map.Session = session;
        if (Mode == OverlayMode.Map) _map.QueueRedraw();
    }

    public void ShowMessage(string message)
    {
        _message.Text = message;
        _toastRemaining = message.StartsWith("石碑") ? 8 : 4.5f;
    }

    public void ShowOverlay(OverlayMode mode, MazeSession session)
    {
        Mode = mode;
        _overlay.Visible = mode != OverlayMode.None;
        if (mode == OverlayMode.None) { GetViewport().GuiReleaseFocus(); return; }
        _context.Visible = false;
        _walkHint.Visible = false;
        _toast.Visible = false;
        var isMap = mode == OverlayMode.Map;
        var isHelp = mode == OverlayMode.Help;
        _map.Visible = isMap;
        _map.Session = session;
        _map.QueueRedraw();
        var size = isMap ? new Vector2(860, 660) : isHelp ? new Vector2(660, 530) : new Vector2(570, 390);
        _modal.OffsetLeft = -size.X / 2;
        _modal.OffsetRight = size.X / 2;
        _modal.OffsetTop = -size.Y / 2;
        _modal.OffsetBottom = size.Y / 2;
        _modalTitle.Text = mode switch
        {
            OverlayMode.Map => "走过的路",
            OverlayMode.Complete => "回廊尽头，微光再现",
            OverlayMode.Restart => "重新踏入回廊？",
            OverlayMode.Help => "探索指引",
            _ => "在此稍作停留"
        };
        var elapsed = $"{(int)session.ElapsedSeconds / 60:00}:{(int)session.ElapsedSeconds % 60:00}";
        _modalBody.Text = mode switch
        {
            OverlayMode.Map => "亮色是眼前的地形，暗色是记忆。未知的区域仍然隐在黑暗里。",
            OverlayMode.Complete => $"你找到了出口。\n行进用时 {elapsed}    ·    旧银币 {session.TreasureCount} 枚\n" + (session.ShortcutUnlocked ? "回程捷径已打开，你已认出了庭院与深廊的连接。" : "还有回接旧路的捷径，留待下一次探索。"),
            OverlayMode.Restart => "本次探索、钥匙与门的状态将重置。\n可以重新体验第一次推开门的瞬间。",
            OverlayMode.Help => "左键点击地面任意位置前往，右键停止。\n点击可见物件，靠近后交互。\n开门后，点击门后的地面穿过。\n\nWASD / 方向键移动，E 交互。\nM 地图 · Esc 暂停 / 返回 · R 重开 · F1 帮助。\n\n墙和关闭的门会遮挡视线。\n地图只记录走过或实际看见的地形。",
            _ => $"探索已暂停。    行进用时 {elapsed}\n准备好后，沿着你记得的路继续前行。"
        };
        _primary.Text = mode is OverlayMode.Complete or OverlayMode.Restart ? "重新探索" : "继续探索";
        _restart.Visible = mode == OverlayMode.Pause;
        _cancel.Visible = mode == OverlayMode.Restart;
        _primary.GrabFocus();
    }

    public bool BlocksWorldPointer(Vector2 point) => Mode != OverlayMode.None ||
        _worldBlockers.Any(control => control.IsVisibleInTree() && control.GetGlobalRect().HasPoint(point));

    private void BuildOverlay()
    {
        _overlay = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        Root.AddChild(_overlay);
        _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0.015f, 0.025f, 0.032f, 0.94f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _modal = new PanelContainer();
        _overlay.AddChild(_modal);
        _modal.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _modal.AddThemeStyleboxOverride("panel", PanelStyle(new Color("111e24"), new Color("546b6a"), 30));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 20);
        _modal.AddChild(column);
        _modalTitle = Label("", 30, Gold);
        column.AddChild(_modalTitle);
        _modalBody = Label("", 18, Text, true);
        _modalBody.AddThemeConstantOverride("line_spacing", 8);
        column.AddChild(_modalBody);
        _map = new ExplorationMap { CustomMinimumSize = new Vector2(0, 400), SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(_map);
        column.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        column.AddChild(row);
        _primary = Button("继续探索", () => { if (Mode is OverlayMode.Complete or OverlayMode.Restart) RestartRequested?.Invoke(); else ResumeRequested?.Invoke(); });
        _primary.Name = "ModalPrimary";
        row.AddChild(_primary);
        _restart = Button("重新开始", () => RestartConfirmationRequested?.Invoke(), false);
        _restart.Name = "ModalRestart";
        row.AddChild(_restart);
        _cancel = Button("取消重开", () => ResumeRequested?.Invoke(), false);
        _cancel.Name = "ModalCancel";
        row.AddChild(_cancel);
        var quit = Button("退出 Demo", () => QuitRequested?.Invoke(), false);
        quit.Name = "ModalQuit";
        row.AddChild(quit);
    }

    private static string Region(MazeSession session)
    {
        var p = session.Position;
        if (p.X >= 25 && p.Y >= 12) return "微光祠堂";
        if (p.X >= 20 && p.Y >= 16) return "遗落的侧室";
        if (p.X < 11 && p.Y >= 15) return "入口庭院";
        if (p.X >= 13 && p.Y <= 10) return "碎石大厅";
        if (p.X < 13) return "曲折北廊";
        return "回声石廊";
    }

    private static Label Label(string text, int size, Color color, bool wrap = false)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        if (wrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private static Button Button(string text, Action action, bool primary = true)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeFontSizeOverride("font_size", 17);
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeStyleboxOverride("normal", PanelStyle(new Color(0.065f, 0.11f, 0.13f, 0.9f), new Color(primary ? "526e6a" : "354b50"), 8));
        button.AddThemeStyleboxOverride("hover", PanelStyle(new Color("344d4e"), Gold, 8));
        button.AddThemeStyleboxOverride("pressed", PanelStyle(new Color("18272d"), Gold, 8));
        button.AddThemeStyleboxOverride("focus", PanelStyle(new Color(0, 0, 0, 0), Gold, 0));
        button.Pressed += action;
        return button;
    }

    private static Button IconButton(string name, string glyph, string tooltip, Action action)
    {
        var button = Button(glyph == "help" ? "?" : "", action, false);
        button.Name = name;
        button.TooltipText = tooltip;
        button.CustomMinimumSize = new Vector2(44, 44);
        if (glyph != "help") button.Icon = CreateIcon(glyph);
        button.AddThemeFontSizeOverride("font_size", 23);
        return button;
    }

    private static Texture2D CreateIcon(string glyph)
    {
        using var image = Image.CreateEmpty(22, 22, false, Image.Format.Rgba8);
        void Rect(int x, int y, int width, int height)
        {
            for (var py = y; py < y + height; py++)
                for (var px = x; px < x + width; px++) image.SetPixel(px, py, Text);
        }
        if (glyph == "pause") { Rect(5, 4, 4, 14); Rect(13, 4, 4, 14); }
        else
        {
            Rect(2, 5, 2, 14); Rect(8, 2, 2, 14); Rect(14, 5, 2, 14); Rect(19, 2, 2, 14);
            for (var x = 3; x < 9; x++) { Rect(x, 5 - (x - 3) / 2, 1, 2); Rect(x, 18 - (x - 3) / 2, 1, 2); }
            for (var x = 10; x < 15; x++) { Rect(x, 2 + (x - 10) / 2, 1, 2); Rect(x, 15 + (x - 10) / 2, 1, 2); }
            for (var x = 16; x < 20; x++) { Rect(x, 4 - (x - 16) / 2, 1, 2); Rect(x, 17 - (x - 16) / 2, 1, 2); }
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static StyleBoxFlat PanelStyle(Color background, Color border, int padding)
        => new() { BgColor = background, BorderColor = border, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding };
}
