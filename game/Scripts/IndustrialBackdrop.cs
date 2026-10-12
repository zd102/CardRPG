using Godot;
using System;

namespace CardRPG;

/// <summary>Code-native industrial artwork with optional generated texture overrides.</summary>
public partial class IndustrialBackdrop : Control
{
    public bool IsMap { get; set; }
    public string Location { get; set; } = "gate";
    private Texture2D? _art;
    private static readonly Color Brass = new("d3ad70");
    private static readonly Color Teal = new("78b6ac");
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        Resized += QueueRedraw;
        var asset = Location switch { "armory" => "armory", "echo" => "echo", "battle" => "sentinel", _ => "foundry" };
        var path = $"res://Art/Generated/{asset}.png";
        if (!IsMap && ResourceLoader.Exists(path)) _art = GD.Load<Texture2D>(path);
    }
    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        if (_art != null)
        {
            var source = _art.GetSize();
            var scale = Math.Max(Size.X / source.X, Size.Y / source.Y);
            var crop = Size / scale;
            DrawTextureRectRegion(_art, new Rect2(Vector2.Zero, Size), new Rect2((source - crop) / 2, crop));
            return;
        }
        DrawSetTransform(Vector2.Zero, 0, Size / new Vector2(1000, 500));
        for (var y = 0; y < 500; y += 4)
            DrawRect(new Rect2(0, y, 1000, 4), new Color("203c40").Lerp(new Color("0b151c"), y / 500f));
        if (IsMap) { Map(); return; }
        if (Location is "armory" or "echo")
        {
            var scale = Math.Min(Size.X / 800, Size.Y / 440);
            DrawSetTransform(new Vector2(Size.X * .7f - 690 * scale, Size.Y / 2 - 240 * scale), 0, Vector2.One * scale);
            if (Location == "armory") Armory(); else Echo();
            return;
        }
        Foundry();
        if (Location == "battle") Sentinel();
        DrawRect(new Rect2(0, 0, 1000, 500), new Color("64857c"), false, 1);
    }
    private void Map()
    {
        for (var x = 0; x < 1000; x += 40) DrawLine(new(x, 0), new(x, 500), new Color("ffffff06"));
        for (var y = 0; y < 500; y += 40) DrawLine(new(0, y), new(1000, y), new Color("ffffff06"));
        Vector2[] p = [new(100, 250), new(340, 120), new(340, 380), new(610, 120), new(860, 120), new(750, 380)];
        string[] ids = ["gate", "square", "scrap", "workshop", "furnace", "exit"];
        int[][] edges = [[0, 1], [0, 2], [1, 2], [1, 3], [2, 3], [2, 5], [3, 4], [4, 5]];
        foreach (var e in edges)
        {
            var active = ids[e[0]] == Location || ids[e[1]] == Location;
            DrawLine(p[e[0]], p[e[1]], active ? new Color("8f9e7d") : new Color("3a5558"), active ? 3 : 2, true);
            DrawCircle((p[e[0]] + p[e[1]]) / 2, 3, active ? Brass : new Color("537073"));
        }
        var current = Array.IndexOf(ids, Location);
        if (current >= 0) DrawCircle(p[current], 62, new Color("d3ad7012"));
    }
    private void Foundry()
    {
        DrawCircle(new(790, 110), 62, new Color("b7c8b214"));
        for (var i = 0; i < 17; i++)
        {
            var x = i * 67 - 25; var h = 60 + i * 43 % 100;
            DrawRect(new Rect2(x, 280 - h, 58, h + 110), new Color("263e40"));
            if (i % 3 == 0) DrawRect(new Rect2(x + 18, 115 - h / 2, 12, 200), new Color("2a4243"));
        }
        for (var i = 0; i < 10; i++)
        {
            var x = 60 + i * 114;
            DrawRect(new Rect2(x, 246 - i % 3 * 30, 92, 150), new Color("1a2e33"));
            for (var j = 0; j < 3; j++) DrawRect(new Rect2(x + 12 + j * 23, 270 - i % 3 * 30, 8, 22), new Color("c3a16a35"));
        }
        DrawColoredPolygon([new(0, 365), new(1000, 325), new(1000, 500), new(0, 500)], new Color("111f26"));
        var glow = Location is "furnace" or "battle" ? new Color("df8b4f") : Brass;
        DrawRect(new Rect2(530, 128, 254, 238), new Color("172a30"));
        DrawColoredPolygon([new(514, 129), new(651, 75), new(802, 129)], new Color("2e484a"));
        DrawRect(new Rect2(550, 147, 215, 204), new Color("2a4143"));
        for (var i = 0; i < 7; i++)
        {
            DrawRect(new Rect2(557 + i * 30, 159, 17, 70), new Color(glow, .26f));
            DrawLine(new(557 + i * 30, 182), new(574 + i * 30, 182), new Color("23383c"), 3);
            DrawLine(new(557 + i * 30, 204), new(574 + i * 30, 204), new Color("23383c"), 3);
        }
        DrawRect(new Rect2(608, 258, 93, 105), new Color("0c181e"));
        DrawRect(new Rect2(610, 260, 89, 101), new Color(glow, .5f));
        DrawLine(new(654, 260), new(654, 361), new Color("1e3034"), 5);
        for (var i = 0; i < 6; i++) DrawLine(new(545 + i * 44, 134), new(545 + i * 44, 366), new Color("476064"), 3);
        DrawLine(new(430, 95), new(430, 349), new Color("344c4d"), 18);
        DrawLine(new(830, 155), new(830, 330), new Color("344c4d"), 21);
        DrawLine(new(420, 95), new(821, 95), new Color("56706a"), 9);
        DrawLine(new(755, 99), new(755, 214), new Color("ad9b72"), 2);
        DrawArc(new(755, 219), 8, 0, Mathf.Pi, 16, Brass, 3, true);
        DrawLine(new(430, 133), new(526, 228), new Color("344c4d"), 4);
        DrawLine(new(817, 98), new(746, 166), new Color("344c4d"), 4);
        DrawLine(new(680, 366), new(390, 500), new Color("587172"), 3);
        DrawLine(new(710, 366), new(535, 500), new Color("587172"), 3);
        for (var i = 0; i < 9; i++)
        {
            var t = i / 9f;
            DrawLine(new(680 - 290 * t, 366 + 134 * t), new(710 - 175 * t, 366 + 134 * t), new Color("3f5556"), 4);
        }
        for (var i = 0; i < 4; i++) Crate(120 + i * 72, 357 - i % 2 * 26, 64, 61);
        DrawLine(new(52, 65), new(52, 460), new Color("101f25"), 19);
        DrawLine(new(0, 60), new(370, 60), new Color("102128"), 18);
        DrawLine(new(60, 73), new(169, 176), new Color("172b31"), 7);
        DrawLine(new(335, 64), new(335, 147), new Color("6f8175"), 2);
        DrawCircle(new(335, 153), 19, new Color("e9b96b10"));
        DrawRect(new Rect2(325, 147, 20, 9), Brass);
        if (Location == "scrap") { Crate(807, 336, 83, 63); Crate(846, 305, 66, 43); }
        if (Location == "exit")
        {
            DrawRect(new Rect2(799, 282, 170, 92), new Color("35504f"));
            for (var i = 0; i < 7; i++) DrawLine(new(811 + i * 22, 290), new(811 + i * 22, 363), new Color("192e32"), 3);
        }
        for (var i = 0; i < 35; i++) DrawCircle(new(110 + i * 79 % 840, 70 + i * 67 % 350), i % 3 == 0 ? 1.6f : .7f, new Color("d3c19540"));
    }
    private void Crate(float x, float y, float w, float h)
    {
        DrawRect(new Rect2(x, y, w, h), new Color("364441"));
        DrawRect(new Rect2(x, y, w, h), new Color("79836a"), false, 2);
        DrawLine(new(x + 7, y + 5), new(x + w - 7, y + h - 5), new Color("7a8266"), 3);
        DrawLine(new(x + w - 7, y + 5), new(x + 7, y + h - 5), new Color("7a8266"), 3);
    }
    private void Armory()
    {
        for (var i = 0; i < 8; i++) DrawLine(new(0, 70 + i * 57), new(1000, 20 + i * 57), new Color("73908210"), 2);
        DrawCircle(new(580, 240), 172, new Color("d3ad7008"));
        DrawArc(new(580, 240), 176, 0, Mathf.Tau, 96, new Color("71878050"), 1, true);
        DrawLine(new(365, 385), new(550, 173), new Color("0b161c"), 29);
        DrawLine(new(365, 380), new(550, 170), Brass, 13);
        DrawColoredPolygon([new(476, 99), new(604, 140), new(582, 208), new(454, 167)], new Color("78938c"));
        DrawPolyline([new(476, 99), new(604, 140), new(582, 208), new(454, 167), new(476, 99)], Brass, 3, true);
        DrawLine(new(663, 390), new(758, 128), new Color("ad936a"), 12);
        for (var i = 0; i < 5; i++) DrawArc(new(748 - i * 4, 135 + i * 12), 21, .1f, 5.9f, 32, Teal, 4, true);
        DrawCircle(new(767, 105), 14, new Color("8bdaca"));
        DrawRect(new Rect2(741, 292, 97, 104), new Color("3b5656"));
        DrawRect(new Rect2(741, 292, 97, 104), Brass, false, 4);
        DrawCircle(new(790, 343), 28, new Color("131f27"));
        DrawArc(new(790, 343), 21, 0, Mathf.Tau, 32, Brass, 3, true);
        DrawCircle(new(790, 343), 8, Teal);
        DrawLine(new(260, 429), new(901, 429), new Color("718780"), 2);
    }
    private void Echo()
    {
        var center = new Vector2(690, 227);
        for (var i = 6; i > 0; i--) DrawCircle(center, 24 + i * 26, new Color("7dd1bf07"));
        DrawLine(new(690, 140), new(690, 404), new Color("718680"), 14);
        DrawRect(new Rect2(591, 404, 198, 22), new Color("4b6360"));
        DrawRect(new Rect2(626, 373, 128, 31), new Color("2b4647"));
        for (var i = 0; i < 3; i++) DrawArc(center, 68 + 31 * i, -.8f + i * .65f, 4.8f + i * .65f, 80, i == 1 ? Teal : Brass, 4, true);
        for (var i = 0; i < 12; i++) DrawCircle(center + Vector2.FromAngle(i * Mathf.Tau / 12) * 132, 4, Brass);
        DrawColoredPolygon([center + new Vector2(0, -53), center + new Vector2(30, 0), center + new Vector2(0, 53), center + new Vector2(-30, 0)], Teal);
        DrawColoredPolygon([center + new Vector2(0, -53), center + new Vector2(30, 0), center + new Vector2(0, 53)], new Color("c9e8d4"));
    }
    private void Sentinel()
    {
        DrawRect(new Rect2(0, 0, 1000, 500), new Color("08121c80"));
        DrawColoredPolygon([new(609, 180), new(786, 180), new(825, 320), new(760, 384), new(635, 384), new(575, 320)], new Color("354b4d"));
        DrawPolyline([new(609, 180), new(786, 180), new(825, 320), new(760, 384)], Brass, 3, true);
        DrawRect(new Rect2(651, 108, 92, 91), new Color("51635d"));
        DrawRect(new Rect2(667, 141, 62, 8), Brass);
        DrawRect(new Rect2(619, 370, 53, 119), new Color("32464a"));
        DrawRect(new Rect2(730, 370, 53, 119), new Color("32464a"));
        DrawCircle(new(699, 273), 48, new Color("18282e"));
        DrawArc(new(699, 273), 40, 0, Mathf.Tau, 40, Brass, 5, true);
        DrawCircle(new(699, 273), 25, new Color("cf9660"));
        DrawLine(new(531, 206), new(531, 470), new Color("928567"), 16);
        DrawRect(new Rect2(475, 177, 112, 69), new Color("637169"));
    }
}
