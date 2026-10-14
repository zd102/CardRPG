using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame : Control
{
    private static readonly Color Ink = new("253d3a"), Muted = new("4b5b50"), Accent = new("923d2c"),
        Danger = new("a43d2e"), Success = new("446249"), Surface = new("e7dcc2"), Outline = new("8b967e");
    private Control _root = null!;
    private string _panel = "", _saveWarning = "", _actionWarning = "";
    private int _target;
    private bool _verify;
    private bool _layoutQueued;
    private ScrollContainer? _scroll;
    private readonly List<ScrollContainer> _pageScrolls = new();
    private string _page = "", _toast = "";
    private sealed record PanelReturn(string Panel, string? Focus, int[] Scrolls, string GearFilter);
    private readonly Stack<PanelReturn> _panelHistory = new();
    private int[]? _restoreScrolls;
    private string? _requestedFocus, _sceneFocus;
    private string _gearFilter = "cargo", _selectedGear = "", _craftTab = "recipes", _questFilter = "active";
    private long _selectedStock;
    private Control? _hud;
    private ulong _toastUntil;
    public ChapterSession Session { get; private set; } = new();
    public Dictionary<string, Button> Buttons { get; } = new();
    public bool IsFeedbackPlaying => FeedbackBusy;
    internal bool SimulateSaveFailure { get; set; }
    public override void _Ready()
    {
        _verify = OS.GetCmdlineUserArgs().Contains("--verify-chapter");
        Theme = new Theme { DefaultFont = ChapterSkin.BodyFont, DefaultFontSize = 16 };
        GetWindow().MinSize = new Vector2I(1100, 760); GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestQuit;
        Resized += RefreshLayout;
        Theme.SetStylebox("panel", "TooltipPanel", Style(Surface, Outline, 14));
        Theme.SetFontSize("font_size", "TooltipLabel", 15);
        if (!_verify) { Load(); LoadPreferences(); } Render();
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
        Session = session; _panel = ""; _target = 0; _panelHistory.Clear(); _selectedCard = -1; _feedback.Clear(); _dialogAction = null;
        _toast = ""; _toastUntil = 0; _actionWarning = ""; _lastFeedback.Clear();
        _gearFilter = "cargo"; _stockFilter = "all"; _craftTab = "recipes"; _questFilter = "active"; _selectedGear = ""; _selectedStock = 0; Render();
    }
    public void Change(Func<bool> action)
    {
        var before = Session.ToJson(); var ground = Session.State.Ground.Keys.ToHashSet();
        var oldState = Session.PreviewCopy().State; Session.BattleFeedback.Clear();
        if (!action()) { _actionWarning = Session.FailureReason; Render(); return; }
        _actionWarning = "";
        if (!Save()) { Session = ChapterSession.FromJson(before); _actionWarning = "本次操作已撤回，资源没有消耗。"; Render(); return; }
        if (Session.State.Phase == ChapterPhase.Exploring && Session.State.Ground.Keys.Except(ground).Any())
        { _panel = oldState.Phase == ChapterPhase.Battle ? "loot" : "gear"; _gearFilter = "cargo"; _panelHistory.Clear(); Cue("reward"); }
        if (Session.State.Phase == ChapterPhase.Battle) { _panel = ""; _panelHistory.Clear(); }
        _selectedCard = -1;
        var b = Session.State.Battle;
        if (b != null && (_target >= b.Enemies.Count || b.Enemies[_target].Health == 0))
            _target = Math.Max(0, b.Enemies.FindIndex(e => e.Health > 0));
        _toast = Session.State.Log.LastOrDefault() ?? ""; _toastUntil = Time.GetTicksMsec() + 5500;
        _feedback = Session.BattleFeedback.ToList(); _feedbackStarted = Time.GetTicksMsec();
        if (_feedback.Count > 0) _lastFeedback = _feedback.ToList();
        DetectStory(oldState);
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
        _panelHistory.Push(new(_panel, GetViewport().GuiGetFocusOwner()?.Name.ToString(), _pageScrolls.Select(p => p.ScrollVertical).ToArray(), _gearFilter));
        _panel = panel; _requestedFocus = "close_panel"; Render();
    }
    private void ClosePanel()
    {
        if ((_panel == "gear" && _gearFilter == "cargo" || _panel == "loot") && Session.State.Phase == ChapterPhase.Exploring)
        {
            var before = Session.ToJson(); Session.ClosePickup();
            if (!Save()) { Session = ChapterSession.FromJson(before); Render(); return; }
        }
        _actionWarning = "";
        if (_panelHistory.TryPop(out var previous))
        { _panel = previous.Panel; _requestedFocus = previous.Focus; _restoreScrolls = previous.Scrolls; _gearFilter = previous.GearFilter; }
        else { _panel = ""; _requestedFocus = _sceneFocus; }
        _dialogAction = null; Render();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        switch (key.Keycode)
        {
            case Key.Escape when _panel != "": ClosePanel(); break;
            case Key.Escape when _selectedCard >= 0: _selectedCard = -1; Render(); break;
            case Key.Escape: Toggle("system"); break;
            case Key.Enter when _panel == "" && Session.State.Phase is ChapterPhase.Extracted or ChapterPhase.Defeated: Change(Session.ReturnToCamp); break;
            case Key.M: Toggle("map"); break;
            case Key.I: Toggle("gear"); break;
            case Key.C: Toggle("character"); break;
            case Key.J: Toggle("journal"); break;
            case Key.F1: Toggle("help"); break;
            case Key.Space when _panel == "" && Session.State.Phase == ChapterPhase.Battle: RequestEndTurn(); break;
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
        if (s.Battle is { } activeBattle && (_target >= activeBattle.Enemies.Count || activeBattle.Enemies[_target].Health == 0))
            _target = Math.Max(0, activeBattle.Enemies.FindIndex(enemy => enemy.Health > 0));
        var scrolls = _restoreScrolls ?? (page == _page && _scroll != null ? _pageScrolls.Select(p => p.ScrollVertical).ToArray() : []); _restoreScrolls = null;
        _page = page; _scroll = null; _pageScrolls.Clear();
        _inspection = null; _inspectionCopy = null; _inspected = null;
        if (_root != null) { RemoveChild(_root); _root.QueueFree(); }
        Buttons.Clear(); _root = new Control { Name = "ChapterPresentation", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_root); _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var scene = new ChapterScene { Name = "World", Location = s.Phase is ChapterPhase.Camp or ChapterPhase.Extracted or ChapterPhase.Defeated ? "camp" : s.Battle?.Id == "trial" ? "A04" : s.Location,
            Lit = s.Done.Contains("Q01"), Robot = s.Done.Contains("S07"), Rescued = s.World.Contains("robot-rescued"), Clothes = s.Done.Contains("S09"), Door = s.World.Contains("dark-door"), Powered = s.World.Contains("station"),
            ShowDoor = Session.IsRevealed("A06"), ShowTrain = Session.IsRevealed("A07") };
        _root.AddChild(scene); scene.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (s.Phase == ChapterPhase.Battle && _panel == "")
        {
            var shade = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, Texture = new GradientTexture2D
            { Width = 64, Height = 128, FillFrom = new Vector2(.5f, 0), FillTo = new Vector2(.5f, 1),
                Gradient = new Gradient { Colors = [Colors.Transparent, new Color("20332d38"), new Color("20332d38"), Colors.Transparent], Offsets = [0, .2f, .8f, 1] } } };
            _root.AddChild(shade); Dock(shade, 0, 170, 0, -260, ar: 1, ab: 1);
        }
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
            var error = Panel(_root, Surface, Danger); error.ZIndex = 40; Dock(error, 24, -100, -24, -24, ar: 1, at: 1, ab: 1);
            error.AddChild(Text(_saveWarning, 15, Danger));
        }
        InspectionLayer();
        FeedbackLayer();
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
        if (_verify && SimulateSaveFailure) { _saveWarning = "保存失败（隔离验证）：本次操作未保存，请重试。"; return false; }
        if (_verify) { _saveWarning = ""; return true; }
        try
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
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
