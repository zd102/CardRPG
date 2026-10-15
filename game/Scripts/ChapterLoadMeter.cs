using Godot;
using System;

namespace CardRPG;

/// <summary>Current / predicted load on one scale, including overflow beyond the limit.</summary>
public partial class ChapterLoadMeter : Control
{
    public int Current { get; set; }
    public int Predicted { get; set; }
    public int Limit { get; set; }
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0, 18);
        Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        var scale = Math.Max(1, Math.Max(Limit, Math.Max(Current, Predicted)) * 1.08f);
        float X(int value) => Math.Clamp(value / scale, 0, 1) * Size.X;
        var bar = new Rect2(0, 4, Size.X, 10);
        DrawRect(bar, new Color("394349"));
        var retained = Math.Min(Current, Predicted);
        DrawRect(new Rect2(0, 4, X(Math.Min(retained, Limit)), 10), new Color("92a787"));
        if (retained > Limit) DrawRect(new Rect2(X(Limit), 4, X(retained) - X(Limit), 10), new Color("d47d64"));
        if (Predicted > Current) DrawRect(new Rect2(X(Current), 4, X(Predicted) - X(Current), 10), new Color("d6ba70"));
        if (Predicted < Current)
        {
            DrawRect(new Rect2(X(Predicted), 4, X(Current) - X(Predicted), 10), new Color("657887"));
            for (float x = X(Predicted); x < X(Current); x += 7) DrawLine(new Vector2(x, 13), new Vector2(Math.Min(x + 5, X(Current)), 5), new Color("a4c4db"));
        }
        DrawLine(new Vector2(X(Limit), 0), new Vector2(X(Limit), 18), new Color("eb8b72"), 2);
        DrawLine(new Vector2(X(Current), 3), new Vector2(X(Current), 15), new Color("7ec2ee"), 2);
        DrawLine(new Vector2(X(Predicted), 0), new Vector2(X(Predicted), 18), new Color("f4ead4"), 2);
    }
}
