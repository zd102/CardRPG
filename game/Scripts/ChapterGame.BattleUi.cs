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
        var ribbon = Row(16); _root.AddChild(ribbon); Dock(ribbon, 26, 108, 760, 151);
        ribbon.AddChild(Inline($"第 {b.Round} 回合", 20, Ink));
        ribbon.AddChild(Inline($"行动 {b.Action}   魔法 {b.Mana}   护盾 {b.Shield}", 16, Accent));
        ribbon.AddChild(Inline(trial ? "练习靶不会还击" : $"敌方预计伤害 {b.Enemies.Sum(ChapterSession.EnemyDamage)}", 14, trial ? Success : Danger));
        var end = Button("end_turn", "结束回合  →", () => Change(() => Session.EndTurn()), true, true);
        _root.AddChild(end); Dock(end, -212, 91, -28, 135, al: 1, ar: 1);
        var retreat = Button(trial ? "end_trial" : "retreat", trial ? "结束试用" : "战术撤退", () => Change(() => trial ? Session.EndTrial() : Session.EndTurn(true)));
        retreat.TooltipText = trial ? "免费退出，真实生命与物品不变。" : "承受本轮敌人意图，存活后退回进入位置。敌人状态保留，仍需自行撤离。";
        _root.AddChild(retreat); Dock(retreat, -212, 144, -28, 188, al: 1, ar: 1);
        var help = Text(trial ? Session.State.World.Contains("trial-complete") ? "试用完成 · 检修手套配方已开放" : "先打出「预备工具」，再攻击一次" : "点击敌人选择目标  ·  点击手牌出牌", 13, Muted);
        _root.AddChild(help); Dock(help, 28, 158, 700, 186);
        if (b.Id == "E13")
        {
            var warning = Text(b.Round <= 3 ? "3 次敌方阶段后额外弹簧被运走 · 破甲锤首奖保留" : "额外包已运走 · 破甲锤仍可取得", 13, Accent);
            _root.AddChild(warning); Dock(warning, 28, 188, 700, 211);
        }
        for (var i = 0; i < b.Enemies.Count; i++)
        {
            var index = i; var e = b.Enemies[i]; var selected = _target == i;
            var width = b.Enemies.Count == 1 ? .34f : b.Enemies.Count == 2 ? .28f : .245f;
            var x = .5f + (i - (b.Enemies.Count - 1) / 2f) * (width + .025f);
            var target = Button("target_" + i, "", () => { _target = index; Render(); }, e.Health > 0);
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                target.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            target.TooltipText = ChapterSession.EnemyName(e.Id) + "\n" + ChapterSession.EnemyIntent(e, b.Enemies.Count);
            _root.AddChild(target); Dock(target, 0, 0, 0, 0, x - width / 2, .265f, x + width / 2, .715f);
            var actor = new ChapterActor { Name = "Actor_" + i, Art = "enemy-" + e.Id, Selected = selected, Defeated = e.Health == 0 };
            target.AddChild(actor); Dock(actor, 10, 73, -10, 0, ar: 1, ab: 1);
            var caption = Column(4); caption.MouseFilter = MouseFilterEnum.Ignore;
            target.AddChild(caption); Dock(caption, 12, 0, -12, 69, ar: 1);
            var name = Text((selected ? "◆ " : "") + ChapterSession.EnemyName(e.Id) + $"  {e.Health}/{e.MaxHealth}", 16, selected ? Accent : Ink);
            name.HorizontalAlignment = HorizontalAlignment.Center; caption.AddChild(name);
            HealthBar(caption, e.Health, e.MaxHealth, e.Health == 0 ? Muted : Danger);
            var intent = Text((e.Shield > 0 ? $"盾 {e.Shield} · " : "") + ChapterSession.EnemyIntent(e, b.Enemies.Count), 13, e.Health == 0 ? Muted : Danger);
            intent.HorizontalAlignment = HorizontalAlignment.Center; caption.AddChild(intent);
        }
        var draw = Text($"抽牌 {b.Draw.Count}  ·  弃牌 {b.Discard.Count}  ·  额外抽牌 {b.ExtraDraw}/2", 12, Muted);
        draw.HorizontalAlignment = HorizontalAlignment.Center;
        _root.AddChild(draw); Dock(draw, -280, -225, 280, -204, .5f, 1, .5f, 1);
        // Individual floating cards, no bottom panel or separate command area.
        var hand = new HBoxContainer { Name = "FloatingHand", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        hand.AddThemeConstantOverride("separation", 9); _root.AddChild(hand); Dock(hand, 32, -198, -32, -16, at: 1, ar: 1, ab: 1);
        var cardWidth = Mathf.Min(158, (Size.X - 82 - Math.Max(0, b.Hand.Count - 1) * 9) / Math.Max(1, b.Hand.Count));
        for (var i = 0; i < b.Hand.Count; i++)
        {
            var index = i; var c = ChapterContent.Cards[b.Hand[i]]; var playable = Session.CanPlay(i, _target);
            var card = Button("card_" + i, "", () => Change(() => Session.PlayCard(index, _target)), playable);
            card.CustomMinimumSize = new Vector2(cardWidth, 182); card.ClipContents = true;
            var cardColor = c.Mana > 0 || c.Id == "C05" ? new Color("84dce7") : c.Shield > 0 || c.Damage == 0 ? Success : Accent;
            card.AddThemeStyleboxOverride("normal", Style(new Color("122334"), cardColor));
            card.AddThemeStyleboxOverride("hover", Style(new Color("314951"), new Color("fff0b5")));
            card.AddThemeStyleboxOverride("disabled", Style(new Color("14212e"), new Color("607e87")));
            hand.AddChild(card);
            var art = ChapterArt.Picture(c.Mana > 0 || c.Id == "C05" ? "card-arc" : c.Shield > 0 || c.Damage == 0 ? "card-ward" : "card-strike");
            card.AddChild(art); Dock(art, 3, 3, -3, -3, ar: 1, ab: 1);
            if (!playable) art.Modulate = new Color(.45f, .5f, .55f);
            var shadow = new TextureRect
            {
                MouseFilter = MouseFilterEnum.Ignore,
                Texture = new GradientTexture2D
                {
                    FillFrom = Vector2.Zero, FillTo = Vector2.Down,
                    Gradient = new Gradient { Colors = [new Color("101e34c9"), new Color("101e3410"), new Color("101e34e8"), new Color("101e34fa")], Offsets = [0, .33f, .58f, 1] }
                }
            };
            card.AddChild(shadow); Dock(shadow, 3, 3, -3, -3, ar: 1, ab: 1);
            var cost = Text($"{c.Action} 行动  /  {c.Mana} 魔法", 12, cardColor);
            card.AddChild(cost); Dock(cost, 10, 7, -8, 28, ar: 1);
            var title = Text(c.Name, 19, Ink); card.AddChild(title); Dock(title, 10, 88, -8, 115, ar: 1);
            var effect = Text(c.Text, 12, Ink); effect.MaxLinesVisible = 3; effect.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            card.AddChild(effect); Dock(effect, 10, 118, -8, -8, ar: 1, ab: 1);
            card.TooltipText = c.Name + $" · {c.Action} 行动 / {c.Mana} 魔法\n" + c.Text + "\n来源 · " +
                string.Join(" / ", b.Equipment.Where(g => Session.GearCards(g).Contains(c.Id)).Select(g => ChapterContent.Gear[g].Name)) + (playable ? "\n点击出牌" : "\n当前资源不足");
        }
    }
}
