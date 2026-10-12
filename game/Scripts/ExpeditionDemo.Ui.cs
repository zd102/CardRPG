using Godot;
using System;

namespace CardRPG;

public partial class ExpeditionDemo
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
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color); return label;
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
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = 10, ContentMarginBottom = 10
        };
    }
    private static PanelContainer Panel(Node parent, Color? background = null, Color? border = null)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Style(background ?? Surface, border ?? Outline)); parent.AddChild(panel); return panel;
    }
    private static VBoxContainer CardBox(Node parent, Color? background = null, int gap = 10)
    {
        var panel = Panel(parent, background); var box = Column(gap); panel.AddChild(box); return box;
    }
    private static void Heading(Node parent, string title, string subtitle)
    {
        var box = Column(4); parent.AddChild(box); box.AddChild(Text(title, 21, Ink)); box.AddChild(Text(subtitle, 16, Muted));
    }
    private static void Eyebrow(Node parent, string text, Color? color = null) => parent.AddChild(Text(text, 13, color ?? Accent));
    private static void Metric(Node parent, string label, string value, Color color, string? symbol = null)
    {
        var row = Row(8); parent.AddChild(row);
        if (symbol != null) row.AddChild(new ExpeditionGlyph { Symbol = symbol, CustomMinimumSize = new Vector2(25, 32), Tint = color });
        var box = Column(0); row.AddChild(box);
        box.AddChild(Inline(label, 12, Muted));
        var number = Text(value, 16, color); number.AutowrapMode = TextServer.AutowrapMode.Off; box.AddChild(number);
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
        var button = new Button
        {
            Name = id, Text = label, Disabled = !enabled, CustomMinimumSize = new Vector2(0, 44),
            FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", primary ? new Color("172329") : Ink);
        button.AddThemeColorOverride("font_hover_color", primary ? new Color("172329") : Ink);
        button.AddThemeColorOverride("font_pressed_color", Ink);
        button.AddThemeColorOverride("font_disabled_color", new Color("92a09b"));
        button.AddThemeStyleboxOverride("normal", Style(primary ? Accent : new Color("1e3035"), primary ? Accent : new Color("485d5d")));
        button.AddThemeStyleboxOverride("hover", Style(primary ? new Color("f0d6a2") : new Color("334c4e"), Accent));
        button.AddThemeStyleboxOverride("pressed", Style(new Color("18272e"), Accent));
        button.AddThemeStyleboxOverride("disabled", Style(new Color("172327"), new Color("344548")));
        var focus = Style(Colors.Transparent, new Color("f9edcb")); focus.BorderWidthBottom = focus.BorderWidthLeft = focus.BorderWidthRight = focus.BorderWidthTop = 2;
        button.AddThemeStyleboxOverride("focus", focus);
        button.Pressed += () => { if (!button.Disabled) action(); };
        Buttons[id] = button; return button;
    }
}
