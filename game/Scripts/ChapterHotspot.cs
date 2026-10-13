using Godot;

namespace CardRPG;

/// <summary>A compact marker; pointer and keyboard focus reveal the same description.</summary>
public partial class ChapterHotspot : Button
{
    public Color Tint { get; set; } = new("d3ad68");
    public bool Exit { get; set; }
    public string Symbol { get; set; } = "loot";
    public string Caption { get; set; } = "";
    public bool Important { get; set; }
    public override string _GetTooltip(Vector2 atPosition) => "";
    public override void _Ready()
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        var glyph = new ExpeditionGlyph { Symbol = Symbol, Tint = Disabled ? new Color("89958d") : Tint, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(glyph); glyph.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        glyph.OffsetLeft = glyph.OffsetTop = 12; glyph.OffsetRight = glyph.OffsetBottom = -12;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw; Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        var active = IsHovered() || HasFocus(); var color = Disabled ? new Color("89958d") : Tint;
        var center = Size / 2;
        if (Exit)
        {
            Vector2[] points = [center+new Vector2(0,-26), center+new Vector2(26,0), center+new Vector2(0,26), center+new Vector2(-26,0), center+new Vector2(0,-26)];
            DrawColoredPolygon(points, new Color("f1e7ce")); DrawPolyline(points, color, active ? 3 : 2);
        }
        else { DrawCircle(center,24,new Color("f1e7ce")); DrawArc(center,24,0,Mathf.Tau,32,color,active ? 3 : 2); }
        if (Important) DrawCircle(new Vector2(Size.X-3,3),5,new Color("ead3a0"));
        if (Disabled) DrawLine(center+new Vector2(-13,13),center+new Vector2(13,-13),color,2);
        if (!active && !Important && !Exit) return;
        var font = GetThemeDefaultFont(); var width = font.GetStringSize(Caption,fontSize:14).X;
        DrawRect(new Rect2((Size.X-width)/2-8,Size.Y+5,width+16,26),new Color("f1e7ce"));
        DrawString(font,new Vector2((Size.X-width)/2,Size.Y+24),Caption,fontSize:14,modulate:color);
    }
}
