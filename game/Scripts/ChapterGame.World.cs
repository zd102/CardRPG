using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private void Header()
    {
        var s = Session.State;
        _hud = new Control { Name = "Hud", MouseFilter = MouseFilterEnum.Ignore };
        _root.AddChild(_hud); _hud.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var plate = FlatPanel(_hud, 12); plate.Name = "Vitals";
        Dock(plate, 20, 18, 310, 82);
        var status = Column(6); plate.AddChild(status);
        var vitals = Row(18); status.AddChild(vitals);
        SymbolValue(vitals, "heart", $"{s.Health}/42", s.Health <= 14 ? Danger : Success, "生命；低于三分之一时需要补给。");
        SymbolValue(vitals, "supply", s.Supplies.ToString(), Ink, "补给；探索中可消耗一份恢复生命。");
        SymbolValue(vitals, "bag", $"{Session.CarryUsed}/{Session.CarryCapacity}", Accent, Session.LoadDescription);
        HealthBar(status, s.Health, 42, s.Health <= 14 ? Danger : Success);
        var locationPlate = FlatPanel(_hud, 8); locationPlate.Name = "LocationPlate";
        Dock(locationPlate, -110, 18, 110, 82, .5f, 0, .5f, 0);
        var title = Column(3); locationPlate.AddChild(title);
        var name = Text(s.Phase is ChapterPhase.Camp or ChapterPhase.Extracted or ChapterPhase.Defeated ? "余火站" : s.Battle?.Id == "trial" ? "工坊台架" : ChapterContent.Places[s.Location].Name, 20, Ink);
        name.HorizontalAlignment = HorizontalAlignment.Center; title.AddChild(name);
        var location = Row(6); location.Alignment = BoxContainer.AlignmentMode.Center; title.AddChild(location);
        if (s.Phase is ChapterPhase.Camp or ChapterPhase.Extracted or ChapterPhase.Defeated)
            SymbolValue(location, "home", "安全", Muted, "安全据点；整备、阅读与制作不推进区域压力。", 14);
        else
            SymbolValue(location, "eye", s.Pressure.ToString(), s.Warned ? Danger : Muted, Session.PressureText + "\n" + Session.PatrolText, 14);
        var menu = Row(6); _hud.AddChild(menu); Dock(menu, -378, 18, -20, 64, al: 1, ar: 1);
        foreach (var item in new[] { ("character", "character", "角色全览 · C", "character"), ("map", "map", "地图 · M", "map"), ("inventory", "bag", "装备与背包 · I", "gear"),
                     ("journal", "journal", "任务与发现 · J", "journal"), ("craft", "craft", "制作与改装", "craft"),
                     ("help", "help", "图标与操作指引 · F1", "help"), ("quit", "close", "保存并退出", "quit") })
        {
            var id = item.Item1; var panel = item.Item4;
            var button = IconButton(id, item.Item2, panel == "quit" ? "暂停与设置 · Esc" : item.Item3, () => Toggle(panel == "quit" ? "system" : panel));
            button.CustomMinimumSize = new Vector2(46, 46);
            var ready = ChapterContent.Quests.Count(q => Session.CanTurnIn(q.Id));
            if (id == "journal" && ready > 0)
            {
                var count = Inline(ready.ToString(), 14, Accent); button.AddChild(count); Dock(count, -16, 0, -2, 20, al: 1, ar: 1);
            }
            menu.AddChild(button);
        }
        if (_panel == "" && s.Phase is ChapterPhase.Camp or ChapterPhase.Exploring)
        {
            var objective = Button("objective", "", () => Toggle("journal")); objective.TooltipText = "当前目标\n" + Session.Objective;
            _hud.AddChild(objective); Dock(objective, 20, 96, 370, 164);
            var strip = Row(9); strip.MouseFilter = MouseFilterEnum.Ignore; objective.AddChild(strip); Dock(strip,12,8,-12,-8,ar:1,ab:1);
            strip.AddChild(Glyph("journal", Accent, 24)); var objectiveCopy = Text(Session.NextObjective,16,Ink); objectiveCopy.AddThemeFontSizeOverride("font_size",16); objectiveCopy.SizeFlagsHorizontal=SizeFlags.ExpandFill; strip.AddChild(objectiveCopy);
        }
    }

    private VBoxContainer Scroll(Node parent)
    {
        var scroll = new ScrollContainer { FollowFocus = true, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _scroll ??= scroll;
        _pageScrolls.Add(scroll);
        parent.AddChild(scroll); var box = Column(12); box.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(box); return box;
    }

    private Button Pin(string id, string label, Vector2 point, Action action, string tooltip, bool enabled = true, bool exit = false, Color? tint = null)
    {
        var button = new ChapterHotspot
        {
            Name = id, Caption = label, TooltipText = label + "\n" + tooltip, Disabled = !enabled, Exit = exit, ShowLabel = _labels,
            Symbol = PinSymbol(id, exit), Important = id is "act_relay" or "act_clamp" or "act_calibrate" || id == "camp_quests" && Session.CanTurnIn("Q01"),
            Tint = tint ?? (exit ? Success : Accent), FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.Pressed += () => { if (!button.Disabled && !FeedbackBusy) { Cue("click"); action(); } };
        Inspect(button, () => button.TooltipText);
        _root.AddChild(button);
        var world = _root.GetNode<ChapterScene>("World");
        void PositionPin()
        {
            if (world.Size.X <= 0 || world.Size.Y <= 0) return;
            var p = world.ScenePoint(point);
            p.X = Mathf.Clamp(p.X, 90, world.Size.X - 90);
            p.Y = Mathf.Clamp(p.Y, 250, world.Size.Y - 150);
            // Keep labels legible at native pixel size when the scene is cropped by a narrow window.
            var occupied = Buttons.Values.OfType<ChapterHotspot>().Where(other => other != button).Select(other => other.LabelRect().Grow(4)).ToArray();
            var position = p;
            foreach (var offset in new[] { Vector2.Zero, new Vector2(0,-100),new Vector2(0,100),new Vector2(-220,0),new Vector2(220,0),new Vector2(-220,-100),new Vector2(220,-100),new Vector2(-220,100),new Vector2(220,100),new Vector2(0,-200),new Vector2(0,200),new Vector2(-440,0),new Vector2(440,0),new Vector2(-440,-100),new Vector2(440,-100),new Vector2(-440,100),new Vector2(440,100) })
            {
                var candidate = p + offset;
                var half = Math.Max(90, button.Caption.Length * 7 + 14);
                candidate.X = Mathf.Clamp(candidate.X, half, world.Size.X - half);
                candidate.Y = Mathf.Clamp(candidate.Y, 250, world.Size.Y - 150);
                var rect = new Rect2(candidate - new Vector2(half, 30), new Vector2(half*2, 88));
                if (occupied.Any(other => rect.Intersects(other))) continue;
                position = candidate; break;
            }
            Dock(button, position.X - 26, position.Y - 26, position.X + 26, position.Y + 26);
            button.SceneAnchor = p - position + new Vector2(26,26); button.QueueRedraw();
        }
        world.Resized += PositionPin; PositionPin();
        Buttons[id] = button; return button;
    }

    private static string PinSymbol(string id, bool exit) => exit ? "exit" : id switch
    {
        "camp_equipment" or "field_equipment" or "act_bench" or "act_calibrate" or "act_repair" => "craft",
        "camp_quests" => "journal", "camp_preparation" => "bag", "camp_trial" => "target", "home_robot" or "act_rescue" => "help",
        "act_K01" or "act_K02" or "act_K09" or "act_E13" => "hammer", "act_supplies" or "act_train-supply" => "supply",
        "act_dark" => "eye", "act_wire" or "act_train-wire" => "wire", "act_scrap" => "steel",
        "act_R01" or "act_R07" => "guard", "act_relay" => "bolt", _ => "loot"
    };

    private void Camp()
    {
        var s = Session.State;
        Pin("camp_equipment", "工作台 · 调整装备", new(.23f, .65f), () => Toggle("gear"),
            string.Join(" / ", Session.Equipped.Select(g => ChapterContent.Gear[g].Name)) + $"\n卡组 {Session.Deck().Count} 张。点击调整穿戴、备用和携带物。");
        var turnIns = ChapterContent.Quests.Count(q => Session.CanTurnIn(q.Id));
        Pin("camp_quests", turnIns > 0 ? $"岑砧 · {turnIns} 项可交付" : "岑砧 · 任务与发现", new(.48f, .52f), () => Toggle("journal"),
            s.Done.Contains("Q01") ? "工作灯亮了。暖黄的旧日投影里，城市还有晴天。\n点击查看任务或交付带回的物品。" : "岑砧在工作灯下等着那只封装继电器。\n取得任务物后安全撤离，在这里交付。");
        Pin("camp_preparation", "行前整备", new(.89f, .78f), () => Toggle("prepare"),
            "选择装备、背包、带入物与出发地点；检查格位、负重和补给预留。");
        Pin("depart", "出发 · " + ChapterContent.Places[s.Start].Name, new(.79f, .43f),
            () => Toggle("prepare"), "查看最终出发状态，从" + ChapterContent.Places[s.Start].Name + "出征。" + Session.LoadDescription, exit: true);
        if (s.Done.Contains("Q02"))
            Pin("camp_trial", "台架 · 免费试用", new(.35f, .76f), () => Change(Session.StartTrial), "借用检修手套，在不会还击的台架上试牌。可以随时退出。");
        if (s.Done.Contains("S07"))
            Pin("home_robot", "维修偶", new(.52f, .75f), () => { _toast = "小维修偶挥了挥扳手，指向工坊墙后那一抹荧光。"; _toastUntil = Time.GetTicksMsec() + 5000; Render(); }, "它已经在余火站安了家。");
    }

    private static readonly Dictionary<string, (Vector2 Point, string Label)> ActionPins = new()
    {
        ["relay"] = (new(.90f,.66f), "封装继电器"), ["rescue"] = (new(.27f,.54f), "救出维修偶"),
        ["scrap"] = (new(.15f,.70f), "拆解废机"), ["supplies"] = (new(.77f,.53f), "补给堆"),
        ["cloth"] = (new(.66f,.75f), "工装残片"), ["medicine"] = (new(.84f,.45f), "站务药匣"),
        ["frame"] = (new(.47f,.78f), "旧货架"), ["bag-cache"] = (new(.65f,.72f), "行李柜"),
        ["K02"] = (new(.54f,.64f), "搬运傀儡"),
        ["clamp"] = (new(.27f,.68f), "工坊夹具"), ["K01"] = (new(.34f,.60f), "运输队"),
        ["crate"] = (new(.55f,.75f), "开启货箱"), ["E13"] = (new(.60f,.60f), "军需领班"),
        ["leader-tool"] = (new(.60f,.60f), "领班工具箱"), ["transfer"] = (new(.79f,.66f), "转运箱"),
        ["bench"] = (new(.37f,.58f), "启动台架"), ["calibrate"] = (new(.50f,.60f), "校准夹具"),
        ["dark"] = (new(.83f,.36f), "关闭检修灯"),
        ["R01"] = (new(.49f,.34f), "共鸣 · 余烬护层"), ["R07"] = (new(.60f,.34f), "共鸣 · 厚壁余温"),
        ["repair"] = (new(.29f,.61f), "修复货站线路"), ["sign"] = (new(.47f,.59f), "领取货站路签"),
        ["wire"] = (new(.275f,.71f), "搜索线缆柜"),
        ["shovel"] = (new(.51f,.55f), "荧光货单与工具"),
        ["train-inspect"] = (new(.60f,.75f), "检查无人车厢"), ["train-wire"] = (new(.80f,.77f), "导能线 ×2"),
        ["train-supply"] = (new(.94f,.80f), "密封补给"), ["train-parcel"] = (new(.26f,.67f), "居民包裹"),
        ["package"] = (new(.26f,.67f), "带回居民包裹"),
        ["K09"] = (new(.50f,.30f), "迎战巡逻")
    };
    private static readonly Dictionary<string, Vector2[]> ExitPins = new()
    {
        ["A01"] = [new(.66f,.52f), new(.25f,.50f)],
        ["A02"] = [new(.10f,.75f), new(.10f,.48f), new(.83f,.55f), new(.17f,.61f)],
        ["A03"] = [new(.10f,.48f), new(.18f,.39f), new(.85f,.43f), new(.91f,.76f)],
        ["A04"] = [new(.16f,.46f), new(.12f,.79f), new(.95f,.62f), new(.81f,.53f)],
        ["A05"] = [new(.19f,.46f), new(.65f,.63f)],
        ["A06"] = [new(.84f,.55f)], ["A07"] = [new(.62f,.48f)]
    };

    private void Explore()
    {
        var s = Session.State; var place = ChapterContent.Places[s.Location];
        foreach (var action in Session.Actions())
        {
            if (action.Id == "heal")
            {
                var heal = IconButton("act_heal", "supply", action.Name + "\n" + action.Description, () => Change(() => Session.Act("heal")));
                heal.Disabled = !action.Enabled; _hud!.AddChild(heal); Dock(heal, 320, 18, 366, 64);
                continue;
            }
            var (point, label) = action.Id == "extract"
                ? (s.Location == "A01" ? new Vector2(.81f,.66f) : new Vector2(.76f,.67f), "安全撤离")
                : ActionPins.GetValueOrDefault(action.Id, (new Vector2(.5f,.5f), action.Name));
            // The square's workshop exit occupies the patrol's usual position.
            if (action.Id == "K09" && s.Location == "A02") point = new Vector2(.70f, .25f);
            if (action.Id == "crate" && s.Run.Contains("crate-start")) label = "继续开箱 · 2/2";
            if (action.Id is "relay" or "clamp") label = "拿取" + label;
            if (action.Id is "K01" or "K02" or "E13") label = "迎战" + label;
            if (action.Id != "extract") label += " · +" + Session.SceneActionCost(action.Id == "crate" ? 2 : 1);
            Pin("act_" + action.Id, label, point, () => ExploreAction(action.Id),
                action.Name + "\n" + action.Description, action.Enabled, action.Id == "extract",
                action.Id is "K01" or "K02" or "K09" or "E13" ? Danger : null);
        }
        if (Session.CanEquip)
            Pin("field_equipment", "台架 · 调整装备", new(.37f,.58f), () => Toggle("gear"), "免费调整本次携带的装备与战利品。");
        for (var i = 0; i < place.Neighbors.Length; i++)
        {
            var id = place.Neighbors[i]; if (!Session.IsRevealed(id)) continue;
            Pin("move_" + id, ChapterContent.Places[id].Name, ExitPins[s.Location][i],
                () => Change(() => Session.Move(id)), "前往" + ChapterContent.Places[id].Name + $"\n行动值 +{Session.SceneActionCost()}。离开当前地点可以避开已抵达的巡逻。", exit: true);
        }
        if (Session.GroundHere.Any())
            Pin("field_loot", "查看遗留物", new(.66f,.85f), () => { _gearFilter = "cargo"; Toggle("gear"); }, "本地点已发现的物品仍在原位。打开行囊选择拾取。");
        var load = ChapterSkin.Panel(_hud!, padding: 8); Dock(load, 20, 188, 416, 232);
        load.AddChild(Text($"{Session.CarriedGrams / 1000m:0.##}/20 kg" + (Session.Overweight ? " · 超重，行动值 ×2" : " · 负重正常"), 16, Session.Overweight ? Danger : Ink));
        var nearest = Session.NearestExit();
        var escape = ChapterSkin.Panel(_hud!, padding: 10); Dock(escape,20,-66,285,-20,at:1,ab:1);
        SymbolValue(escape,"exit",ChapterContent.Places[nearest.Place].Name + $"  ·  {nearest.Distance}",Success,"最近撤离点；还需移动 " + nearest.Distance + " 步。",16);
        if (s.Warned || s.PatrolEta >= 0 && s.PatrolPlace == "")
        {
            var warning = ChapterSkin.Panel(_hud!, padding:10); warning.Name = "PatrolWarning"; Dock(warning,-280,82,-20,128,al:1,ar:1);
            SymbolValue(warning,"warning",s.Warned ? "巡逻抵达 · 可离开" : $"巡逻将至 · {s.PatrolEta}",Danger,Session.PatrolText,16);
        }
    }

    private void Toast()
    {
        var toast = ChapterSkin.Panel(_root, padding: 14); toast.Name = "TransientNotice"; toast.MouseFilter = MouseFilterEnum.Ignore;
        var left = Mathf.Max(304,(Size.X-620)/2);
        Dock(toast,left,-94,Mathf.Min(Size.X-24,left+620),-16,at:1,ab:1);
        var label = Text(_toast.Replace("\n", "  "), 14, Ink); label.MaxLinesVisible = 2; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        toast.AddChild(label);
        var remaining = Math.Max(.1, (_toastUntil - Time.GetTicksMsec()) / 1000.0);
        var tween = toast.CreateTween(); tween.TweenInterval(Math.Max(0, remaining - .2));
        tween.TweenProperty(toast, "modulate:a", 0f, .2); tween.TweenCallback(Callable.From(toast.QueueFree));
    }

}
