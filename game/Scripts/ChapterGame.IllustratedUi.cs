using CardRPG.Core;
using Godot;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private static ChapterIllustration Illustration(Node parent,string art,float width=100,float height=100,bool mounted=true)
    {
        var picture=new ChapterIllustration { Art=art,Mounted=mounted,CustomMinimumSize=new Vector2(width,height),SizeFlagsVertical=SizeFlags.ShrinkCenter };
        parent.AddChild(picture); return picture;
    }
    private static string CardArt(ChapterCard card) => card.Id switch
    { "C04" or "C05"=>"G02", "C06"=>"G03", "C18"=>"G25", "C07"=>"G15", "C35" or "C36"=>"G26", _=>card.Damage>0?"G01":"G09" };

    private void SupplyCards(Node parent, string gear, bool compact = false)
    {
        var list = Column(8); list.Name = "SupplyCards"; parent.AddChild(list);
        foreach (var group in Session.GearCards(gear).GroupBy(id => id))
        {
            var card = ChapterContent.Cards[group.Key]; var row = Column(4); list.AddChild(row);
            var button = Button("supply_" + gear + "_" + card.Id, card.Name + UiCopy.Format("UI_47A141B9A986", group.Count(), card.Action, card.Mana), () =>
            {
                _detailItemId = 0; _detailTitle = card.Name;
                _detailText = $"{card.Action} 行动 / {card.Mana} 魔法\n{card.Text}\n\n来自 {ChapterContent.ItemName(gear)} ×{group.Count()}";
                OpenNested("detail");
            });
            button.Alignment = HorizontalAlignment.Left;
            button.TooltipText = card.Name + $" ×{group.Count()}\n" + card.Text; row.AddChild(button);
            var effects = Row(8); row.AddChild(effects); effects.AddChild(Glyph(CardSymbol(card), Ink, 22));
            var copy = Text(card.Text, 16, Ink); copy.SizeFlagsHorizontal = SizeFlags.ExpandFill; effects.AddChild(copy);
        }
    }

    private void IllustratedMaterials(Node parent,int[] amounts,Color? color=null)
    {
        var row=new HFlowContainer(); row.AddThemeConstantOverride("h_separation",16); parent.AddChild(row);
        for(var i=0;i<amounts.Length;i++)
        {
            var group=Row(5); row.AddChild(group); ItemVisual(group, ChapterContent.MaterialIds[i], 28);
            group.AddChild(Inline(amounts[i].ToString(),20,color??Ink));
            var caption=ChapterContent.MaterialName(i); group.FocusMode=FocusModeEnum.All; Inspect(group,()=>caption);
        }
    }

    private void ObjectInventory(Node parent,System.Collections.Generic.IEnumerable<string> items)
    {
        var ids=items.ToArray();
        if(ids.Length==0) { parent.AddChild(Inline("—",20,Muted)); return; }
        var grid=new GridContainer {Columns=2}; grid.AddThemeConstantOverride("h_separation",16); parent.AddChild(grid);
        foreach(var id in ids)
        {
            var group=Column(3); grid.AddChild(group); Illustration(group,id,90,82);
            group.AddChild(Inline(ChapterContent.Gear.TryGetValue(id,out var gear)?gear.Name:ChapterContent.ItemName(id),14,Ink));
        }
    }

    private void ProcessTrail(Node parent,(string Art,string Caption,bool Done)[] steps)
    {
        var row=Row(14); parent.AddChild(row);
        for(var i=0;i<steps.Length;i++)
        {
            if(i>0) row.AddChild(Glyph("arrow",Outline,22));
            var box=Column(4); box.SizeFlagsHorizontal=SizeFlags.ExpandFill; row.AddChild(box);
            Illustration(box,steps[i].Art,64,68,false);
            var caption=Inline((steps[i].Done?"✓ ":"")+steps[i].Caption,14,steps[i].Done?Success:Ink);
            caption.HorizontalAlignment=HorizontalAlignment.Center; box.AddChild(caption);
        }
    }
}
