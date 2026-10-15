using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private readonly HashSet<string> _readDiscoveries = new(), _dismissedOpportunities = new();
    private readonly List<string> _discoveryOrder = new();
    private string _discoverySort = "recent";
    private bool _allOpportunities;
    private ulong _rescueStarted;
    private IEnumerable<string> KnownDiscoveries => Session.State.Done.Concat(Session.State.World).Concat(Session.State.Intel).Where(DiscoveryKnown).Distinct();
    private static bool DiscoveryKnown(string key) => Stories.ContainsKey(key) || key is "dark-door" or "shovel-inspected" or "leader-won" or "trial-complete" or "S01" or "S07" or "relay-seen" or "glowing-manifest";
    private static string DiscoveryTitle(string key) => Stories.TryGetValue(key, out var story) ? story.Title : key switch
    { "dark-door" => "荧光门显露", "shovel-inspected" or "glowing-manifest" => "夹层货单与清障铲", "leader-won" => "军需领班已击败", "trial-complete" => "检修工具试用完成", "S01" => "前进站开放", "S07" => "维修偶在余火站安家", _ => "工坊墙后的荧光线索" };
    private static string DiscoveryArt(string key) => key switch
    { "robot-rescued" or "S07" => "repair-robot", "Q01" or "relay-seen" => "T01", "Q02" or "trial-complete" => "G26", "station" or "S01" => "T11", "S09" => "clothes-rack", "leader-won" => "G03", _ => "G25" };
    private void RememberDiscoveries()
    {
        var known = KnownDiscoveries.ToHashSet();
        // Legacy saves have no event timestamp for every discovery. Preserve the observed order without inventing dates.
        foreach (var key in known.Where(k => !_discoveryOrder.Contains(k))) _discoveryOrder.Add(key);
        SavePreferences();
    }
    private void RecallDiscovery(string key)
    {
        _readDiscoveries.Add(key); SavePreferences();
        if (Stories.ContainsKey(key)) { _story = key; OpenNested("story"); return; }
        _detailItemId = 0; _detailTitle = DiscoveryTitle(key);
        _detailText = key switch
        {
            "dark-door" => "维修工坊的检修灯熄灭后，荧光门永久显露。地图上的夹层入口已经开放。",
            "leader-won" => "首胜已永久记录。破甲锤若尚未登记，可回广场取回领班工具箱；回顾不会再次发奖。",
            "trial-complete" => "先使用预备工具，再打出攻击牌，已完成台架试用。检修工具手套配方开放；借装不留入真实库存。",
            "S01" => "前进站可以作为部署起点。货站修复和安全出口永久保留。",
            "S07" => "小维修偶已在余火站安家。它会挥动扳手，指向维修工坊墙后的荧光线索。",
            "relay-seen" => "工坊墙后有荧光痕迹。可到维修工坊检查检修灯。",
            _ => "夹层中的货单和清障工具已被发现。物品仍以实际库存为准，回顾不会生成物品。"
        };
        OpenNested("detail");
    }
    private void DiscoveryJournal(VBoxContainer box)
    {
        Tabs(box,"discovery_sort_",[("recent","最近记录"),("name","名称")],_discoverySort,value=>_discoverySort=value);
        var known = KnownDiscoveries.ToHashSet();
        var keys = _discoveryOrder.Where(known.Contains).Concat(known.Except(_discoveryOrder)).Distinct();
        keys = _discoverySort == "name" ? keys.OrderBy(DiscoveryTitle) : keys.Reverse();
        var page = Scroll(box);
        foreach (var key in keys)
        {
            var row = Row(12); page.AddChild(row); Illustration(row,DiscoveryArt(key),76,70,false);
            var details = FlexibleColumn(row);
            details.AddChild(Button("recall_"+key,(_readDiscoveries.Contains(key) ? "" : UiCopy.Text("UI_D5BB2D7EC9EF"))+DiscoveryTitle(key),()=>RecallDiscovery(key)));
            details.AddChild(Text(Stories.TryGetValue(key,out var story) ? story.Person : UiCopy.Text("UI_E123B1F0AC13"),14,Muted));
        }
        if (known.Count == 0) page.AddChild(Text(UiCopy.Text("UI_262570B24104"),16,Muted));
        box.AddChild(Button("discoveries_read",UiCopy.Text("UI_AA1B652683A2"),()=>{ _readDiscoveries.UnionWith(known); SavePreferences(); Render(); }));
        box.AddChild(Text(UiCopy.Text("UI_499FC608BF1A"),14,Muted));
    }
    private IEnumerable<(string Key,string Title,string Target)> Opportunities()
    {
        foreach(var quest in ChapterContent.Quests.Where(q=>Session.CanTurnIn(q.Id))) yield return ("quest:"+quest.Id,"可交付："+quest.Name,"quest:"+quest.Id);
        if(Session.State.Done.Contains("Q02")) { yield return ("craft","工坊制作与改装已开放","craft"); yield return ("trial","免费试用检修工具","recipe:G26"); }
        if(Session.State.Done.Contains("S01")) yield return ("forward","可以从前进站部署","prepare");
        if(Session.State.Extractions>0||Session.State.Done.Count>0)
        {
            foreach(var kind in ChapterContent.Recipes.Keys.Where(Session.RecipeOpen).Concat(new[]{"BP01","BP02","BP03"}.Where(Session.BackpackRecipeOpen)))
                yield return ("recipe:"+kind,"新配方可查看："+ChapterContent.ItemName(kind),"recipe:"+kind);
            foreach(var kind in Session.State.Owned.Where(k=>k is not ("G01" or "G02" or "G09")))yield return("gear:"+kind,"已登记装备："+ChapterContent.ItemName(kind),"gear:"+kind);
            if(Session.State.Done.Contains("Q02"))foreach(var kind in Session.State.Owned)foreach(var mod in new[]{"D01","D08"}.Where(m=>ChapterSession.CompatibleMod(kind,m)))
                yield return("mod:"+kind+":"+mod,"可查看改装："+ChapterContent.ItemName(kind)+" / "+(mod=="D01"?"反击":"抽牌"),"mod:"+kind+":"+mod);
        }
        foreach(var key in KnownDiscoveries.Where(k=>!_readDiscoveries.Contains(k))) yield return ("discovery:"+key,"新发现："+DiscoveryTitle(key),"discovery:"+key);
    }
    private void OpportunityEntry()
    {
        var entries = Opportunities().Where(o=>!_dismissedOpportunities.Contains(o.Key)).ToArray();
        if(entries.Length == 0) return;
        var news=Button("new_opportunities",UiCopy.Text("UI_FEF508B8C26F")+entries.Length,()=>Toggle("opportunities"));
        _hud!.AddChild(news); Dock(news,-300,Session.State.Warned?144:174,-20,Session.State.Warned?188:218,al:1,ar:1);
    }
    private void OpportunityPage(VBoxContainer box)
    {
        var entries=Opportunities().Where(o=>!_dismissedOpportunities.Contains(o.Key)).ToArray(); var page=Scroll(box);
        if(entries.Length>4)box.AddChild(Button("opportunities_expand",_allOpportunities?UiCopy.Text("UI_A1DFC6A11B2F"):UiCopy.Text("UI_0380F4AB531C")+(entries.Length-4)+UiCopy.Text("UI_F9D529EACDF3"),()=>{_allOpportunities=!_allOpportunities;Render();}));
        foreach(var entry in entries.Take(_allOpportunities?entries.Length:4))
        {
            var row=Row(); page.AddChild(row);
            row.AddChild(Button("opportunity_"+entry.Key,entry.Title+" →",()=>
            {
                if(entry.Target.StartsWith("discovery:")) RecallDiscovery(entry.Target[10..]);
                else if(entry.Target.StartsWith("gear:"))OpenGearComparison(entry.Target[5..]);
                else if(entry.Target.StartsWith("mod:")){var parts=entry.Target.Split(':');OpenNested("craft");_craftTab="mods";_selectedModGear=parts[1];_selectedMod=parts[2];Render();}
                else if(entry.Target.Contains(':')) OpenPinned(entry.Target);
                else OpenNested(entry.Target);
            }));
            row.AddChild(Button("dismiss_"+entry.Key,UiCopy.Text("UI_DE32E20193AD"),()=>{_dismissedOpportunities.Add(entry.Key); SavePreferences(); Render();}));
        }
        if(entries.Length==0) page.AddChild(Text(UiCopy.Text("UI_E468BB14D4AD"),16,Muted));
        box.AddChild(Button("dismiss_all_opportunities",UiCopy.Text("UI_ACA8C9AE391B"),()=>{foreach(var entry in entries)_dismissedOpportunities.Add(entry.Key);SavePreferences();ClosePanel();}));
    }
    private string PressureForecast(int cost)
    {
        var s=Session.State; var threshold=s.Extractions==0?24:18;
        var next=new[]{(10,"转运箱开始撤走预告"),(13,"转运箱撤走"),(threshold,"巡逻出发"),(26,"新精英护盾增加")}.OrderBy(p=>p.Item1).FirstOrDefault(p=>p.Item1>s.Pressure && (p.Item1!=threshold || !s.Run.Contains("K09")));
        return Session.PressureText+"\n"+(next.Item1==0?"没有更高的已知压力阶段":$"距 {next.Item2} 还差 {next.Item1-s.Pressure} 压力")+$"\n此操作 {s.Pressure} → {s.Pressure+cost}"+(next.Item1>0&&s.Pressure+cost>=next.Item1?" · 将跨过阈值":"")+"\n"+Session.PatrolText;
    }
    private void SceneTraces()
    {
        var s=Session.State;
        var actions=s.Location switch { "A02"=>new[]{"K01","crate","transfer"},"A03"=>new[]{"scrap","supplies","cloth","K02","rescue"},"A04"=>new[]{"bench","dark"},"A05"=>new[]{"repair","wire","medicine","frame"},"A07"=>new[]{"train-inspect"},_=>Array.Empty<string>() };
        foreach(var id in actions)
        {
            var key=id switch {"rescue"=>"robot-rescued","dark"=>"dark-door","repair"=>"station",_=>id};
            if(!s.Run.Contains(key)&&!s.World.Contains(key)||Buttons.ContainsKey("act_"+id)||id=="bench"&&Buttons.ContainsKey("field_equipment"))continue;
            var (point,label)=ActionPins[id];
            var pin=Pin("trace_"+id,"✓ "+label,point,()=>
            {
                if(DiscoveryKnown(key)) {RecallDiscovery(key);return;}
                _detailItemId=0;_detailTitle=UiCopy.Text("UI_D097506C04C2")+label;
                var ground=Session.GroundHere.Where(g=>g.Key.Contains(id)).SelectMany(g=>s.Stock.Where(i=>i.Container==g.Key)).ToArray();
                _detailText=UiCopy.Text("UI_4B1FB2DE88B4")+(ground.Length>0?UiCopy.Text("UI_3B4A629A21E3")+string.Join("\n",ground.Select(i=>ChapterContent.ItemName(i.Kind)+" ×"+i.Quantity)):UiCopy.Text("UI_625B6FE6C698"));OpenNested("detail");
            },UiCopy.Text("UI_28B84D31B6F9"),tint:Muted);
            ((ChapterHotspot)pin).Symbol="check";
        }
    }
    private void RobotGesture()
    {
        if(_root.GetNodeOrNull<ChapterScene>("World") is { } scene && !_reducedMotion)
        {scene.RobotGesture=1; var tween=scene.CreateTween();tween.TweenMethod(Callable.From<float>(v=>{scene.RobotGesture=v;scene.QueueRedraw();}),1f,0f,2.5);}
        _detailItemId=0;_detailTitle=UiCopy.Text("UI_20CDC02CB48D");_detailText=UiCopy.Text("UI_5BBA1EF085D1");
        // Keep the scene gesture visible; the persistent record is one explicit click away.
        _toast=_detailText;_toastUntil=Time.GetTicksMsec()+5500;Toast();
    }
    private void ReflowHotspotLabels()
    {
        var pins=Buttons.Values.OfType<ChapterHotspot>().Where(p=>GodotObject.IsInstanceValid(p)&&p.IsInsideTree()).ToArray();
        var markers=pins.Select(p=>p.GetRect().Grow(5)).ToArray();var placed=new List<Rect2>();
        foreach(var pin in pins)
        {
            var size=pin.LabelRect().Size;var preferred=pin.Position+new Vector2((pin.Size.X-size.X)/2,pin.Size.Y+5);
            var candidates=new List<Vector2>{preferred};
            for(var y=252f;y+size.Y<Size.Y-96;y+=size.Y+10)
                for(var x=16f;x+size.X<Size.X-16;x+=32)candidates.Add(new(x,y));
            foreach(var point in candidates.OrderBy(p=>p.DistanceSquaredTo(preferred)))
            {
                var rect=new Rect2(point,size);if(!new Rect2(12,244,Size.X-24,Size.Y-340).Encloses(rect)||markers.Any(m=>m.Intersects(rect))||placed.Any(p=>p.Grow(4).Intersects(rect)))continue;
                pin.LabelOffset=point-preferred;placed.Add(rect);pin.QueueRedraw();break;
            }
        }
    }
}
