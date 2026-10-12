using Godot;

namespace CardRPG;

/// <summary>Code-native scene illustration, scaled to its container; no external assets.</summary>
public partial class IndustrialBackdrop : Control
{
    public bool IsMap { get; set; }
    public string Location { get; set; } = "gate";
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        var s = Size;
        DrawRect(new Rect2(Vector2.Zero, s), new Color("111f25"));
        for (var i = 0; i < 25; i++)
        {
            var x = i * s.X / 24;
            DrawLine(new Vector2(x, 0), new Vector2(x, s.Y), new Color("17272e"));
        }
        for (var i = 0; i < 12; i++) DrawLine(new Vector2(0, i * s.Y / 11), new Vector2(s.X, i * s.Y / 11), new Color("17272e"));
        if (IsMap)
        {
            Vector2[] p = [new(.10f, .5f), new(.34f, .24f), new(.34f, .76f), new(.61f, .24f), new(.86f, .24f), new(.75f, .76f)];
            int[][] edges = [[0, 1], [0, 2], [1, 2], [1, 3], [2, 3], [2, 5], [3, 4], [4, 5]];
            foreach (var e in edges)
            {
                var a = p[e[0]] * s; var b = p[e[1]] * s;
                DrawLine(a, b, new Color("3b555a"), 3, true);
            }
            return;
        }
        // Silhouette, pipework, overhead gantry and foreground crates express the foundry.
        DrawRect(new Rect2(0, s.Y * .73f, s.X, s.Y * .27f), new Color("1b2e32"));
        for (var i = 0; i < 7; i++)
        {
            var h = s.Y * (.2f + (i % 3) * .075f);
            DrawRect(new Rect2(i * s.X / 7, s.Y * .73f - h, s.X / 8, h), new Color("24363b"));
        }
        var furnace = Location == "furnace";
        DrawRect(new Rect2(s.X * .42f, s.Y * .15f, s.X * .21f, s.Y * .58f), new Color("344348"));
        DrawRect(new Rect2(s.X * .46f, s.Y * .39f, s.X * .13f, s.Y * .34f), new Color(furnace ? "ad613b" : "101a21"));
        DrawRect(new Rect2(s.X * .46f, s.Y * .39f, s.X * .13f, s.Y * .34f), new Color("b99255"), false, 2);
        DrawLine(new Vector2(s.X * .1f, s.Y * .19f), new Vector2(s.X * .88f, s.Y * .19f), new Color("587074"), 6);
        DrawLine(new Vector2(s.X * .8f, s.Y * .19f), new Vector2(s.X * .8f, s.Y * .5f), new Color("b99255"), 3);
        for (var i = 0; i < 3; i++)
        {
            var rect = new Rect2(s.X * (.1f + i * .07f), s.Y * (.64f - i % 2 * .09f), s.X * .065f, s.Y * .14f);
            DrawRect(rect, new Color("584e3b")); DrawRect(rect, new Color("a38a60"), false, 2);
            DrawLine(rect.Position, rect.End, new Color("a38a60"), 1);
        }
        DrawLine(new Vector2(s.X * .65f, s.Y), new Vector2(s.X * .85f, s.Y * .73f), new Color("506366"), 3);
        DrawLine(new Vector2(s.X * .78f, s.Y), new Vector2(s.X * .93f, s.Y * .73f), new Color("506366"), 3);
    }
}
