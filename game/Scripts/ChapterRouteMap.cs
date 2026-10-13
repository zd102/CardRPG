using Godot;
using CardRPG.Core;
using System.Collections.Generic;

namespace CardRPG;

/// <summary>Read-only route sheet. Shares the discovered graph, never movement actions.</summary>
public partial class ChapterRouteMap : Control
{
    public string Current { get; set; } = "";
    public HashSet<string> Revealed { get; set; } = new();
    public bool StationPowered { get; set; }
    private StyleBoxTexture _paper = null!;
    private static readonly Dictionary<string, Vector2> Points = new()
    {
        ["A01"] = new(.13f, .42f), ["A02"] = new(.36f, .23f), ["A03"] = new(.35f, .70f),
        ["A04"] = new(.62f, .52f), ["A05"] = new(.67f, .91f), ["A06"] = new(.87f, .52f), ["A07"] = new(.64f, .13f)
    };
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Linear;
        _paper = ChapterSkin.Box("ledger-paper");
        Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        _paper.Draw(GetCanvasItem(), new Rect2(Vector2.Zero, Size));
        var font = GetThemeDefaultFont(); var ink = ChapterSkin.PaperInk;
        Vector2 Point(string id) => new Vector2(24, 28) + Points[id] * (Size - new Vector2(48, 75));
        foreach (var p in ChapterContent.Places.Values)
        foreach (var next in p.Neighbors)
        {
            if (string.CompareOrdinal(p.Id, next) >= 0 || !Revealed.Contains(p.Id) || !Revealed.Contains(next)) continue;
            DrawDashedLine(Point(p.Id), Point(next), new Color("64715b"), 2, 7);
        }
        foreach (var id in Revealed)
        {
            var p = Point(id); var current = id == Current;
            var safe = id == "A01" || id == "A05" && StationPowered;
            var texture = ChapterArt.Load(id);
            if (texture != null)
            {
                var height = Mathf.Clamp(Size.Y * .13f, 32, 52);
                var photo = new Rect2(p - new Vector2(44, height + 9), new Vector2(88, height));
                DrawRect(photo.Grow(4), new Color("e7dcc2"));
                DrawTextureRect(texture, photo, false);
                DrawRect(photo.Grow(4), current ? new Color("a84c37") : new Color("8b967e"), false, current ? 3 : 1);
            }
            if (safe)
                DrawColoredPolygon([p + new Vector2(0, -12), p + new Vector2(12, 0), p + new Vector2(0, 12), p + new Vector2(-12, 0)], current ? new Color("9b542c") : ink);
            else DrawCircle(p, current ? 11 : 8, current ? new Color("9b542c") : ink);
            DrawCircle(p, 4, new Color("e2d0a3"));
            if (current) DrawArc(p, 16, 0, Mathf.Tau, 32, ink, 2, true);
            var title = ChapterContent.Places[id].Name;
            var width = font.GetStringSize(title, fontSize: 16).X;
            DrawString(font, p + new Vector2(-width / 2, 29), title, fontSize: 16, modulate: ink);
            var state = current ? "当前位置" : safe ? "安全撤离" : id;
            var subwidth = font.GetStringSize(state, fontSize: 12).X;
            DrawString(font, p + new Vector2(-subwidth / 2, 47), state, fontSize: 12, modulate: ChapterSkin.PaperMuted);
        }
    }
}
