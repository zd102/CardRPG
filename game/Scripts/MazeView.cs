using CardRPG.Core;
using Godot;
using System;

namespace CardRPG;

public partial class MazeView : Node2D
{
    public const int TilePixels = 48;
    public MazeSession Session { get; private set; } = null!;
    public MouseNavigator? Navigation { get; set; }
    public Cell? HoveredCell { get; set; }
    private TileMapLayer _terrain = null!;
    private TileMapLayer _memory = null!;
    private int[,] _cellState = null!;
    private float _animation;
    private int _revision = -1;
    private static readonly Color Ink = new("11191e");
    private static readonly Color Stone = new("71817e");
    private static readonly Color Gold = new("e9b96a");
    private static readonly Color Teal = new("86d5c3");

    public void Initialize(MazeSession session)
    {
        Session = session;
        _cellState = new int[session.Map.Width, session.Map.Height];
        foreach (var cell in session.Map.Cells) _cellState[cell.X, cell.Y] = -1;
        var tileSet = CreateTiles();
        _memory = new TileMapLayer { TileSet = tileSet, Scale = new Vector2(3, 3), Modulate = new Color(0.25f, 0.30f, 0.34f) };
        _terrain = new TileMapLayer { TileSet = tileSet, Scale = new Vector2(3, 3) };
        // Terrain precedes this node's drawings; entities and the player are drawn on top.
        _memory.ShowBehindParent = true;
        _terrain.ShowBehindParent = true;
        AddChild(_memory);
        AddChild(_terrain);
        TextureFilter = TextureFilterEnum.Nearest;
        Refresh(0);
    }

    public void ReplaceSession(MazeSession session)
    {
        Session = session;
        _memory.Clear();
        _terrain.Clear();
        foreach (var c in session.Map.Cells) _cellState[c.X, c.Y] = -1;
        _revision = -1;
        Refresh(0);
    }

    public void Refresh(float delta)
    {
        _animation += delta;
        if (_revision != Session.VisibilityRevision)
        {
            _revision = Session.VisibilityRevision;
            foreach (var c in Session.Map.Cells)
            {
                var visible = Session.IsVisible(c);
                var seen = Session.WasSeen(c);
                var distance = System.Numerics.Vector2.Distance(Session.Position, c.Center);
                var shade = Math.Clamp((int)(distance / 1.5f), 0, 4);
                var state = visible ? 10 + shade : seen ? 1 : 0;
                if (_cellState[c.X, c.Y] == state) continue;
                _cellState[c.X, c.Y] = state;
                var position = new Vector2I(c.X, c.Y);
                var index = Session.Map[c] == '#' ? 4 : Session.Map[c] == 'G' ? 5 : (c.X * 17 + c.Y * 13) % 4;
                if (seen) _memory.SetCell(position, 0, new Vector2I(index, 0));
                if (visible) _terrain.SetCell(position, 0, new Vector2I(index, 0), shade);
                else _terrain.EraseCell(position);
            }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Session == null) return;
        DrawNavigation();
        var target = Session.InteractionTarget();
        foreach (var c in Session.Map.Cells)
        {
            var tile = Session.Map[c];
            if (!MazeDefinition.IsObject(tile)) continue;
            var visible = Session.IsVisible(c);
            // Only terrain and the last observed door state are retained in memory.
            if (!visible && (!Session.WasSeen(c) || !MazeDefinition.IsDoor(tile))) continue;
            if (Session.IsCollected(c)) continue;
            var origin = new Vector2(c.X * TilePixels, c.Y * TilePixels);
            var tint = visible ? Colors.White : new Color(0.24f, 0.28f, 0.31f);
            var open = visible ? Session.IsDoorOpen(c) : Session.RememberedOpen(c);
            DrawObject(origin, tile, open, tint, c);
            if (target == c || HoveredCell == c)
            {
                var corners = new[] { new Vector2(2, 2), new Vector2(40, 2), new Vector2(2, 40), new Vector2(40, 40) };
                foreach (var offset in corners) DrawRect(new Rect2(origin + offset, new Vector2(6, 6)), Gold, false, 1);
            }
        }
        DrawPlayer();
    }

    private void DrawNavigation()
    {
        if (Navigation?.Destination is not System.Numerics.Vector2 destination) return;
        var previous = new Vector2(Session.Position.X, Session.Position.Y) * TilePixels;
        foreach (var point in Navigation.RemainingPath)
        {
            var next = new Vector2(point.X, point.Y) * TilePixels;
            DrawLine(previous, next, new Color(0.56f, 0.81f, 0.75f, 0.45f), 1.5f);
            DrawCircle(next, 2, Teal);
            previous = next;
        }
        if (Session.WasSeen(Cell.At(destination)))
        {
            var marker = new Vector2(destination.X, destination.Y) * TilePixels;
            DrawArc(marker, 8, 0, Mathf.Tau, 24, Teal, 1.5f);
            DrawCircle(marker, 2, Teal);
        }
    }

