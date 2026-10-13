using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    // Children ignore pointer input so the complete tile remains a keyboard-accessible button.
    private Button ItemTile(string id, string kind, string title, string caption, Action select, bool selected, float height = 126)
    {
        var tile = Button(id, "", select);
        tile.CustomMinimumSize = new Vector2(96, height);
        tile.AddThemeStyleboxOverride("normal", Style(selected ? new Color("d4d7bd") : Surface, selected ? Accent : Outline, 4));
        tile.TooltipText = title + "\n" + caption;
        if (kind != "")
        {
            Control art = kind is "U01" or "M08" ? Glyph("supply", Accent, 0) : new ChapterIllustration { Art = ItemArt(kind), Mounted = false };
            tile.AddChild(art); Dock(art, 12, 8, -12, -48, ar: 1, ab: 1);
        }
        else
        {
            var empty = Glyph("lock", Outline, 0); tile.AddChild(empty); Dock(empty, -14, 14, 14, 42, .5f, 0, .5f, 0);
        }
        var label = Inline(title, 14, Ink); label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.HorizontalAlignment = HorizontalAlignment.Center; tile.AddChild(label); Dock(label, 5, -46, -5, -24, ar: 1, at: 1, ab: 1);
        var note = Inline(caption, 14, selected ? Accent : Muted); note.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        note.HorizontalAlignment = HorizontalAlignment.Center; tile.AddChild(note); Dock(note, 5, -25, -5, -4, ar: 1, at: 1, ab: 1);
        return tile;
    }

    private void EquipmentBoard(Node parent, string[] equipment, string prefix, bool overview)
    {
        var grid = new GridContainer { Name = "LoadoutSummary", Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); parent.AddChild(grid);
        foreach (var slot in new[] { "weapon", "body", "hands", "backpack" })
        {
            var kind = equipment.FirstOrDefault(id => ChapterContent.Gear[id].Slot == slot) ?? "";
            var label = kind == "" ? slot == "hands" && !Session.State.Done.Contains("Q02") ? "尚未解锁" : "未穿戴" : ChapterContent.ItemName(kind);
            var tile = ItemTile(prefix + "_" + slot, kind, label, SlotName(slot), () =>
            {
                _gearFilter = slot; _selectedGear = kind;
                if (overview) OpenNested("gear"); else Render();
            }, !overview && _gearFilter == slot, 134);
            tile.SizeFlagsHorizontal = SizeFlags.ExpandFill; grid.AddChild(tile);
            var supply = kind == "" ? "" : overview && Session.State.Battle?.Id == "trial" ? string.Join("、", ChapterContent.Gear[kind].Cards.GroupBy(c => c).Select(g => ChapterContent.Cards[g.Key].Name + " ×" + g.Count())) : Session.CardList(kind);
            tile.TooltipText = SlotName(slot) + " · " + label + (kind == "" ? "" : $"\n{ChapterContent.Items[kind].Grams / 1000m:0.##} kg · " + (slot == "backpack" ? $"增加 {ChapterContent.BagCapacity(kind)} 格" : supply)) + "\n点击查看这个部位";
        }
    }

    private void CharacterOverview(VBoxContainer box)
    {
        var s = Session.State; var battle = s.Battle;
        var equipment = (battle?.Equipment ?? Session.Equipped.ToList()).ToArray();
        var cards = battle == null ? Session.Deck() : battle.Hand.Concat(battle.Draw).Concat(battle.Discard).ToList();
        var context = battle == null ? "当前穿戴构筑" : battle.Id == "trial" ? "台架试用 · 借用构筑" : $"战斗中 · 第 {battle.Round} 回合";
        box.AddChild(Text(context + "  /  " + (Session.AtBase ? "余火站" : ChapterContent.Places[s.Location].Name), 16, Muted));
        var columns = Row(16); columns.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(columns);
        var profileColumn = FlexibleColumn(columns, .88f); var profile = Scroll(profileColumn); profile.AddThemeConstantOverride("separation", 8);
        var vitals = CardBox(profile, new Color("d4d7bd"), 4);
        var identity = Row(10); vitals.AddChild(identity); identity.AddChild(Glyph("character", Ink, 28)); identity.AddChild(Inline("远征者", 20, Ink));
        var health = Row(8); vitals.AddChild(health);
        var hp = Inline($"{s.Health} / {ChapterSession.MaxHealth}", 28, s.Health <= 14 ? Danger : Ink); hp.Name = "OverviewHealth"; health.AddChild(hp);
        var healthLabel = Inline("生命", 14, Muted); healthLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter; health.AddChild(healthLabel);
        HealthBar(vitals, s.Health, ChapterSession.MaxHealth, Success);
        var resources = new GridContainer { Columns = 2 }; resources.AddThemeConstantOverride("h_separation", 12); resources.AddThemeConstantOverride("v_separation", 12); profile.AddChild(resources);
        Metric(resources, battle == null ? "每回合行动" : "剩余行动", (battle?.Action ?? 3).ToString(), Ink, "action");
        Metric(resources, battle == null ? "魔法上限" : "当前魔法", battle == null ? "3" : $"{battle.Mana}/3", Ink, "mana");
        Metric(resources, "当前护盾", (battle?.Shield ?? 0).ToString(), Ink, "guard");
        Metric(resources, "随身补给", s.Supplies.ToString(), Ink, "supply");
        Rule(profile);
        profile.AddChild(Text("携带状态", 20, Ink)); InventoryStatus(profile);
        profile.AddChild(Text(battle == null ? "每回合抽 5 张 · 魔法恢复 1" : $"手牌 {battle.Hand.Count} · 抽牌堆 {battle.Draw.Count} · 弃牌堆 {battle.Discard.Count}", 14, Muted));
        profile.AddChild(Text(s.Resonance switch
        {
            "R01" when battle?.Id != "trial" => "余烬护层：每场首回合获得 5 护盾。",
            "R07" when battle?.Id != "trial" => "厚壁余温：每回合首次单次获得至少 7 护盾，额外获得 2。",
            _ => "当前无生效共鸣"
        }, 14, Muted));

        var wearing = FlexibleColumn(columns, .95f); wearing.AddChild(Text(battle == null ? "穿戴装备" : "本场装备", 20, Ink));
        var board = Scroll(wearing); EquipmentBoard(board, battle?.Id == "trial" && s.Backpack != "" ? equipment.Append(s.Backpack).ToArray() : equipment, "overview", true);
        board.AddChild(Text(battle?.Id == "trial" ? "试用借装不改变真实穿戴与负重。" : Session.CanEquip ? "点击部位可调整装备。" : "可查看装备；换装需到据点或台架。", 14, Muted));
        board.AddChild(Button("overview_inventory", "管理随身与仓库 →", () => { _gearFilter = "cargo"; OpenNested("gear"); }));

        var deckColumn = FlexibleColumn(columns, 1.45f);
        var heading = Text($"完整卡组 · {cards.Count} 张", 20, Ink); heading.Name = "OverviewDeckCount"; deckColumn.AddChild(heading);
        var deck = Scroll(deckColumn);
        deck.AddChild(Text(battle == null ? "按卡牌合并副本 · 标注装备来源" : "本场实际副本 · 手牌、抽牌堆与弃牌堆合计", 14, Muted));
        var grid = new GridContainer { Name = "OverviewDeck", Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); deck.AddChild(grid);
        foreach (var group in cards.GroupBy(id => id).OrderBy(g => g.Key))
        {
            var card = ChapterContent.Cards[group.Key];
            var sources = string.Join("、", equipment.Where(id => (battle?.Id == "trial" ? ChapterContent.Gear[id].Cards : Session.GearCards(id)).Contains(card.Id)).Select(ChapterContent.ItemName));
            var tile = Button("overview_card_" + card.Id, "", () => { });
            tile.CustomMinimumSize = new Vector2(130, 244); tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            tile.AddThemeStyleboxOverride("normal", ChapterSkin.Box("ledger-paper", 6));
            tile.SetMeta("copies", group.Count()); grid.AddChild(tile);
            var costs = Inline($"{card.Action} 行动 / {card.Mana} 魔法", 14, Muted); tile.AddChild(costs); Dock(costs, 8, 6, -8, 27, ar: 1);
            var art = new ChapterIllustration { Art = CardArt(card), Mounted = false }; tile.AddChild(art); Dock(art, 15, 31, -15, 85, ar: 1);
            var title = Inline(card.Name + " ×" + group.Count(), 16, Ink); tile.AddChild(title); Dock(title, 8, 88, -8, 114, ar: 1);
            var effect = Text(card.Text, 14, Ink); tile.AddChild(effect); Dock(effect, 8, 117, -8, -36, ar: 1, ab: 1);
            var source = Inline(sources, 14, Accent); source.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            tile.AddChild(source); Dock(source, 8, -29, -8, -6, ar: 1, at: 1, ab: 1);
            tile.TooltipText = card.Name + $" ×{group.Count()} · {card.Action} 行动 / {card.Mana} 魔法\n" + card.Text + "\n来源：" + sources;
        }
    }
}
