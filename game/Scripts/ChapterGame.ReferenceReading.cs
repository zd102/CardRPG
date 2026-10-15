using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private string _referenceText = "", _detailKeyword = "";
    private void PinReference(string text)
    {
        _referenceText = text; _detailKeyword = "";
        if (_measurement != null) _measurement.DetailOpens++;
        Render();
    }
    private void DetailComparison(VBoxContainer page)
    {
        if (_referenceText != "" && _referenceText != _detailText && _referenceText != _detailTitle + "\n" + _detailText)
        {
            page.AddChild(Text(UiCopy.Text("UI_32521B3050BC"), 14, Muted));
            var columns = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            columns.AddThemeConstantOverride("h_separation", 20); page.AddChild(columns);
            var reference = FlexibleColumn(columns); reference.Name = "PinnedReference"; reference.AddChild(Text(UiCopy.Text("UI_33E803C301CA"), 18, Ink)); reference.AddChild(Text(_referenceText, 16, Ink));
            var current = FlexibleColumn(columns); current.AddChild(Text(UiCopy.Text("UI_21EB03E5FDBB") + _detailTitle, 18, Ink)); current.AddChild(Text(_detailText, 18, Ink));
        }
        else page.AddChild(Text(_detailText, 18, Ink));
        var keywords = new (string Word, string Rule)[]
        {
            ("护盾", "护盾先抵挡伤害；普通护盾在自己下回合开始时清除。敌方护盾规则以其意图为准。"),
            ("稳固", "稳固最多保留 4 点护盾到下回合；不是额外获得 4 点护盾。"),
            ("穿盾", "穿盾伤害按卡牌规则绕过敌方护盾；不会把护盾转化成额外伤害。"),
            ("额外抽牌", "额外抽牌共享本回合 2 张上限；抽牌堆查看按种类汇总，不公开随机顺序。"),
            ("超重", "负重超过 20 kg 时，场景行动代价翻倍；容量与重量是两个独立约束。")
        };
        var related = keywords.Where(k => (_detailText + _referenceText).Contains(k.Word, StringComparison.Ordinal)).ToArray();
        if (related.Length == 0) return;
        var links = new HFlowContainer(); page.AddChild(links);
        foreach (var (word, rule) in related)
            links.AddChild(Button("detail_keyword_" + Array.FindIndex(keywords, k => k.Word == word), word + "…", () => { _detailKeyword = _detailKeyword == rule ? "" : rule; Render(); }));
        if (_detailKeyword != "")
        {
            page.AddChild(Text(_detailKeyword, 16, Ink));
            page.AddChild(Button("detail_keyword_close", UiCopy.Text("UI_B8C44573E39D"), () => { _detailKeyword = ""; Render(); }));
        }
    }
}
