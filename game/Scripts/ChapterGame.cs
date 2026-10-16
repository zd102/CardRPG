using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame : Control
{
    private static readonly Color Ink = new("253d3a"), Muted = new("526153"), Accent = new("315d4c"),
        Danger = new("a43d2e"), Success = new("35644e"), Surface = new("f3eddf"), Outline = new("b6bda9");
    private Control _root = null!;
    private string _panel = "", _saveWarning = "", _actionWarning = "";
    private int _target;
    private bool _verify;
    private bool _layoutQueued;
    private int _sessionGeneration;
    private ScrollContainer? _scroll;
    private readonly List<ScrollContainer> _pageScrolls = new();
    private string _page = "", _toast = "";
    private sealed record PanelContext(string Craft, string QuestFilter, string Recipe, string Quest, string Gear, long Stock, string StockFilter, string Search, string StorageCraft, string Bag);
    private sealed record PanelReturn(string Panel, string? Focus, int[] Scrolls, string GearFilter, PanelContext? Context = null);
    private readonly Stack<PanelReturn> _panelHistory = new();
    private readonly Dictionary<string, int[]> _pagePositions = new();
    private readonly Dictionary<string, int> _readingPositions = new();
    private int[]? _restoreScrolls;
    private string? _requestedFocus, _sceneFocus;
    private string _gearFilter = "cargo", _selectedGear = "", _craftTab = "recipes", _questFilter = "active";
    private long _selectedStock;
    private readonly HashSet<long> _recentLoot = new();
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
        ConfigureAccessibleControls();
        GetWindow().MinSize = new Vector2I(1024, 720); GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestQuit;
        GetWindow().DpiChanged += RefreshLayout;
        Resized += RefreshLayout;
        Theme.SetStylebox("panel", "TooltipPanel", Style(Surface, Outline, 14));
        Theme.SetFontSize("font_size", "TooltipLabel", 15);
        if (!_verify) { Load(); LoadPreferences(); LoadMeasurements(); _startupOpen = true; _panel = "startup"; _requestedFocus = "@initial"; } Render();
        GD.Print("CHAPTER_READY | first chapter, outer ring, versioned saves");
        if (_verify) AddChild(new ChapterVerification { Game = this });
    }
    private void RefreshLayout()
    {
        if (_layoutQueued) return;
        _layoutQueued = true;
        Callable.From(() => { _layoutQueued = false; ApplyUiScale(); Render(); }).CallDeferred();
    }
    public void ReplaceSession(ChapterSession session)
    {
        _sessionGeneration++;_rescueStarted=0;_craftedResult="";_batchQuestScheduled=false;_referenceText="";_detailKeyword="";
        _stockSelection.Clear(); _stockQuantities.Clear(); _recentLoot.Clear(); _stockUndoBefore = _stockUndoAfter = null; _pagePositions.Clear(); _readingPositions.Clear(); _pageScrolls.Clear(); _scroll = null; _page = "";
        Session = session; _panel = ""; _target = 0; _panelHistory.Clear(); _selectedCard = -1; _feedback.Clear(); _dialogAction = null;
        _mapPlace = ""; _waypoint = ""; _stockPaired = _pairedAwaitBatch = false; _pairedIdentity = _pairedSource = ""; _panelFocus.Clear(); _detailItemId = 0;
        _deckMatrix = false; _lastEquippedKind = _pinnedGear = _deckSource = _matrixGear = _matrixCard = ""; _prepareMapExpanded = false;
        _handTokens.Clear(); _handDisplayOrder.Clear(); _handScroll = 0; _reorderHand = false; _battleGestureVersion++; _battleDeltas.Clear();
        _clickMoveId = 0; _stockArrivals.Clear();
        _stockBatches = _stockBatchMode = false; _stockGearSlot = ""; _quantityGroup = []; _stockSearch = "";
        _receipts.Clear(); _presetDraft = null; _presetDraftName = ""; _presetCompare = "";
        _batchQuestRunning = false; _batchQuestRemaining.Clear(); _batchQuestCompleted.Clear();
        _toast = ""; _toastUntil = 0; _actionWarning = ""; _lastFeedback.Clear();
        _gearFilter = "cargo"; _stockFilter = "all"; _craftTab = "recipes"; _questFilter = "active"; _selectedGear = ""; _selectedStock = 0; Render();
    }
    public void Change(Func<bool> action)
    {
        if (FeedbackBusy) return;
        var frame = Engine.GetProcessFrames(); if (_lastCommandFrame == frame) return; _lastCommandFrame = frame;
        var before = Session.ToJson(); var ground = Session.State.Ground.Keys.ToHashSet(); var playedCard = _cardCommandIndex;
        var playedDisplay=playedCard>=0&&playedCard<_handTokens.Count?_handDisplayOrder.IndexOf(_handTokens[playedCard]):-1;
        var oldState = Session.PreviewCopy().State; Session.BattleFeedback.Clear();
        if (!action()) { if (_measurement != null) _measurement.Errors++; _actionWarning = Session.FailureReason; Receipt(oldState, false, _actionWarning == "" ? "对象或操作条件已变化，未执行。" : _actionWarning); Render(); return; }
        _actionWarning = "";
        if (!Save()) { if (_measurement != null) { _measurement.Errors++; _measurement.Recoveries++; } Session = ChapterSession.FromJson(before); _actionWarning = "本次操作已撤回，资源没有消耗。"; Receipt(oldState, false, _actionWarning); Render(); return; }
        Receipt(oldState, true);
        SynchronizeHand(oldState, playedCard); RecordBattleDeltas(oldState);
        RememberDiscoveries();
        if (!oldState.World.Contains("robot-rescued") && Session.State.World.Contains("robot-rescued")) _rescueStarted = Time.GetTicksMsec();
        if (oldState.Phase == ChapterPhase.Battle && Session.State.Phase == ChapterPhase.Exploring)
        { _recentLoot.Clear(); _recentLoot.UnionWith(Session.State.Stock.Select(i => i.Id).Except(oldState.Stock.Select(i => i.Id))); }
        if (!_stockChanging) _stockUndoBefore = _stockUndoAfter = null;
        if (!_stockChanging && Session.State.Phase == ChapterPhase.Exploring && Session.State.Ground.Keys.Except(ground).Any())
        {
            _panel = _panel is "" or "loot" || oldState.Phase == ChapterPhase.Battle ? "loot" : "gear"; _gearFilter = "cargo"; _panelHistory.Clear();
            var arrived=Session.State.Ground.Keys.Except(ground).ToHashSet();_selectedStock=Session.State.Stock.FirstOrDefault(i=>arrived.Contains(i.Container))?.Id??0;
            _requestedFocus="@initial";_sceneFocus="field_loot";Cue("reward");
        }
        if (Session.State.Phase == ChapterPhase.Battle) { _panel = ""; _panelHistory.Clear(); }
        _selectedCard = -1;
        var b = Session.State.Battle;
        if (b != null && (_target >= b.Enemies.Count || b.Enemies[_target].Health == 0))
            _target = NearestLivingTarget(b);
        if (b != null && playedDisplay >= 0 && _handDisplayOrder.Count > 0) _requestedFocus = "card_" + _handTokens.IndexOf(_handDisplayOrder[Math.Min(playedDisplay,_handDisplayOrder.Count-1)]);
        _toast = Session.State.Log.LastOrDefault() ?? ""; _toastUntil = Time.GetTicksMsec() + 5500;
        _lastFeedback = Session.BattleFeedback.Count > 0 ? Session.BattleFeedback.ToList() : _lastFeedback;
        _feedback = Session.State.Phase == ChapterPhase.Battle ? Session.BattleFeedback.ToList() : new(); _feedbackStarted = Time.GetTicksMsec();
        DetectStory(oldState);
        if (oldState.Battle?.Id == "trial" && Session.State.Phase == ChapterPhase.Camp && _trialRecipeReturn != "")
        { _selectedRecipe = _trialRecipeReturn; _trialRecipeReturn = ""; _craftTab = "recipes"; _panel = "craft"; _requestedFocus = "@initial"; }
        Render();
    }
    private void Toggle(string panel)
    {
        if (_panel == panel) { ClosePanel(); return; }
        if (panel == "gear") _gearFilter = "cargo";
        if (_panel != "" && GetViewport().GuiGetFocusOwner() is { } owner) _panelFocus[_panel] = owner.Name.ToString();
        if (Session.State.Phase == ChapterPhase.Exploring && (_panel == "loot" || _panel == "gear" && _gearFilter == "cargo" || _panelHistory.Any(p => p.Panel == "loot" || p.Panel == "gear" && p.GearFilter == "cargo")))
        { OpenNested(panel); return; }
        if (_panel == "") _sceneFocus = GetViewport().GuiGetFocusOwner()?.Name.ToString();
        _panelHistory.Clear(); _panel = panel; _requestedFocus = "@initial"; Render();
    }
    private int NearestLivingTarget(ChapterBattle battle) => battle.Enemies.Select((enemy, index) => (enemy, index)).Where(p => p.enemy.Health > 0).OrderBy(p => Math.Abs(p.index - _target)).ThenBy(p => p.index).Select(p => p.index).DefaultIfEmpty(0).First();
    private void OpenNested(string panel)
    {
        if (_measurement != null && panel is "detail" or "recipe-preview" or "material-info") _measurement.DetailOpens++;
        if (panel == "detail") _detailKeyword = "";
        _panelHistory.Push(new(_panel, GetViewport().GuiGetFocusOwner()?.Name.ToString(), _pageScrolls.Select(p => p.ScrollVertical).ToArray(), _gearFilter,
            new(_craftTab, _questFilter, _selectedRecipe, _selectedQuest, _selectedGear, _selectedStock, _stockFilter, _stockSearch, _storageCraftTab, _selectedBag)));
        _panel = panel; _requestedFocus = "@initial"; Render();
    }
    private bool _allowClosePickup;
    private void ClosePanel()
    {
        if (_panel == "startup") return;
        var closedStory = _panel == "story";
        if (_panelHistory.Count == 0 && (_panel == "gear" && _gearFilter == "cargo" || _panel == "loot") && Session.State.Phase == ChapterPhase.Exploring)
        {
            if (!_allowClosePickup && Session.GroundHere.Any(g => g.Value.FreePickup))
            {
                Confirm(UiCopy.Text("UI_2EF8690D3DE2"), UiCopy.Text("UI_02957A1159FB"), () => { _allowClosePickup = true; ClosePanel(); }, UiCopy.Text("UI_D467AA66AD13")); return;
            }
            _allowClosePickup = false;
            var before = Session.ToJson(); Session.ClosePickup();
            if (!Save()) { Session = ChapterSession.FromJson(before); Render(); return; }
        }
        _actionWarning = "";
        if (_panelHistory.TryPop(out var previous))
        {
            _panel = previous.Panel; _requestedFocus = previous.Focus; _restoreScrolls = previous.Scrolls; _gearFilter = previous.GearFilter;
            if (previous.Context is { } context)
            { _craftTab = context.Craft; _questFilter = context.QuestFilter; _selectedRecipe = context.Recipe; _selectedQuest = context.Quest; _selectedGear = context.Gear; _selectedStock = context.Stock; _stockFilter = context.StockFilter; _stockSearch = context.Search; _storageCraftTab = context.StorageCraft; _selectedBag = context.Bag; }
        }
        else { _panel = ""; _requestedFocus = _sceneFocus; }
        if(closedStory){_readDiscoveries.Add(_story);SavePreferences();}
        _dialogAction = null; Render();
        if (closedStory && _batchQuestRunning) ScheduleQuestBatch();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (TextEntryFocused()) return;
        if (ExclusivePanel && key.Keycode != Key.Escape) return;
        var navigation = _keys.FirstOrDefault(p => p.Value == key.Keycode && p.Key is "map" or "gear" or "character" or "journal" or "help").Key;
        if (navigation != null) { Toggle(navigation); GetViewport().SetInputAsHandled(); return; }
        switch (key.Keycode)
        {
            case Key.Escape when _panel != "": ClosePanel(); break;
            case Key.Escape when _selectedCard >= 0: _selectedCard = -1; Render(); break;
            case Key.Escape: Toggle("system"); break;
            case Key.Enter when _panel == "" && Session.State.Phase is ChapterPhase.Extracted or ChapterPhase.Defeated: Change(Session.ReturnToCamp); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }
    private static void Dock(Control c, float l, float t, float r, float b, float al = 0, float at = 0, float ar = 0, float ab = 0)
    {
        c.AnchorLeft = al; c.AnchorTop = at; c.AnchorRight = ar; c.AnchorBottom = ab;
        c.OffsetLeft = l; c.OffsetTop = t; c.OffsetRight = r; c.OffsetBottom = b;
    }
    private static void FocusLater(Control target) => Callable.From(() => FocusAfterLayout(target)).CallDeferred();
    private static async void FocusAfterLayout(Control target)
    {
        if (GodotObject.IsInstanceValid(target) && target.IsInsideTree() && target.IsVisibleInTree() && target.FocusMode != FocusModeEnum.None)
        {
            target.GrabFocus();
            var tree = target.GetTree();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(target) || !target.IsInsideTree() || !target.HasFocus()) return;
            for (Node? parent = target.GetParent(); parent != null; parent = parent.GetParent())
                if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(target);
        }
    }
    public void Render()
    {
        _renderGeneration++; if (_leftHeld) _blockLeftRelease = true;
        UpdateInputBoundary();
        if (GetViewport().GuiIsDragging()) GetViewport().GuiCancelDrag();
        _dragPreviewText = null;
        _dragChecks.Clear();
        CloseStockMenu();
        if (_page != "") _pagePositions[_page] = _pageScrolls.Select(p => p.ScrollVertical).ToArray();
        foreach (var saved in _pageScrolls.Where(p => p.HasMeta("reading_key"))) _readingPositions[saved.GetMeta("reading_key").AsString()] = saved.ScrollVertical;
        var focus = _requestedFocus ?? GetViewport().GuiGetFocusOwner()?.Name.ToString(); _requestedFocus = null;
        var s = Session.State; var page = _panel + ":" + s.Phase + ":" + s.Location + ":" + (_panel == "gear" ? _gearFilter : _panel == "craft" ? _craftTab+":"+_storageCraftTab : _panel == "journal" ? _questFilter : _panel=="system"?_settingsTab:"");
        if (_measurement != null && _page != "" && _page != page) _measurement.PageChanges++;
        if (s.Battle is { } activeBattle && (_target >= activeBattle.Enemies.Count || activeBattle.Enemies[_target].Health == 0))
            _target = NearestLivingTarget(activeBattle);
        var scrolls = _restoreScrolls ?? _pagePositions.GetValueOrDefault(page, []); _restoreScrolls = null;
        _page = page; _scroll = null; _pageScrolls.Clear();
        CompactDensity = _pageDensity.GetValueOrDefault(DensityPage, _defaultCompact);
        _inspection = null; _inspectionCopy = null; _inspected = null;
        _resumePointerInspection=null;_pointerInspected=null;
        if (_root != null)
        {
            if (_root.FindChild("HandViewport", true, false) is ScrollContainer oldHand && _handTokens.Count > 0) _handScroll = oldHand.ScrollHorizontal;
            RemoveChild(_root); _root.QueueFree();
        }
        Buttons.Clear(); _root = new Control { Name = "ChapterPresentation", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_root); _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var scene = new ChapterScene { Name = "World", Location = s.Phase is ChapterPhase.Camp or ChapterPhase.Extracted or ChapterPhase.Defeated ? "camp" : s.Battle?.Id == "trial" ? "A04" : s.Location,
            Lit = s.Done.Contains("Q01"), Robot = s.Done.Contains("S07"), Rescued = s.World.Contains("robot-rescued"), Clothes = s.Done.Contains("S09"), Door = s.World.Contains("dark-door"), Powered = s.World.Contains("station"),
            ShowDoor = Session.IsRevealed("A06"), ShowTrain = Session.IsRevealed("A07"), RescueAt = _reducedMotion ? 0 : _rescueStarted };
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
        if (_saveWarning != "" && _panel == "" && s.Phase is ChapterPhase.Camp or ChapterPhase.Exploring or ChapterPhase.Battle)
        {
            var error = Panel(_root, Surface, Danger); error.Name = "SaveFailure";
            Dock(error, 24, 92, -24, 170, ar: 1);
            SaveFailureContent(error);
        }
        InspectionLayer();
        FeedbackLayer();
        if(_panel=="" && s.Phase is ChapterPhase.Camp or ChapterPhase.Exploring) Callable.From(ReflowHotspotLabels).CallDeferred();
        for (var i = 0; i < Math.Min(scrolls.Length, _pageScrolls.Count); i++)
            _pageScrolls[i].SetDeferred(ScrollContainer.PropertyName.ScrollVertical, scrolls[i]);
        foreach (var detail in _pageScrolls.Where(p => p.HasMeta("reading_key"))) detail.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, _readingPositions.GetValueOrDefault(detail.GetMeta("reading_key").AsString()));
        if (focus == "@initial") focus = InitialPanelFocus();
        if (focus != null && Buttons.TryGetValue(focus, out var button) && !button.Disabled && button.FocusMode != FocusModeEnum.None)
            FocusLater(button);
        else if (focus != null && _root.FindChild(focus, true, false) is Control input && input.FocusMode != FocusModeEnum.None) FocusLater(input);
        else if (Buttons.TryGetValue("close_panel", out var close) && close.Visible) FocusLater(close);
        QueueViewportMeasurement();
    }
    private static string Materials(int[] amounts) => string.Join(" / ", amounts.Select((n, i) => ChapterContent.MaterialName(i) + " " + n));
    private string SavePath => ProjectSettings.GlobalizePath("user://chapter1-inventory-v2.json");
    private void RequestQuit() { if (_startupOpen || Save()) GetTree().Quit(); else Render(); }
    public bool Save()
    {
        if (_verify && SimulateSaveFailure) { _saveWarning = "保存失败（隔离验证）：本次操作未保存，请重试。"; return false; }
        if (_verify) { _saveWarning = ""; return true; }
        try
        {
            SaveStore.Save(Session);
            _saveWarning = ""; _lastSavedAt = DateTime.Now.ToString("HH:mm:ss"); return true;
        }
        catch (Exception e) { _saveWarning = "未能写入存档，请检查磁盘后重试。"; GD.PushWarning(e.Message); return false; }
    }
    private void Load()
    {
        var source = File.Exists(SavePath) ? SavePath : ProjectSettings.GlobalizePath("user://chapter1-outer-ring-v1.json");
        try
        {
            if (File.Exists(source))
            {
                Session = ChapterSession.FromJson(File.ReadAllText(source));
                _loadedSave = true;
            }
        }
        catch (Exception e)
        {
            _loadedSave = false;
            _startupProblem = "章节存档无法读取，原文件保持原位。可选择下方可用备份恢复，或先归档再新建；退出不会覆盖旧档。"; GD.PushWarning(e.Message);
        }
    }
}
