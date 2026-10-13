using Godot;

namespace CardRPG;

/// <summary>Physical notebook details kept outside the readable content area.</summary>
public partial class ChapterPaperBinding : Control
{
    public override void _Ready() { MouseFilter=MouseFilterEnum.Ignore; Resized+=QueueRedraw; }
    public override void _Draw()
    {
        var ink=new Color("253d3a"); var edge=new Color("748777");
        DrawLine(new(99,62),new(99,Size.Y-48),edge,1);
        for(var y=102f;y<Size.Y-64;y+=56)
        {
            DrawCircle(new(10,y),3,ink);
            DrawArc(new(8,y),7,-1.6f,1.6f,16,edge,2,true);
        }
        DrawPolyline([new(Size.X-66,0),new(Size.X-66,17),new(Size.X-49,10),new(Size.X-32,17),new(Size.X-32,0)],new Color("a84c37"),6,true);
        DrawLine(new(0,Size.Y+13),new(Size.X+10,Size.Y+13),edge,1);
        DrawLine(new(4,Size.Y+17),new(Size.X+14,Size.Y+17),edge,1);
    }
}
