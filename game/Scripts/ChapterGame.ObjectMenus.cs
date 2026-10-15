using Godot;
using System;
using System.Linq;
using CardRPG.Core;

namespace CardRPG;

public partial class ChapterGame
{
    private void BindObjectMenu(Button button, Action open)
    {
        button.GuiInput+=input=>
        {
            if(input is InputEventMouseButton{ButtonIndex:MouseButton.Right,Pressed:true} || input is InputEventKey{Pressed:true,Echo:false} key && (key.Keycode==Key.Menu || key.Keycode==Key.F10&&key.ShiftPressed))
            {button.AcceptEvent(); open();}
        };
    }
    private void ObjectMenu(string title, params (string Id,string Caption,Action Action,bool Enabled)[] entries)
    {
        if(GetViewport().GuiIsDragging())return;
        CloseStockMenu();_stockMenuFocus=GetViewport().GuiGetFocusOwner()?.Name.ToString();_inspection?.Hide();
        var shade=new Control{Name="ObjectContextMenu",MouseFilter=MouseFilterEnum.Stop,ZIndex=60};_root.AddChild(shade);shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);_stockMenu=shade;
        shade.GuiInput+=e=>{if(e is InputEventMouseButton{Pressed:true}){shade.AcceptEvent();CloseStockMenu();}};
        var panel=FlatPanel(shade,12);panel.MouseFilter=MouseFilterEnum.Stop;
        var point=GetGlobalMousePosition();var height=Math.Min(Size.Y-40,100+entries.Length*54);
        panel.Position=new Vector2(Math.Clamp(point.X,20,Size.X-430),Math.Clamp(point.Y,20,Size.Y-height-20));panel.Size=new Vector2(410,height);
        var content=Column(8);panel.AddChild(content);content.AddChild(Text(title,20,Ink));
        var scroll=new ScrollContainer {FollowFocus=true,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=SizeFlags.ExpandFill};content.AddChild(scroll);var page=Column(8);page.SizeFlagsHorizontal=SizeFlags.ExpandFill;scroll.AddChild(page);
        foreach(var entry in entries)page.AddChild(Button("menu_"+entry.Id,entry.Caption,()=>{CloseStockMenu();entry.Action();},entry.Enabled));
        page.AddChild(Button("menu_close",UiCopy.Text("UI_97889C508F46"),CloseStockMenu));
        FocusLater(Buttons.First(p=>p.Key.StartsWith("menu_")&&!p.Value.Disabled).Value);
    }
    private void RecipeMenu(string kind)=>ObjectMenu("图纸 · "+ChapterContent.ItemName(kind),
        ("recipe","查看条件与材料",()=>OpenRecipe(kind),true),
        ("recipe_plan",Session.State.RecipePlan.Contains(kind)?"移出材料计划":"加入材料计划",()=>ToggleRecipePlan(kind),Session.State.RecipePlan.Contains(kind)||!Session.HasPhysical(kind)&&Session.State.RecipePlan.Count<4),
        ("compare","比较当前装备",()=>OpenGearComparison(kind),ChapterContent.Gear.ContainsKey(kind)),
        ("pin",_pinnedTasks.Contains("recipe:"+kind)?"取消固定入口":"固定入口",()=>TogglePinReference("recipe:"+kind),_pinnedTasks.Contains("recipe:"+kind)||_pinnedTasks.Count<4));
    private void QuestMenu(string id)=>ObjectMenu("任务 · "+ChapterContent.Quests.Single(q=>q.Id==id).Name,
        ("quest","查看任务",()=>OpenPinned("quest:"+id),true),
        ("track","追踪 / 取消追踪",()=>Change(()=>Session.TrackQuest(Session.State.TrackedQuest==id?"":id)),!Session.State.Done.Contains(id)),
        ("deliver","交付此任务",()=>DeliverQuest(id),Session.CanTurnIn(id)),
        ("pin",_pinnedTasks.Contains("quest:"+id)?"取消固定入口":"固定入口",()=>TogglePinReference("quest:"+id),_pinnedTasks.Contains("quest:"+id)||_pinnedTasks.Count<4));
    private void TogglePinReference(string key) {if(!_pinnedTasks.Remove(key)&&_pinnedTasks.Count<4)_pinnedTasks.Add(key);SavePreferences();Render();}
    private void CardMenu(string id)=>ObjectMenu(ChapterContent.Cards[id].Name,
        ("card_detail","固定完整规则与供牌来源",()=>{_detailItemId=0;_detailTitle=ChapterContent.Cards[id].Name;_detailText=CardRules(id);OpenNested("detail");},true),
        ("deck","查看当前完整卡组",()=>OpenNested("character"),true));
}
