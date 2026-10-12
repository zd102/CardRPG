using CardRPG.Core;
using Godot;
using System;

namespace CardRPG;

/// <summary>A single place, with physical props and exits aligned to its HUD interactions.</summary>
public partial class ExpeditionLocalScene : Control
{
    public const float Left = 240, Right = 240, Top = 274, Bottom = 192;
    public const float ExitTop = 260, ExitBottom = 170;
    public string Location { get; set; } = "gate";
    public string[] SiteIds { get; set; } = [];
    public bool Powered { get; set; }
    private static readonly Color Brass = new("d8b77b");
    private static readonly Color Steel = new("54716e");
    private static readonly Color Glow = new("8fcab8");
    public static Vector2 ActionPoint(string id) => id switch
    {
        "convoy" or "intel" => new(.2f, .08f),
        "prepare" or "alarm" => new(.8f, .08f),
        "patrol" => new(.2f, .46f), "safe" => new(.8f, .46f),
        "salvage" or "workshop" or "field_equipment" => new(.2f, .22f),
        "power" or "blessing" => new(.8f, .22f),
        "boss" => new(.5f, .46f), "extract" => new(.5f, .25f),
        "heal" => new(.2f, .84f), "wait" => new(.8f, .84f),
        _ => new(.5f, .46f)
    };
    public static Vector2 ExitPoint(int count, int index) => count switch
    {
        2 => new(index == 0 ? .09f : .91f, .5f),
        3 => index switch { 0 => new(.09f, .25f), 1 => new(.09f, .80f), _ => new(.91f, .5f) },
        _ => new(index < 2 ? .09f : .91f, index % 2 == 0 ? .25f : .80f)
    };
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; ClipContents = true; Resized += QueueRedraw; }
    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        DrawSetTransform(Vector2.Zero, 0, Size / new Vector2(1000, 600));
        Environment();
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var neighbors = ExpeditionContent.Places[Location].Neighbors;
        for (var i = 0; i < neighbors.Length; i++)
        {
            var point = ExitPoint(neighbors.Length, i);
            var position = new Vector2(point.X * Size.X, ExitTop + point.Y * (Size.Y - ExitTop - ExitBottom));
            Exit(position, point.X < .5f);
        }
        foreach (var id in SiteIds)
        {
            var p = new Vector2(Left, Top) + ActionPoint(id) * (Size - new Vector2(Left + Right, Top + Bottom));
            Prop(id, p);
        }
    }
    private void Environment()
    {
        for (var y = 0; y < 600; y += 4)
            DrawRect(new Rect2(0, y, 1000, 4), new Color("254550").Lerp(new Color("10232b"), y / 600f));
        var interior = Location is "workshop" or "furnace" or "exit";
        for (var i = 0; i < 12; i++)
        {
            var x = i * 92 - 20; var height = 70 + i * 37 % 100;
            DrawRect(new Rect2(x, 245 - height, 74, height + 30), new Color("28434a"));
            for (var j = 0; j < 3; j++) DrawRect(new Rect2(x + 10 + j * 21, 230 - height, 8, 22), new Color("b5b17c22"));
        }
        DrawColoredPolygon([new(0, 278), new(1000, 250), new(1000, 600), new(0, 600)], new Color("152e34"));
        for (var i = 0; i < 9; i++) DrawLine(new(500, 252), new(-500 + i * 250, 600), new Color("65807712"), 1, true);
        for (var i = 0; i < 7; i++) DrawLine(new(0, 315 + i * 47), new(1000, 290 + i * 47), new Color("65807712"), 1, true);
        if (interior)
        {
            for (var i = 0; i < 5; i++)
            {
                DrawRect(new Rect2(55 + i * 228, 15, 18, 310), new Color("233b43"));
                DrawLine(new(60 + i * 228, 50), new(150 + i * 228, 135), new Color("35545a"), 8, true);
            }
            DrawLine(new(0, 52), new(1000, 52), new Color("3f6063"), 15, true);
        }
        switch (Location)
        {
            case "gate":
                DrawColoredPolygon([new(355, 286), new(503, 139), new(680, 284)], new Color("4b655c"));
                DrawColoredPolygon([new(503, 139), new(680, 284), new(729, 261), new(566, 123)], new Color("3a514d"));
                DrawColoredPolygon([new(460, 286), new(505, 179), new(554, 286)], new Color("122831"));
                DrawLine(new(504, 135), new(504, 291), new Color("a69b72"), 3, true);
                DrawLine(new(333, 203), new(720, 182), new Color("7e8a70"), 2, true);
                for (var i = 0; i < 5; i++) DrawCircle(new(346 + i * 88, 202 - i * 5), 4, Brass);
                BackgroundCrate(292, 278, 52, 37); BackgroundCrate(673, 261, 57, 41);
                break;
            case "square":
                DrawRect(new Rect2(270, 163, 483, 129), new Color("29464c"));
                DrawRect(new Rect2(493, 202, 106, 89), new Color("142c33"));
                for (var i = 0; i < 11; i++) DrawRect(new Rect2(287 + i * 41, 179, 18, 38), new Color("78958438"));
                DrawLine(new(222, 104), new(222, 304), Steel, 11, true);
                DrawLine(new(783, 104), new(783, 283), Steel, 11, true);
                DrawLine(new(222, 104), new(783, 104), Steel, 13, true);
                DrawLine(new(649, 111), new(649, 206), Brass, 2, true);
                DrawArc(new(649, 209), 9, 0, Mathf.Pi, 18, Brass, 3, true);
                BackgroundCrate(148, 297, 62, 48); BackgroundCrate(784, 295, 76, 47);
                break;
            case "scrap":
                for (var i = 0; i < 10; i++) BackgroundCrate(153 + i * 73, 224 + i % 3 * 19, 74, 24 + i % 4 * 16);
                DrawPolyline([new(337, 315), new(510, 348), new(715, 282), new(831, 360)], new Color("639084"), 4, true);
                for (var i = 0; i < 5; i++) DrawArc(new(590 + i * 31, 206), 23, Mathf.Pi, Mathf.Tau, 24, Steel, 6, true);
                break;
            case "workshop":
                DrawRect(new Rect2(251, 143, 490, 145), new Color("223c43"));
                for (var i = 0; i < 3; i++)
                {
                    DrawRect(new Rect2(284 + i * 151, 158, 119, 98), new Color("172f37"));
                    DrawLine(new(284 + i * 151, 201), new(403 + i * 151, 201), Steel, 4, true);
                    for (var j = 0; j < 3; j++) DrawRect(new Rect2(297 + i * 151 + j * 33, 174, 21, 25), new Color("647768"));
                }
                DrawLine(new(205, 134), new(800, 134), Steel, 7, true);
                DrawCircle(new(497, 144), 18, new Color("e0bd7020"));
                DrawRect(new Rect2(480, 138, 34, 7), Brass);
                break;
            case "furnace":
                for (var i = 4; i > 0; i--) DrawCircle(new(510, 250), 52 + i * 30, new Color("e4a05a06"));
                DrawRect(new Rect2(349, 138, 311, 183), new Color("33484a"));
                DrawColoredPolygon([new(328, 139), new(501, 66), new(681, 139)], new Color("3c5556"));
                DrawRect(new Rect2(440, 177, 137, 145), new Color("ad7547"));
                for (var i = 0; i < 6; i++) DrawRect(new Rect2(446 + i * 22, 181, 8, 138), new Color("394447"));
                DrawLine(new(366, 113), new(366, 24), Steel, 26, true);
                DrawLine(new(620, 115), new(620, 13), Steel, 31, true);
                break;
            case "exit":
                DrawRect(new Rect2(331, 126, 350, 215), new Color("102a34"));
                for (var i = 0; i < 6; i++) DrawLine(new(347 + i * 62, 107), new(347 + i * 62, 342), new Color("54736e"), 5, true);
                DrawLine(new(315, 116), new(695, 116), Steel, 15, true);
                DrawRect(new Rect2(325, 307, 366, 24), new Color("557069"));
                DrawLine(new(397, 334), new(249, 600), Steel, 3, true);
                DrawLine(new(624, 334), new(748, 600), Steel, 3, true);
                DrawCircle(new(694, 135), 8, Powered ? Glow : new Color("bf8061"));
                break;
        }
        for (var i = 0; i < 31; i++) DrawCircle(new(63 + i * 83 % 890, 178 + i * 71 % 383), i % 3 == 0 ? 1.3f : .7f, new Color("b6c4a620"));
    }
    private void BackgroundCrate(float x, float y, float width, float height)
    {
        DrawRect(new Rect2(x, y - height, width, height), new Color("314a48"));
        DrawRect(new Rect2(x, y - height, width, height), new Color("71806a"), false, 1);
        DrawLine(new(x + 3, y - height + 3), new(x + width - 3, y - 3), new Color("65745f"), 2, true);
    }
    private void Exit(Vector2 p, bool left)
    {
        DrawColoredPolygon([p + new Vector2(-68, 56), p + new Vector2(68, 56), p + new Vector2(left ? -140 : 215, 136), p + new Vector2(left ? -215 : 140, 136)], new Color("75938814"));
        DrawLine(p + new Vector2(0, -65), p + new Vector2(0, -21), Steel, 5, true);
        var sign = left ? new Vector2[] { new(-39, -65), new(-22, -77), new(40, -77), new(40, -53), new(-22, -53) }
            : [new(-40, -77), new(22, -77), new(39, -65), new(22, -53), new(-40, -53)];
        for (var i = 0; i < sign.Length; i++) sign[i] += p;
        DrawColoredPolygon(sign, new Color("537b70"));
        DrawPolyline([p + new Vector2(left ? 10 : -10, -72), p + new Vector2(left ? -7 : 7, -65), p + new Vector2(left ? 10 : -10, -58)], Glow, 2, true);
    }
    private void Prop(string id, Vector2 p)
    {
        DrawColoredPolygon([p + new Vector2(-63, 5), p + new Vector2(-10, -8), p + new Vector2(72, 6), p + new Vector2(15, 17)], new Color("07151d70"));
        switch (id)
        {
            case "convoy": case "patrol": case "boss":
                DrawRect(new Rect2(p + new Vector2(-23, -40), new Vector2(46, 38)), Steel);
                DrawRect(new Rect2(p + new Vector2(-14, -62), new Vector2(28, 22)), Steel.Lightened(.15f));
                DrawRect(new Rect2(p + new Vector2(-9, -54), new Vector2(18, 4)), Brass);
                DrawRect(new Rect2(p + new Vector2(-19, -2), new Vector2(13, 11)), Steel.Darkened(.3f));
                DrawRect(new Rect2(p + new Vector2(6, -2), new Vector2(13, 11)), Steel.Darkened(.3f));
                DrawCircle(p + new Vector2(0, -23), id == "boss" ? 12 : 8, new Color("172b31"));
                DrawCircle(p + new Vector2(0, -23), id == "boss" ? 7 : 4, Brass);
                DrawLine(p + new Vector2(35, -40), p + new Vector2(35, 8), Brass, 4, true);
                DrawRect(new Rect2(p + new Vector2(20, -46), new Vector2(30, 15)), Steel.Lightened(.2f));
                break;
            case "safe":
                DrawRect(new Rect2(p + new Vector2(-37, -48), new Vector2(74, 53)), new Color("476662"));
                DrawRect(new Rect2(p + new Vector2(-29, -42), new Vector2(56, 43)), Brass, false, 2);
                DrawCircle(p + new Vector2(4, -22), 12, new Color("16303a"));
                DrawArc(p + new Vector2(4, -22), 10, 0, Mathf.Tau, 24, Brass, 2, true);
                DrawLine(p + new Vector2(-19, -33), p + new Vector2(-19, -11), Brass, 3, true);
                break;
            case "blessing":
                for (var i = 2; i >= 0; i--) DrawArc(p + new Vector2(0, -32), 15 + i * 10, -.4f, 5.5f, 40, i == 1 ? Glow : Brass, 2, true);
                DrawColoredPolygon([p + new Vector2(0, -51), p + new Vector2(12, -32), p + new Vector2(0, -13), p + new Vector2(-12, -32)], Glow);
                DrawRect(new Rect2(p + new Vector2(-22, 0), new Vector2(44, 7)), Steel);
                break;
            case "heal":
                DrawRect(new Rect2(p + new Vector2(-25, -34), new Vector2(50, 36)), new Color("365f58"));
                DrawRect(new Rect2(p + new Vector2(-12, -42), new Vector2(24, 8)), Glow, false, 2);
                DrawLine(p + new Vector2(0, -26), p + new Vector2(0, -6), Glow, 4, true);
                DrawLine(p + new Vector2(-10, -16), p + new Vector2(10, -16), Glow, 4, true);
                break;
            case "wait":
                DrawLine(p + new Vector2(-17, 8), p + new Vector2(0, -22), Steel, 3, true);
                DrawLine(p + new Vector2(18, 8), p + new Vector2(0, -22), Steel, 3, true);
                DrawLine(p + new Vector2(0, -22), p + new Vector2(0, -44), Steel, 4, true);
                DrawLine(p + new Vector2(-24, -46), p + new Vector2(23, -34), Brass, 11, true);
                break;
            case "power":
                DrawRect(new Rect2(p + new Vector2(-30, -55), new Vector2(60, 60)), Steel);
                DrawRect(new Rect2(p + new Vector2(-22, -46), new Vector2(44, 29)), new Color("173339"));
                DrawCircle(p + new Vector2(-9, -31), 5, Powered ? Glow : Brass);
                DrawLine(p + new Vector2(13, -39), p + new Vector2(7, -23), Brass, 3, true);
                break;
            case "alarm":
                DrawLine(p + new Vector2(-32, 6), p + new Vector2(-32, -60), Steel, 5, true);
                DrawLine(p + new Vector2(-32, -60), p + new Vector2(22, -60), Steel, 5, true);
                DrawColoredPolygon([p + new Vector2(-9, -53), p + new Vector2(10, -53), p + new Vector2(19, -25), p + new Vector2(-18, -25)], Brass);
                DrawLine(p + new Vector2(0, -25), p + new Vector2(0, 4), Brass, 2, true);
                break;
            case "workshop": case "intel":
                DrawRect(new Rect2(p + new Vector2(-45, -19), new Vector2(90, 9)), Steel.Lightened(.15f));
                DrawLine(p + new Vector2(-34, -10), p + new Vector2(-34, 9), Steel, 5, true);
                DrawLine(p + new Vector2(34, -10), p + new Vector2(34, 9), Steel, 5, true);
                if (id == "intel") DrawRect(new Rect2(p + new Vector2(-22, -35), new Vector2(44, 14)), new Color("b7b593"));
                else { DrawLine(p + new Vector2(-20, -24), p + new Vector2(9, -41), Brass, 4, true); DrawRect(new Rect2(p + new Vector2(2, -46), new Vector2(25, 11)), Steel.Lightened(.3f)); }
                break;
            case "extract":
                DrawLine(p + new Vector2(-46, 3), p + new Vector2(-46, -58), Steel, 6, true);
                DrawLine(p + new Vector2(46, 3), p + new Vector2(46, -58), Steel, 6, true);
                DrawLine(p + new Vector2(-46, -58), p + new Vector2(46, -58), Steel, 7, true);
                DrawPolyline([p + new Vector2(-16, -39), p + new Vector2(12, -24), p + new Vector2(-16, -9)], Location == "exit" && !Powered ? Brass : Glow, 4, true);
                break;
            default:
                BackgroundCrate(p.X - 38, p.Y + 1, 49, 34);
                BackgroundCrate(p.X + 7, p.Y + 5, 41, 25);
                break;
        }
        DrawLine(p + new Vector2(0, 5), p + new Vector2(0, 14), new Color("aec2a970"), 1, true);
    }
}
