using Godot;
using System.Collections.Generic;

namespace CardRPG;

/// <summary>Generated print surfaces, nine-sliced without stretching their engraved edges.</summary>
public static class ChapterSkin
{
    public static readonly Color PaperInk = new("253d3a"), PaperMuted = new("56665b");
    private static Texture2D? _surfaces, _symbols;
    private static readonly Dictionary<int, Texture2D> Tiles = new();
    private static readonly Dictionary<string, Texture2D> Icons = new();
    public static readonly SystemFont BodyFont = new() { FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "sans-serif"] };
    public static readonly SystemFont TitleFont = new() { FontNames = ["STZhongsong", "SimSun", "Noto Serif CJK SC", "serif"], FontWeight = 600 };
    public static readonly SystemFont NumberFont = new() { FontNames = ["Bahnschrift", "Consolas", "monospace"], FontWeight = 600 };

    public static StyleBoxTexture Box(string name = "field-panel", int padding = 18, Color? tint = null)
    {
        _surfaces ??= GD.Load<Texture2D>("res://Art/Print/Interface/surfaces.png");
        var index = name switch { "ledger-paper" => 0, "button" => 2, "brass-button" => 3, _ => 1 };
        if (!Tiles.TryGetValue(index, out var tile))
        {
            var size = _surfaces.GetSize() / 2;
            // Normalize the source pixel density before nine-slicing: an engraved corner
            // occupies 18 logical pixels on a control, regardless of the generated resolution.
            using var atlasImage = _surfaces.GetImage();
            using var tileImage = atlasImage.GetRegion(new Rect2I((int)(index % 2 * size.X), (int)(index / 2 * size.Y), (int)size.X, (int)size.Y));
            tileImage.Resize(128, 128, Image.Interpolation.Lanczos);
            Tiles[index] = tile = ImageTexture.CreateFromImage(tileImage);
        }
        return new StyleBoxTexture
        {
            Texture = tile, ModulateColor = tint ?? Colors.White,
            TextureMarginLeft = 18, TextureMarginRight = 18, TextureMarginTop = 18, TextureMarginBottom = 18,
            ContentMarginLeft = padding, ContentMarginRight = padding,
            ContentMarginTop = padding, ContentMarginBottom = padding
        };
    }

    public static Texture2D? Icon(string symbol)
    {
        symbol = symbol switch { "prepare" => "bag", "bolt" or "spark" or "staff" => "action", "armor" or "bash" or "riposte" => "guard", "strike" or "breaker" or "cleave" or "punch" => "hammer", "route" => "map", _ => symbol };
        if (Icons.TryGetValue(symbol, out var cached)) return cached;
        string[] names = ["heart", "supply", "bag", "character", "map", "journal", "craft", "help", "close", "action", "mana", "guard", "hammer", "draw", "home", "exit", "eye", "check", "lock", "steel", "wire", "spring", "loot", "round"];
        var index = System.Array.IndexOf(names, symbol);
        if (index < 0) return null;
        _symbols ??= GD.Load<Texture2D>("res://Art/Print/Interface/symbols.png");
        var cell = _symbols.GetSize() / new Vector2(6, 4);
        var inset = cell * .1f;
        return Icons[symbol] = new AtlasTexture { Atlas = _symbols, Region = new Rect2(new Vector2(index % 6, index / 6) * cell + inset, cell - inset * 2), FilterClip = true };
    }

    public static PanelContainer Panel(Node parent, string name = "field-panel", int padding = 18)
    {
        var panel = new PanelContainer { TextureFilter = CanvasItem.TextureFilterEnum.Linear };
        panel.AddThemeStyleboxOverride("panel", Box(name, padding));
        parent.AddChild(panel);
        return panel;
    }
}
