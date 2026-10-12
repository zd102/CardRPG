using Godot;
using System.Collections.Generic;

namespace CardRPG;

/// <summary>Imported generated art shared by scenery, collectible cards and world actors.</summary>
public static class ChapterArt
{
    private static readonly Dictionary<string, Texture2D> Cache = new();
    public static Texture2D? Load(string name)
    {
        if (Cache.TryGetValue(name, out var texture)) return texture;
        var path = "res://Art/Generated/chapter/" + name + ".png";
        if (!ResourceLoader.Exists(path)) return null;
        texture = GD.Load<Texture2D>(path); Cache[name] = texture; return texture;
    }

    public static TextureRect Picture(string name)
    {
        return new TextureRect
        {
            Texture = Load(name), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }
}
