using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame : Control
{
    private static readonly Color Ink = new("fff0d4"), Muted = new("bed2cf"), Accent = new("f4c578"),
        Danger = new("ffad8a"), Success = new("8fdbc0"), Surface = new("192c3ef2"), Outline = new("96b3aa7d");
    private Control _root = null!;
    private string _panel = "", _saveWarning = "";
    private int _target;
    private bool _verify;
    private bool _layoutQueued;
    private ScrollContainer? _scroll;
    private string _page = "", _toast = "";
    private ulong _toastUntil;
    public ChapterSession Session { get; private set; } = new();
    public Dictionary<string, Button> Buttons { get; } = new();
    public override void _Ready()
    {
        _verify = OS.GetCmdlineUserArgs().Contains("--verify-chapter");
        Theme = new Theme { DefaultFont = new SystemFont { FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "sans-serif"] }, DefaultFontSize = 16 };
        GetWindow().MinSize = new Vector2I(1100, 760); GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestQuit;
        Resized += RefreshLayout;
        Theme.SetStylebox("panel", "TooltipPanel", Style(new Color("101b20f5"), Outline, 14));
        Theme.SetFontSize("font_size", "TooltipLabel", 15);
        if (!_verify) Load(); Render();
        GD.Print("CHAPTER_READY | first chapter, outer ring, versioned saves");
        if (_verify) AddChild(new ChapterVerification { Game = this });
    }
    private void RefreshLayout()
    {
        if (_layoutQueued) return;
        _layoutQueued = true;
        Callable.From(() => { _layoutQueued = false; Render(); }).CallDeferred();
    }
    public void ReplaceSession(ChapterSession session) { Session = session; _panel = ""; _target = 0; Render(); }
    public void Change(Func<bool> action)
    {
        if (!action()) return;
        var b = Session.State.Battle;
        if (b != null && (_target >= b.Enemies.Count || b.Enemies[_target].Health == 0))
            _target = Math.Max(0, b.Enemies.FindIndex(e => e.Health > 0));
        _toast = Session.State.Log.LastOrDefault() ?? ""; _toastUntil = Time.GetTicksMsec() + 5500;
        Save(); Render();
    }
    private void Toggle(string panel) { _panel = _panel == panel ? "" : panel; Render(); }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        switch (key.Keycode)
        {
            case Key.Escape when _panel != "": _panel = ""; Render(); break;
            case Key.M: Toggle("map"); break;
            case Key.I: Toggle("gear"); break;
            case Key.J: Toggle("journal"); break;
            case Key.F1: Toggle("help"); break;
            case Key.Space when _panel == "" && Session.State.Phase == ChapterPhase.Battle: Change(() => Session.EndTurn()); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }
    private static void Dock(Control c, float l, float t, float r, float b, float al = 0, float at = 0, float ar = 0, float ab = 0)
    {
        c.AnchorLeft = al; c.AnchorTop = at; c.AnchorRight = ar; c.AnchorBottom = ab;
        c.OffsetLeft = l; c.OffsetTop = t; c.OffsetRight = r; c.OffsetBottom = b;
    }
    public void Render()
    {
        var focus = GetViewport().GuiGetFocusOwner()?.Name.ToString();
        var s = Session.State; var page = _panel + ":" + s.Phase + ":" + s.Location;
        var scroll = page == _page ? _scroll?.ScrollVertical ?? 0 : 0; _page = page; _scroll = null;
        if (_root != null) { RemoveChild(_root); _root.QueueFree(); }
        Buttons.Clear(); _root = new Control { Name = "ChapterPresentation", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_root); _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var scene = new ChapterScene { Name = "World", Location = s.Phase is ChapterPhase.Camp or ChapterPhase.Extracted or ChapterPhase.Defeated ? "camp" : s.Battle?.Id == "trial" ? "A04" : s.Location,
            Lit = s.Done.Contains("Q01"), Robot = s.Done.Contains("S07"), Rescued = s.World.Contains("robot-rescued"), Clothes = s.Done.Contains("S09"), Door = s.World.Contains("dark-door"), Powered = s.World.Contains("station"), Map = _panel == "map",
            ShowDoor = Session.IsRevealed("A06"), ShowTrain = Session.IsRevealed("A07") };
        _root.AddChild(scene); scene.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SceneShade(); Header();
        if (_panel != "") ShowPanel();
        else switch (s.Phase)
        {
            case ChapterPhase.Camp: Camp(); break;
            case ChapterPhase.Exploring: Explore(); break;
            case ChapterPhase.Battle: Battle(); break;
            default: Debrief(); break;
        }
        if (_panel == "" && s.Phase is ChapterPhase.Camp or ChapterPhase.Exploring && _toast != "" && Time.GetTicksMsec() < _toastUntil) Toast();
        if (_saveWarning != "")
        {
            var error = Panel(_root, new Color("492c28")); Dock(error, 24, 130, -24, 188, ar: 1);
            error.AddChild(Text(_saveWarning, 15, Danger));
        }
        if (_scroll != null) _scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, scroll);
        if (focus != null && Buttons.TryGetValue(focus, out var button) && !button.Disabled) button.CallDeferred(Control.MethodName.GrabFocus);
    }
    private static string Materials(int[] amounts) => string.Join(" / ", amounts.Select((n, i) => ChapterContent.MaterialName(i) + " " + n));
    private string SavePath => ProjectSettings.GlobalizePath("user://chapter1-outer-ring-v1.json");
    private void RequestQuit() { if (Save()) GetTree().Quit(); else Render(); }
    public bool Save()
    {
        if (_verify) return true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            File.WriteAllText(SavePath + ".tmp", Session.ToJson()); File.Move(SavePath + ".tmp", SavePath, true);
            _saveWarning = ""; return true;
        }
        catch (Exception e) { _saveWarning = "保存失败，进度仍在内存。请检查磁盘后重试：" + e.Message; GD.PushWarning(_saveWarning); return false; }
    }
    private void Load()
    {
        try { if (File.Exists(SavePath)) Session = ChapterSession.FromJson(File.ReadAllText(SavePath)); }
        catch (Exception e)
        {
            if (File.Exists(SavePath)) File.Copy(SavePath, SavePath + ".invalid-" + DateTime.UtcNow.Ticks);
            _saveWarning = "章节存档无法读取，已保留备份并开始新进度。"; GD.PushWarning(e.Message);
        }
    }
}
