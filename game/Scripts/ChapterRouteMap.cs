using Godot;
using CardRPG.Core;
using System.Collections.Generic;
using System;
using System.Linq;

namespace CardRPG;

/// <summary>Read-only route sheet. Shares the discovered graph, never movement actions.</summary>
public partial class ChapterRouteMap : Control
{
    public string Current { get; set; } = "";
    public HashSet<string> Revealed { get; set; } = new();
    public bool StationPowered { get; set; }
    public List<string> Route { get; set; } = new();
    public string Selected { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Waypoint { get; set; } = "";
    public string Patrol { get; set; } = "";
    public Dictionary<string, int> GroundQuantities { get; set; } = new();
    public bool Deployment { get; set; }
    public float ReadingScale { get; set; } = 1;
    public event Action<string>? PlaceSelected;
    private StyleBoxTexture _paper = null!;
    private static readonly Dictionary<string, Vector2> Points = new()
    {
        ["A01"] = new(.13f, .42f), ["A02"] = new(.36f, .23f), ["A03"] = new(.35f, .70f),
        ["A04"] = new(.62f, .52f), ["A05"] = new(.67f, .91f), ["A06"] = new(.87f, .52f), ["A07"] = new(.64f, .13f)
    };
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop; FocusMode = FocusModeEnum.All; TextureFilter = TextureFilterEnum.Linear;
        _paper = ChapterSkin.Box("ledger-paper");
        Resized += QueueRedraw;
    }
    private Vector2 Point(string id)
    {
        if (Compact)
        {
            var position = id switch
            {
                "A01" => new Vector2(.25f, .04f), "A03" => new Vector2(.75f, .04f),
                "A02" => new Vector2(.25f, .31f), "A04" => new Vector2(.75f, .31f),
                "A07" => new Vector2(.25f, .58f), "A05" => new Vector2(.75f, .58f),
                _ => new Vector2(.75f, .85f)
            };
            return new Vector2(position.X * Size.X, 20 + position.Y * (Size.Y - 110));
        }
        return new Vector2(24, 28) + Points[id] * (Size - new Vector2(48, 100));
    }
    private bool Compact => Size.X / ReadingScale < 720;
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse)
        {
            var id = Revealed.OrderBy(id => Point(id).DistanceSquaredTo(mouse.Position)).FirstOrDefault();
            if (id != null && new Rect2(Point(id) - new Vector2(55, 60), new Vector2(110, 116)).HasPoint(mouse.Position))
            { AcceptEvent(); PlaceSelected?.Invoke(id); }
        }
        if (input is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var places = Revealed.OrderBy(id => Points[id].X).ThenBy(id => Points[id].Y).ToArray();
            if (places.Length == 0) return;
            var next = (Array.IndexOf(places, Selected) + (key.Keycode is Key.Right or Key.Down ? 1 : places.Length - 1) + places.Length) % places.Length;
            AcceptEvent(); PlaceSelected?.Invoke(places[next]);
        }
    }
    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        _paper.Draw(GetCanvasItem(), new Rect2(Vector2.Zero, Size));
        var font = GetThemeDefaultFont(); var ink = ChapterSkin.PaperInk;
        var compact = Compact;
        foreach (var p in ChapterContent.Places.Values)
        foreach (var next in p.Neighbors)
        {
            if (string.CompareOrdinal(p.Id, next) >= 0 || !Revealed.Contains(p.Id) || !Revealed.Contains(next)) continue;
            var from = Point(p.Id); var to = Point(next);
            var bypass = compact && Math.Abs(from.X - to.X) < 1 && Math.Abs(from.Y - to.Y) > (Size.Y - 110) * .3f;
            var points = bypass ? new[] { from, from + new Vector2(70, 0), to + new Vector2(70, 0), to } : new[] { from, to };
            for (var n = 1; n < points.Length; n++) DrawDashedLine(points[n - 1], points[n], new Color("64715b"), 2, 7);
            if (Route.Contains(p.Id) && Route.Contains(next) && System.Math.Abs(Route.IndexOf(p.Id) - Route.IndexOf(next)) == 1)
                DrawPolyline(points, new Color("446249"), 4, true);
        }
        foreach (var id in Revealed)
        {
            var p = Point(id); var current = id == Current;
            var safe = id == "A01" || id == "A05" && StationPowered;
            var priority=current || id==Selected || id==Waypoint || id==Objective || safe;
            var texture = ChapterArt.Load(id);
            if (texture != null && !compact)
            {
                var height = Mathf.Clamp(Size.Y * .13f, 28, compact ? 38 : 52);
                var photoWidth = compact || !priority ? 64 : 88;
                var photo = new Rect2(p - new Vector2(photoWidth / 2f, height + 9), new Vector2(photoWidth, height));
                DrawRect(photo.Grow(4), new Color("e7dcc2"));
                DrawTextureRect(texture, photo, false, priority?Colors.White:new Color(.78f,.78f,.72f,.8f));
                DrawRect(photo.Grow(4), current ? new Color("a84c37") : new Color("8b967e"), false, current ? 3 : 1);
            }
            if (safe)
                DrawColoredPolygon([p + new Vector2(0, -12), p + new Vector2(12, 0), p + new Vector2(0, 12), p + new Vector2(-12, 0)], current ? new Color("9b542c") : ink);
            else DrawCircle(p, current ? 11 : 8, current ? new Color("9b542c") : ink);
            DrawCircle(p, 4, new Color("e2d0a3"));
            if (current) DrawArc(p, 16, 0, Mathf.Tau, 32, ink, 2, true);
            if (id == Selected) DrawRect(new Rect2(p - new Vector2(20, 20), new Vector2(40, 40)), ink, false, 2);
            var title = ChapterContent.Places[id].Name;
            var fontSize = (int)((priority?16:14) * ReadingScale); var subSize = (int)(14 * ReadingScale);
            var width = font.GetStringSize(title, fontSize: fontSize).X;
            var labelX = Mathf.Clamp(p.X - width / 2, 10, Size.X - width - 10);
            var state = string.Join(" · ", new[] { Deployment && id == Selected ? "拟部署" : current ? "当前" : "", id == Waypoint ? "标记" : id == Objective ? "任务" : "", safe ? "撤离" : "" }.Where(s => s != ""));
            var marks = string.Join(" · ", new[] { GroundQuantities.GetValueOrDefault(id) > 0 ? "遗留 " + GroundQuantities[id] : "", id == Patrol ? "⚠ 巡逻" : "" }.Where(s => s != ""));
            var subwidth = font.GetStringSize(state, fontSize: subSize).X;
            var markwidth = font.GetStringSize(marks, fontSize: subSize).X;
            var labelWidth = Math.Max(width, Math.Max(subwidth, markwidth));
            DrawRect(new Rect2(Math.Clamp(p.X - labelWidth / 2 - 3, 3, Math.Max(3, Size.X - labelWidth - 6)), p.Y + 10, labelWidth + 6, fontSize + (state == "" ? 0 : subSize + 4) + (marks == "" ? 0 : subSize + 4) + 7), new Color("e7e0cb"));
            DrawString(font, new Vector2(labelX, p.Y + 29), title, fontSize: fontSize, modulate: ink);
            DrawString(font, p + new Vector2(-subwidth / 2, 29 + subSize + 4), state, fontSize: subSize, modulate: ChapterSkin.PaperMuted);
            DrawString(font, p + new Vector2(-markwidth / 2, 29 + (state == "" ? 1 : 2) * (subSize + 4)), marks, fontSize: subSize, modulate: id == Patrol ? new Color("9c362c") : ink);
        }
    }
}
