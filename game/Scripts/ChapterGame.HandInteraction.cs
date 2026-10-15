using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private readonly List<long> _handTokens = new(), _handDisplayOrder = new();
    private long _nextHandToken, _focusedHandToken;
    private int _cardCommandIndex = -1;
    private bool _reorderHand, _dragCards = true;
    private ulong _battleGestureVersion, _battleDeltaUntil;
    private PanelContainer? _cardZoom;
    private Label? _cardZoomText;
    private readonly Dictionary<int, string> _battleDeltas = new();

    private void SynchronizeHand(ChapterState? before = null, int playedIndex = -1)
    {
        var battle = Session.State.Battle;
        if (battle == null) { _handTokens.Clear(); _handDisplayOrder.Clear(); return; }
        if (before?.Battle is { } old && old.Id == battle.Id && old.Round == battle.Round && _handTokens.Count == old.Hand.Count)
        {
            if (playedIndex >= 0 && playedIndex < _handTokens.Count) _handTokens.RemoveAt(playedIndex);
        }
        else if (before != null || _handTokens.Count > battle.Hand.Count) { _handTokens.Clear(); _handDisplayOrder.Clear(); }
        while (_handTokens.Count < battle.Hand.Count) _handTokens.Add(++_nextHandToken);
        _handDisplayOrder.RemoveAll(token => !_handTokens.Contains(token));
        foreach (var token in _handTokens) if (!_handDisplayOrder.Contains(token)) _handDisplayOrder.Add(token);
    }
    private void RecordBattleDeltas(ChapterState before)
    {
        _battleGestureVersion++; _battleDeltas.Clear();
        if (before.Battle is not { } old) return;
        var current = Session.State.Battle ?? Session.LastResolvedBattle;
        if (current == null) return;
        for (var i = 0; i < Math.Min(old.Enemies.Count, current.Enemies.Count); i++)
        {
            var health = current.Enemies[i].Health - old.Enemies[i].Health; var shield = current.Enemies[i].Shield - old.Enemies[i].Shield;
            if (health != 0 || shield != 0) _battleDeltas[i] = $"生命 {health:+0;-0;0} · 护盾 {shield:+0;-0;0}";
        }
        _battleDeltaUntil = Time.GetTicksMsec() + (ulong)Math.Max(1800, Session.BattleFeedback.Count * _feedbackStepMs);
    }
    private bool CardDropData(Variant data, out int index)
    {
        index = -1;
        if (data.VariantType != Variant.Type.Dictionary || _panel != "" || FeedbackBusy || Session.State.Battle == null) return false;
        var payload = data.AsGodotDictionary();
        if (!payload.ContainsKey("type") || payload["type"].AsString() != "card" || !payload.ContainsKey("version") || payload["version"].AsInt64() != (long)_battleGestureVersion || !payload.ContainsKey("token")) return false;
        index = _handTokens.IndexOf(payload["token"].AsInt64()); return index >= 0 && index < Session.State.Battle.Hand.Count;
    }
    private bool CardTargetAllowed(Variant data, int target, bool self)
    {
        if (_reorderHand || !CardDropData(data, out var index)) return false;
        var card = ChapterContent.Cards[Session.State.Battle!.Hand[index]];
        return (self ? card.Damage == 0 : card.Damage > 0) && Session.CanPlay(index, target);
    }
    private void BindCardTarget(Control control, int target, bool self = false)
    {
        control.SetDragForwarding(default, Callable.From<Vector2, Variant, bool>((_, data) => CardTargetAllowed(data, target, self)), Callable.From<Vector2, Variant>((_, data) =>
        {
            Callable.From(() =>
            {
                if (!CardTargetAllowed(data, target, self) || !CardDropData(data, out var index)) return;
                _selectedCard = index; _target = target; CommitSelectedCard();
            }).CallDeferred();
        }));
        foreach (var child in control.GetChildren().OfType<Control>()) BindCardTarget(child, target, self);
    }
    private void BindHandCard(Button card, long token)
    {
        void Zoom()
        {
            _focusedHandToken = token;
            var index = _handTokens.IndexOf(token);
            if (index < 0 || Session.State.Battle == null || _selectedCard >= 0 || _cardZoom == null || !GodotObject.IsInstanceValid(_cardZoom) || GetViewport().GuiIsDragging()) return;
            var definition = ChapterContent.Cards[Session.State.Battle.Hand[index]];
            var battle=Session.State.Battle;
            var lesson=battle.Id=="trial" ? (Session.State.World.Contains("trial-complete")?"✓ 试用完成":battle.TrialToolUsed?"✓ 已预备工具 → 现在攻击":"1. 预备工具 → 2. 攻击")+$" · 实际额外抽牌 {battle.ExtraDraw}/2\n" : "";
            _cardZoomText!.Text = lesson+definition.Name + UiCopy.Format("UI_E91FB92AC35F", definition.Action, definition.Mana) + definition.Text + "\n" + (_reorderHand ? UiCopy.Text("UI_F48943B12991") : UiCopy.Text("UI_B1AC07F10221"));
            _cardZoom.Show();
        }
        card.FocusEntered += Zoom;
        card.MouseEntered += () => { if (_inputModality == "mouse") Zoom(); };
        card.SetDragForwarding(Callable.From<Vector2, Variant>(_ =>
        {
            var index = _handTokens.IndexOf(token);
            if (!_dragCards || index < 0 || _panel != "" || FeedbackBusy || Session.State.Battle == null) return default;
            if (!_reorderHand && !Enumerable.Range(0, Session.State.Battle.Enemies.Count).Any(target => Session.CanPlay(index, target))) return default;
            _cardZoom?.Hide(); _inspection?.Hide();
            var preview = new PanelContainer(); preview.AddThemeStyleboxOverride("panel", Style(Surface, Ink));
            preview.AddChild(Text((_reorderHand ? UiCopy.Text("UI_9527E281F3B5") : UiCopy.Text("UI_1751CA3D2A96")) + ChapterContent.Cards[Session.State.Battle.Hand[index]].Name + UiCopy.Text("UI_A8E59236DBC7"), 16, Ink)); card.SetDragPreview(preview);
            return new Godot.Collections.Dictionary { ["type"] = "card", ["token"] = token, ["version"] = (long)_battleGestureVersion };
        }), Callable.From<Vector2, Variant, bool>((_, data) => _reorderHand && CardDropData(data, out var index) && _handTokens[index] != token), Callable.From<Vector2, Variant>((_, data) =>
        {
            if (!_reorderHand || !CardDropData(data, out var index)) return;
            var moved = _handTokens[index];
            Callable.From(() => { if (!_handDisplayOrder.Contains(token) || !_handDisplayOrder.Contains(moved)) return; _handDisplayOrder.Remove(moved); _handDisplayOrder.Insert(_handDisplayOrder.IndexOf(token), moved); _requestedFocus = "card_" + _handTokens.IndexOf(moved); Render(); }).CallDeferred();
        }));
    }
    private void MoveHandDisplay(int direction)
    {
        var from = _handDisplayOrder.IndexOf(_focusedHandToken); if (from < 0) return;
        var to = Math.Clamp(from + direction, 0, _handDisplayOrder.Count - 1); if (from == to) return;
        _handDisplayOrder.RemoveAt(from); _handDisplayOrder.Insert(to, _focusedHandToken); Render();
    }
    private string EnemyIntentWithTarget(ChapterBattle battle, int index)
    {
        var enemy = battle.Enemies[index];
        if (enemy.Health > 0 && enemy.Id == "E03" && enemy.Cycle % 3 == 0)
        {
            var ally = battle.Enemies.Select((e, i) => (e, i)).Where(p => p.i != index && p.e.Health > 0).OrderBy(p => p.e.Health).FirstOrDefault();
            var target = ally.e == null ? index : ally.i;
            var gain=Math.Min(4,20-battle.Enemies[target].Shield);
            return $"护盾 +{gain} → {target + 1}号（上限 20；按执行时最低生命重选）";
        }
        if(enemy.Health>0 && enemy.Id=="E03" && enemy.Cycle%3==2 && !enemy.Signaled && battle.Enemies.Count>=3) return "全体存活者（含自身）护盾 +3，上限 20";
        return ChapterSession.EnemyIntent(enemy, battle.Enemies.Count);
    }
    private void ShowBattleReceipts()
    {
        _detailItemId = 0; _detailTitle = UiCopy.Text("UI_4996B25A60CF");
        _detailText = string.Join("\n", Session.State.Events.Where(e => e.Type == "battle").TakeLast(80).Select(e => e.Text));
        if (_detailText == "") _detailText = UiCopy.Text("UI_834F5A724D28");
        OpenNested("detail");
    }
}
