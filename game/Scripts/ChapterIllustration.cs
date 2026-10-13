using Godot;

namespace CardRPG;

/// <summary>A mounted catalogue illustration with ruled guides and a printed registration mark.</summary>
public partial class ChapterIllustration : Control
{
    public string Art { get; set; } = "G01";
    public bool Mounted { get; set; } = true;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore; TextureFilter=TextureFilterEnum.Linear;
        Resized+=QueueRedraw;
    }
    public override void _Draw()
    {
        if(Size.X<=0 || Size.Y<=0) return;
        var ink=new Color("253d3a"); var faint=new Color("89947770");
        if(Mounted)
        {
            var center=Size/2; var radius=Mathf.Min(Size.X,Size.Y)*.39f;
            DrawCircle(center,radius,new Color("c1c5a96b"));
            DrawArc(center,radius,-.5f,4.5f,48,faint,1,true);
            DrawDashedLine(new(8,Size.Y-12),new(Size.X-8,Size.Y-12),faint,1,5);
            DrawLine(new(10,8),new(28,8),ink,2); DrawLine(new(10,8),new(10,26),ink,2);
            DrawLine(new(Size.X-10,Size.Y-8),new(Size.X-28,Size.Y-8),ink,2);
            DrawLine(new(Size.X-10,Size.Y-8),new(Size.X-10,Size.Y-26),ink,2);
            DrawCircle(new(Size.X-16,12),3,new Color("a84c37"));
        }
        var texture=ChapterArt.Load(Art); if(texture==null) return;
        var available=Size-new Vector2(Mounted?24:2,Mounted?18:2);
        var scale=Mathf.Min(available.X/texture.GetWidth(),available.Y/texture.GetHeight());
        var drawn=texture.GetSize()*scale;
        DrawTextureRect(texture,new Rect2((Size-drawn)/2,drawn),false);
    }
}
