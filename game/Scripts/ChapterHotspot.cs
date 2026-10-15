using Godot;
using System;

namespace CardRPG;

/// <summary>A compact marker; pointer and keyboard focus reveal the same description.</summary>
public partial class ChapterHotspot : Button
{
    public Color Tint { get; set; } = new("d3ad68");
    public bool Exit { get; set; }
    public string Symbol { get; set; } = "loot";
    public string Caption { get; set; } = "";
    public bool Important { get; set; }
    public bool ShowLabel { get; set; }
    public Vector2 SceneAnchor { get; set; } = new(26,26);
    public Vector2 LabelOffset { get; set; }
    public int LabelSize { get; set; } = 14;
    public Rect2 LabelRect()
    {
        var width = Math.Max(60, GetThemeDefaultFont().GetStringSize(Caption, fontSize:LabelSize).X + 16);
        return new Rect2(Position + new Vector2((Size.X-width)/2,Size.Y+5) + LabelOffset, new Vector2(width,LabelSize+12));
    }
    public override string _GetTooltip(Vector2 atPosition) => "";
    public override bool _HasPoint(Vector2 point) => new Rect2(Vector2.Zero, Size).HasPoint(point)
        || (ShowLabel || Important || Exit || IsHovered() || HasFocus()) && new Rect2(LabelRect().Position - Position, LabelRect().Size).HasPoint(point);
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
        if (active && !Disabled)
        {
            DrawRect(new Rect2(SceneAnchor-new Vector2(48,64),new Vector2(96,100)),new Color(color,.10f));
            DrawRect(new Rect2(SceneAnchor-new Vector2(48,64),new Vector2(96,100)),new Color(color,.65f),false,2);
            DrawArc(SceneAnchor, 34, 0, Mathf.Tau, 40, new Color(color, .55f), 2, true);
        }
        if (SceneAnchor.DistanceTo(center) > 12) { DrawLine(center,SceneAnchor,new Color("f1e7ce"),3,true); DrawCircle(SceneAnchor,3,color); }
        if (Exit)
        {
            Vector2[] points = [center+new Vector2(0,-26), center+new Vector2(26,0), center+new Vector2(0,26), center+new Vector2(-26,0), center+new Vector2(0,-26)];
            DrawColoredPolygon(points, new Color("f1e7ce")); DrawPolyline(points, color, active ? 3 : 2);
        }
        else { DrawCircle(center,24,new Color("f1e7ce")); DrawArc(center,24,0,Mathf.Tau,32,color,active ? 3 : 2); }
        if (Important) DrawCircle(new Vector2(Size.X-3,3),5,new Color("ead3a0"));
        if (Disabled) DrawLine(center+new Vector2(-13,13),center+new Vector2(13,-13),color,2);
        if (!active && !Important && !Exit && !ShowLabel) return;
        var font = GetThemeDefaultFont(); var rect = LabelRect(); rect.Position -= Position;
        if (LabelOffset.LengthSquared() > 16) DrawLine(center, rect.GetCenter(), new Color("f1e7ce"), 2, true);
        DrawRect(rect,new Color("f1e7ce"));
        DrawString(font,rect.Position+new Vector2(8,LabelSize+5),Caption,fontSize:LabelSize,modulate:color);
    }
}
