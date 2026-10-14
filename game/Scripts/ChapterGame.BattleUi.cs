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
        var command = FlatPanel(_hud!, 10); command.Name = "BattleResources";
        Dock(command,24,-254,420,-202,at:1,ab:1);
        var resources = Row(12); command.AddChild(resources);
        var energy = Row(14); resources.AddChild(energy);
        Pips(energy,"action",b.Action,3,Accent,"行动；每回合恢复至 3。");
        Pips(energy,"mana",b.Mana,3,new Color("466c65"),"魔法；每回合恢复 1，上限 3。");
        var defense = Row(12); resources.AddChild(defense);
        SymbolValue(defense,"guard",b.Shield.ToString(),Success,"护盾；下回合清除，稳固最多保留 4。");
        SymbolValue(defense,"round",b.Round.ToString(),Ink,"当前回合");

        var end = Button("end_turn","结束回合",RequestEndTurn,primary:!trial || !Session.State.World.Contains("trial-complete"));
        end.TooltipText = "结束回合 · Space\n弃掉剩余手牌，敌人执行当前公开意图，然后恢复行动并抽牌。";
        _root.AddChild(end); Dock(end,-128,-174,-20,-88,al:1,at:1,ar:1,ab:1);
        var retreat = Button(trial ? "end_trial" : "retreat", trial ? "结束试用" : "撤退…", Retreat, primary:trial && Session.State.World.Contains("trial-complete"));
        retreat.TooltipText = trial ? "结束试用并查看配方；免费归还借装。" : TurnForecast(true);
        _root.AddChild(retreat); Dock(retreat,-128,-74,-20,-28,al:1,at:1,ar:1,ab:1);
        var risk = Panel(_hud!, Surface, Outline); Dock(risk,-156,-260,-20,-202,al:1,at:1,ar:1,ab:1);
        risk.AddChild(Inline(trial ? "试用无伤害" : $"预计损血 {Math.Min(Session.State.Health, Math.Max(0, b.Enemies.Sum(ChapterSession.EnemyDamage)-b.Shield))}",16,Ink));
        if (trial)
        {
            var guidePlate = Panel(_hud!, Surface, Outline); Dock(guidePlate,24,106,310,156);
            guidePlate.AddChild(Inline(Session.State.World.Contains("trial-complete") ? "✓ 试用完成，可查看配方" : "预备工具 → 攻击",16,Ink));
        }
        if (b.Id == "E13")
        {
            var bonus = ChapterSkin.Panel(_hud!,padding:10); Dock(bonus,-236,104,-20,152,al:1,ar:1);
            SymbolValue(bonus,"spring",b.BonusSecured ? "已保住" : b.Round <= 3 ? $"剩 {4-b.Round} 回合" : "已运走",Accent,
                "在前 3 次敌方阶段前击败军需领班可保住额外弹簧。破甲锤首奖始终保留。",16);
        }
        for (var i = 0; i < b.Enemies.Count; i++)
        {
            var index = i; var e = b.Enemies[i]; var selected = _target == i;
            var width = b.Enemies.Count == 1 ? .26f : b.Enemies.Count == 2 ? .25f : .235f;
            var x = .5f + (i-(b.Enemies.Count-1)/2f)*(width+.035f);
            var target = Button("target_"+i,"",() => { _target=index; Render(); },e.Health>0);
            foreach (var state in new[] {"normal","hover","pressed","disabled"}) target.AddThemeStyleboxOverride(state,new StyleBoxEmpty());
            target.TooltipText = ChapterSession.EnemyName(e.Id) + $"  {e.Health}/{e.MaxHealth}\n" + ChapterSession.EnemyIntent(e,b.Enemies.Count);
            _root.AddChild(target); Dock(target,0,156,0,-274,x-width/2,0,x+width/2,1);
            var actor = new ChapterActor { Name="Actor_"+i, Art="enemy-"+e.Id, Selected=selected, Defeated=e.Health==0 };
            target.AddChild(actor); Dock(actor,10,102,-10,0,ar:1,ab:1);
            var plate = FlatPanel(target,8); plate.MouseFilter=MouseFilterEnum.Ignore;
            Dock(plate,6,0,-6,100,ar:1);
            var caption = Column(4); caption.MouseFilter=MouseFilterEnum.Ignore; plate.AddChild(caption);
            var name = Inline((selected ? "◆ " : "")+ChapterSession.EnemyName(e.Id),16,selected ? Accent : Ink);
            name.HorizontalAlignment=HorizontalAlignment.Center; caption.AddChild(name);
            HealthBar(caption,e.Health,e.MaxHealth,e.Health==0 ? Muted : Danger);
            var intent = Row(12); intent.Alignment=BoxContainer.AlignmentMode.Center; intent.MouseFilter=MouseFilterEnum.Ignore; caption.AddChild(intent);
            SymbolValue(intent,"heart",e.Health.ToString(),Ink,"",14);
            if (e.Shield>0) SymbolValue(intent,"guard",e.Shield.ToString(),Success,"",14);
            var (symbol,value)=IntentSymbol(e,b.Enemies.Count);
            SymbolValue(intent,symbol,value,e.Health==0 ? Muted : Danger,"",16);
            if (e.Health > 0) caption.AddChild(Text(ChapterSession.EnemyIntent(e,b.Enemies.Count),14,Ink));
        }
        var piles = Row(24); piles.Alignment=BoxContainer.AlignmentMode.Center;
        var pilesPlate = FlatPanel(_hud!, 5); pilesPlate.Name = "CardPiles";
        Dock(pilesPlate,432,-254,712,-202,0,1,0,1); pilesPlate.AddChild(piles);
        SymbolValue(piles,"draw",b.Draw.Count.ToString(),Ink,"抽牌堆；耗尽时洗入弃牌。");
        SymbolValue(piles,"discard",b.Discard.Count.ToString(),Muted,"弃牌堆");
        SymbolValue(piles,"draw",$"+{b.ExtraDraw}/2",Muted,"本回合已经额外抽取的牌数；上限 2。",14);
        var hand = new HBoxContainer { Name="FloatingHand", Alignment=BoxContainer.AlignmentMode.Center, MouseFilter=MouseFilterEnum.Ignore };
        hand.AddThemeConstantOverride("separation",10); _root.AddChild(hand); Dock(hand,24,-194,-146,-18,at:1,ar:1,ab:1);
        var cardWidth=Mathf.Min(146,(Size.X-170-Math.Max(0,b.Hand.Count-1)*10)/Math.Max(1,b.Hand.Count));
        for (var i=0;i<b.Hand.Count;i++)
        {
            var index=i; var cardData=ChapterContent.Cards[b.Hand[i]]; var playable=Session.CanPlay(i,_target);
            // Unavailable cards remain focusable so keyboard users can inspect the cost and reason.
            var card=Button("card_"+i,"",() => SelectCard(index));
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
            var keyword=Inline(CardKeyword(cardData.Id),14,ChapterSkin.PaperMuted); keyword.HorizontalAlignment=HorizontalAlignment.Center;
            card.AddChild(keyword); Dock(keyword,5,150,-5,171,ar:1);
            card.TooltipText=cardData.Name+$" · {cardData.Action} 行动 / {cardData.Mana} 魔法\n"+cardData.Text+"\n来源 · "+
                string.Join(" / ",b.Equipment.Where(g=>Session.GearCards(g).Contains(cardData.Id)).Select(g=>ChapterContent.Gear[g].Name))+
                (playable ? "" : "\n"+(b.Action<cardData.Action ? "行动不足" : b.Mana<cardData.Mana ? "魔法不足" : "需要存活的目标"));
        }
        if (_battleGuide && !trial)
        {
            var hint = NavigationButton("battle_guide", "选牌 → 选目标 → 确认出牌 · 收起", false, () => { _battleGuide = false; SavePreferences(); Render(); });
            _root.AddChild(hint); Dock(hint,-190,94,190,134,.5f,0,.5f,0);
        }
        if (_selectedCard >= 0 && _selectedCard < b.Hand.Count)
        {
            var preview = Panel(_root, Surface, Ink); preview.Name = "CardPreview";
            Dock(preview,-300,-442,300,-266,.5f,1,.5f,1);
            var layout = Column(6); preview.AddChild(layout);
            var copy = Scroll(layout); copy.AddChild(Text(CardPreview(),16,Ink));
            var actions = Row(12); layout.AddChild(actions);
            actions.AddChild(Button("cancel_card", "取消 · Esc", () => { _selectedCard = -1; Render(); }));
            actions.AddChild(Button("play_selected", "确认出牌 · ← → 切换目标", CommitSelectedCard, Session.CanPlay(_selectedCard,_target),true));
        }
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
