using CardRPG.Core;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private void ModificationsWorkspace(VBoxContainer page)
    {
        page.AddChild(Text(UiCopy.Text("UI_7AE1CD45238B"), 16, Muted));
        var choices = Session.State.Owned.Order().SelectMany(g => new[] { "D01", "D08" }.Where(m => ChapterSession.CompatibleMod(g, m)).Select(m => (Gear: g, Mod: m))).ToArray();
        if (choices.Length == 0) { page.AddChild(Text(UiCopy.Text("UI_8A8F8BE5612B"), 18, Muted)); return; }
        if (!choices.Contains((_selectedModGear, _selectedMod))) (_selectedModGear, _selectedMod) = choices[0];
        var split = Row(14); split.SizeFlagsVertical = SizeFlags.ExpandFill; page.AddChild(split);
        var directory = Directory(split); var list = Scroll(directory, "mod_directory"); var s = Session.State;
        foreach (var choice in choices)
        {
            var installed = s.Mods.GetValueOrDefault(choice.Gear) == choice.Mod; var owned = s.BoughtMods.Contains(choice.Gear + ":" + choice.Mod);
            list.AddChild(Button("mod_choice_" + choice.Gear + "_" + choice.Mod, (_selectedModGear == choice.Gear ? "◆ " : "") + ChapterContent.ItemName(choice.Gear) + "\n" + (installed ? UiCopy.Text("UI_FE2B255FF8E5") : owned ? UiCopy.Text("UI_822F1C0E043B") : UiCopy.Text("UI_EFCC576AC93B")), () => { (_selectedModGear, _selectedMod) = choice; Render(); }));
        }
        var gear = _selectedModGear; var mod = _selectedMod;
        var bought = s.BoughtMods.Contains(gear + ":" + mod); var active = s.Mods.GetValueOrDefault(gear) == mod;
        var detail = Section(split); detail.AddChild(Text(ChapterContent.ItemName(gear) + (active ? UiCopy.Text("UI_678F79624601") : UiCopy.Text("UI_226CB5D2D1D7")), 20, Ink));
        var fromId = mod == "D01" ? "C01" : "C03"; var toId = mod == "D01" ? "C19" : "C26";
        var original = ChapterContent.Cards[fromId]; var changed = ChapterContent.Cards[toId];
        var copies = ChapterContent.Gear[gear].Cards.Count(id => id == fromId);
        detail.AddChild(Text($"只替换 1 张 {original.Name} → {changed.Name} · 装备供牌仍为 {ChapterContent.Gear[gear].Cards.Length} 张", 16, Ink));
        var body = Scroll(detail, "mod_detail", gear + ":" + mod);
        var comparison = Row(18); body.AddChild(comparison);
        foreach (var card in new[] { original, changed })
        {
            var side = FlexibleColumn(comparison); var cardRow = Row(12); side.AddChild(cardRow);
            var art = ActionArt(card); art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered; art.CustomMinimumSize = new Vector2(80, 80); cardRow.AddChild(art);
            var caption = FlexibleColumn(cardRow); caption.AddChild(Text((card == original ? "原卡 · " : "改装后 · ") + card.Name, 20, Ink));
            caption.AddChild(Text(CardCoreEffect(card), 16, Ink));
        }
        var values = ToolsSection(body, "mod", "完整数值与规则对照");
        foreach (var (label, before, after) in new[] { ("行动", original.Action, changed.Action), ("魔法", original.Mana, changed.Mana), ("伤害", original.Damage, changed.Damage), ("护盾", original.Shield, changed.Shield) })
            (before == after ? values : body).AddChild(Text($"{label}   {before} → {after}" + (before == after ? UiCopy.Text("UI_F3969BF4AC3D") : $"（{after - before:+0;-0}）"), 16, Ink));
        values.AddChild(Text(UiCopy.Text("UI_1F37D70032F1") + original.Text + UiCopy.Text("UI_60398163876F") + changed.Text, 18, Ink));
        if (mod == "D08") body.AddChild(Text(UiCopy.Text("UI_3BC84F5009A9"), 16, Muted));
        body.AddChild(Button("mod_deck_preview", UiCopy.Text("UI_C14D23F5DAF6"), () =>
        {
            var preview = Session.PreviewCopy(); preview.State.Mods[gear] = active ? "" : mod;
            _detailTitle = UiCopy.Text("UI_812C36528622");
            _detailText = UiCopy.Text("UI_9804F15D9BE8") + ChapterContent.ItemName(gear) + UiCopy.Text("UI_A7910BBABBCA") + (Session.Equipped.Contains(gear) ? "" : UiCopy.Text("UI_04AA3C61F6CF"))
                + string.Join("\n", preview.Deck().GroupBy(id => id).OrderBy(g => g.Key).Select(g => ChapterContent.Cards[g.Key].Name + " ×" + g.Count())); OpenNested("detail");
        }));
        if (!bought) Costs(detail, [2, 0, 1],s.Done.Contains("Q02"));
        var available = s.Phase == ChapterPhase.Camp && s.Done.Contains("Q02") && (bought || Session.AvailableMaterial(0) >= 2 && Session.AvailableMaterial(2) >= 1);
        detail.AddChild(Text(UiCopy.Text("UI_E66E1A238C67") + ChapterContent.ItemName(gear) + (bought ? UiCopy.Text("UI_09601B3859B3") : UiCopy.Text("UI_53A55A58148C")), 16, Muted));
        if (!available) detail.AddChild(Text(s.Phase != ChapterPhase.Camp ? UiCopy.Text("UI_251FEEFCD521") : !s.Done.Contains("Q02") ? UiCopy.Text("UI_EB9FFCD6313E") : UiCopy.Text("UI_320CDABE7A7C"), 16, Danger));
        detail.AddChild(Button("mod_" + gear + "_" + mod, bought ? active ? UiCopy.Text("UI_7A023E4D53FE") : UiCopy.Text("UI_202F9415A6BC") : UiCopy.Text("UI_3C35081B3D8C") + ChapterContent.ItemName(gear), () => Change(() => bought ? Session.SetMod(gear, active ? "" : mod) : Session.BuyMod(gear, mod)), available, true));
    }
}
