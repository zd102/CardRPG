using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{

    private void Battle()
    {
        var b = Session.State.Battle!; var trial = b.Id == "trial";
        SynchronizeHand(); _cardZoom = null; _cardZoomText = null;
        var chosen = _selectedCard >= 0 && _selectedCard < b.Hand.Count ? ChapterContent.Cards[b.Hand[_selectedCard]] : null;
        var playable = Enumerable.Range(0, b.Hand.Count).Any(i => b.Enemies.Select((_, t) => t).Any(t => Session.CanPlay(i, t)));
        var handHeight = ReadingScale > 1 ? 280 : 226;
        var railBottom = -handHeight - 26;
        var railTop = railBottom - (chosen == null ? 62 : 100);
        var workWidth = Math.Min(Size.X - 40, 1360);
        var workLeft = (Size.X - workWidth) / 2;
        var resourceWidth = Size.X < 1100 ? 134 : 154;
        var turnWidth = ReadingScale > 1 ? 232 : Size.X < 1100 ? 180 : 204;

        // Intent, body, health and the selected card's consequence share one target area.
        var cardResult = chosen == null ? null : Session.Preview(s => s.PlayCard(_selectedCard, _target));
        for (var i = 0; i < b.Enemies.Count; i++)
        {
            var index = i; var enemy = b.Enemies[i];
            var selected = enemy.Health > 0 && (chosen == null ? _target == i : chosen.Damage > 0 && (chosen.Id == "C18" || _target == i));
            var width = b.Enemies.Count == 1 ? .30f : b.Enemies.Count == 2 ? .29f : .27f;
            var x = .5f + (i - (b.Enemies.Count - 1) / 2f) * (width + .035f);
            var target = Button("target_" + i, "", () => { _target = index; Render(); }, enemy.Health > 0);
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) target.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            target.TooltipText = $"{i + 1}号 · {ChapterSession.EnemyName(enemy.Id)}\n{EnemyIntentWithTarget(b, i)}";
            _root.AddChild(target); Dock(target, 0, 106, 0, railTop - 14, x - width / 2, 0, x + width / 2, 1);
            var intentHeight = ReadingScale > 1 ? 72 : 52;
            var statsHeight = enemy.Id == "E13" ? 104 : enemy.Shield > 0 ? 88 : 72;
            var actor = new ChapterActor { Name = "Actor_" + i, Art = "enemy-" + enemy.Id, Selected = selected, Defeated = enemy.Health == 0 };
            target.AddChild(actor); Dock(actor, 22, intentHeight, -22, -statsHeight, ar: 1, ab: 1);
            var intentPlate = DarkPanel(target, 10); intentPlate.MouseFilter = MouseFilterEnum.Ignore;
            Dock(intentPlate, 8, 0, -8, intentHeight, ar: 1);
            var intentCopy = enemy.Id == "E03" && enemy.Cycle % 3 == 2 && !enemy.Signaled && b.Enemies.Count >= 3 ? "全体护盾 +3" : EnemyIntentWithTarget(b, i).Split('（')[0];
            var intent = Text(enemy.Health == 0 ? "已击败" : intentCopy, 16, enemy.Health == 0 ? OnNight : Brass);
            intent.HorizontalAlignment = HorizontalAlignment.Center; intentPlate.AddChild(intent);
            var stats = FlatPanel(target, 10); stats.MouseFilter = MouseFilterEnum.Ignore;
            Dock(stats, 8, -statsHeight, -8, 0, ar: 1, at: 1, ab: 1);
            var caption = Column(3); stats.AddChild(caption);
            var names = Row(10); caption.AddChild(names);
            var name = Text($"{i + 1} · " + ChapterSession.EnemyName(enemy.Id), 16, Ink); name.SizeFlagsHorizontal = SizeFlags.ExpandFill; names.AddChild(name);
            var after = cardResult?.LastResolvedBattle?.Enemies.ElementAtOrDefault(i);
            var affected = chosen != null && chosen.Damage > 0 && (chosen.Id == "C18" || _target == i);
            names.AddChild(Inline(affected ? $"{enemy.Health} → {after?.Health ?? 0}" : $"{enemy.Health}/{enemy.MaxHealth}", 16, affected ? Danger : Ink));
            var inspectEnemy = QuietButton("enemy_info_" + i, "?", () =>
            {
                _detailItemId = 0; _detailTitle = ChapterSession.EnemyName(enemy.Id);
                _detailText = $"生命 {enemy.Health}/{enemy.MaxHealth} · 护盾 {enemy.Shield}\n\n本轮意图：{EnemyIntentWithTarget(b, index)}";
                OpenNested("detail");
            });
            inspectEnemy.CustomMinimumSize = new Vector2(26, 26); inspectEnemy.TooltipText = "查看完整敌人意图"; names.AddChild(inspectEnemy);
            HealthBar(caption, enemy.Health, enemy.MaxHealth, enemy.Health == 0 ? Muted : Danger);
            if (enemy.Shield > 0) caption.AddChild(Text($"护盾 {enemy.Shield}" + (affected ? $" → {after?.Shield ?? 0}" : ""), 14, Success));
            if (enemy.Id == "E13") caption.AddChild(Text(b.BonusSecured ? "额外奖励已保住" : b.Round <= 3 ? $"{4 - b.Round} 回合内击败可保住额外奖励" : "额外奖励已撤走", 14, Muted));
            if (_battleDeltas.TryGetValue(i, out var delta) && Time.GetTicksMsec() < _battleDeltaUntil)
            { var receipt = Text(delta, 16, OnNight); target.AddChild(receipt); Dock(receipt, 16, 54, -16, 86, ar: 1); }
            BindCardTarget(target, index);
        }

        // Costs sit beside the hand, and the self-target area is explicitly named.
        var command = DarkPanel(_hud!, 14); command.Name = "BattleResources";
        Dock(command, workLeft, -handHeight - 12, workLeft + resourceWidth, -18, at: 1, ab: 1);
        var resources = Column(10); command.AddChild(resources);
        resources.AddChild(Inline($"第 {b.Round} 回合", 16, OnNight));
        var afterCard = cardResult?.State.Battle;
        SymbolValue(resources, "action", afterCard == null ? $"{b.Action} / 3 行动点" : $"{b.Action} → {afterCard.Action} 行动点", Brass, "战斗行动点每回合恢复至 3。", 16);
        SymbolValue(resources, "mana", afterCard == null ? $"{b.Mana} / 3 魔法" : $"{b.Mana} → {afterCard.Mana} 魔法", OnNight, "魔法每回合恢复 1，上限 3。", 16);
        SymbolValue(resources, "guard", afterCard == null ? $"{b.Shield} 护盾" : $"{b.Shield} → {afterCard.Shield} 护盾", OnNight, "护盾在下回合清除；稳固最多保留 4。", 16);
        if (chosen?.Damage == 0) resources.AddChild(Text("防护牌作用于自身", 14, Brass));
        else resources.AddChild(Text("防护牌可拖到此处", 14, OnNight));
        BindCardTarget(command, _target, true);

        var turn = FlatPanel(_hud!, 12); turn.Name = "EndTurnForecast";
        Dock(turn, workLeft + workWidth - turnWidth, -handHeight - 12, workLeft + workWidth, -18, at: 1, ab: 1);
        var turnBody = Column(5); turn.AddChild(turnBody);
        var afterTurn = trial ? null : Session.Preview(s => s.EndTurn());
        turnBody.AddChild(Text(trial ? "免费试用" : "现在直接结束", 14, Muted));
        var currentForecast = Text(trial ? "不消耗真实生命" : $"生命 {Session.State.Health} → {afterTurn?.State.Health ?? Session.State.Health}", 16, afterTurn?.State.Phase == ChapterPhase.Defeated ? Danger : Ink);
        currentForecast.Name = "CurrentEndHealth"; turnBody.AddChild(currentForecast);
        if (cardResult != null && !trial)
        {
            // Run both real commands on the same isolated state, including kills and passives.
            var thenEnd = cardResult.State.Phase == ChapterPhase.Battle ? cardResult.Preview(s => s.EndTurn()) : cardResult;
            var projected = Text(cardResult.State.Phase != ChapterPhase.Battle ? "打出此牌后 · 战斗结束" : $"打出此牌再结束\n生命 {Session.State.Health} → {thenEnd?.State.Health ?? cardResult.State.Health}", 16, thenEnd?.State.Phase == ChapterPhase.Defeated ? Danger : Success);
            projected.Name = "SelectedEndHealth"; turnBody.AddChild(projected);
        }
        var end = Button("end_turn", chosen != null ? "按当前状态结束" : playable ? "结束回合" : "无可用牌 · 结束", RequestEndTurn, primary: true);
        end.TooltipText = TurnForecast() + "\n快捷键 " + KeyLabel("endturn"); turnBody.AddChild(end);
        turnBody.AddChild(QuietButton(trial ? "end_trial" : "retreat", trial ? "结束试用" : "战术撤退…", Retreat));

        var handViewport = new ScrollContainer { Name = "HandViewport", VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, FollowFocus = true };
        _root.AddChild(handViewport);
        handViewport.SetDeferred(ScrollContainer.PropertyName.ScrollHorizontal, _handScroll);
        Dock(handViewport, workLeft + resourceWidth + 10, -handHeight - 12, workLeft + workWidth - turnWidth - 10, -18, at: 1, ab: 1);
        var hand = new HBoxContainer { Name = "FloatingHand", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hand.AddThemeConstantOverride("separation", CompactDensity ? 6 : 9); handViewport.AddChild(hand);
        var cardWidth = Mathf.Clamp((workWidth - resourceWidth - turnWidth - 20 - Math.Max(0, b.Hand.Count - 1) * (CompactDensity ? 6 : 9)) / Math.Max(1, b.Hand.Count), 118 * ReadingScale, 156 * ReadingScale);
        foreach (var token in _handDisplayOrder)
        {
            var index = _handTokens.IndexOf(token); var data = ChapterContent.Cards[b.Hand[index]]; var can = Session.CanPlay(index, _target);
            var card = Button("card_" + index, "", () => { if (_reorderHand) _focusedHandToken = token; else SelectCard(index); });
            card.CustomMinimumSize = new Vector2(cardWidth, handHeight - 16); card.ClipContents = true;
            var sheet = Style(can ? Surface : new Color("d8d7cc"), _selectedCard == index ? Accent : new Color("657262"), 6);
            sheet.BorderWidthTop = sheet.BorderWidthBottom = sheet.BorderWidthLeft = sheet.BorderWidthRight = _selectedCard == index ? 3 : 1;
            card.AddThemeStyleboxOverride("normal", sheet); card.AddThemeStyleboxOverride("hover", Style(new Color("fff9eb"), Accent, 6)); hand.AddChild(card);
            var art = ActionArt(data); card.AddChild(art); Dock(art, 4, 4, -4, 99, ar: 1); if (!can) art.Modulate = new Color(.65f, .65f, .65f);
            var cost = FlatPanel(card, 4); cost.MouseFilter = MouseFilterEnum.Ignore; Dock(cost, 8, 8, -8, 36, ar: 1);
            cost.AddChild(Inline(data.Mana > 0 ? (data.Action > 0 ? $"{data.Action} 行动 · {data.Mana} 魔法" : $"{data.Mana} 魔法") : $"{data.Action} 行动点", 14, Ink));
            var name = Text(data.Name, 16, Ink); name.HorizontalAlignment = HorizontalAlignment.Center; card.AddChild(name); Dock(name, 6, 104, -6, 132, ar: 1);
            var effect = Text(CardCoreEffect(data), 16, Ink); effect.HorizontalAlignment = HorizontalAlignment.Center;
            card.AddChild(effect); Dock(effect, 8, 137, -8, handHeight - 40, ar: 1);
            var note = Inline(can ? CardRole(data) + (CardKeyword(data.Id) == "" ? "" : " · " + CardKeyword(data.Id)) : b.Action < data.Action ? $"缺 {data.Action - b.Action} 行动" : b.Mana < data.Mana ? $"缺 {data.Mana - b.Mana} 魔法" : "无合法目标", 14, can ? Muted : Danger);
            note.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; note.HorizontalAlignment = HorizontalAlignment.Center; card.AddChild(note);
            Dock(note, 6, -30, -6, -6, ar: 1, at: 1, ab: 1);
            card.TooltipText = data.Name + $"\n{data.Action} 行动 / {data.Mana} 魔法\n" + CardRules(data.Id)
                + (can ? "" : b.Action < data.Action ? "\n行动不足" : b.Mana < data.Mana ? "\n魔法不足" : "\n没有合法目标");
            BindHandCard(card, token); BindObjectMenu(card, () => CardMenu(data.Id));
        }

        // One compact action rail, with a full readable preview available on demand.
        var preview = FlatPanel(_root, 12); preview.Name = "CardPreview";
        Dock(preview, workLeft, railTop, workLeft + workWidth, railBottom, at: 1, ab: 1);
        var layout = Row(12); preview.AddChild(layout);
        var information = FlexibleColumn(layout);
        if (_feedback.Count > 0 && Time.GetTicksMsec() - _feedbackStarted <= (ulong)(_feedback.Count * _feedbackStepMs + 1600))
            information.AddChild(new VBoxContainer { Name = "BattleFeedbackHost", CustomMinimumSize = new Vector2(0, 36) });
        var copy = Scroll(information);
        if (chosen != null)
        {
            copy.AddChild(Text(chosen.Name + " · " + (chosen.Damage == 0 ? "作用于自身" : chosen.Id == "C18" ? "全体敌人" : $"目标 { _target + 1} · {ChapterSession.EnemyName(b.Enemies[_target].Id)}"), 20, Ink));
            copy.AddChild(Text(chosen.Text + $"  消耗 {chosen.Action} 行动 / {chosen.Mana} 魔法", 16, Ink));
            var actions = Row(8); actions.SizeFlagsVertical = SizeFlags.ShrinkCenter; layout.AddChild(actions);
            actions.AddChild(QuietButton("card_more", "完整预览", () => { _detailItemId = 0; _detailTitle = chosen.Name; _detailText = CardPreview(); OpenNested("detail"); }));
            actions.AddChild(Button("cancel_card", "取消", () => { _selectedCard = -1; Render(); }));
            actions.AddChild(Button("play_selected", "打出此牌 →", CommitSelectedCard, Session.CanPlay(_selectedCard, _target), true));
        }
        else
        {
            _cardZoom = preview;
            _cardZoomText = Text(trial ? (Session.State.World.Contains("trial-complete") ? "✓ 试用完成，可以返回制作工具。" : "先预备工具，再打出攻击牌。") : _battleGuide ? "选择手牌 → 选择目标 → 打出；也可直接拖拽。" : "选择手牌查看效果", 16, Ink);
            copy.AddChild(_cardZoomText);
            var piles = Row(6); piles.Name = "CardPiles"; piles.SizeFlagsVertical = SizeFlags.ShrinkCenter; layout.AddChild(piles);
            piles.AddChild(QuietButton("inspect_draw", "抽 " + b.Draw.Count, () => ShowPile(b.Draw, "抽牌堆 · 按种类汇总，不表示顺序")));
            piles.AddChild(QuietButton("inspect_discard", "弃 " + b.Discard.Count, () => ShowPile(b.Discard, "弃牌堆")));
            piles.AddChild(QuietButton("hand_order", _reorderHand ? "完成整理" : "理牌", () => { _reorderHand = !_reorderHand; Render(); }));
            if (_reorderHand)
            {
                piles.AddChild(QuietButton("hand_left", "←", () => MoveHandDisplay(-1)));
                piles.AddChild(QuietButton("hand_right", "→", () => MoveHandDisplay(1)));
            }
            else piles.AddChild(QuietButton("battle_log", "记录", ShowBattleReceipts));
        }
    }

    private void ShowPile(System.Collections.Generic.IEnumerable<string> cards, string title)
    {
        _detailItemId = 0;
        _detailTitle = title;
        _detailText = string.Join("\n\n", cards.GroupBy(id => id).OrderBy(g => g.Key).Select(g => { var card = ChapterContent.Cards[g.Key]; return $"{card.Name} ×{g.Count()} · {card.Action} 行动 / {card.Mana} 魔法\n{card.Text}"; }));
        if (_detailText == "") _detailText = UiCopy.Text("UI_31FE3E656A9F");
        OpenNested("detail");
    }

    private static (string Symbol,string Value) IntentSymbol(ChapterEnemy enemy,int count)
    {
        if (enemy.Health==0) return ("check","—");
        var damage=ChapterSession.EnemyDamage(enemy);
        if (damage>0) return ("hammer",damage.ToString());
        return enemy.Id switch
        {
            "E01" => ("round","蓄力"), "E02" => ("guard","=6"), "E13" => ("guard","+8"),
            "E03" when enemy.Cycle%3==0 => ("guard","援 +4"),
            "E03" when enemy.Signaled => ("round","休整"),
            "E03" when count>=3 => ("all","盾 +3"),
            "E03" => ("all","召 +1"), _ => ("target","0")
        };
    }
}
