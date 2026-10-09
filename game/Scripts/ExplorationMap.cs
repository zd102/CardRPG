using CardRPG.Core;
using Godot;
using System.Linq;

namespace CardRPG;

public partial class ExplorationMap : Control
{
    public MazeSession Session { get; set; } = null!;

    public override void _Draw()
    {
        if (Session == null) return;
        var map = Session.Map;
        var known = map.Cells.Where(Session.WasSeen).ToArray();
        if (known.Length == 0) return;
        var minimum = new Vector2(known.Min(c => c.X) - 1, known.Min(c => c.Y) - 1);
        var extent = new Vector2(known.Max(c => c.X) + 2, known.Max(c => c.Y) + 2) - minimum;
        var tile = Mathf.Floor(Mathf.Min(22, Mathf.Min(Size.X / extent.X, Size.Y / extent.Y)));
        var offset = (Size - extent * tile) / 2 - minimum * tile;
        foreach (var cell in map.Cells)
        {
            if (!Session.WasSeen(cell)) continue;
            var terrain = map[cell];
            var visible = Session.IsVisible(cell);
            var color = terrain == '#' ? new Color("34464d") : new Color("697a73");
            if (MazeDefinition.IsDoor(terrain)) color = Session.RememberedOpen(cell) ? new Color("849783") : new Color("b38a58");
            if (terrain == 'G') color = new Color("587778");
            if (!visible) color = color.Darkened(0.45f);
            var position = offset + new Vector2(cell.X, cell.Y) * tile;
            DrawRect(new Rect2(position, new Vector2(tile - 1, tile - 1)), color);
            if (visible && terrain == 'E') DrawRect(new Rect2(position + Vector2.One * tile * 0.25f, Vector2.One * tile * 0.5f), new Color("90dec8"));
        }
        var player = offset + new Vector2(Session.Position.X, Session.Position.Y) * tile;
        DrawCircle(player, tile * 0.38f, new Color("ffe1a1"));
        DrawArc(player, tile * 0.65f, 0, Mathf.Tau, 20, new Color("d7ad67"), 1.5f, true);
    }
}
