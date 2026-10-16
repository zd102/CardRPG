using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private static readonly Color Night = new("203631"), OnNight = new("f4eedf"), Brass = new("d6b375");

    private void ItemVisual(Node parent, string kind, int size)
    {
        if (kind is "U01" or "M08") parent.AddChild(Glyph("supply", Accent, size));
        else Illustration(parent, ItemArt(kind), size, size, false);
    }
    private readonly HashSet<string> _expandedTools = new();
    private readonly Dictionary<string, bool> _pageDensity = new();
    private bool _defaultCompact;
    private string DensityPage => _panel == "gear" ? "gear_" + (_gearFilter == "cargo" ? "cargo" : "equipment") : _panel == "craft" ? "craft_" + _craftTab : _panel == "" ? Session.State.Phase.ToString() : _panel;
    private void TogglePageDensity()
    { _pageDensity[DensityPage] = !CompactDensity; SavePreferences(); Render(); }

    // Disclosure is a reading preference, independent of progression and save-game state.
    private VBoxContainer ToolsSection(Node parent, string key, string caption)
    {
        var expanded = _expandedTools.Contains(key);
        var toggle = NavigationButton("tools_" + key, (expanded ? "−  " : "+  ") + caption, expanded, () =>
        {
            if (!_expandedTools.Remove(key)) _expandedTools.Add(key);
            SavePreferences(); Render();
        });
        toggle.Alignment = HorizontalAlignment.Left;
        parent.AddChild(toggle);
        var body = Column(8); body.Visible = expanded; body.Name = "Tools_" + key;
        parent.AddChild(body); return body;
    }

    private static PanelContainer DarkPanel(Node parent, int padding = 14)
    {
        var panel = new PanelContainer(); parent.AddChild(panel);
        var style = Style(new Color("203631f2"), new Color("53655b"), padding);
        style.ShadowColor = new Color(0, 0, 0, .18f); style.ShadowSize = 10;
        panel.AddThemeStyleboxOverride("panel", style); return panel;
    }

    private Button QuietButton(string id, string caption, Action action)
    {
        var button = Button(id, caption, action); button.CustomMinimumSize = new Vector2(0, 36);
        button.AddThemeStyleboxOverride("normal", Style(Colors.Transparent, Colors.Transparent, 8));
        return button;
    }

    private Button DarkButton(string id, string caption, Action action)
    {
        var button = Button(id, caption, action); button.CustomMinimumSize = new Vector2(0, 40);
        foreach (var state in new[] { "normal", "hover", "pressed" })
            button.AddThemeStyleboxOverride(state, Style(state == "normal" ? Colors.Transparent : new Color("3b5147"), Colors.Transparent, 10));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(state, OnNight);
        return button;
    }

    private static TextureRect ActionArt(ChapterCard card)
    {
        var index = card.Id is "C26" or "C36" ? 4 : card.Id == "C18" ? 5 : card.Id is "C04" or "C05" ? 2
            : card.Damage > 0 && card.Shield > 0 ? 3 : card.Damage == 0 ? 1 : 0;
        var atlas = GD.Load<Texture2D>("res://Art/Print/card-actions.png");
        var cell = atlas.GetSize() / new Vector2(3, 2);
        return new TextureRect
        {
            Texture = new AtlasTexture { Atlas = atlas, Region = new Rect2(new Vector2(index % 3, index / 3) * cell, cell), FilterClip = true },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.Linear, MouseFilter = MouseFilterEnum.Ignore
        };
    }

    private static string CardRole(ChapterCard card) => card.Id is "C26" or "C36" ? "准备" : card.Id == "C18" ? "群攻"
        : card.Damage > 0 && card.Shield > 0 ? "攻防" : card.Damage == 0 ? "防护" : card.Mana > 0 ? "导能" : "攻击";

    private void CraftedEquipAction(Node parent, string kind)
    {
        var worn = Session.Equipped.Contains(kind);
        var row = Row(10); parent.AddChild(row);
        var source = Session.State.Stock.FirstOrDefault(item => item.Kind == kind);
        var copy = Text(worn ? "已穿戴 · 能力已生效" : (_craftedResult == kind ? "制作完成 · " : "已获得 · ") + (source == null ? "实物不在身边" : ContainerName(source.Container)), 16, Success);
        copy.SizeFlagsHorizontal = SizeFlags.ExpandFill; copy.SizeFlagsVertical = SizeFlags.ShrinkCenter; row.AddChild(copy);
        if (!worn) row.AddChild(Button("crafted_equip_" + kind, "立即穿戴", () => ChangeEquipment(kind), Session.CanEquipItem(kind), true));
    }

    private void EquipmentImpact(Node parent, string kind)
    {
        var gear = ChapterContent.Gear[kind];
        var old = Session.Equipped.FirstOrDefault(id => ChapterContent.Gear[id].Slot == gear.Slot);
        if (_panel == "gear" && _pinnedGear != "" && _pinnedGear != kind && ChapterContent.Gear[_pinnedGear].Slot == gear.Slot) old = _pinnedGear;
        var hero = Row(18); parent.AddChild(hero); Illustration(hero, ItemArt(kind), 116, 116, false);
        var copy = FlexibleColumn(hero); copy.AddChild(Text(gear.Name, 28, Ink));
        copy.AddChild(Text(gear.Hint, 16, Muted));
        if (gear.Slot == "backpack")
        {
            copy.AddChild(Text(Session.Equipped.Contains(kind) ? $"携带容量  {Session.CarryCapacity} 格" : $"携带容量  {Session.CarryCapacity} → {4 + ChapterContent.BagCapacity(kind)} 格", 20, Success));
            copy.AddChild(Text("增加携带空间，不改变战斗卡组。", 16, Muted)); return;
        }
        var before = old == null ? new Dictionary<string, int>() : Session.GearCards(old).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var after = Session.GearCards(kind).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var changes = before.Keys.Union(after.Keys).Where(id => before.GetValueOrDefault(id) != after.GetValueOrDefault(id)).ToArray();
        copy.AddChild(Text(old == kind ? "当前穿戴 · 以下卡牌已加入卡组" : old == null ? "穿戴后加入卡组" : "替换 " + ChapterContent.ItemName(old) + " 后", 16, Ink));
        var removed = changes.Where(id => after.GetValueOrDefault(id) < before.GetValueOrDefault(id)).ToArray();
        var added = changes.Where(id => after.GetValueOrDefault(id) > before.GetValueOrDefault(id)).ToArray();
        string Changes(string[] ids) => string.Join("、", ids.Select(id => ChapterContent.Cards[id].Name + " ×" + Math.Abs(after.GetValueOrDefault(id) - before.GetValueOrDefault(id))));
        if (removed.Length > 0) copy.AddChild(Text("移出：" + Changes(removed), 16, Danger));
        if (added.Length > 0) copy.AddChild(Text("加入：" + Changes(added), 16, Success));
    }
}
