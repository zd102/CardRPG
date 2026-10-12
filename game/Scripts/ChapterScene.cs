using Godot;
using System.Collections.Generic;
using CardRPG.Core;

namespace CardRPG;

/// <summary>Full-bleed generated pixel scenery with persistent world state.</summary>
public partial class ChapterScene : Control
{
    public string Location { get; set; } = "camp";
    public bool Lit { get; set; }
    public bool Robot { get; set; }
    public bool Rescued { get; set; }
    public bool Clothes { get; set; }
    public bool Door { get; set; }
    public bool Powered { get; set; }
    public bool Map { get; set; }
    public bool ShowDoor { get; set; }
    public bool ShowTrain { get; set; }
    public string ArtName => Location switch
    {
        "camp" when Lit => "camp-lit", "A03" when Rescued => "A03-rescued",
        "A04" when Door => "A04-dark", "A05" when Powered => "A05-powered", _ => Location
    };
    // Use the same cover transform for scenery, props and interaction markers.
    public Rect2 SceneryRect()
    {
        var texture = ChapterArt.Load(ArtName) ?? ChapterArt.Load(Location);
        if (texture == null) return new Rect2(Vector2.Zero, Size);
        var scale = Mathf.Max(Size.X / texture.GetWidth(), Size.Y / texture.GetHeight());
        var drawn = texture.GetSize() * scale;
        return new Rect2((Size - drawn) / 2, drawn);
    }
    public Vector2 ScenePoint(Vector2 point)
    {
        var rect = SceneryRect(); return rect.Position + rect.Size * point;
    }
    public static readonly Dictionary<string, Vector2> MapPoints = new()
    {
        ["A01"] = new(.16f, .49f), ["A02"] = new(.37f, .36f), ["A03"] = new(.35f, .66f),
        ["A04"] = new(.59f, .51f), ["A05"] = new(.61f, .76f), ["A06"] = new(.83f, .61f), ["A07"] = new(.68f, .30f)
    };
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ClipContents = true;
        TextureFilter = TextureFilterEnum.Nearest; Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        var texture = ChapterArt.Load(ArtName) ?? ChapterArt.Load(Location);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("182c3a"));
        if (texture != null)
            DrawTextureRect(texture, SceneryRect(), false);
        if (Map) { DrawMap(); return; }
        if (Location == "camp")
        {
            if (Clothes) DrawProp("clothes-rack", new(.36f, .66f), .23f);
            if (Robot) DrawProp("repair-robot", new(.53f, .745f), .15f);
        }
    }
    private void DrawProp(string name, Vector2 feet, float heightRatio)
    {
        if (ChapterArt.Load(name) is not { } texture) return;
        var height = SceneryRect().Size.Y * heightRatio;
        var width = height * texture.GetWidth() / texture.GetHeight();
        DrawTextureRect(texture, new Rect2(ScenePoint(feet) - new Vector2(width / 2, height), new Vector2(width, height)), false);
    }
    private void DrawMap()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0e1b28ed"));
        for (var x = 0; x < Size.X; x += 32) DrawLine(new(x, 120), new(x, Size.Y), new Color("91b9b50a"));
        for (var y = 120; y < Size.Y; y += 32) DrawLine(new(0, y), new(Size.X, y), new Color("91b9b50a"));
        bool Visible(string id) => (id != "A06" || ShowDoor) && (id != "A07" || ShowTrain);
        foreach (var p in ChapterContent.Places.Values)
        foreach (var next in p.Neighbors)
        {
            if (string.CompareOrdinal(p.Id, next) > 0 || !Visible(p.Id) || !Visible(next)) continue;
            DrawDashedLine(MapPoints[p.Id] * Size, MapPoints[next] * Size, new Color("83a498"), 3, 9);
        }
        foreach (var (id, point) in MapPoints)
        {
            if (!Visible(id)) continue;
            var p = point * Size; var color = id == Location ? new Color("ffd588") : new Color("88bab0");
            DrawRect(new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), new Color("182c3a"));
            DrawRect(new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), color, false, 2);
            DrawRect(new Rect2(p - new Vector2(3, 3), new Vector2(6, 6)), color);
        }
    }
}
