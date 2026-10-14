using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

namespace CardRPG;

/// <summary>One logical inventory slot is one cell; wide items occupy adjacent cells.</summary>
public partial class ChapterSlotGrid : Container
{
    public int Capacity { get; set; }
    public bool ShowEmpty { get; set; } = true;
    public bool ListMode { get; set; }
    public int Columns { get; private set; } = 1;
    private float _height;
    private const float Gap = 6, Cell = 92, RowHeight = 106;

    public override Vector2 _GetMinimumSize() => new(0, _height);

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren) return;
        Columns = ListMode ? 1 : Math.Max(2, (int)((Size.X + Gap) / (Cell + Gap)));
        var width = (Size.X - (Columns - 1) * Gap) / Columns;
        var cells = 0; var occupied = new HashSet<int>();
        foreach (var item in GetChildren().OfType<Control>())
        {
            var span = ListMode ? 1 : Math.Min(Columns, item.GetMeta("slots", 1).AsInt32());
            var start = 0;
            while (start % Columns + span > Columns || Enumerable.Range(start, span).Any(occupied.Contains)) start++;
            FitChildInRect(item, new Rect2(start % Columns * (width + Gap), start / Columns * (RowHeight + Gap), span * width + (span - 1) * Gap, RowHeight));
            foreach (var cell in Enumerable.Range(start, span)) occupied.Add(cell);
            cells = Math.Max(cells, start + span);
        }
        var total = ListMode || !ShowEmpty ? cells : Math.Max(cells, Capacity);
        var height = Math.Max(1, (int)Math.Ceiling(total / (float)Columns)) * (RowHeight + Gap) - Gap;
        if (!Mathf.IsEqualApprox(height, _height)) { _height = height; UpdateMinimumSize(); }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (ListMode || !ShowEmpty || Capacity <= 0) return;
        var width = (Size.X - (Columns - 1) * Gap) / Columns;
        for (var i = 0; i < Capacity; i++)
        {
            var rect = new Rect2(i % Columns * (width + Gap), i / Columns * (RowHeight + Gap), width, RowHeight);
            DrawRect(rect, new Color("dfddcf"));
            DrawRect(rect, new Color("bdc0b1"), false, 1);
            var center = rect.GetCenter();
            DrawLine(center - new Vector2(4, 0), center + new Vector2(4, 0), new Color("b3b8a7"));
            DrawLine(center - new Vector2(0, 4), center + new Vector2(0, 4), new Color("b3b8a7"));
        }
    }
}
