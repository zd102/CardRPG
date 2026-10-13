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

    private void SupplyCards(Node parent,string gear,bool compact=false)
    {
        var row=new HFlowContainer { Name="SupplyCards" };
        row.AddThemeConstantOverride("h_separation",8); row.AddThemeConstantOverride("v_separation",8); parent.AddChild(row);
        foreach(var group in Session.GearCards(gear).GroupBy(id=>id))
        {
            var card=ChapterContent.Cards[group.Key];
            var tile=Button("supply_"+gear+"_"+card.Id,"",()=>{});
            tile.CustomMinimumSize=new Vector2(compact?94:112,142);
            tile.AddThemeStyleboxOverride("normal",ChapterSkin.Box("ledger-paper",6));
            row.AddChild(tile);
            var costs=Row(4); costs.MouseFilter=MouseFilterEnum.Ignore; tile.AddChild(costs); Dock(costs,5,3,-26,25,ar:1);
            SymbolValue(costs,card.Mana>0?"mana":"action",(card.Mana>0?card.Mana:card.Action).ToString(),Ink,size:14);
            var art=new ChapterIllustration {Art=CardArt(card),Mounted=false}; tile.AddChild(art); Dock(art,8,28,-8,65,ar:1);
            var title=Inline(card.Name,14,Ink); title.HorizontalAlignment=HorizontalAlignment.Center;
            tile.AddChild(title); Dock(title,3,66,-3,87,ar:1);
            var count=Inline("×"+group.Count(),14,Accent); count.HorizontalAlignment=HorizontalAlignment.Right;
            tile.AddChild(count); Dock(count,-29,3,-5,25,al:1,ar:1);
            var effects=Column(1); effects.MouseFilter=MouseFilterEnum.Ignore; tile.AddChild(effects); Dock(effects,4,89,-4,115,ar:1);
            CardEffects(effects,card,Ink,size:14);
            var keyword=Inline(CardKeyword(card.Id),14,Muted); keyword.HorizontalAlignment=HorizontalAlignment.Center;
            tile.AddChild(keyword); Dock(keyword,3,118,-3,139,ar:1);
            tile.TooltipText=card.Name+$" ×{group.Count()} · {card.Action} 行动 / {card.Mana} 魔法\n"+card.Text;
        }
    }

    private void IllustratedMaterials(Node parent,int[] amounts,Color? color=null)
    {
        var row=new HFlowContainer(); row.AddThemeConstantOverride("h_separation",16); parent.AddChild(row);
        for(var i=0;i<amounts.Length;i++)
        {
            var group=Row(5); row.AddChild(group); Illustration(group,ItemArt(ChapterContent.MaterialIds[i]),28,28,false);
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
