using Godot;

namespace CardRPG;

/// <summary>Shared, deliberately simple pictograms. The chapter provides focus and hover descriptions.</summary>
public partial class ExpeditionGlyph : Control
{
    public string Symbol { get; set; } = "echo";
    public Color Tint { get; set; } = new("dfbb7b");
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        if (ChapterSkin.Icon(Symbol) is { } texture)
        {
            TextureFilter = TextureFilterEnum.Linear;
            var edge = Mathf.Min(Size.X, Size.Y);
            DrawTextureRect(texture, new Rect2((Size - Vector2.One * edge) / 2, Vector2.One * edge), false, Tint);
            return;
        }
        var scale = Mathf.Min(Size.X, Size.Y) / 48;
        DrawSetTransform((Size - new Vector2(48, 48) * scale) / 2, 0, Vector2.One * scale);
        var color = Tint;
        switch (Symbol)
        {
            case "settings":
                DrawArc(new(24,24),12,0,Mathf.Tau,32,color,3);
                DrawCircle(new(24,24),4,color);
                for (var i = 0; i < 8; i++) { var ray = Vector2.FromAngle(i * Mathf.Tau / 8); DrawLine(new Vector2(24,24) + ray * 14, new Vector2(24,24) + ray * 21,color,5); }
                break;
            case "character":
                DrawArc(new(24,14),8,0,Mathf.Tau,24,color,3);
                DrawPolyline([new(8,43),new(8,34),new(15,27),new(33,27),new(40,34),new(40,43),new(8,43)],color,3); break;
            case "heart":
                DrawColoredPolygon([new(24,41),new(5,23),new(5,12),new(12,7),new(19,7),new(24,13),new(29,7),new(36,7),new(43,12),new(43,23)], color); break;
            case "action":
                DrawColoredPolygon([new(25,3),new(8,27),new(21,27),new(19,45),new(40,18),new(26,18)], color); break;
            case "mana":
                DrawColoredPolygon([new(24,4),new(38,28),new(36,38),new(24,44),new(12,38),new(10,28)], color);
                DrawLine(new(17,29),new(17,34),new Color("202d2b"),3); break;
            case "map":
                DrawPolyline([new(5,10),new(17,5),new(31,11),new(43,6),new(43,38),new(31,43),new(17,37),new(5,42),new(5,10)],color,3);
                DrawLine(new(17,5),new(17,37),color,2); DrawLine(new(31,11),new(31,43),color,2); break;
            case "bag": case "prepare":
                DrawRect(new Rect2(10,14,28,29),color,false,3);
                DrawPolyline([new(17,14),new(17,5),new(31,5),new(31,14)],color,3);
                DrawRect(new Rect2(16,26,16,11),color,false,2); break;
            case "journal":
                DrawRect(new Rect2(8,6,32,37),color,false,3);
                DrawLine(new(15,6),new(15,43),color,3);
                DrawPolyline([new(21,18),new(25,23),new(34,14)],color,3);
                DrawLine(new(21,32),new(33,32),color,3); break;
            case "craft": case "gear":
                DrawPolyline([new(28,5),new(25,14),new(33,22),new(42,19),new(39,28),new(29,31),new(13,44),new(5,36),new(21,22),new(19,12),new(28,5)],color,3); break;
            case "help":
                DrawArc(new(24,24),20,0,Mathf.Tau,32,color,3);
                DrawPolyline([new(17,16),new(20,11),new(29,11),new(33,16),new(32,22),new(24,28)],color,3);
                DrawCircle(new(24,36),2,color); break;
            case "close":
                DrawLine(new(12,12),new(36,36),color,3); DrawLine(new(36,12),new(12,36),color,3); break;
            case "arrow": case "exit":
                if (Symbol == "exit") DrawPolyline([new(19,5),new(7,5),new(7,43),new(19,43)],color,3);
                DrawLine(new(16,24),new(42,24),color,3);
                DrawPolyline([new(30,12),new(42,24),new(30,36)],color,3); break;
            case "round":
                DrawArc(new(24,24),17,-1.5f,3.8f,24,color,3);
                DrawPolyline([new(4,16),new(10,9),new(17,17)],color,3); break;
            case "eye":
                DrawPolyline([new(3,24),new(13,13),new(24,9),new(35,13),new(45,24),new(35,35),new(24,39),new(13,35),new(3,24)],color,3);
                DrawCircle(new(24,24),7,color); break;
            case "warning":
                DrawPolyline([new(24,4),new(45,42),new(3,42),new(24,4)],color,3);
                DrawLine(new(24,17),new(24,29),color,4); DrawCircle(new(24,35),2,color); break;
            case "steel":
                DrawPolyline([new(5,31),new(12,14),new(34,14),new(43,31),new(5,31),new(14,39),new(36,39),new(43,31)],color,3); break;
            case "wire":
                DrawArc(new(24,24),16,0,Mathf.Tau,24,color,3); DrawArc(new(24,24),8,0,Mathf.Tau,24,color,3);
                DrawPolyline([new(40,24),new(40,43),new(32,43)],color,3); break;
            case "spring":
                DrawPolyline([new(14,5),new(34,11),new(14,17),new(34,23),new(14,29),new(34,35),new(14,41)],color,3); break;
            case "draw": case "discard":
                DrawPolyline([new(7,32),new(7,6),new(29,6)],color,3);
                DrawRect(new Rect2(14,12,25,31),color,false,3);
                DrawLine(new(21,26),new(32,26),color,3);
                if (Symbol == "draw") DrawLine(new(27,20),new(27,32),color,3); break;
            case "all":
                foreach (var x in new[] {10,24,38}) { DrawCircle(new(x,15),5,color); DrawLine(new(x,24),new(x,38),color,4); } break;
            case "target":
                DrawArc(new(24,24),15,0,Mathf.Tau,24,color,3);
                DrawLine(new(24,1),new(24,15),color,3); DrawLine(new(24,33),new(24,47),color,3);
                DrawLine(new(1,24),new(15,24),color,3); DrawLine(new(33,24),new(47,24),color,3); break;
            case "check":
                DrawPolyline([new(8,25),new(20,37),new(41,10)],color,4); break;
            case "lock":
                DrawRect(new Rect2(10,22,28,22),color,false,3);
                DrawPolyline([new(16,22),new(16,7),new(32,7),new(32,22)],color,3); break;
            case "empty":
                DrawArc(new(24,24),18,0,Mathf.Tau,32,color,2); DrawLine(new(13,24),new(35,24),color,3); break;
            case "slots":
                for(var y=0;y<2;y++) for(var x=0;x<2;x++) DrawRect(new Rect2(5+x*22,5+y*22,16,16),color,false,3); break;
            case "thread":
                DrawPolyline([new(5,9),new(42,39),new(5,39),new(42,9)],color,3); break;
            case "frame":
                DrawRect(new Rect2(6,5,36,38),color,false,4);DrawLine(new(6,18),new(42,18),color,3);DrawLine(new(6,31),new(42,31),color,3);break;
            case "home":
                DrawPolyline([new(3,23),new(24,5),new(45,23),new(38,23),new(38,43),new(10,43),new(10,23)],color,3);
                DrawRect(new Rect2(20,29,9,14),color,false,2); break;
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
