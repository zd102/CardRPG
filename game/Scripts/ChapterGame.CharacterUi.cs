using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private Button EquipmentTile(string id, string kind, string caption, Action select, bool selected, bool compact = false)
    {
        var tile = Button(id, "", select); tile.CustomMinimumSize = new Vector2(0, compact ? 68 : 88); tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        tile.AddThemeStyleboxOverride("normal", Style(selected ? new Color("dce4d1") : new Color("eeeadf"), selected ? Ink : Outline, 8));
        Control art = kind == "" ? Glyph("empty", Outline, 0) : new ChapterIllustration { Art = ItemArt(kind), Mounted = false, MouseFilter = MouseFilterEnum.Ignore };
        tile.AddChild(art); Dock(art, 8, 10, compact ? 52 : 64, compact ? 58 : 76);
        var slot = Inline(caption, 14, Muted); tile.AddChild(slot); Dock(slot, compact ? 60 : 74, compact ? 5 : 10, -8, compact ? 27 : 34, ar: 1);
        var title = Text(kind == "" ? UiCopy.Text("UI_7EEA754DDA60") : ChapterContent.ItemName(kind), 16, Ink);
        tile.AddChild(title); Dock(title, compact ? 60 : 74, compact ? 29 : 36, -8, compact ? 65 : 78, ar: 1);
        return tile;
    }

    private void LoadoutColumn(Node parent)
    {
        var panel = FlatPanel(parent, 10); panel.Name = "LoadoutColumn";
        panel.CustomMinimumSize = new Vector2(Size.X < 1200 ? 208 : 232, 0);
        var column = Column(8); panel.AddChild(column);
        column.AddChild(Text(UiCopy.Text("UI_BB02B624AD60"), 20, Ink));
        var list = Scroll(column);
        var grid = new GridContainer { Name = "LoadoutSummary", Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("v_separation", 8); list.AddChild(grid);
        foreach (var slot in new[] { "weapon", "body", "hands", "backpack" })
        {
            var kind = Session.Equipped.FirstOrDefault(g => ChapterContent.Gear[g].Slot == slot) ?? "";
            var tile = EquipmentTile("wear_" + slot, kind, SlotName(slot), () => { _gearFilter = slot; _selectedGear = kind; if (_panel == "gear") Render(); else OpenNested("gear"); }, _panel == "gear" && _gearFilter == slot, true);
            grid.AddChild(tile); BindEquipmentDrop(tile, slot);
            if (kind != "" && kind == _lastEquippedKind && Time.GetTicksMsec() - _lastEquipmentChange < 12000) tile.AddThemeStyleboxOverride("normal", Style(new Color("dce4d1"), Success, 8));
        }
        column.AddChild(Inline(UiCopy.Format("UI_FF9AE905A263", Session.Deck().Count), 16, Ink));
        column.AddChild(Inline(UiCopy.Format("UI_C8A4EC3DDB4F", Session.CarriedGrams / 1000m), 16, Session.Overweight ? Danger : Ink));
        AddLoadMeter(column, "LoadoutWeightMeter", Session.CarriedGrams, Session.CarriedGrams, ChapterSession.WeightLimitGrams);
    }

    // Children ignore pointer input so the complete tile remains a keyboard-accessible button.
    private Button ItemTile(string id, string kind, string title, string caption, Action select, bool selected, float height = 126)
    {
        var tile = Button(id, "", select);
        tile.CustomMinimumSize = new Vector2(96, height);
        tile.AddThemeStyleboxOverride("normal", Style(selected ? new Color("dbe5cb") : new Color("ede5cf"), selected ? Ink : Outline, 8));
        tile.TooltipText = title + "\n" + caption;
        if (kind != "")
        {
            Control art = kind is "U01" or "M08" ? Glyph("supply", Accent, 0) : new ChapterIllustration { Art = ItemArt(kind), Mounted = false };
            tile.AddChild(art); Dock(art, 12, 8, -12, -52 * ReadingScale, ar: 1, ab: 1);
        }
        else
        {
            var empty = Glyph(title.Contains("未解锁") ? "lock" : "empty", Outline, 0); tile.AddChild(empty); Dock(empty, -14, 14, 14, 42, .5f, 0, .5f, 0);
        }
        if (kind.StartsWith("BP"))
        {
            var capacity=Inline((4+ChapterContent.BagCapacity(kind))+UiCopy.Text("UI_6D1F8AD23EF0"),14,Ink);capacity.Name="CapacityBadge"; capacity.HorizontalAlignment=HorizontalAlignment.Right; tile.AddChild(capacity); Dock(capacity,-67,5,-7,26,1,0,1,0);
            var badge=Glyph(kind=="BP01"?"thread":kind=="BP02"?"slots":"frame",Accent,22);badge.Name="BagBadge";tile.AddChild(badge);Dock(badge,6,8,28,30);
        }
        var label = Inline(title, 16, Ink); label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.Name="ItemTitle";
        label.AddThemeFontSizeOverride("font_size", (int)(16 * ReadingScale));
        label.HorizontalAlignment = HorizontalAlignment.Center; tile.AddChild(label); Dock(label, 5, -46 * ReadingScale, -5, -24 * ReadingScale, ar: 1, at: 1, ab: 1);
        var note = Inline(caption, 14, selected ? Accent : Muted); note.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        note.Name="ItemCaption";
        note.AddThemeFontSizeOverride("font_size", (int)(14 * ReadingScale));
        note.HorizontalAlignment = HorizontalAlignment.Center; tile.AddChild(note); Dock(note, 5, -25 * ReadingScale, -5, -4, ar: 1, at: 1, ab: 1);
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
            tile.TooltipText = SlotName(slot) + " · " + label + (kind == "" ? "" : $"\n{ChapterContent.Items[kind].Grams / 1000m:0.##} kg · " + (slot == "backpack" ? UiCopy.Format("UI_2FFC6E94DF9C", ChapterContent.BagCapacity(kind)) : supply)) + UiCopy.Text("UI_54CC8ED8BA5E");
        }
    }

}
