using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;

namespace CardRPG;

/// <summary>Read-only district map. Non-interactive labels share the same bounds and positions.</summary>
public partial class ExpeditionRegion : Control
{
    public const float Left = 96, Top = 246, Right = 96, Bottom = 205;
    public static readonly IReadOnlyDictionary<string, Vector2> Points = new Dictionary<string, Vector2>
    {
        ["gate"] = new(.10f, .53f), ["square"] = new(.34f, .25f), ["scrap"] = new(.32f, .82f),
        ["workshop"] = new(.61f, .20f), ["furnace"] = new(.85f, .34f), ["exit"] = new(.77f, .81f)
    };
    public string Location { get; set; } = "gate";
    public bool Powered { get; set; }
    private static readonly Color Brass = new("d7ba7b");
    private static readonly Color Metal = new("496463");
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; ClipContents = true; Resized += QueueRedraw; }
    private Vector2 PositionOf(string id) => new Vector2(Left, Top) + Points[id] * (Size - new Vector2(Left + Right, Top + Bottom));
    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        for (var y = 0f; y < Size.Y; y += 4)
            DrawRect(new Rect2(0, y, Size.X, 4), new Color("203d47").Lerp(new Color("142a30"), y / Size.Y));
        // Distant chimneys, haze, perimeter pipes and service railways continue beneath the HUD.
        for (var i = 0; i < 21; i++)
        {
            var x = i * Size.X / 20;
            var h = 40 + i * 37 % 110;
            DrawRect(new Rect2(x - 30, 170 - h, 58, h + 20), new Color("253f48"));
            if (i % 3 == 0)
            {
                DrawRect(new Rect2(x, 85 - h / 3, 11, 100), new Color("34525a"));
                DrawCircle(new(x + 16, 78 - h / 3), 25, new Color("a0b7a90b"));
            }
        }
        DrawColoredPolygon([new(0, 198), new(Size.X, 150), new(Size.X, Size.Y), new(0, Size.Y)], new Color("142c32"));
        for (var i = 0; i < 15; i++)
        {
            var y = 235 + i * 43;
            DrawLine(new(0, y), new(Size.X, y - 120), new Color("597c7010"), 1, true);
        }
        DrawLine(new(Size.X * .55f, Size.Y + 20), new(Size.X * .86f, 115), new Color("425d5b"), 9, true);
        DrawLine(new(Size.X * .55f + 20, Size.Y + 20), new(Size.X * .86f + 20, 115), new Color("425d5b"), 3, true);
        var visited = new HashSet<string>();
        foreach (var (id, place) in ExpeditionContent.Places)
        {
            foreach (var neighbor in place.Neighbors)
            {
                var edge = string.CompareOrdinal(id, neighbor) < 0 ? id + neighbor : neighbor + id;
                if (!visited.Add(edge)) continue;
                var a = PositionOf(id) + new Vector2(0, 8); var b = PositionOf(neighbor) + new Vector2(0, 8);
                DrawLine(a, b, new Color("0c2028"), 21, true);
                DrawLine(a, b, new Color("3a5555"), 14, true);
                var active = id == Location || neighbor == Location;
                DrawDashedLine(a, b, active ? new Color("c6b581b0") : new Color("83988760"), 2, 9, true);
            }
        }
        foreach (var (id, _) in Points) Landmark(id, PositionOf(id));
        // Quiet foreground debris adds depth without covering the interactive ground plane.
        for (var i = 0; i < 8; i++)
        {
            var p = new Vector2(26 + i * 170, Size.Y - 39 - i % 3 * 12);
            Block(p, 90, 36, 19, new Color("223c40"), new Color("3e5654"));
        }
        for (var i = 0; i < 45; i++)
            DrawCircle(new Vector2((i * 97 + 23) % (int)Size.X, 190 + (i * 67) % Math.Max(1, (int)Size.Y - 240)), i % 3 == 0 ? 1.5f : .7f, new Color("bfd1ba26"));
    }
    private void Landmark(string id, Vector2 p)
    {
        var current = id == Location;
        var rim = current ? Brass : new Color("6d8580");
        DrawColoredPolygon([p + new Vector2(-100, -12), p + new Vector2(-16, -55), p + new Vector2(112, 10), p + new Vector2(15, 53)], new Color("0b1b2380"));
        if (current)
        {
            DrawColoredPolygon([p + new Vector2(-96, 0), p + new Vector2(0, -46), p + new Vector2(96, 0), p + new Vector2(0, 47)], new Color("d6b77516"));
            DrawPolyline([p + new Vector2(-96, 0), p + new Vector2(0, -46), p + new Vector2(96, 0), p + new Vector2(0, 47), p + new Vector2(-96, 0)], new Color(Brass, .75f), 1.5f, true);
        }
        switch (id)
        {
            case "gate":
                Block(p + new Vector2(-20, 0), 85, 42, 35, Metal, rim);
                DrawLine(p + new Vector2(23, -18), p + new Vector2(23, -81), rim, 4, true);
                DrawColoredPolygon([p + new Vector2(23, -81), p + new Vector2(63, -72), p + new Vector2(23, -58)], new Color("b6a36d"));
                Block(p + new Vector2(39, 7), 28, 18, 17, new Color("627062"), rim);
                break;
            case "square":
                Block(p, 121, 55, 48, Metal, rim);
                Block(p + new Vector2(-52, 22), 34, 20, 20, new Color("7b7657"), rim);
                Block(p + new Vector2(-25, 36), 28, 18, 17, new Color("716f55"), rim);
                DrawLine(p + new Vector2(-72, -4), p + new Vector2(-72, -99), rim, 5, true);
                DrawLine(p + new Vector2(-72, -99), p + new Vector2(72, -55), rim, 5, true);
                DrawLine(p + new Vector2(42, -63), p + new Vector2(42, -28), Brass, 1.5f, true);
                DrawArc(p + new Vector2(42, -23), 5, 0, Mathf.Pi, 15, Brass, 2, true);
                break;
            case "scrap":
                for (var i = 0; i < 5; i++) Block(p + new Vector2(-44 + i * 23, i % 2 * 19 - 12), 46, 25, 15 + i % 3 * 8, new Color("526b61"), rim);
                DrawLine(p + new Vector2(47, -1), p + new Vector2(47, -62), rim, 3, true);
                DrawLine(p + new Vector2(47, -62), p + new Vector2(15, -74), rim, 3, true);
                DrawCircle(p + new Vector2(15, -74), 5, new Color("96c9b5"));
                break;
            case "workshop":
                Block(p, 100, 52, 52, new Color("567570"), rim);
                Block(p + new Vector2(30, -10), 34, 22, 67, Metal, rim);
                DrawCircle(p + new Vector2(-16, -50), 12, new Color("112e36"));
                DrawArc(p + new Vector2(-16, -50), 10, 0, Mathf.Tau, 32, new Color("9aceb9"), 2, true);
                DrawLine(p + new Vector2(-16, -58), p + new Vector2(-16, -42), Brass, 2, true);
                break;
            case "furnace":
                Block(p, 115, 62, 71, new Color("425d5c"), rim);
                Block(p + new Vector2(-25, -16), 30, 23, 113, Metal, rim);
                Block(p + new Vector2(16, -30), 26, 19, 98, Metal, rim);
                DrawColoredPolygon([p + new Vector2(-7, -5), p + new Vector2(-7, -47), p + new Vector2(22, -60), p + new Vector2(22, -16)], new Color("bb8a58"));
                DrawLine(p + new Vector2(7, -12), p + new Vector2(7, -53), new Color("e0b376"), 2, true);
                for (var i = 0; i < 4; i++) DrawCircle(p + new Vector2(-27 + i * 5, -136 - i * 9), 12 + i * 3, new Color("abc2b008"));
                break;
            case "exit":
                Block(p, 128, 48, 28, Metal, rim);
                for (var i = 0; i < 4; i++) DrawLine(p + new Vector2(-50 + i * 31, 0), p + new Vector2(-50 + i * 31, -78), rim, 3, true);
                DrawLine(p + new Vector2(-52, -78), p + new Vector2(44, -78), rim, 4, true);
                DrawCircle(p + new Vector2(46, -83), 5, Powered ? new Color("97d5bd") : new Color("bb8669"));
                break;
        }
        DrawLine(p + new Vector2(0, 21), p + new Vector2(0, 35), current ? Brass : rim, 1.5f, true);
    }
    private void Block(Vector2 p, float width, float depth, float height, Color fill, Color edge)
    {
        var a = p + new Vector2(-width / 2, 0); var b = p + new Vector2(0, -depth / 2);
        var c = p + new Vector2(width / 2, 0); var d = p + new Vector2(0, depth / 2); var up = new Vector2(0, -height);
        DrawColoredPolygon([a, d, d + up, a + up], fill.Darkened(.25f));
        DrawColoredPolygon([d, c, c + up, d + up], fill.Darkened(.45f));
        DrawColoredPolygon([a + up, b + up, c + up, d + up], fill);
        DrawPolyline([a, a + up, b + up, c + up, c, d, a], new Color(edge, .6f), 1, true);
        DrawPolyline([a + up, d + up, c + up], new Color(edge, .65f), 1, true);
        DrawLine(d, d + up, new Color(edge, .55f), 1, true);
        if (height < 30) return;
        for (var i = 1; i <= 3; i++)
        {
            var position = a.Lerp(d, i / 4f) + new Vector2(0, -height * .58f);
            DrawLine(position, position + new Vector2(0, 8), new Color("c4b77990"), 4, true);
        }
    }
}
