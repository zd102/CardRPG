using Godot;

namespace CardRPG;

/// <summary>Shared vector symbols. All interactive meanings also have visible text labels.</summary>
public partial class ExpeditionGlyph : Control
{
    public string Symbol { get; set; } = "echo";
    public Color Tint { get; set; } = new("dfbb7b");
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        var scale = Mathf.Min(Size.X, Size.Y) / 48;
        DrawSetTransform((Size - new Vector2(48, 48) * scale) / 2, 0, Vector2.One * scale);
        var color = Tint;
        switch (Symbol)
        {
            case "guard": case "armor": case "bash": case "riposte":
                DrawColoredPolygon([new(10, 8), new(24, 4), new(38, 8), new(36, 29), new(24, 43), new(12, 29)], new Color(color, .1f));
                DrawPolyline([new(10, 8), new(24, 4), new(38, 8), new(36, 29), new(24, 43), new(12, 29), new(10, 8)], color, 2, true);
                DrawLine(new(24, 12), new(24, 33), color, 2, true);
                DrawLine(new(17, 21), new(31, 21), color, 2, true); break;
            case "bolt": case "staff": case "spark":
                DrawPolyline([new(29, 4), new(12, 26), new(24, 24), new(18, 44), new(37, 19), new(25, 21), new(29, 4)], color, 2, true); break;
            case "strike": case "hammer": case "breaker": case "cleave": case "punch":
                DrawLine(new(12, 41), new(30, 17), color, 5, true);
                DrawColoredPolygon([new(18, 5), new(42, 19), new(33, 30), new(11, 16)], new Color(color, .18f));
                DrawPolyline([new(18, 5), new(42, 19), new(33, 30), new(11, 16), new(18, 5)], color, 2, true); break;
            case "loot":
                DrawPolyline([new(5, 15), new(24, 6), new(43, 15), new(43, 36), new(24, 44), new(5, 36), new(5, 15), new(24, 24), new(43, 15)], color, 2, true);
                DrawLine(new(24, 24), new(24, 44), color, 2, true); break;
            case "supply":
                DrawRect(new Rect2(12, 11, 24, 32), new Color(color, .1f));
                DrawRect(new Rect2(12, 11, 24, 32), color, false, 2);
                DrawRect(new Rect2(17, 4, 14, 7), color, false, 2);
                DrawLine(new(17, 27), new(31, 27), color, 3, true);
                DrawLine(new(24, 20), new(24, 34), color, 3, true); break;
            case "route":
                DrawPolyline([new(9, 35), new(22, 13), new(39, 28)], color, 2, true);
                foreach (var p in new Vector2[] { new(9, 35), new(22, 13), new(39, 28) }) { DrawCircle(p, 5, new Color("16282e")); DrawArc(p, 5, 0, Mathf.Tau, 24, color, 2, true); } break;
            default:
                DrawArc(new(24, 24), 19, -.6f, 4.9f, 48, color, 1.5f, true);
                DrawArc(new(24, 24), 14, .5f, 5.8f, 48, color, 1, true);
                DrawPolyline([new(24, 9), new(32, 24), new(24, 39), new(16, 24), new(24, 9)], color, 2, true);
                DrawCircle(new(24, 24), 3, new Color("91c4b2")); break;
        }
    }
}
