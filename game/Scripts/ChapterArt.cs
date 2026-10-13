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
        var equipment = new[] { "G01", "G02", "G03", "G09", "G10", "G15", "G25", "G26" };
        var actors = new[] { "enemy-E01", "enemy-E02", "enemy-E03", "enemy-E13", "enemy-dummy", "repair-robot", "clothes-rack", "crest" };
        var items = new[] { "T01", "T02", "T11", "T14", "satchel", "steel", "wire", "spring" };
        var index = System.Array.IndexOf(equipment,name); var atlasName="equipment";
        if(index<0) { index=System.Array.IndexOf(actors,name); atlasName="actors"; }
        if(index<0) { index=System.Array.IndexOf(items,name); atlasName="items"; }
        var atlasPath="res://Art/Print/"+atlasName+".png";
        if(index>=0 && ResourceLoader.Exists(atlasPath))
        {
            var atlas=GD.Load<Texture2D>(atlasPath); var size=atlas.GetSize()/new Vector2(4,2);
            var region=new Rect2(new Vector2(index%4,index/4)*size,size);
            // The character sheet has a taller first row. Use its actual empty gutters.
            if(atlasName=="actors")
            {
                float[] columns=[0,.25f,.5f,.743f,1]; var y=index<4?0:.557f; var bottom=index<4?.557f:1;
                region=new Rect2(new Vector2(columns[index%4],y)*atlas.GetSize(),new Vector2(columns[index%4+1]-columns[index%4],bottom-y)*atlas.GetSize());
            }
            texture=new AtlasTexture { Atlas=atlas, Region=region, FilterClip=true };
            Cache[name]=texture; return texture;
        }
        var cardGear=name switch { "card-strike"=>"G01", "card-ward"=>"G09", "card-arc"=>"G02", _=>"" };
        if(cardGear!="") return Load(cardGear);
        var path = "res://Art/Print/" + name + ".png";
        if (!ResourceLoader.Exists(path)) path = "res://Art/Generated/chapter/" + name + ".png";
        if (!ResourceLoader.Exists(path)) return null;
        texture = GD.Load<Texture2D>(path); Cache[name] = texture; return texture;
    }

    public static TextureRect Picture(string name)
    {
        return new TextureRect
        {
            Texture = Load(name), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }
}
