using System.Numerics;
using System.Text.Json;

namespace CardRPG.Core;

public readonly record struct Cell(int X, int Y)
{
    public Vector2 Center => new(X + 0.5f, Y + 0.5f);
    public static Cell At(Vector2 point) => new((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));
}

/// <summary>The JSON layout is the only source of terrain and object positions.</summary>
public sealed class MazeDefinition
{
    public string Title { get; }
    public IReadOnlyList<string> Rows { get; }
    public int Width => Rows[0].Length;
    public int Height => Rows.Count;
    public Cell Start { get; }
    public IEnumerable<Cell> Cells
    {
        get
        {
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                    yield return new Cell(x, y);
        }
    }

    public MazeDefinition(string title, IEnumerable<string> rows)
    {
        Title = title;
        Rows = Array.AsReadOnly(rows.ToArray());
        if (Rows.Count < 3 || Rows[0].Length < 3 || Rows.Any(row => row.Length != Width))
            throw new ArgumentException("Maze rows must form a rectangle at least 3 × 3.");
        if (Rows.Any(row => row.Any(c => !"#.PDLSGKCBNEX".Contains(c))))
            throw new ArgumentException("Maze contains an unknown tile symbol.");
        if (Cells.Any(c => (c.X == 0 || c.Y == 0 || c.X == Width - 1 || c.Y == Height - 1) && this[c] != '#'))
            throw new ArgumentException("Maze boundary must be enclosed by walls.");
        var starts = Cells.Where(c => this[c] == 'P').ToArray();
        if (starts.Length != 1)
            throw new ArgumentException("Maze must contain exactly one player start.");
        Start = starts[0];
    }

    public bool Contains(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;
    public char this[Cell cell] => Contains(cell) ? Rows[cell.Y][cell.X] : '#';
    public static bool IsDoor(char tile) => tile is 'D' or 'L' or 'S';
    public static bool IsObject(char tile) => tile is 'D' or 'L' or 'S' or 'K' or 'C' or 'B' or 'N' or 'E';

    public static MazeDefinition FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new MazeDefinition(root.GetProperty("title").GetString() ?? "微型迷宫",
            root.GetProperty("rows").EnumerateArray().Select(row => row.GetString() ?? ""));
    }
}
