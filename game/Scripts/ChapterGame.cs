using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame : Control
{
    private static readonly Color Ink = new("253d3a"), Muted = new("56665b"), Accent = new("a84c37"),
        Danger = new("a43d2e"), Success = new("446249"), Surface = new("e7dcc2"), Outline = new("8b967e");
    private Control _root = null!;
    private string _panel = "", _saveWarning = "", _actionWarning = "";
    private int _target;
    private bool _verify;
    private bool _layoutQueued;
    private ScrollContainer? _scroll;
    private readonly List<ScrollContainer> _pageScrolls = new();
    private string _page = "", _toast = "";
    private readonly Stack<string> _panelHistory = new();
    private string? _requestedFocus, _sceneFocus;
    private string _gearFilter = "all", _selectedGear = "", _craftTab = "recipes", _questFilter = "all";
    private long _selectedStock;
    private Control? _hud;
    private ulong _toastUntil;
    public ChapterSession Session { get; private set; } = new();
    public Dictionary<string, Button> Buttons { get; } = new();
    public override void _Ready()
    {
        _verify = OS.GetCmdlineUserArgs().Contains("--verify-chapter");
        Theme = new Theme { DefaultFont = ChapterSkin.BodyFont, DefaultFontSize = 16 };
        GetWindow().MinSize = new Vector2I(1100, 760); GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestQuit;
        Resized += RefreshLayout;
        Theme.SetStylebox("panel", "TooltipPanel", Style(Surface, Outline, 14));
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
    public void ReplaceSession(ChapterSession session)
    {
        Session = session; _panel = ""; _target = 0; _panelHistory.Clear();
        _gearFilter = "all"; _craftTab = "recipes"; _questFilter = "all"; _selectedGear = ""; _selectedStock = 0; Render();
    }
    public void Change(Func<bool> action)
    {
        var before = Session.ToJson(); var ground = Session.State.Ground.Keys.ToHashSet();
        if (!action()) { _actionWarning = Session.FailureReason; Render(); return; }
        _actionWarning = "";
        if (!Save()) { Session = ChapterSession.FromJson(before); Render(); return; }
        if (Session.State.Phase == ChapterPhase.Exploring && Session.State.Ground.Keys.Except(ground).Any())
        { _panel = "gear"; _gearFilter = "cargo"; _panelHistory.Clear(); }
        if (Session.State.Phase == ChapterPhase.Battle) { _panel = ""; _panelHistory.Clear(); }
        var b = Session.State.Battle;
        if (b != null && (_target >= b.Enemies.Count || b.Enemies[_target].Health == 0))
            _target = Math.Max(0, b.Enemies.FindIndex(e => e.Health > 0));
        _toast = Session.State.Log.LastOrDefault() ?? ""; _toastUntil = Time.GetTicksMsec() + 5500;
        Render();
    }
    private void Toggle(string panel)
    {
        if (_panel == panel) { ClosePanel(); return; }
        if (_panel == "") _sceneFocus = GetViewport().GuiGetFocusOwner()?.Name.ToString();
        _panelHistory.Clear(); _panel = panel; _requestedFocus = "close_panel"; Render();
    }
    private void OpenNested(string panel)
    {
        _panelHistory.Push(_panel); _panel = panel; _requestedFocus = "close_panel"; Render();
    }
    private void ClosePanel()
    {
        if (_panel == "gear" && _gearFilter == "cargo" && Session.State.Phase == ChapterPhase.Exploring)
        {
            var before = Session.ToJson(); Session.ClosePickup();
            if (!Save()) { Session = ChapterSession.FromJson(before); Render(); return; }
        }
        _actionWarning = "";
        _panel = _panelHistory.Count > 0 ? _panelHistory.Pop() : "";
        _requestedFocus = _panel == "" ? _sceneFocus : "close_panel"; Render();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        switch (key.Keycode)
        {
            case Key.Escape when _panel != "": ClosePanel(); break;
            case Key.M: Toggle("map"); break;
            case Key.I: Toggle("gear"); break;
            case Key.C: Toggle("character"); break;
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
    private static void FocusLater(Control target) => Callable.From(() =>
    {
        if (GodotObject.IsInstanceValid(target) && target.IsInsideTree() && target.IsVisibleInTree() && target.FocusMode != FocusModeEnum.None)
            target.GrabFocus();
    }).CallDeferred();
    public void Render()
    {
        var focus = _requestedFocus ?? GetViewport().GuiGetFocusOwner()?.Name.ToString(); _requestedFocus = null;
        var s = Session.State; var page = _panel + ":" + s.Phase + ":" + s.Location + ":" + _gearFilter + ":" + _craftTab + ":" + _questFilter;
        var scrolls = page == _page && _scroll != null ? _pageScrolls.Select(p => p.ScrollVertical).ToArray() : [];
        _page = page; _scroll = null; _pageScrolls.Clear();
        _inspection = null; _inspectionCopy = null; _inspected = null;
        if (_root != null) { RemoveChild(_root); _root.QueueFree(); }
        Buttons.Clear(); _root = new Control { Name = "ChapterPresentation", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_root); _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var scene = new ChapterScene { Name = "World", Location = s.Phase is ChapterPhase.Camp or ChapterPhase.Extracted or ChapterPhase.Defeated ? "camp" : s.Battle?.Id == "trial" ? "A04" : s.Location,
            Lit = s.Done.Contains("Q01"), Robot = s.Done.Contains("S07"), Rescued = s.World.Contains("robot-rescued"), Clothes = s.Done.Contains("S09"), Door = s.World.Contains("dark-door"), Powered = s.World.Contains("station"),
            ShowDoor = Session.IsRevealed("A06"), ShowTrain = Session.IsRevealed("A07") };
        _root.AddChild(scene); scene.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Header();
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
            var error = Panel(_root, Surface, Danger); Dock(error, 24, 130, -24, 188, ar: 1);
            error.AddChild(Text(_saveWarning, 15, Danger));
        }
        InspectionLayer();
        for (var i = 0; i < Math.Min(scrolls.Length, _pageScrolls.Count); i++)
            _pageScrolls[i].SetDeferred(ScrollContainer.PropertyName.ScrollVertical, scrolls[i]);
        if (focus != null && Buttons.TryGetValue(focus, out var button) && !button.Disabled && button.FocusMode != FocusModeEnum.None)
            FocusLater(button);
        else if (Buttons.TryGetValue("close_panel", out var close) && close.Visible) FocusLater(close);
    }
    private static string Materials(int[] amounts) => string.Join(" / ", amounts.Select((n, i) => ChapterContent.MaterialName(i) + " " + n));
    private string SavePath => ProjectSettings.GlobalizePath("user://chapter1-inventory-v2.json");
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
        var source = File.Exists(SavePath) ? SavePath : ProjectSettings.GlobalizePath("user://chapter1-outer-ring-v1.json");
        try
        {
            if (File.Exists(source))
            {
                Session = ChapterSession.FromJson(File.ReadAllText(source));
                if (source != SavePath) Save();
            }
        }
        catch (Exception e)
        {
            if (File.Exists(source)) File.Copy(source, source + ".invalid-" + DateTime.UtcNow.Ticks);
            _saveWarning = "章节存档无法读取，已保留备份并开始新进度。"; GD.PushWarning(e.Message);
        }
    }
}
