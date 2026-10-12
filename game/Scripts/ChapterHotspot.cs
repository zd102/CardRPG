using Godot;

namespace CardRPG;

/// <summary>A keyboard-accessible interaction pinned to an object in the painted world.</summary>
public partial class ChapterHotspot : Button
{
    public Color Tint { get; set; } = new("dfbb7b");
    public bool Exit { get; set; }
    public override void _Ready()
    {
        AddThemeFontSizeOverride("font_size", 14);
        AddThemeColorOverride("font_color", new Color("f3ebd7"));
        AddThemeColorOverride("font_hover_color", Colors.White);
        AddThemeColorOverride("font_disabled_color", new Color("a4aaa2"));
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginTop = 31, ContentMarginBottom = 6, ContentMarginLeft = 10, ContentMarginRight = 10 });
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw; Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        var active = IsHovered() || HasFocus(); var color = Disabled ? new Color("87958b") : Tint;
        var center = new Vector2(Size.X / 2, 12);
        DrawCircle(center, active ? 16 : 13, new Color("071114c9"));
        Vector2[] diamond = [center + new Vector2(0, -9), center + new Vector2(9, 0), center + new Vector2(0, 9), center + new Vector2(-9, 0), center + new Vector2(0, -9)];
        DrawPolyline(diamond, color, active ? 2 : 1.2f, true);
        if (Exit) DrawPolyline([center + new Vector2(-3, -4), center + new Vector2(2, 0), center + new Vector2(-3, 4)], color, 1.5f, true);
        else DrawCircle(center, 2.5f, color);
        DrawLine(center + new Vector2(0, 12), center + new Vector2(0, 24), new Color(color, .6f), 1, true);
        // The native button paints the label. A thin underline never covers its glyphs.
        DrawLine(new Vector2(12, Size.Y - 2), new Vector2(Size.X - 12, Size.Y - 2), new Color(color, active ? .9f : .3f), 1, true);
    }
}