    private void Pixel(Vector2 origin, float x, float y, float width, float height, Color color)
        => DrawRect(new Rect2(origin + new Vector2(x * 3, y * 3), new Vector2(width * 3, height * 3)), color);

    private void DrawObject(Vector2 origin, char tile, bool open, Color tint, Cell cell)
    {
        void P(float x, float y, float w, float h, Color color) => Pixel(origin, x, y, w, h, color * tint);
        if (MazeDefinition.IsDoor(tile))
        {
            var vertical = Session.Map[new Cell(cell.X, cell.Y - 1)] == '#' && Session.Map[new Cell(cell.X, cell.Y + 1)] == '#';
            var wood = tile == 'L' ? new Color("827252") : tile == 'S' ? new Color("637c79") : new Color("916c49");
            if (vertical)
            {
                P(5, 0, 6, 16, Ink); P(5, 0, 6, 2, Stone); P(5, 14, 6, 2, Stone);
                if (!open) { P(6, 2, 4, 12, wood); P(6, 4, 4, 1, Gold.Darkened(0.3f)); P(6, 11, 4, 1, Gold.Darkened(0.3f)); P(7, 7, 2, 2, Gold); }
                else { P(9, 2, 2, 5, wood); P(5, 2, 4, 12, new Color("2b3535")); }
            }
            else
            {
                P(0, 4, 16, 7, Ink); P(0, 3, 2, 10, Stone); P(14, 3, 2, 10, Stone);
                if (!open) { P(2, 5, 12, 5, wood); P(3, 5, 1, 5, wood.Lightened(0.16f)); P(7, 5, 1, 5, Ink); P(10, 7, 2, 2, Gold); }
                else { P(2, 5, 2, 7, wood); P(5, 5, 8, 5, new Color("303a38")); }
            }
            if (tile == 'L' && !open) { P(7, 5, 3, 4, Gold); P(8, 6, 1, 2, Ink); }
            return;
        }
        switch (tile)
        {
            case 'B':
                P(1, 2, 14, 13, new Color(0.66f, 0.34f, 0.12f, 0.12f));
                P(4, 11, 8, 3, Ink); P(5, 10, 6, 3, Stone); P(6, 12, 4, 2, new Color("3c4848"));
                P(5, 6, 6, 4, new Color("ad5d38")); P(6, 4, 4, 6, Gold); P(7, 3 + (int)(_animation * 3) % 2, 2, 6, new Color("fff0ac"));
                break;
            case 'K':
                P(3, 11, 10, 3, Ink); P(5, 9, 6, 3, Stone); P(6, 8, 4, 2, new Color("a1aba1"));
                P(7, 3, 1, 5, Gold); P(6, 2, 3, 1, Gold); P(6, 3, 1, 2, Gold); P(8, 3, 1, 2, Gold); P(8, 6, 2, 1, Gold);
                break;
            case 'C':
                P(2, 6, 12, 8, Ink); P(3, 5, 10, 7, new Color("84654b")); P(3, 5, 10, 2, new Color("ad8b5a"));
                P(4, 5, 1, 7, Gold); P(11, 5, 1, 7, Gold); P(3, 9, 10, 1, Ink); P(7, 8, 2, 3, Gold);
                break;
            case 'N':
                P(3, 4, 10, 11, Ink); P(4, 3, 8, 11, Stone); P(5, 2, 6, 2, Stone.Lightened(0.15f));
                P(6, 5, 4, 1, Ink); P(6, 7, 3, 1, Ink); P(6, 9, 4, 1, Ink); P(3, 13, 10, 2, new Color("4c5e5c"));
                break;
            case 'E':
                P(1, 2, 14, 13, Ink); P(2, 2, 3, 13, Stone); P(11, 2, 3, 13, Stone); P(3, 1, 10, 3, Stone);
                P(5, 4, 6, 10, new Color("244344")); P(6, 5, 4, 8, Teal.Darkened(0.3f)); P(7, 5, 2, 8, Teal);
                P(1, 13, 14, 2, new Color("809690")); P(0, 15, 16, 1, Stone);
                break;
        }
    }

