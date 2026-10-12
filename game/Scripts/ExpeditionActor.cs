using Godot;

using System;

namespace CardRPG;

/// <summary>World-space enemy silhouette. Selection and intent are separate keyboard-accessible HUD controls.</summary>
public partial class ExpeditionActor : Control
{
    public bool Selected { get; set; }
    public bool Boss { get; set; }
    public bool Defeated { get; set; }
    public Action? Picked { get; set; }
    public override void _Ready()
    {
        MouseFilter = Defeated ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Resized += QueueRedraw;
    }
    public override void _GuiInput(InputEvent @event)
    {
        if (!Defeated && @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            AcceptEvent(); Picked?.Invoke();
        }
    }
    public override void _Draw()
    {
        var scale = Mathf.Min(Size.X / 180, Size.Y / 180);
        DrawSetTransform(new Vector2((Size.X - 180 * scale) / 2, Size.Y - 180 * scale), 0, Vector2.One * scale);
        var edge = Defeated ? new Color("687574") : Selected ? new Color("e4c580") : new Color("9ca68c");
        var metal = Defeated ? new Color("263237") : new Color("526966");
        DrawColoredPolygon([new(14, 169), new(85, 157), new(167, 167), new(93, 180)], new Color("030b148a"));
        if (Selected) DrawPolyline([new(12, 165), new(85, 153), new(168, 165), new(94, 178), new(12, 165)], new Color(edge, .8f), 1.5f, true);
        DrawRect(new Rect2(62, 118, 22, 45), metal.Darkened(.35f));
        DrawRect(new Rect2(106, 118, 22, 45), metal.Darkened(.35f));
        DrawRect(new Rect2(56, 155, 31, 12), metal);
        DrawRect(new Rect2(103, 155, 33, 12), metal);
        DrawColoredPolygon([new(60, 47), new(125, 47), new(141, 97), new(119, 132), new(68, 132), new(47, 99)], metal);
        DrawPolyline([new(60, 47), new(125, 47), new(141, 97), new(119, 132), new(68, 132), new(47, 99), new(60, 47)], edge, 1.5f, true);
        DrawRect(new Rect2(76, 14, 36, 38), metal.Lightened(.15f));
        DrawRect(new Rect2(80, 26, 28, 7), new Color("132329"));
        DrawRect(new Rect2(84, 28, 20, 3), edge);
        DrawLine(new(130, 64), new(148, 109), metal, 18, true);
        DrawLine(new(51, 62), new(36, 107), metal, 18, true);
        DrawLine(new(29, 61), new(29, 157), edge.Darkened(.2f), 7, true);
        DrawColoredPolygon([new(6, 52), new(49, 45), new(58, 67), new(14, 74)], metal.Lightened(.2f));
        DrawPolyline([new(6, 52), new(49, 45), new(58, 67), new(14, 74), new(6, 52)], edge, 1, true);
        DrawCircle(new(93, 84), Boss ? 23 : 17, new Color("12272c"));
        DrawArc(new(93, 84), Boss ? 19 : 13, 0, Mathf.Tau, 40, edge, 2.5f, true);
        DrawCircle(new(93, 84), Boss ? 13 : 8, Defeated ? new Color("344544") : new Color("dba062"));
        foreach (var p in new Vector2[] { new(64, 58), new(119, 58), new(72, 119), new(118, 119) }) DrawCircle(p, 2, edge);
    }
}
