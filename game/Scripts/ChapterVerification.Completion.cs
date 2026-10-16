using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CardRPG;

public partial class ChapterVerification
{
    // These helpers send raw input only: no forced focus, automatic scroll or direct business calls.
    private async Task RawClick(string id)
    {
        if(!Game.Buttons.ContainsKey(id)) {await Capture("failure-raw-"+id+".png");GD.Print("RAW_MISSING "+id+" available="+string.Join(",",Game.Buttons.Keys));}
        Require(Game.Buttons.TryGetValue(id,out var button)&&!button.Disabled,"Raw click target exists: "+id);
        AssertVisible(id);var point=button!.GetGlobalRect().GetCenter();
        for(var parent=button.GetParent();parent!=null;parent=parent.GetParent())if(parent is Control{ClipContents:true} clip)Require(clip.GetGlobalRect().Encloses(button.GetGlobalRect()),"Raw click cannot bypass clipping: "+id);
        await Pointer(point,MouseButton.Left,true);await Pointer(point,MouseButton.Left,false);await Frames(4);
    }
    private async Task RawTabTo(string id,int budget=90)
    {
        for(var i=0;i<budget;i++) {if(GetViewport().GuiGetFocusOwner()?.Name==id)return;await PressKey(Key.Tab);}
        throw new InvalidOperationException("Natural Tab path cannot reach "+id);
    }
    private async Task RawDragCard(int index,Vector2 target,bool cancel=false)
    {
        AssertVisible("card_"+index);var source=Game.Buttons["card_"+index].GetGlobalRect().GetCenter();
        await Pointer(source,MouseButton.Left,true);
        foreach(var point in new[]{source+new Vector2(30,-12),(source+target)/2,target})
        {GetViewport().PushInput(new InputEventMouseMotion{Position=point,GlobalPosition=point,Relative=new Vector2(30,-12),ButtonMask=MouseButtonMask.Left},true);await Frames(3);}
        Require(GetViewport().GuiIsDragging(),"Raw pointer gesture starts a card drag");
        if(cancel)await PressKey(Key.Escape);
        await Pointer(target,MouseButton.Left,false);await Frames(5);
    }
    private async Task Joy(JoyButton button)
    {
        GetViewport().PushInput(new InputEventJoypadButton{ButtonIndex=button,Pressed=true});await Frames(2);
        GetViewport().PushInput(new InputEventJoypadButton{ButtonIndex=button,Pressed=false});await Frames(4);
    }
    private async Task VerifyCompletion(string completed)
    {
        Require(Math.Abs(ChapterGame.UiScaleFor(3840,2160,192,1)-2)<.001,"HiDPI scale policy uses actual window DPI");
        Require(Math.Abs(ChapterGame.UiScaleFor(1280,720,96,1)-720f/760)<.001,"720 scale policy retains minimum logical layout height");
        GetWindow().Size=new Vector2I(1280,800);UiCopy.Stress=false;
        Game.ReplaceSession(ChapterSession.FromJson(completed));await Frames(8);
        var before=Game.Session.ToJson();
        Game.ShowStartupForVerification();await Frames();await Capture("completion-startup.png");
        await RawClick("startup_new");await RawClick("cancel_action");Require(Game.Session.ToJson()==before,"Starting over requires explicit archive confirmation");await RawClick("startup_continue");
        await RawClick("camp_preparation");Require(Game.Buttons.ContainsKey("stock_more"),"Camp cargo object opens inventory directly");
        Require(GetViewport().GuiGetFocusOwner()?.Name.ToString().StartsWith("stock_")==true,"Inventory naturally focuses a real object");
        await PressKey(Key.F6);await RawTabTo("stock_more");await PressKey(Key.Enter);
        Require(Game.Buttons.ContainsKey("menu_detail")&&Game.Session.ToJson()==before,"Natural keyboard path opens actions without mutation");
        await RawClick("menu_detail");Require(Game.Buttons.ContainsKey("detail_return"),"Context detail opens");
        await RawClick("detail_reference");await RawClick("detail_return");await PressKey(Key.Escape);await PressKey(Key.C);
        await RawTabTo("overview_card_C01");await PressKey(Key.Enter);await RawTabTo("overview_detail");await PressKey(Key.Enter);
        Require(Game.GetNode("ChapterPresentation").FindChild("PinnedReference",true,false)!=null,"A pinned snapshot persists while another object is opened for comparison");
        Require(Game.Session.ToJson()==before,"Reading and pinning comparisons do not mutate stock or RNG");
        AssertManualLayout();await Capture("completion-pinned-comparison.png");await RawClick("detail_clear_reference");
        await PressKey(Key.Escape);await PressKey(Key.Escape);Require(Game.Session.ToJson()==before,"Nested detail return and menu cancel preserve stock");
        await RawClick("depart");Require(Game.Buttons.ContainsKey("prepare_depart"),"Departure object opens a separate final check");await PressKey(Key.Escape);
        await Joy(JoyButton.Start);Require(Game.Buttons.ContainsKey("resume"),"Synthetic controller Start reaches system menu");
        await Joy(JoyButton.B);Require(Game.Buttons.ContainsKey("depart"),"Synthetic controller B returns without activating departure");
        await Joy(JoyButton.DpadRight);Require(GetViewport().GuiGetFocusOwner() is ChapterHotspot,"D-pad enters spatial scene navigation");
        var focus=GetViewport().GuiGetFocusOwner();GetViewport().PushInput(new InputEventJoypadMotion{Axis=JoyAxis.LeftX,AxisValue=.7f});await Frames();
        var moved=GetViewport().GuiGetFocusOwner();GetViewport().PushInput(new InputEventJoypadMotion{Axis=JoyAxis.LeftX,AxisValue=.66f});await Frames();
        Require(GetViewport().GuiGetFocusOwner()==moved,"Axis hysteresis prevents repeated navigation while held");
        GetViewport().PushInput(new InputEventJoypadMotion{Axis=JoyAxis.LeftX,AxisValue=.1f});await Frames();
        Require(Game.Session.ToJson()==before,"Input modality changes never execute scene actions");

        // Fixture setup ends before measured mouse input begins.
        var field=new ChapterSession();field.Depart();field.Move("A03");field.Act("scrap");Game.ReplaceSession(field);await Frames(8);
        await PressKey(Key.I);var freeKey=Game.Session.GroundHere.Single().Key;before=Game.Session.ToJson();
        await RawClick("close_panel");Require(Game.Buttons.ContainsKey("confirm_action")&&Game.Session.GroundHere.Single().Value.FreePickup,"Closing a free pickup session requires an explicit choice");
        await RawClick("cancel_action");Require(Game.Session.ToJson()==before,"Cancel retains the free pickup boundary");
        await RawTabTo("loot_plan_"+freeKey.Replace(':','_'));await PressKey(Key.Enter);
        Require(Game.Buttons.ContainsKey("confirm_action"),"Custom pickup plan is reachable by natural tab navigation");
        await Capture("completion-pickup-plan.png");await PressKey(Key.Escape);Require(Game.Session.ToJson()==before,"Nested plan cancellation does not finish free pickup");

        Game.ReplaceSession(ChapterSession.FromJson(completed));await Frames(8);await RawClick("camp_trial");before=Game.Session.ToJson();
        var hand=Game.GetNode<Control>("ChapterPresentation/HandViewport/FloatingHand");var names=hand.GetChildren().OfType<Button>().Select(b=>b.Name.ToString()).ToArray();
        await RawClick("hand_order");await RawClick("card_0");await RawClick("hand_right");
        hand=Game.GetNode<Control>("ChapterPresentation/HandViewport/FloatingHand");Require(hand.GetChild(1).Name=="card_0"&&Game.Session.ToJson()==before,"Hand ordering changes display only, preserving RNG and real piles");
        await RawClick("hand_order");await RawDragCard(0,new Vector2(8,8));Require(Game.Session.ToJson()==before,"Invalid card drop is lossless");
        var resource=(Control)Game.GetNode<Control>("ChapterPresentation").FindChild("BattleResources",true,false);
        await RawDragCard(0,resource.GetGlobalRect().GetCenter(),true);Require(Game.Session.ToJson()==before,"Esc cancels card drag without playing");
        await RawDragCard(0,resource.GetGlobalRect().GetCenter());Require(Game.Session.State.Battle!.TrialToolUsed&&Game.Session.State.Battle.ExtraDraw==2,"Self-target drag plays the actual draw card exactly once");
        if(Game.Buttons.ContainsKey("skip_feedback"))await RawClick("skip_feedback");
        Require(Game.Session.State.Battle!.Hand.Count==6,"Actual tool draw produces six cards");await Capture("completion-six-cards.png");
        var attack=Game.Session.State.Battle!.Hand.IndexOf("C01");await RawDragCard(attack,Game.Buttons["target_0"].GetGlobalRect().GetCenter());
        Require(Game.Session.State.World.Contains("trial-complete"),"Tool then attack completes the real two-step trial");
        if(Game.Buttons.ContainsKey("skip_feedback"))await RawClick("skip_feedback");await Capture("completion-battle-drag.png");
        var trial=Game.Session.PreviewCopy();trial.State.Battle!.Action=0;trial.State.Battle.Mana=0;Game.ReplaceSession(trial);await Frames();
        Require(Game.Buttons["end_turn"].Text.Contains("无可用牌"),"No-legal-card hint uses actual resource and target checks");
        Require(Game.Session.State.Battle.Round==1,"No-legal-card hint does not automatically end the turn");

        Game.ReplaceSession(ChapterSession.FromJson(completed));await Frames(8);await RawClick("craft");await RawClick("recipe_G26");await RawTabTo("recipe_trial");
        var trialStock=JsonSerializer.Serialize(Game.Session.State.Stock);var trialRng=Game.Session.State.RandomState;
        await PressKey(Key.Enter);Require(Game.Session.State.Battle?.Id=="trial","Recipe starts borrowed trial");await RawClick("end_trial");
        Require(Game.Buttons.TryGetValue("recipe_G26",out var recipeReturn)&&recipeReturn.Text.StartsWith("◆"),"Leaving recipe-linked trial returns to the same recipe, even if already completed");
        Require(JsonSerializer.Serialize(Game.Session.State.Stock)==trialStock&&Game.Session.State.RandomState==trialRng,"Borrowed trial returns real inventory and RNG unchanged");

        Game.ReplaceSession(ChapterSession.FromJson(completed));await Frames(8);await PressKey(Key.I);await Capture("completion-inventory.png");await VerifyRenderedContrast();
        await PressKey(Key.Escape);GetWindow().Size=new Vector2I(1280,720);await Frames(10);await PressKey(Key.I);AssertManualLayout();await Capture("completion-720.png");await PressKey(Key.Escape);
        GetWindow().Size=new Vector2I(1100,760);await Frames(10);await PressKey(Key.Escape);await RawTabTo("font_150");await PressKey(Key.Enter);await RawClick("resume");
        UiCopy.Stress=true;Game.Render();await Frames(8);await PressKey(Key.I);AssertManualLayout();AssertStorageGrid();await Capture("completion-expanded-copy.png");
        await RawTabTo("stock_more");await PressKey(Key.Enter);await RawClick("menu_detail");AssertManualLayout();await Capture("completion-expanded-detail.png");
        UiCopy.Stress=false;await PressKey(Key.Escape);await PressKey(Key.Escape);await PressKey(Key.Escape);await RawTabTo("font_100");await PressKey(Key.Enter);await RawClick("resume");
        GetWindow().Size=new Vector2I(1280,800);await Frames(8);
        var delivery=ChapterSession.FromJson(JsonSerializer.Serialize(new ChapterState {ContentVersion="chapter1-outer-ring-v1",Extractions=1,Bank=[0,0,0],Cargo=[0,0,0],StoredItems=["T01"],World=["robot-rescued","robot-arrived"]}));
        Game.ReplaceSession(delivery);await Frames(8);await PressKey(Key.J);await RawClick("quest_batch");await RawClick("quest_batch_begin");await RawClick("confirm_action");
        Require(Game.Session.State.Done.Contains("Q01")&&!Game.Session.State.Done.Contains("S07")&&Game.Buttons.ContainsKey("story_continue"),"Quest queue pauses after its first story before executing another reward");
        await Capture("completion-batch-story.png");await RawClick("story_stop_batch");await Frames(15);Require(!Game.Session.State.Done.Contains("S07"),"Stopping at a story leaves later deliveries pending");
        Game.SimulateSaveFailure=true;before=Game.Session.ToJson();await RawClick("quest_batch_resume");Require(Game.Session.ToJson()==before,"Failed save rolls back only the attempted quest and retains the previous completed quest");
        Game.SimulateSaveFailure=false;await RawClick("quest_batch_resume");await Frames(10);Require(Game.Session.State.Done.SetEquals(["Q01","S07"]),"Resuming the explicit queue completes each selected reward once");await Capture("completion-batch-complete.png");
        GD.Print("UI_COMPLETION_VERIFIED | raw navigation, input ownership, synthetic controller, pickup boundary, card drag/cancel, hand display order, 720 render, expanded names/rules; real hardware and human efficiency not measured");
    }
    private static double Luminance(Color color)
    {double Convert(double x)=>x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);return .2126*Convert(color.R)+.7152*Convert(color.G)+.0722*Convert(color.B);}
    private async Task VerifyRenderedContrast()
    {
        if(_captures==null)return;
        var labels=new List<Label>();void Collect(Node node){if(node is Label label&&label.IsVisibleInTree()&&label.Text.Length>0)labels.Add(label);foreach(var child in node.GetChildren())Collect(child);}Collect(Game.GetNode("ChapterPresentation"));
        var candidates=labels.Where(l=>l.GetThemeColor("font_color").A>.99f).ToArray();
        using var foreground=GetViewport().GetTexture().GetImage();
        var old=candidates.Select(l=>l.SelfModulate).ToArray();foreach(var label in candidates)label.SelfModulate=new Color(1,1,1,0);await Frames(4);
        using var background=GetViewport().GetTexture().GetImage();for(var i=0;i<candidates.Length;i++)candidates[i].SelfModulate=old[i];await Frames(4);
        var results=new List<object>();
        foreach(var label in candidates)
        {
            var rect=label.GetGlobalRect().Intersection(GetViewport().GetVisibleRect());bool disabled=false;
            for(var parent=label.GetParent();parent!=null;parent=parent.GetParent()) {if(parent is Button {Disabled:true})disabled=true;if(parent is Control{ClipContents:true} clip)rect=rect.Intersection(clip.GetGlobalRect());}
            if(disabled||rect.Size.X<1||rect.Size.Y<1)continue;
            var ink=label.GetThemeColor("font_color");double lowest=100;Vector2I sample=default;Color fg=default,bg=default;var pixels=0;
            for(var y=Math.Max(0,(int)rect.Position.Y);y<Math.Min(foreground.GetHeight(),rect.End.Y);y++)for(var x=Math.Max(0,(int)rect.Position.X);x<Math.Min(foreground.GetWidth(),rect.End.X);x++)
            {
                var a=foreground.GetPixel(x,y);var b=background.GetPixel(x,y);
                if(Math.Abs(a.R-ink.R)+Math.Abs(a.G-ink.G)+Math.Abs(a.B-ink.B)>.035 ||Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)<.15)continue;
                pixels++;var first=Luminance(a);var second=Luminance(b);var ratio=(Math.Max(first,second)+.05)/(Math.Min(first,second)+.05);
                if(ratio<lowest){lowest=ratio;sample=new(x,y);fg=a;bg=b;}
            }
            if(pixels<4)continue;
            var font=label.GetThemeFontSize("font_size");var target=font>=24?3:4.5;
            results.Add(new{Text=label.Text,Pixels=pixels,MinimumRatio=lowest,Target=target,X=sample.X,Y=sample.Y,Foreground=fg.ToHtml(),ActualBackground=bg.ToHtml()});
            Require(lowest>=target,"Rendered text contrast: "+label.Text+" = "+lowest.ToString("0.00"));
        }
        Require(results.Count>=10,"Contrast audit measures actual visible text on rendered backgrounds");
        File.WriteAllText(Path.Combine(_captures,"contrast-samples.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        foreground.SavePng(Path.Combine(_captures,"contrast-with-text.png"));background.SavePng(Path.Combine(_captures,"contrast-background.png"));
    }
}