    private void DrawPlayer()
    {
        var origin = new Vector2(MathF.Round(Session.Position.X * TilePixels) - 24, MathF.Round(Session.Position.Y * TilePixels) - 30);
        Pixel(origin, 4, 12, 9, 3, new Color(0, 0, 0, 0.4f));
        Pixel(origin, 5, 6, 7, 7, Ink);
        Pixel(origin, 5, 7, 7, 5, new Color("456d71"));
        Pixel(origin, 4, 9, 9, 3, new Color("547f80"));
        Pixel(origin, 7, 7, 2, 5, new Color("739b98"));
        Pixel(origin, 6, 12, 2, 2, new Color("a0a9a0"));
        Pixel(origin, 10, 12, 2, 2, new Color("a0a9a0"));
        Pixel(origin, 6, 2, 6, 5, Ink);
        Pixel(origin, 7, 2, 4, 2, new Color("9caba6"));
        Pixel(origin, 6, 4, 6, 2, new Color("aebdb5"));
        Pixel(origin, 7, 4, 4, 1, new Color("25383e"));
        Pixel(origin, 11, 8, 2, 2, new Color("d7c39e"));
        Pixel(origin, 13, 7, 2, 5, new Color("d0934f"));
        Pixel(origin, 13, 8, 1, 3, new Color("fff0b3"));
        var direction = new Vector2(Session.Facing.X, Session.Facing.Y);
        var center = new Vector2(Session.Position.X, Session.Position.Y) * TilePixels;
        DrawLine(center + direction * 18, center + direction * 22, Teal, 2);
    }

    private static TileSet CreateTiles()
    {
        var atlas = Image.CreateEmpty(96, 16, false, Image.Format.Rgba8);
        void Fill(int tile, int x, int y, int w, int h, string hex)
        {
            var color = new Color(hex);
            for (var py = y; py < y + h; py++)
                for (var px = x; px < x + w; px++) atlas.SetPixel(tile * 16 + px, py, color);
        }
        for (var tile = 0; tile < 4; tile++)
        {
            Fill(tile, 0, 0, 16, 16, "293335");
            Fill(tile, 1, 1, 14, 6, tile % 2 == 0 ? "45504c" : "404c49");
            Fill(tile, 1, 8, 7, 7, "3e4947"); Fill(tile, 9, 8, 6, 7, "48514b");
            Fill(tile, 2, 1, 12, 1, "525c52"); Fill(tile, 2, 8, 5, 1, "4e584f");
            Fill(tile, 9, 9, 1, 5, "505b51");
            if (tile == 1) { Fill(tile, 5, 3, 1, 3, "313d3c"); Fill(tile, 6, 5, 3, 1, "313d3c"); }
            if (tile == 2) { Fill(tile, 12, 11, 2, 1, "687061"); Fill(tile, 2, 12, 2, 2, "354b41"); }
            if (tile == 3) { Fill(tile, 4, 9, 2, 1, "747668"); Fill(tile, 11, 3, 2, 1, "596459"); }
        }
        Fill(4, 0, 0, 16, 16, "152128"); Fill(4, 0, 1, 16, 8, "51666a");
        Fill(4, 1, 1, 14, 1, "71817e"); Fill(4, 1, 3, 6, 4, "5c7072"); Fill(4, 8, 3, 7, 4, "485c61");
        Fill(4, 0, 9, 16, 5, "304249"); Fill(4, 1, 10, 9, 3, "3b5055"); Fill(4, 11, 10, 4, 3, "35494f");
        Fill(4, 0, 14, 16, 2, "111c23");
        Fill(5, 0, 0, 16, 16, "303e40"); Fill(5, 1, 2, 14, 2, "7d8980"); Fill(5, 1, 12, 14, 2, "63736e");
        for (var x = 2; x < 16; x += 4) { Fill(5, x, 2, 2, 12, "22313b"); Fill(5, x, 2, 1, 12, "87948a"); }
        var source = new TileSetAtlasSource { Texture = ImageTexture.CreateFromImage(atlas), TextureRegionSize = new Vector2I(16, 16) };
        for (var tile = 0; tile < 6; tile++)
        {
            var coordinates = new Vector2I(tile, 0);
            source.CreateTile(coordinates);
            for (var shade = 1; shade <= 4; shade++)
            {
                source.CreateAlternativeTile(coordinates, shade);
                var brightness = 1 - shade * 0.12f;
                source.GetTileData(coordinates, shade).Modulate = new Color(brightness, brightness, brightness);
            }
        }
        var result = new TileSet { TileSize = new Vector2I(16, 16) };
        result.AddSource(source, 0);
        return result;
    }
}
