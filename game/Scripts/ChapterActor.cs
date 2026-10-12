using Godot;

namespace CardRPG;

/// <summary>Generated pixel actor in world space, with a native selection marker.</summary>
public partial class ChapterActor : Control
{
    public string Art { get; set; } = "enemy-E01";
    public bool Selected { get; set; }
    public bool Defeated { get; set; }
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest; Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        var center = new Vector2(Size.X / 2, Size.Y - 12);
        Vector2[] ground = [center + new Vector2(-Size.X * .34f, 0), center + new Vector2(0, -16),
            center + new Vector2(Size.X * .34f, 0), center + new Vector2(0, 16)];
        DrawColoredPolygon(ground, new Color("0711269c"));
        if (Selected && !Defeated)
            DrawPolyline([ground[0], ground[1], ground[2], ground[3], ground[0]], new Color("ffd789"), 2);
        var texture = ChapterArt.Load(Art);
        if (texture == null) return;
        var scale = Mathf.Min(Size.X / texture.GetWidth(), (Size.Y - 14) / texture.GetHeight());
        var drawn = texture.GetSize() * scale;
        DrawTextureRect(texture, new Rect2(new Vector2((Size.X - drawn.X) / 2, Size.Y - 18 - drawn.Y), drawn),
            false, Defeated ? new Color(.5f, .5f, .55f, .35f) : Colors.White);
    }
}
