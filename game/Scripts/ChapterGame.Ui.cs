using Godot;
using System;

namespace CardRPG;

public partial class ChapterGame
{
    private static VBoxContainer Column(int gap = 10)
    {
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", gap); return v;
    }
    private static HBoxContainer Row(int gap = 18)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", gap); return h;
    }
    private static VBoxContainer FlexibleColumn(Node parent, float ratio = 1)
    {
        var box = Column(12); box.SizeFlagsHorizontal = SizeFlags.ExpandFill; box.SizeFlagsStretchRatio = ratio; parent.AddChild(box); return box;
    }
    private static Label Text(string value, int size, Color color)
    {
        size = size <= 14 ? 14 : size <= 17 ? 16 : size <= 21 ? 20 : 28;
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontOverride("font", size >= 20 ? ChapterSkin.TitleFont : ChapterSkin.BodyFont);
        label.AddThemeConstantOverride("line_spacing", 4); return label;
    }
    private static Label Inline(string value, int size, Color color)
    {
        var label = Text(value, size, color); label.AutowrapMode = TextServer.AutowrapMode.Off; return label;
    }
    private static StyleBoxFlat Style(Color background, Color border, int padding = 12)
    {
        return new StyleBoxFlat
        {
            BgColor = background, BorderColor = border,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 0, CornerRadiusTopRight = 0, CornerRadiusBottomLeft = 0, CornerRadiusBottomRight = 0,
            ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = 10, ContentMarginBottom = 10
        };
    }
    private static PanelContainer Panel(Node parent, Color? background = null, Color? border = null)
    {
        var panel = ChapterSkin.Panel(parent, background.HasValue ? "field-panel" : "ledger-paper", 14);
        if (border.HasValue) panel.AddThemeStyleboxOverride("panel", Style(background ?? Surface, border.Value));
        return panel;
    }
    private static VBoxContainer CardBox(Node parent, Color? background = null, int gap = 10)
    {
        var panel = Panel(parent, background); var box = Column(gap); panel.AddChild(box); return box;
    }
    private static VBoxContainer Paper(Node parent, int padding = 20)
    {
        var panel = ChapterSkin.Panel(parent, "ledger-paper", padding);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var box = Column(12); panel.AddChild(box); return box;
    }
    private static VBoxContainer Section(Node parent, float ratio = 1)
    {
        var panel = ChapterSkin.Panel(parent, "ledger-paper", 12);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.SizeFlagsStretchRatio = ratio;
        var box = Column(12); panel.AddChild(box); return box;
    }
    private static void Rule(Node parent)
    {
        var line = new HSeparator();
        line.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = Outline, Thickness = 1 });
        parent.AddChild(line);
    }
    private static void Fill(Node parent) => parent.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
    private static string SlotName(string slot) => slot switch { "weapon" => "武器", "body" => "身体", "backpack" => "背包", _ => "手部" };
    private static void Heading(Node parent, string title, string subtitle)
    {
        var box = Column(4); parent.AddChild(box); box.AddChild(Text(title, 21, Ink)); box.AddChild(Text(subtitle, 16, Muted));
    }
    private static void Eyebrow(Node parent, string text, Color? color = null) => parent.AddChild(Text(text, 13, color ?? Accent));
    private static void Metric(Node parent, string label, string value, Color color, string? symbol = null)
    {
        var row = Row(8); row.SizeFlagsHorizontal = SizeFlags.ExpandFill; parent.AddChild(row);
        if (symbol != null) row.AddChild(new ExpeditionGlyph { Symbol = symbol, CustomMinimumSize = new Vector2(25, 32), Tint = color });
        var box = Column(0); row.AddChild(box);
        box.AddChild(Inline(label, 12, Muted));
        var number = Text(value, 20, color); number.AddThemeFontOverride("font", ChapterSkin.NumberFont); number.AutowrapMode = TextServer.AutowrapMode.Off; box.AddChild(number);
    }
    private static void HealthBar(Node parent, int value, int max, Color color)
    {
        var bar = new ProgressBar { MinValue = 0, MaxValue = max, Value = value, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 5), MouseFilter = MouseFilterEnum.Ignore };
        var background = Style(new Color("0d171c"), Colors.Transparent, 0); background.ContentMarginTop = background.ContentMarginBottom = 0;
        var fill = Style(color, Colors.Transparent, 0); fill.ContentMarginTop = fill.ContentMarginBottom = 0;
        bar.AddThemeStyleboxOverride("background", background); bar.AddThemeStyleboxOverride("fill", fill); parent.AddChild(bar);
    }
    private void Guide(Node parent, string number, string title, string body, string symbol)
    {
        var p = Panel(parent); p.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var box = Column(12); p.AddChild(box);
        var line = Row(); box.AddChild(line);
        var label = Text(number, 28, Accent); label.SizeFlagsHorizontal = SizeFlags.ExpandFill; line.AddChild(label);
        line.AddChild(new ExpeditionGlyph { Symbol = symbol, CustomMinimumSize = new Vector2(40, 40) });
        box.AddChild(Text(title, 21, Ink)); box.AddChild(Text(body, 16, Muted));
    }
    private Button Button(string id, string label, Action action, bool enabled = true, bool primary = false)
    {
        var button = new ChapterButton
        {
            Name = id, Text = label, Disabled = !enabled, CustomMinimumSize = new Vector2(0, 46),
            FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand,
            TextureFilter = TextureFilterEnum.Linear
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", primary ? new Color("f1e7ce") : Ink);
        button.AddThemeColorOverride("font_hover_color", primary ? new Color("f1e7ce") : Ink);
        button.AddThemeColorOverride("font_focus_color", primary ? new Color("f1e7ce") : Ink);
        button.AddThemeColorOverride("font_pressed_color", Ink);
        button.AddThemeColorOverride("font_disabled_color", primary ? new Color("e7dcc2") : new Color("56665b"));
        StyleBoxTexture Skin(Color? tint = null)
        {
            var skin = ChapterSkin.Box(primary ? "brass-button" : "button", 14, tint);
            skin.ContentMarginTop = skin.ContentMarginBottom = 9; return skin;
        }
        button.AddThemeStyleboxOverride("normal", Skin());
        button.AddThemeStyleboxOverride("hover", Skin(new Color(1.22f, 1.18f, 1.08f)));
        button.AddThemeStyleboxOverride("pressed", Skin(new Color(.78f, .85f, .8f)));
        button.AddThemeStyleboxOverride("disabled", Skin(new Color(.55f, .62f, .59f)));
        button.AddThemeColorOverride("font_pressed_color", primary ? new Color("f1e7ce") : Ink);
        var focus = Style(Colors.Transparent, Accent); focus.BorderWidthBottom = focus.BorderWidthLeft = focus.BorderWidthRight = focus.BorderWidthTop = 3;
        button.AddThemeStyleboxOverride("focus", focus);
        button.Pressed += () => { if (!button.Disabled) action(); };
        Inspect(button, () => button.TooltipText);
        Buttons[id] = button; return button;
    }
}
