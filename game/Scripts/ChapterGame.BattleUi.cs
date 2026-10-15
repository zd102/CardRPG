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
        var selectedDefinition = _selectedCard >= 0 && _selectedCard < b.Hand.Count ? ChapterContent.Cards[b.Hand[_selectedCard]] : null;
        var hasPlayable = Enumerable.Range(0, b.Hand.Count).Any(i => b.Enemies.Select((_, t) => t).Any(t => Session.CanPlay(i, t)));
        var command = FlatPanel(_hud!, 10); command.Name = "BattleResources";
        Dock(command,24,104,510,156);
        if (selectedDefinition?.Damage == 0) command.AddThemeStyleboxOverride("panel", Style(Surface, Accent, 10));
        var resources = Row(12); command.AddChild(resources);
        var energy = Row(14); resources.AddChild(energy);
        energy.AddChild(Inline(UiCopy.Text("UI_253FB75906C6"), 14, Ink));
        Pips(energy,"action",b.Action,3,Accent,"行动；每回合恢复至 3。", selectedDefinition?.Action ?? 0);
        energy.AddChild(Inline(UiCopy.Text("UI_F92FD0C0B8C9"), 14, Ink));
        Pips(energy,"mana",b.Mana,3,new Color("466c65"),"魔法；每回合恢复 1，上限 3。", selectedDefinition?.Mana ?? 0);
        var defense = Row(12); resources.AddChild(defense);
        SymbolValue(defense,"guard",b.Shield.ToString(),Success,"护盾；下回合清除，稳固最多保留 4。");
        SymbolValue(defense,"round",b.Round.ToString(),Ink,"当前回合");
        BindCardTarget(command, _target, true);

        var end = Button("end_turn",UiCopy.Text("UI_975778D99095"),RequestEndTurn,primary:!trial || !Session.State.World.Contains("trial-complete"));
        end.TooltipText = UiCopy.Text("UI_098F1C50510A") + KeyLabel("endturn") + UiCopy.Text("UI_519FA49E9ABF");
        _root.AddChild(end); Dock(end,-128,-174,-20,-88,al:1,at:1,ar:1,ab:1);
        var retreat = Button(trial ? "end_trial" : "retreat", trial ? UiCopy.Text("UI_3E50C62FE50B") : UiCopy.Text("UI_C208091F008A"), Retreat, primary:trial && Session.State.World.Contains("trial-complete"));
        retreat.TooltipText = trial ? UiCopy.Text("UI_B4338FA22BDD") : TurnForecast(true);
        _root.AddChild(retreat); Dock(retreat,-128,-74,-20,-28,al:1,at:1,ar:1,ab:1);
        var risk = Panel(_hud!, Surface, Outline); risk.Name = "EndTurnForecast"; Dock(risk,-156,-362,-20,-202,al:1,at:1,ar:1,ab:1);
        var afterTurn = trial ? null : Session.Preview(s => s.EndTurn());
        var riskBody = Column(3); risk.AddChild(riskBody); var riskScroll = Scroll(riskBody);
        riskScroll.AddChild(Text((trial ? UiCopy.Text("UI_35168A5D192A") : UiCopy.Format("UI_E897973AD6D1", Math.Min(b.Shield, b.Enemies.Sum(ChapterSession.EnemyDamage)), Session.State.Health, afterTurn?.State.Health ?? Session.State.Health) + (afterTurn?.State.Phase == ChapterPhase.Defeated ? UiCopy.Text("UI_A7D3A1EF0FDF") : "")) + (!hasPlayable ? UiCopy.Text("UI_416B27EC1792") : ""),16,afterTurn?.State.Phase == ChapterPhase.Defeated ? Danger : Ink));
        if (!hasPlayable) { end.Text = UiCopy.Text("UI_AC1901DAF75F"); end.AddThemeStyleboxOverride("normal", Style(Surface, Accent, 8)); }
        for (var i = 0; i < b.Enemies.Count; i++)
        {
            var index = i; var e = b.Enemies[i]; var selected = e.Health > 0 && (selectedDefinition == null ? _target == i : selectedDefinition.Damage > 0 && (selectedDefinition.Id == "C18" || _target == i));
            var width = b.Enemies.Count == 1 ? .26f : b.Enemies.Count == 2 ? .25f : .235f;
            var x = .5f + (i-(b.Enemies.Count-1)/2f)*(width+.035f);
            var target = Button("target_"+i,"",() => { _target=index; Render(); },e.Health>0);
            foreach (var state in new[] {"normal","hover","pressed","disabled"}) target.AddThemeStyleboxOverride(state,new StyleBoxEmpty());
            target.TooltipText = UiCopy.Format("UI_3EC29E9F696C", i + 1) + ChapterSession.EnemyName(e.Id) + $"  {e.Health}/{e.MaxHealth}\n" + EnemyIntentWithTarget(b,i);
            _root.AddChild(target); Dock(target,0,164,0,-374,x-width/2,0,x+width/2,1);
            var backdrop = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore }; backdrop.AddThemeStyleboxOverride("panel", Style(new Color("dfd9c090"), selected ? Accent : Outline, 0)); target.AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var actor = new ChapterActor { Name="Actor_"+i, Art="enemy-"+e.Id, Selected=selected, Defeated=e.Health==0 };
            target.AddChild(actor); Dock(actor,10,e.Id=="E13"?156:124,-10,0,ar:1,ab:1);
            var plate = FlatPanel(target,8); plate.MouseFilter=MouseFilterEnum.Ignore;
            Dock(plate,6,0,-6,e.Id=="E13"?156:124,ar:1);
            var caption = Column(4); caption.MouseFilter=MouseFilterEnum.Ignore; plate.AddChild(caption);
            var name = Inline((selected ? "◆ " : "")+UiCopy.Format("UI_3EC29E9F696C", i + 1)+ChapterSession.EnemyName(e.Id),16,selected ? Accent : Ink);
            name.HorizontalAlignment=HorizontalAlignment.Center; caption.AddChild(name);
            HealthBar(caption,e.Health,e.MaxHealth,e.Health==0 ? Muted : Danger);
            var intent = Row(12); intent.Alignment=BoxContainer.AlignmentMode.Center; intent.MouseFilter=MouseFilterEnum.Ignore; caption.AddChild(intent);
            SymbolValue(intent,"heart",e.Health.ToString(),Ink,"",14);
            if (e.Shield>0) SymbolValue(intent,"guard",e.Shield.ToString(),Success,"",14);
            var (symbol,value)=IntentSymbol(e,b.Enemies.Count);
            SymbolValue(intent,symbol,value,e.Health==0 ? Muted : Danger,"",16);
            if (e.Health > 0) caption.AddChild(Text(EnemyIntentWithTarget(b,i),14,Ink));
            if (e.Id == "E13") caption.AddChild(Text(b.BonusSecured ? UiCopy.Text("UI_48A1A3755772") : b.Round <= 3 ? UiCopy.Format("UI_9E23634F73AB", 4 - b.Round) : UiCopy.Text("UI_E074137B7A1E"), 14, Accent));
            if(e.Id=="E13")caption.AddChild(Text(UiCopy.Text("UI_2B19DEE92A1F"),14,Muted));
            if (_battleDeltas.TryGetValue(i, out var delta) && Time.GetTicksMsec() < _battleDeltaUntil)
            { var receipt = Text(delta,14,Accent); target.AddChild(receipt); Dock(receipt,10,-28,-10,-2,0,1,1,1); }
            BindCardTarget(target, index);
        }
        var piles = Row(24); piles.Alignment=BoxContainer.AlignmentMode.Center;
        var pilesPlate = FlatPanel(_hud!, 5); pilesPlate.Name = "CardPiles";
        Dock(pilesPlate,522,104,-20,156,ar:1); pilesPlate.AddChild(piles);
        piles.AddChild(Button("inspect_draw", UiCopy.Text("UI_E3EDC37E7122") + b.Draw.Count, () => ShowPile(b.Draw, "抽牌堆 · 按种类汇总，不表示抽牌顺序")));
        piles.AddChild(Button("inspect_discard", UiCopy.Text("UI_83B5355F9420") + b.Discard.Count, () => ShowPile(b.Discard, "弃牌堆 · 按种类汇总")));
        SymbolValue(piles,"draw",$"+{b.ExtraDraw}/2",Muted,"本回合已经额外抽取的牌数；上限 2。",14);
        var hand = new HBoxContainer { Name="FloatingHand", Alignment=BoxContainer.AlignmentMode.Center, MouseFilter=MouseFilterEnum.Ignore };
        hand.AddThemeConstantOverride("separation",10); _root.AddChild(hand); Dock(hand,24,-194,-146,-18,at:1,ar:1,ab:1);
        var cardWidth=Mathf.Min(146,(Size.X-170-Math.Max(0,b.Hand.Count-1)*10)/Math.Max(1,b.Hand.Count));
        foreach (var token in _handDisplayOrder)
        {
            var i = _handTokens.IndexOf(token);
            var index=i; var cardData=ChapterContent.Cards[b.Hand[i]]; var playable=Session.CanPlay(i,_target);
            // Unavailable cards remain focusable so keyboard users can inspect the cost and reason.
            var card=Button("card_"+i,"",() => { if(_reorderHand)_focusedHandToken=token;else SelectCard(index); });
            card.CustomMinimumSize=new Vector2(cardWidth,176); card.ClipContents=true;
            var sheet=Style(playable ? Surface : new Color("d5d2c1"),Ink,12);
            card.AddThemeStyleboxOverride("normal",sheet);
            if (_selectedCard == index) card.AddThemeStyleboxOverride("normal", Style(new Color("eee6d3"), Ink, 10));
            card.AddThemeStyleboxOverride("hover",ChapterSkin.Box("ledger-paper",6));
            card.AddThemeColorOverride("font_color",ChapterSkin.PaperInk); hand.AddChild(card);
            var ink=ChapterSkin.PaperInk;
            var cost=Row(10); cost.MouseFilter=MouseFilterEnum.Ignore; card.AddChild(cost); Dock(cost,10,7,-10,31,ar:1);
            if (cardData.Action>0) SymbolValue(cost,"action",cardData.Action.ToString(),ink,"",14);
            if (cardData.Mana>0) SymbolValue(cost,"mana",cardData.Mana.ToString(),ink,"",14);
            var icon=new ChapterIllustration { Art=CardArt(cardData),Mounted=false }; card.AddChild(icon); Dock(icon,10,30,-10,91,ar:1);
            var badge = Glyph(CardSymbol(cardData), Ink, 20); card.AddChild(badge); Dock(badge,10,37,32,59);
            if (!playable)
            {
                var locked=Glyph("lock",ink,18); card.AddChild(locked); Dock(locked,-28,10,-10,28,al:1,ar:1);
            }
            var title=Inline(cardData.Name,16,ink); title.HorizontalAlignment=HorizontalAlignment.Center; card.AddChild(title); Dock(title,5,92,-5,117,ar:1);
            var effects=Column(1); effects.MouseFilter=MouseFilterEnum.Ignore; card.AddChild(effects); Dock(effects,6,121,-6,150,ar:1);
            CardEffects(effects,cardData,ink,true);
            var reason = playable ? CardKeyword(cardData.Id) : b.Action < cardData.Action ? $"缺 {cardData.Action - b.Action} 行动" : b.Mana < cardData.Mana ? $"缺 {cardData.Mana - b.Mana} 魔法" : "无合法目标";
            var keyword=Inline(reason,14,ChapterSkin.PaperMuted); keyword.HorizontalAlignment=HorizontalAlignment.Center;
            card.AddChild(keyword); Dock(keyword,5,150,-5,171,ar:1);
            card.TooltipText=cardData.Name+UiCopy.Format("UI_E91FB92AC35F", cardData.Action, cardData.Mana)+CardRules(cardData.Id)+
                (playable ? "" : "\n"+(b.Action<cardData.Action ? UiCopy.Text("UI_77406A91D482") : b.Mana<cardData.Mana ? UiCopy.Text("UI_B72B7D458CE9") : UiCopy.Text("UI_224357C5FF56")));
            BindHandCard(card, token);
            BindObjectMenu(card,()=>CardMenu(cardData.Id));
        }
        var preview = Panel(_root, Surface, Ink); preview.Name = "CardPreview";
        Dock(preview,24,-362,-172,-202,0,1,1,1);
        var layout = Row(12); preview.AddChild(layout);
        var information = FlexibleColumn(layout);
        if (_feedback.Count > 0 && Time.GetTicksMsec() - _feedbackStarted <= (ulong)(_feedback.Count * _feedbackStepMs + 1600))
            information.AddChild(new VBoxContainer { Name = "BattleFeedbackHost", CustomMinimumSize = new Vector2(0, 50) });
        var copy = Scroll(information);
        var actions = Column(4); layout.AddChild(actions);
        if (_selectedCard >= 0 && _selectedCard < b.Hand.Count)
        {
            copy.AddChild(Text(CardPreview(),16,Ink));
            actions.AddChild(Button("play_selected", UiCopy.Text("UI_07F0D60BC535"), CommitSelectedCard, Session.CanPlay(_selectedCard,_target),true));
            actions.AddChild(Button("cancel_card", UiCopy.Text("UI_1DF5225A15AB"), () => { _selectedCard = -1; Render(); }));
            actions.AddChild(Button("card_more",UiCopy.Text("UI_D4FE35CC9BCA"),()=>CardMenu(b.Hand[_selectedCard])));
        }
        else
        {
            _cardZoom = preview;
            _cardZoomText = Text(trial ? (Session.State.World.Contains("trial-complete") ? UiCopy.Text("UI_7B013B80122C") : (b.TrialToolUsed ? UiCopy.Text("UI_6605955D99F9") : UiCopy.Text("UI_CE4A7F933A75"))) + UiCopy.Format("UI_578CD15FAD9A", b.ExtraDraw) : _battleGuide ? UiCopy.Text("UI_5833AACD774A") : UiCopy.Text("UI_20E3D759C68C"),16,Ink); copy.AddChild(_cardZoomText);
            actions.AddChild(Button("hand_order", _reorderHand ? UiCopy.Text("UI_BCD102C65B72") : UiCopy.Text("UI_5AA443DCD870"), () => { _reorderHand = !_reorderHand; Render(); }));
            if (_reorderHand)
            { var moves = Row(4); actions.AddChild(moves); moves.AddChild(Button("hand_left", UiCopy.Text("UI_DDBF068D8F93"), () => MoveHandDisplay(-1))); moves.AddChild(Button("hand_right", UiCopy.Text("UI_648D3D189D69"), () => MoveHandDisplay(1))); }
            else actions.AddChild(Button("battle_log", UiCopy.Text("UI_3B2849CFE47C"), () => ShowBattleReceipts()));
        }
        foreach(var action in actions.GetChildren().OfType<Button>()) action.CustomMinimumSize=new Vector2(0,40);
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
