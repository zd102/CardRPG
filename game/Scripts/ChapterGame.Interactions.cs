using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private int _selectedCard = -1;
    private string _dialogTitle = "", _dialogText = "", _dialogAccept = "确认", _choice = "", _story = "";
    private Action? _dialogAction;
    private static float ReadingScale = 1;
    private bool _reducedMotion, _sound = true, _labels, _turnReminder = true, _battleGuide = true;
    private bool _holdLabels;
    private string _trialRecipeReturn = "";
    private int _feedbackStepMs = 180;
    private string _turnReminderMode = "playable";
    private List<string> _feedback = new(), _lastFeedback = new();
    private ulong _feedbackStarted;
    private Label? _feedbackLabel;
    private PanelContainer? _feedbackPanel;
    private int _lastFeedbackIndex = -1;
    private Vector2 _playedFrom;
    private bool FeedbackBusy => Session.State.Phase == ChapterPhase.Battle && !_reducedMotion && _feedback.Count > 0 && Time.GetTicksMsec() - _feedbackStarted < (ulong)(_feedback.Count * _feedbackStepMs);
    private bool ExclusivePanel => _panel is "startup" or "confirm" or "choice" or "story" or "quantity" or "pickup-plan" or "detail" or "recipe-preview" or "keybind";
    private bool TextEntryFocused() => GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit;

    public override void _Input(InputEvent input)
    {
        RecordMeasurementInput(input);
        if (HandleController(input) || HandleInputOwnership(input)) return;
        if (HandleActivationBoundary(input)) return;
        if (HandleKeyRebind(input)) return;
        if (input is InputEventKey labelKey && labelKey.Keycode == Shortcut("labels") && !TextEntryFocused())
        {
            _holdLabels = labelKey.Pressed;
            foreach (var pin in Buttons.Values.OfType<ChapterHotspot>()) { pin.ShowLabel = _labels || _holdLabels; pin.QueueRedraw(); }
            GetViewport().SetInputAsHandled(); return;
        }
        if (GetViewport().GuiIsDragging())
        {
            if (input is InputEventKey { Keycode: Key.Escape, Pressed: true } or InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            { GetViewport().GuiCancelDrag(); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (_stockMenu != null && input is InputEventKey menuKey)
        {
            if (menuKey.Pressed && menuKey.Keycode == Key.Escape) { CloseStockMenu(); GetViewport().SetInputAsHandled(); }
            else if (menuKey.Pressed && menuKey.Keycode is Key.Tab or Key.Up or Key.Down)
            {
                var actions = Buttons.Where(p => p.Key.StartsWith("menu_") && !p.Value.Disabled).Select(p => p.Value).ToArray();
                if (actions.Length > 0)
                {
                    var current = Array.IndexOf(actions, GetViewport().GuiGetFocusOwner());
                    var direction = menuKey.Keycode == Key.Up || menuKey.Keycode == Key.Tab && menuKey.ShiftPressed ? -1 : 1;
                    FocusLater(actions[(current + direction + actions.Length) % actions.Length]);
                }
                GetViewport().SetInputAsHandled();
            }
            else if (menuKey.Keycode is not (Key.Enter or Key.Space or Key.Tab or Key.Up or Key.Down)) GetViewport().SetInputAsHandled();
            return;
        }
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode == Key.F6 && _panel != "" && !ExclusivePanel && !TextEntryFocused())
        { FocusNextRegion(key.ShiftPressed); GetViewport().SetInputAsHandled(); return; }
        if (key.Keycode == Shortcut("inspect") && _inspected != null && _inspectionFullText != "" && !ExclusivePanel)
        { _detailItemId = 0; _detailTitle = UiCopy.Text("UI_17B00C8B46A9"); _detailText = _inspectionFullText; OpenNested("detail"); GetViewport().SetInputAsHandled(); return; }
        if (TextEntryFocused() || ExclusivePanel) return;
        if (SpatialNavigation(key.Keycode)) { GetViewport().SetInputAsHandled(); return; }
        if (key.Keycode == Key.Enter && key.ShiftPressed && _panel == "gear" && _gearFilter == "cargo")
        {
            var item = Session.State.Stock.FirstOrDefault(i => i.Id == _selectedStock);
            if (item != null && !FeedbackBusy)
            {
                RequestStockMove(item.Id, item.Container, ChosenQuantity(item));
            }
            GetViewport().SetInputAsHandled(); return;
        }
        if (_panel == "" && Session.State.Phase == ChapterPhase.Battle && key.Keycode == Shortcut("endturn"))
        { if (!FeedbackBusy) RequestEndTurn(); GetViewport().SetInputAsHandled(); }
        if (_panel == "" && _selectedCard >= 0 && key.Keycode == Key.Enter)
        { if (!FeedbackBusy) CommitSelectedCard(); GetViewport().SetInputAsHandled(); return; }
        if (_panel == "" && _selectedCard >= 0 && Session.State.Battle != null && key.Keycode is Key.Left or Key.Right)
        {
            var selected = ChapterContent.Cards[Session.State.Battle.Hand[_selectedCard]];
            if (selected.Damage == 0 || selected.Id == "C18") { GetViewport().SetInputAsHandled(); return; }
            var living = Session.State.Battle!.Enemies.Select((e, i) => (e, i)).Where(p => p.e.Health > 0).Select(p => p.i).ToArray();
            if (living.Length == 0) return;
            var n = Array.IndexOf(living, _target); _target = living[(n + (key.Keycode == Key.Right ? 1 : living.Length - 1)) % living.Length];
            Render(); GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if(what==NotificationDragEnd && _cardZoom!=null && GodotObject.IsInstanceValid(_cardZoom)) _cardZoom.Show();
        if (what == NotificationApplicationFocusOut)
        { if (IsInsideTree() && GetViewport().GuiIsDragging()) GetViewport().GuiCancelDrag(); _heldActivation.Clear(); _blockedActivation.Clear(); _heldJoyButtons.Clear();_axisX=_axisY=0;_holdLabels=false;foreach(var pin in Buttons.Values.OfType<ChapterHotspot>()){pin.ShowLabel=_labels;pin.QueueRedraw();} _leftHeld = _blockLeftRelease = false; CloseStockMenu(); }
    }

    private Action? _dialogAlternate;
    private string _dialogAlternateCaption="";
    private void Confirm(string title, string copy, Action action, string accept = "确认执行", Action? alternate = null, string alternateCaption = "")
    {
        _dialogTitle = title; _dialogText = copy; _dialogAction = action; _dialogAccept = accept;
        _dialogAlternate=alternate; _dialogAlternateCaption=alternateCaption;
        OpenNested("confirm");
    }

    private void Confirmation(VBoxContainer box)
    {
        var body = Scroll(box); body.AddChild(Text(_dialogText, 18, Ink));
        var row = Row(16); box.AddChild(row);
        row.AddChild(Button("cancel_action", UiCopy.Text("UI_B9B7BA739D8D"), ClosePanel));
        row.AddChild(Button("confirm_action", _dialogAccept, () =>
        { var action = _dialogAction; ClosePanel(); action?.Invoke(); }, primary: true));
        if(_dialogAlternate!=null) box.AddChild(Button("confirm_alternate",_dialogAlternateCaption,()=>{var action=_dialogAlternate;ClosePanel();action?.Invoke();}));
        FocusLater(Buttons["cancel_action"]);
    }

    private void SelectCard(int index)
    {
        if (FeedbackBusy || !Session.CanPlay(index, _target)) return;
        _selectedCard = index; Cue("select"); Render();
    }

    private void CommitSelectedCard()
    {
        var index = _selectedCard;
        if (Buttons.TryGetValue("card_" + index, out var card)) _playedFrom = card.Position + card.GetParent<Control>().Position;
        _cardCommandIndex = index;
        try { Change(() => Session.PlayCard(index,_target)); } finally { _cardCommandIndex = -1; }
    }

    private string CardPreview()
    {
        var b = Session.State.Battle;
        if (b == null || _selectedCard < 0 || _selectedCard >= b.Hand.Count) return "";
        var card = ChapterContent.Cards[b.Hand[_selectedCard]];
        var next = Session.Preview(s => s.PlayCard(_selectedCard, _target));
        if (next == null) return "费用不足或目标无效";
        var lines = new List<string> { card.Name + $" · {card.Action} 行动 / {card.Mana} 魔法", card.Text };
        if (card.Damage > 0)
        {
            foreach (var i in Enumerable.Range(0, b.Enemies.Count).Where(i => b.Enemies[i].Health > 0 && (card.Id == "C18" || i == _target)))
            {
                var enemy = b.Enemies[i]; var after = next.LastResolvedBattle?.Enemies.ElementAtOrDefault(i);
                lines.Add($"{i + 1}号 · " + ChapterSession.EnemyName(enemy.Id) + $"：生命 {enemy.Health} → {after?.Health ?? 0} / 护盾 {enemy.Shield} → {after?.Shield ?? 0}");
            }
        }
        var nb = next.State.Battle;
        if (nb != null) lines.Add($"自身护盾 {b.Shield} → {nb.Shield} · 行动 {b.Action} → {nb.Action} · 魔法 {b.Mana} → {nb.Mana}");
        if (card.Id is "C26" or "C36") lines.Add($"实际额外抽牌 {Math.Min(card.Id == "C36" ? 2 : 1, 2 - b.ExtraDraw)} 张（本回合上限 2）");
        lines.Insert(1, card.Damage == 0 ? "作用于自身 · 无需选择敌人" : card.Id == "C18" ? "作用于全体存活敌人" : $"目标：{_target + 1}号 · ← → 切换");
        return string.Join("\n", lines);
    }

    private string TurnForecast(bool retreat = false)
    {
        var b = Session.State.Battle;
        if (b == null) return "";
        if (b.Id == "trial") return "试用不消耗真实生命与物品，可免费结束。";
        var next = Session.Preview(s => s.EndTurn(retreat))!;
        var damage = Session.State.Health - next.State.Health;
        var copy = $"本轮敌方攻击 {b.Enemies.Sum(ChapterSession.EnemyDamage)} · 当前护盾 {b.Shield}\n预计失去 {damage} 生命，剩余 {next.State.Health}/{ChapterSession.MaxHealth}。";
        if (next.State.Phase == ChapterPhase.Defeated) copy += "\n本次行动将导致紧急回收，随身未保留物品会丢失。";
        else if (retreat) copy += "\n承伤后退回" + ChapterContent.Places[b.ReturnPlace].Name + "；尚未安全撤离，物品没有入库。";
        return copy;
    }

    private void RequestEndTurn()
    {
        if (FeedbackBusy) return;
        var b = Session.State.Battle;
        if (b == null) return;
        var hasPlayable = Enumerable.Range(0, b.Hand.Count).Any(i => Session.CanPlay(i, _target));
        if (_turnReminderMode == "always" || _turnReminderMode == "playable" && hasPlayable)
            Confirm(hasPlayable ? UiCopy.Text("UI_9F9E3A132C1D") : UiCopy.Text("UI_C66067DA32C9"), TurnForecast() + (hasPlayable ? UiCopy.Text("UI_56CDF54948C2") : UiCopy.Text("UI_717F2B4ED078")) + UiCopy.Text("UI_5EBECDE5EE11"), () => Change(() => Session.EndTurn()), UiCopy.Text("UI_975778D99095"));
        else Change(() => Session.EndTurn());
    }

    private void Retreat()
    {
        if (Session.State.Battle?.Id == "trial")
        {
            Change(Session.EndTrial);
            if (Session.State.Phase==ChapterPhase.Camp && Session.State.World.Contains("trial-complete")) { _craftTab = "recipes"; _selectedRecipe = "G26"; if(_panel!="craft")Toggle("craft");else Render(); }
            return;
        }
        var lethal = Session.Preview(s => s.EndTurn(true))?.State.Phase == ChapterPhase.Defeated;
        Confirm(lethal ? UiCopy.Text("UI_9E03E65D7629") : UiCopy.Text("UI_637A0230B6DD"), TurnForecast(true), () => Change(() => Session.EndTurn(true)), lethal ? UiCopy.Text("UI_6D7993DBFA77") : UiCopy.Text("UI_B0C63FB16D70"));
    }

    private void ExploreAction(string id)
    {
        if (id.StartsWith("train-") && id != "train-inspect" || id is "R01" or "R07")
        { _choice = id; OpenNested("choice"); return; }
        if (id == "extract")
        {
            var left = Session.State.Stock.Where(i => Session.State.Ground.ContainsKey(i.Container) && !i.Registered && (i.Kind.StartsWith('T') || ChapterContent.Gear.ContainsKey(i.Kind))).ToArray();
            if (left.Length > 0)
            {
                Confirm(UiCopy.Text("UI_94893DEC457C"), string.Join("\n", left.Select(i => ChapterContent.Places[Session.State.Ground[i.Container].Place].Name + " · " + ChapterContent.ItemName(i.Kind)))
                    + UiCopy.Text("UI_73507E4B78E2"), () => Change(() => Session.Act(id)), UiCopy.Text("UI_45C40607CA33")); return;
            }
        }
        _requestedFocus = "act_" + id;
        Change(() => Session.Act(id));
    }

    private void Choices(VBoxContainer box)
    {
        var resonance = _choice.StartsWith('R');
        box.AddChild(Text(resonance ? UiCopy.Text("UI_9778AA715537") : UiCopy.Text("UI_4613C1254439"), 20, Accent));
        box.AddChild(Text(resonance ? UiCopy.Text("UI_38EDE15DFD34") : UiCopy.Text("UI_EC0409E1D56D"), 16, Muted));
        var page = Scroll(box); var choices = new GridContainer { Columns=Size.X>=1400? (resonance?2:3):1,SizeFlagsHorizontal=SizeFlags.ExpandFill }; choices.AddThemeConstantOverride("h_separation",12);choices.AddThemeConstantOverride("v_separation",12);page.AddChild(choices);
        foreach (var a in Session.Actions().Where(a => resonance ? a.Id is "R01" or "R07" : a.Id is "train-wire" or "train-supply" or "train-parcel"))
        {
            var item = CardBox(choices); var action = a;
            item.GetParent<Control>().SizeFlagsHorizontal=SizeFlags.ExpandFill;
            item.AddChild(Button("choose_" + a.Id, (_choice == a.Id ? "◆ " : "○ ") + a.Name, () => { _choice = action.Id; Render(); }));
            item.AddChild(Text(a.Description, 16, Ink));
            var kind = a.Id switch { "train-wire" => "M02", "train-supply" => "U01", "train-parcel" => "T14", _ => "" };
            if (kind != "")
            { var def = ChapterContent.Items[kind]; var count=kind=="M02"?2:1; var slots=ChapterSession.Slots([new ChapterItem { Id=1,Kind=kind,Quantity=count,Container="carry" }]); item.AddChild(Text(UiCopy.Format("UI_528DA07F2DB3", count, slots, def.Grams * count / 1000m) + (kind == "T14" ? UiCopy.Text("UI_5C5A2EBB7A5F") : kind == "U01" ? UiCopy.Text("UI_F8FA7DA2623D") : UiCopy.Text("UI_B4F35D0C07E8"))+UiCopy.Format("UI_364C31ECC456", Session.SceneActionCost()), 16, Muted)); }
            else item.AddChild(Text(a.Id=="R01" ? UiCopy.Text("UI_FC2B052A8A98") : UiCopy.Format("UI_4145D1DEF59B", Session.Deck().Count(id => ChapterContent.Cards[id].Shield >= 7)),16,Ink));
        }
        if (Session.State.Warned) box.AddChild(Text(UiCopy.Text("UI_93D7C1A82A6F"), 16, Danger));
        var row = Row(12); box.AddChild(row); row.AddChild(Button("choice_cancel", UiCopy.Text("UI_410641B40D8B"), ClosePanel));
        var selectedName = Session.Actions().FirstOrDefault(a => a.Id == _choice)?.Name ?? "此选项";
        row.AddChild(Button("choice_confirm", UiCopy.Text("UI_60AE1D960968") + selectedName + UiCopy.Text("UI_E042C199C39B") + Session.SceneActionCost() + UiCopy.Text("UI_E010E3F578CC"), () => { var id = _choice; ClosePanel(); Change(() => Session.Act(id)); }, primary: true));
    }

    private void SystemMenu(VBoxContainer box)
    {
        box.AddChild(Button("resume", UiCopy.Text("UI_E9D5D9D3CEAA"), ClosePanel, primary: true));
        Tabs(box, "settings_", [("display", "显示与阅读"), ("controls", "操作与提醒"), ("audio", "声音"), ("convenience", "便捷操作")], _settingsTab, id => _settingsTab = id);
        var page = Scroll(box);
        if (_settingsTab == "audio") AudioSettings(page);
        else if (_settingsTab == "convenience") ConvenienceSettings(page);
        else if (_settingsTab == "display")
        {
        page.AddChild(Text(UiCopy.Text("UI_3432881850AC"), 20, Ink));
        Tabs(page, "font_", [("100", "标准 16"), ("125", "较大 20"), ("150", "大字 24")], ((int)(ReadingScale * 100)).ToString(), value => { ReadingScale = int.Parse(value) / 100f; SavePreferences(); });
        page.AddChild(Text(UiCopy.Text("UI_F974A43C9B9F"), 16, Muted));
        page.AddChild(Button("setting_density", UiCopy.Text("UI_388B413287A2") + (CompactDensity ? UiCopy.Text("UI_5D63D587B0B0") : UiCopy.Text("UI_521479CD0B2A")) + UiCopy.Text("UI_6580720DA7FE"), () => { CompactDensity = !CompactDensity; SavePreferences(); Render(); }));
        page.AddChild(Button("setting_ui_scale", UiCopy.Format("UI_F786EAD0E26A", _uiScale), () => { _uiScale = _uiScale < 1.05f ? 1.1f : _uiScale < 1.2f ? 1.25f : 1; ApplyUiScale(); SavePreferences(); Render(); }));
        page.AddChild(Button("setting_nav_labels", UiCopy.Text("UI_A680E23089A5") + (_navigationLabels ? UiCopy.Text("UI_4E1449E7D5E5") : UiCopy.Text("UI_0D720EEEA264")), () => { _navigationLabels = !_navigationLabels; SavePreferences(); Render(); }));
        page.AddChild(Text(UiCopy.Format("UI_F2270527FC21", GetWindow().CurrentScreen + 1, CurrentUiDpi(), Input.GetConnectedJoypads().Count), 14, Muted));
        Setting("setting_motion", "减少动态效果", _reducedMotion, () => _reducedMotion = !_reducedMotion);
        Setting("setting_labels", "常显全部场景标签", _labels, () => _labels = !_labels);
        }
        else
        {
        page.AddChild(Button("setting_reminder", UiCopy.Text("UI_D332C72EF94A") + (_turnReminderMode == "always" ? UiCopy.Text("UI_DE8CA931D74F") : _turnReminderMode == "playable" ? UiCopy.Text("UI_7D749BE646EA") : UiCopy.Text("UI_0F207FC4C5E1")), () => { _turnReminderMode = _turnReminderMode == "always" ? "playable" : _turnReminderMode == "playable" ? "never" : "always"; SavePreferences(); Render(); }));
        page.AddChild(Button("setting_feedback_speed", UiCopy.Text("UI_11ED420E94C1") + (_feedbackStepMs == 180 ? UiCopy.Text("UI_DB945CFE9F9F") : _feedbackStepMs == 360 ? UiCopy.Text("UI_6BEA77ACEFB3") : UiCopy.Text("UI_A63D2DDD3F94")), () => { _feedbackStepMs = _feedbackStepMs == 180 ? 360 : _feedbackStepMs == 360 ? 700 : 180; SavePreferences(); Render(); }));
        Setting("setting_guide", "战斗短引导", _battleGuide, () => _battleGuide = !_battleGuide);
        page.AddChild(Text(InputHelp, 16, Ink));
        }
        void Setting(string id, string title, bool on, Action toggle) => page.AddChild(Button(id, title + (on ? UiCopy.Text("UI_DAA8256B157C") : UiCopy.Text("UI_502E394AE645")), () => { toggle(); SavePreferences(); Render(); }));
        page.AddChild(Button("system_help", UiCopy.Text("UI_25B37D5C985E"), () => OpenNested("help")));
        page.AddChild(Button("system_keys", UiCopy.Text("UI_382FB12F4B04"), () => OpenNested("keys")));
        page.AddChild(Button("system_measurements", _measurement == null ? UiCopy.Text("UI_E6056D940DC8") : UiCopy.Text("UI_9A5AF7B2A56C"), () => OpenNested("measurements")));
        page.AddChild(Button("system_pinned", UiCopy.Text("UI_1C49849F2E02"), () => OpenNested("pinned")));
        Rule(page); page.AddChild(Button("save_quit", UiCopy.Text("UI_ED2C563EFD0E"), RequestQuit));
    }

    private void LoadPreferences()
    {
        var file = new ConfigFile(); if (file.Load("user://ui-settings.cfg") != Error.Ok) return;
        var scale = (float)file.GetValue("ui", "reading", 1f); ReadingScale = scale is 1f or 1.25f or 1.5f ? scale : 1f;
        _reducedMotion = (bool)file.GetValue("ui", "reduced", false); _sound = (bool)file.GetValue("ui", "sound", true);
        _labels = (bool)file.GetValue("ui", "labels", false); _turnReminder = (bool)file.GetValue("ui", "reminder", true);
        _battleGuide = (bool)file.GetValue("ui", "guide", true);
        CompactDensity = (bool)file.GetValue("ui", "compact", false);
        ReadKeyBindings(file);
        _turnReminderMode = file.GetValue("ui", "turn_reminder_mode", _turnReminder ? "playable" : "never").AsString();
        if (_turnReminderMode is not ("always" or "playable" or "never")) _turnReminderMode = "playable";
        _feedbackStepMs = (int)file.GetValue("ui", "feedback_ms", 180); if (_feedbackStepMs is not (180 or 360 or 700)) _feedbackStepMs = 180;
        _quickMove = (bool)file.GetValue("convenience", "quick_move", true); _dragItems = (bool)file.GetValue("convenience", "drag", true);
        _dragCards = (bool)file.GetValue("convenience", "drag_cards", true);
        _hoverTips = (bool)file.GetValue("convenience", "hover", true); _pinEntry = (bool)file.GetValue("convenience", "pin", true);
        _hoverDelayMs = (int)file.GetValue("convenience", "delay_ms", 350); if (_hoverDelayMs is not (0 or 350 or 700)) _hoverDelayMs = 350;
        _volume = Math.Clamp((float)file.GetValue("audio", "normal", .65f), 0, 1); _dangerVolume = Math.Clamp((float)file.GetValue("audio", "danger", .8f), 0, 1);
        _uiScale = Math.Clamp((float)file.GetValue("ui", "scale", 1f), 1, 1.25f); _navigationLabels = (bool)file.GetValue("ui", "nav_labels", true);
        _pinnedTasks.Clear(); _pinnedTasks.AddRange(file.GetValue("ui", "pinned", "").AsString().Split(';', StringSplitOptions.RemoveEmptyEntries).Where(ValidPinned).Distinct().Take(4));
        _trackedMaterial = file.GetValue("ui", "material", "").AsString(); if (!ChapterContent.MaterialIds.Contains(_trackedMaterial)) _trackedMaterial = "";
        _readDiscoveries.UnionWith(file.GetValue("journal","read","").AsString().Split(';',StringSplitOptions.RemoveEmptyEntries).Where(DiscoveryKnown));
        _discoveryOrder.AddRange(file.GetValue("journal","order","").AsString().Split(';',StringSplitOptions.RemoveEmptyEntries).Where(DiscoveryKnown).Distinct());
        _dismissedOpportunities.UnionWith(file.GetValue("journal","dismissed","").AsString().Split(';',StringSplitOptions.RemoveEmptyEntries));
        ApplyUiScale();
    }
    private void SavePreferences()
    {
        if (_verify) return;
        var file = new ConfigFile(); file.SetValue("ui", "reading", ReadingScale); file.SetValue("ui", "reduced", _reducedMotion);
        file.SetValue("ui", "sound", _sound); file.SetValue("ui", "labels", _labels); file.SetValue("ui", "reminder", _turnReminder); file.SetValue("ui", "guide", _battleGuide);
        file.SetValue("ui", "turn_reminder_mode", _turnReminderMode); file.SetValue("ui", "feedback_ms", _feedbackStepMs);
        file.SetValue("ui", "compact", CompactDensity);
        file.SetValue("convenience", "quick_move", _quickMove); file.SetValue("convenience", "drag", _dragItems);
        file.SetValue("convenience", "drag_cards", _dragCards);
        file.SetValue("convenience", "hover", _hoverTips); file.SetValue("convenience", "pin", _pinEntry); file.SetValue("convenience", "delay_ms", _hoverDelayMs);
        file.SetValue("audio", "normal", _volume); file.SetValue("audio", "danger", _dangerVolume); file.SetValue("ui", "scale", _uiScale); file.SetValue("ui", "nav_labels", _navigationLabels);
        file.SetValue("ui", "pinned", string.Join(";", _pinnedTasks)); file.SetValue("ui", "material", _trackedMaterial);
        file.SetValue("journal","read",string.Join(";",_readDiscoveries)); file.SetValue("journal","order",string.Join(";",_discoveryOrder)); file.SetValue("journal","dismissed",string.Join(";",_dismissedOpportunities));
        WriteKeyBindings(file);
        if (file.Save("user://ui-settings.cfg") != Error.Ok) _actionWarning = "设置未能写入磁盘，本次运行仍然生效。";
    }

    private void Cue(string category)
    {
        if (!_sound || _verify) return;
        var frequency = category switch { "select" => 620, "hit" => 130, "shield" => 380, "reward" => 880, "danger" => 180, _ => 480 };
        var bytes = new byte[4410];
        for (var i = 0; i < bytes.Length / 2; i++)
        {
            var value = (short)(Math.Sin(i * Math.Tau * frequency / 22050) * 2400 * Math.Sin(Math.PI * i / (bytes.Length / 2)));
            bytes[i * 2] = (byte)value; bytes[i * 2 + 1] = (byte)(value >> 8);
        }
        var player = new AudioStreamPlayer { VolumeLinear = category == "danger" ? _dangerVolume : _volume, Stream = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 22050, Data = bytes } };
        AddChild(player); player.Finished += player.QueueFree; player.Play();
    }

    private void FeedbackLayer()
    {
        _feedbackLabel = null; _feedbackPanel = null;
        _lastFeedbackIndex = -1;
        if (Session.State.Phase != ChapterPhase.Battle || _panel != "" || _feedback.Count == 0 || Time.GetTicksMsec() - _feedbackStarted > (ulong)(_feedback.Count * _feedbackStepMs + 1600)) return;
        if (!_reducedMotion && _feedback[0].StartsWith("打出 ") && Time.GetTicksMsec() - _feedbackStarted < 100)
        {
            var travel = Panel(_root,Surface,Ink); travel.Name = "CardTravel"; travel.ZIndex = 26; travel.MouseFilter = MouseFilterEnum.Ignore;
            travel.Position = _playedFrom; travel.Size = new Vector2(110,90);
            travel.AddChild(Text(_feedback[0].Split('·')[0],16,Ink));
            var flight = travel.CreateTween().SetParallel();
            flight.TweenProperty(travel,"position",new Vector2(560,Size.Y-250),.18);
            flight.TweenProperty(travel,"modulate:a",0f,.18);
            flight.Chain().TweenCallback(Callable.From(travel.QueueFree));
        }
        if (_root.FindChild("BattleFeedbackHost", true, false) is not Control host) return;
        _feedbackPanel = Panel(host, Surface, Outline); _feedbackPanel.Name = "BattleFeedback";
        var row = Row(8); _feedbackPanel.AddChild(row);
        _feedbackLabel = Inline("", 16, Ink); _feedbackLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _feedbackLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(_feedbackLabel);
        row.AddChild(Button("skip_feedback", UiCopy.Text("UI_F21F0E5E900D"), () => { _feedback.Clear(); Render(); }));
    }

    public override void _Process(double delta)
    {
        PollUiEnvironment(delta);
        if (_measurement != null && FeedbackBusy) _measurement.FeedbackWaitSeconds += delta;
        if (_feedbackLabel == null || !GodotObject.IsInstanceValid(_feedbackLabel) || _feedback.Count == 0) return;
        var elapsed = Time.GetTicksMsec() - _feedbackStarted;
        var index = _reducedMotion ? _feedback.Count - 1 : Math.Min(_feedback.Count - 1, (int)(elapsed / (ulong)_feedbackStepMs));
        _feedbackLabel.Text = UiCopy.Text("UI_FDFB20961676") + (index + 1) + "/" + _feedback.Count + " · " + _feedback[index];
        if (index != _lastFeedbackIndex)
        {
            _lastFeedbackIndex = index; var note = _feedback[index];
            Cue(note.Contains("护盾 +") ? "shield" : note.Contains("伤害") ? "hit" : note.Contains("抵达") ? "danger" : "select");
            if (!_reducedMotion && Session.State.Battle is { } battle && _panel == "")
            {
                for (var i = 0; i < battle.Enemies.Count; i++)
                    if (note.StartsWith($"{i + 1}号 · " + ChapterSession.EnemyName(battle.Enemies[i].Id)) && Buttons.TryGetValue("target_" + i, out var target))
                    {
                        target.Modulate = new Color(1.3f,.75f,.62f); var tween = target.CreateTween(); tween.TweenProperty(target,"modulate",Colors.White,.16);
                    }
            }
        }
        if (elapsed > (ulong)(_feedback.Count * _feedbackStepMs + 1600)) { _feedbackPanel!.GetParent<Control>().Hide(); _feedback.Clear(); }
    }

    private static readonly Dictionary<string, (string Person, string Title, string Copy)> Stories = new()
    {
        ["Q01"] = ("岑砧 · 余火站工匠", "灯又亮了", "岑砧接过继电器，把最后一截线拧紧。暖光落在工具上。\n“这盏灯，今晚不用省着点了。”\n工作灯与后续委托已开放。"),
        ["robot-rescued"] = ("小维修偶", "松开的夹具", "小维修偶敲了敲你的护臂，举起那把弯扳手，慢慢走向入口。\n救援已永久记录；安全撤离后，可以接它进站。"),
        ["station"] = ("远征记录", "升降车重新启动", "货站深处响起电机声，停住的指示灯亮了起来。\n这里现在可以安全撤离。带回路签交付后，还能从这里出发。"),
        ["S09"] = ("余火站居民", "迟到的包裹", "居民小心展开外面的包布，又把它叠好。\n“没想到还有人记得这节车厢。”\n居民的回礼与据点变化已经保存。"),
        ["Q02"] = ("岑砧 · 余火站工匠", "有自己的工具", "岑砧试了试校准后的夹具，给你让出工作台。\n“接下来，试着配一套合你手的工具。”\n制作、改装与免费台架试用已开放。")
    };

    private void DetectStory(ChapterState before)
    {
        var key = Session.State.Done.Except(before.Done).Concat(Session.State.World.Except(before.World)).FirstOrDefault(Stories.ContainsKey);
        if (key == null) return;
        _story = key; _panelHistory.Push(new(_panel, GetViewport().GuiGetFocusOwner()?.Name.ToString(), [], _gearFilter)); _panel = "story"; Cue("reward");
    }
    private void Story(VBoxContainer box)
    {
        var story = Stories[_story]; var page = Scroll(box);
        var identity = Row(12); page.AddChild(identity); Illustration(identity,DiscoveryArt(_story),76,80,false);
        var caption=FlexibleColumn(identity); caption.AddChild(Text(story.Person, 16, Accent)); caption.AddChild(Text(story.Title, 28, Ink));
        page.AddChild(Text(story.Copy, 18, Ink));
        box.AddChild(Button("story_continue", UiCopy.Text("UI_6A9D3749323A"), ClosePanel, primary: true));
        if (_batchQuestRunning && _batchQuestRemaining.Count > 0) box.AddChild(Button("story_stop_batch", UiCopy.Text("UI_7D57764EAFAD"), () => { _batchQuestRunning = false; ClosePanel(); }));
    }
}
