using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private bool _deckMatrix;
    private string _matrixGear = "", _matrixCard = "", _deckSource = "";
    private static string CardCoreEffect(ChapterCard card)
    {
        var parts = new List<string>();
        if (card.Damage > 0) parts.Add((card.Id == "C18" ? "全体伤害 " : card.Id == "C06" ? "穿盾伤害 " : "伤害 ") + card.Damage);
        if (card.Shield > 0) parts.Add("护盾 " + card.Shield);
        if (card.Id == "C05") parts.Add("魔法 +1");
        if (card.Id is "C26" or "C36") parts.Add($"额外抽牌 {(card.Id == "C36" ? 2 : 1)} · 本回合共享上限 2");
        if (card.Id == "C07") parts.Add("消耗至多 8 盾，等量加伤");
        if (card.Id == "C25") parts.Add("下回合保留至多 4 盾");
        return string.Join(" · ", parts);
    }
    private string CardRules(string id)
    {
        var equipment = Session.State.Battle?.Equipment ?? Session.Equipped.ToList(); var trial = Session.State.Battle?.Id == "trial";
        var sources = equipment.Where(g => (trial ? ChapterContent.Gear[g].Cards : Session.GearCards(g)).Contains(id)).ToArray();
        var modified = !trial ? sources.Where(g => Session.State.Mods.GetValueOrDefault(g, "") != "" && Session.GearCards(g).Count(c => c == id) != ChapterContent.Gear[g].Cards.Count(c => c == id)).ToArray() : [];
        return "基础效果：" + ChapterContent.Cards[id].Text + "\n供牌来源：" + (sources.Length == 0 ? "临时 / 尚无可确认来源" : string.Join("、", sources.Select(ChapterContent.ItemName)))
            + "\n改装：" + (modified.Length == 0 ? "此牌无额外改装来源" : string.Join("、", modified.Select(ChapterContent.ItemName)) + "的单副本替换")
            + "\n本趟共鸣：" + (Session.State.Resonance == "R01" ? "每场首回合额外获得 5 护盾" : Session.State.Resonance == "R07" ? "每回合首次单次获得至少 7 护盾时额外 +2，需满足条件" : "无") + (trial ? "\n试用借装，不改变真实装备。" : "");
    }
    private void CostDistribution(Node parent, List<string> cards)
    {
        var row = Row(14); parent.AddChild(row);
        foreach (var resource in new[] { "行动", "魔法" })
        {
            var column = FlexibleColumn(row); var bars = Row(8); column.AddChild(bars);
            foreach (var group in cards.GroupBy(id => resource == "行动" ? ChapterContent.Cards[id].Action : ChapterContent.Cards[id].Mana).OrderBy(g => g.Key))
            {
                var band = FlexibleColumn(bars);
                band.AddChild(Text(UiCopy.Format("UI_9E1B55F794CE", group.Key, resource, group.Count()), 14, Ink));
                var bar = new ProgressBar { Name = "Cost_" + resource + "_" + group.Key, MinValue = 0, MaxValue = Math.Max(1, cards.Count), Value = group.Count(), ShowPercentage = false,
                    CustomMinimumSize = new Vector2(48, 8), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
                bar.AddThemeStyleboxOverride("background", Style(new Color("d0d1c0"), Colors.Transparent, 0));
                bar.AddThemeStyleboxOverride("fill", Style(Success, Colors.Transparent, 0));
                band.AddChild(bar);
            }
        }
    }
    private void DeckSourceMatrix(Node parent, string[] equipment, IEnumerable<IGrouping<string, string>> groups, bool trial)
    {
        var sources = equipment.Where(id => ChapterContent.Gear[id].Cards.Length > 0).ToArray();
        var rows = groups.ToArray();
        int Known(string gear, string card) => (trial ? ChapterContent.Gear[gear].Cards : Session.GearCards(gear)).Count(id => id == card);
        var unknown = rows.Any(g => sources.Sum(id => Known(id, g.Key)) != g.Count());
        var table = new GridContainer { Name = "DeckSourceMatrix", Columns = sources.Length + 2 + (unknown ? 1 : 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        table.AddThemeConstantOverride("h_separation", 10); table.AddThemeConstantOverride("v_separation", 10); parent.AddChild(table);
        table.AddChild(Text(UiCopy.Text("UI_38A29C555C51"), 16, Ink));
        foreach (var id in sources)
        {
            var mod = !trial && Session.State.Mods.TryGetValue(id, out var active) && active != "" ? "\n生效改装：" + ChapterContent.Cards[active == "D01" ? "C19" : "C26"].Name : "";
            table.AddChild(Button("matrix_gear_" + id, (_matrixGear == id ? "◆ " : "") + ChapterContent.ItemName(id) + mod, () => { _matrixGear = _matrixGear == id ? "" : id; Render(); }));
        }
        if (unknown) table.AddChild(Text(UiCopy.Text("UI_7E03CC6B61CB"), 14, Muted));
        table.AddChild(Text(UiCopy.Text("UI_E009C148D189"), 16, Ink));
        foreach (var group in rows.OrderBy(g => ChapterContent.Cards[g.Key].Name))
        {
            var card = ChapterContent.Cards[group.Key];
            var button = Button("source_card_" + card.Id, (_matrixCard == card.Id ? "◆ " : "") + card.Name, () => { _matrixCard = _matrixCard == card.Id ? "" : card.Id; Render(); });
            table.AddChild(button);
            var totalKnown = sources.Sum(id => Known(id, card.Id));
            foreach (var gear in sources)
            {
                var count = totalKnown > group.Count() ? 0 : Known(gear, card.Id);
                var highlight = gear == _matrixGear || card.Id == _matrixCard;
                var cell = Text((highlight ? "● " : "") + (count == 0 ? "—" : count.ToString()), 16, highlight ? Accent : count == 0 ? Muted : Ink);
                cell.SetMeta("source_card", card.Id); cell.SetMeta("copies", count); table.AddChild(cell);
            }
            if (unknown)
            {
                var count = totalKnown > group.Count() ? group.Count() : group.Count() - totalKnown;
                var cell = Text(count == 0 ? "—" : count.ToString(), 16, Muted); cell.SetMeta("source_card", card.Id); cell.SetMeta("copies", count); table.AddChild(cell);
            }
            var total = Text(group.Count().ToString(), 16, Ink); total.SetMeta("card_total", card.Id); total.SetMeta("copies", group.Count()); table.AddChild(total);
        }
        foreach (var cell in table.GetChildren().OfType<Control>()) cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (_matrixCard != "" && rows.Any(g => g.Key == _matrixCard)) parent.AddChild(Button("matrix_card_detail", UiCopy.Text("UI_B80208454B18"), () => { _detailItemId = 0; _detailTitle = ChapterContent.Cards[_matrixCard].Name; _detailText = CardRules(_matrixCard); OpenNested("detail"); }));
    }
}
