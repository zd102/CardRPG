using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private void SceneShade()
    {
        var shade = new TextureRect
        {
            Name = "Atmosphere", MouseFilter = MouseFilterEnum.Ignore,
            Texture = new GradientTexture2D
            {
                FillFrom = Vector2.Zero, FillTo = Vector2.Down,
                Gradient = new Gradient
                {
                    Colors = [new Color("03090bd9"), new Color("03090b8a"), new Color("03090b00"), new Color("03090b00"), new Color("03090b4a")],
                    Offsets = [0, .12f, .28f, .8f, 1]
                }
            }
        };
        _root.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void Header()
    {
        var s = Session.State;
        var vitals = Row(12); _root.AddChild(vitals); Dock(vitals, 24, 21, 320, 80);
        var crest = ChapterArt.Picture("crest"); crest.CustomMinimumSize = new Vector2(44, 50);
        crest.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered; vitals.AddChild(crest);
        var status = Column(4); vitals.AddChild(status);
        status.AddChild(Inline($"生命  {s.Health} / 42", 16, s.Health <= 14 ? Danger : Ink));
        HealthBar(status, s.Health, 42, s.Health <= 14 ? Danger : Success);
        status.AddChild(Inline($"补给 {s.Supplies}    材料 {s.Cargo.Sum()}    物品 {s.Loot.Count + s.Items.Count}", 12, Muted));
        var title = Column(3); _root.AddChild(title); Dock(title, -185, 20, 185, 79, .5f, 0, .5f, 0);
        var name = Text(s.Phase is ChapterPhase.Camp or ChapterPhase.Extracted or ChapterPhase.Defeated ? "余 火 站" : s.Battle?.Id == "trial" ? "工 坊 台 架" : ChapterContent.Places[s.Location].Name, 26, Ink);
        name.HorizontalAlignment = HorizontalAlignment.Center; title.AddChild(name);
        var subtitle = Text(s.Phase == ChapterPhase.Camp ? "EMBER STATION  /  安全据点" : s.Battle?.Id == "trial" ? "WORKBENCH  /  免费试用" : $"OUTER RING  /  {s.Location}  /  压力 {s.Pressure}", 11, Accent);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center; title.AddChild(subtitle);
        var menu = Row(7); _root.AddChild(menu); Dock(menu, -313, 23, -24, 66, al: 1, ar: 1);
        foreach (var item in new[] { ("map", "图", "区域地图  M", "map"), ("inventory", "装", "装备与背包  I", "gear"),
                     ("journal", "志", "任务与旅途记录  J", "journal"), ("craft", "工", "工坊制作与改装", "craft"),
                     ("help", "?", "操作指引  F1", "help"), ("quit", "×", "保存并退出", "quit") })
        {
            var id = item.Item1; var panel = item.Item4;
            var button = Button(id, item.Item2, () => { if (panel == "quit") RequestQuit(); else Toggle(panel); });
            button.CustomMinimumSize = new Vector2(42, 42); button.TooltipText = item.Item3;
            menu.AddChild(button);
        }
        if (_panel == "" && s.Phase is ChapterPhase.Camp or ChapterPhase.Exploring)
        {
            var objective = Text(Session.Objective, 13, new Color("e6d2a8"));
            objective.HorizontalAlignment = HorizontalAlignment.Center;
            _root.AddChild(objective); Dock(objective, -390, 91, 390, 116, .5f, 0, .5f, 0);
        }
    }

    private VBoxContainer Scroll(Node parent)
    {
        _scroll = new ScrollContainer { FollowFocus = true, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        parent.AddChild(_scroll); var box = Column(12); box.SizeFlagsHorizontal = SizeFlags.ExpandFill; _scroll.AddChild(box); return box;
    }

    private Button Pin(string id, string label, Vector2 point, Action action, string tooltip, bool enabled = true, bool exit = false, Color? tint = null)
    {
        var button = new ChapterHotspot
        {
            Name = id, Text = label, TooltipText = tooltip, Disabled = !enabled, Exit = exit,
            Tint = tint ?? (exit ? Success : Accent), FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.Pressed += () => { if (!button.Disabled) action(); };
        _root.AddChild(button);
        var world = _root.GetNode<ChapterScene>("World");
        void PositionPin()
        {
            if (world.Size.X <= 0 || world.Size.Y <= 0) return;
            var p = world.ScenePoint(point);
            p.X = Mathf.Clamp(p.X, 95, world.Size.X - 95);
            p.Y = Mathf.Clamp(p.Y, 220, world.Size.Y - 72);
            Dock(button, p.X - 83, p.Y - 10, p.X + 83, p.Y + 57);
        }
        world.Resized += PositionPin; PositionPin();
        Buttons[id] = button; return button;
    }

    private void Camp()
    {
        var s = Session.State;
        Pin("camp_equipment", "工作台 · 调整装备", new(.23f, .61f), () => Toggle("gear"),
            string.Join(" / ", Session.Equipped.Select(g => ChapterContent.Gear[g].Name)) + $"\n卡组 {Session.Deck().Count} 张。点击调整穿戴、备用和携带物。");
        var turnIns = ChapterContent.Quests.Count(q => Session.CanTurnIn(q.Id));
        Pin("camp_quests", turnIns > 0 ? $"岑砧 · {turnIns} 项可交付" : "岑砧 · 任务与发现", new(.48f, .52f), () => Toggle("journal"),
            s.Done.Contains("Q01") ? "工作灯亮了。暖黄的旧日投影里，城市还有晴天。\n点击查看任务或交付带回的物品。" : "岑砧在工作灯下等着那只封装继电器。\n取得任务物后安全撤离，在这里交付。");
        Pin("camp_preparation", "行前整备", new(.65f, .70f), () => Toggle("prepare"),
            "选择起始装备、备用与出发地点。每次远征恢复生命并补充 2 份补给。");
        Pin("depart", "出发 · " + ChapterContent.Places[s.Start].Name, new(.79f, .43f),
            () => Change(Session.Depart), "从" + ChapterContent.Places[s.Start].Name + "出征。满生命，免费补给 ×2。", exit: true);
        if (s.Done.Contains("Q02"))
            Pin("camp_trial", "台架 · 免费试用", new(.35f, .76f), () => Change(Session.StartTrial), "借用检修手套，在不会还击的台架上试牌。可以随时退出。");
        if (s.Done.Contains("S07"))
            Pin("home_robot", "维修偶", new(.52f, .75f), () => { _toast = "小维修偶挥了挥扳手，指向工坊墙后那一抹荧光。"; _toastUntil = Time.GetTicksMsec() + 5000; Render(); }, "它已经在余火站安了家。");
    }

    private void Preparation(VBoxContainer box)
    {
        var s = Session.State;
        Heading(box, "把收获带回来", "生命与两份补给会在出发时补满。");
        if (s.Extractions == 0)
        {
            var starters = Row(12); box.AddChild(starters);
            foreach (var id in new[] { "G01", "G02" })
            {
                var gear = id;
                starters.AddChild(Button("starter_" + id, (s.Weapon == id ? "已选 · " : "选择 · ") + ChapterContent.Gear[id].Name, () => Change(() => Session.SelectStarter(gear)), true, s.Weapon == id));
            }
        }
        box.AddChild(Text(string.Join(" / ", Session.Equipped.Select(g => ChapterContent.Gear[g].Name)), 21, Ink));
        box.AddChild(Text($"卡组 {Session.Deck().Count} 张 · 备用 {s.Backups.Count}/2\n仓库 · {Materials(s.Bank)}", 16, Muted));
        box.AddChild(Button("prepare_equipment", "调整装备与备用", () => Toggle("gear")));
        if (s.StoredItems.Contains("T02") && !s.StoredCalibration)
            box.AddChild(Button("carry_T02", s.CarryItems.Contains("T02") ? "✓ 已携带待校准夹具" : "携带仓库夹具，出征校准", () => Change(() => Session.ToggleCarryItem("T02"))));
        Heading(box, "选择出发地点", "修复货站并交付路签后，可从更深处开始远征。");
        var routes = Row(12); box.AddChild(routes);
        foreach (var id in new[] { "A01", "A05" })
        {
            var place = id;
            routes.AddChild(Button("start_" + id, (s.Start == id ? "● " : "") + ChapterContent.Places[id].Name,
                () => Change(() => Session.SetStart(place)), id == "A01" || s.Done.Contains("S01"), s.Start == id));
        }
        box.AddChild(Button("prepare_depart", "从" + ChapterContent.Places[s.Start].Name + "出发  →",
            () => { _panel = ""; Change(Session.Depart); }, true, true));
        if (Session.SliceComplete) box.AddChild(Text("外环主线已完成。仍可回访、试装和发现支线；水务区尚未开放。", 15, Success));
    }

    private static readonly Dictionary<string, (Vector2 Point, string Label)> ActionPins = new()
    {
        ["relay"] = (new(.90f,.68f), "封装继电器"), ["rescue"] = (new(.34f,.51f), "救出维修偶"),
        ["scrap"] = (new(.20f,.74f), "拆解废机"), ["supplies"] = (new(.73f,.53f), "补给堆"),
        ["K02"] = (new(.54f,.43f), "搬运傀儡"),
        ["clamp"] = (new(.25f,.64f), "工坊夹具"), ["K01"] = (new(.33f,.42f), "运输队"),
        ["crate"] = (new(.54f,.74f), "开启货箱"), ["E13"] = (new(.62f,.44f), "军需领班"),
        ["leader-tool"] = (new(.62f,.44f), "领班工具箱"), ["transfer"] = (new(.77f,.65f), "转运箱"),
        ["bench"] = (new(.28f,.59f), "启动台架"), ["calibrate"] = (new(.29f,.76f), "校准夹具"),
        ["dark"] = (new(.91f,.40f), "关闭检修灯"),
        ["R01"] = (new(.49f,.46f), "共鸣 · 余烬护层"), ["R07"] = (new(.61f,.62f), "共鸣 · 厚壁余温"),
        ["repair"] = (new(.24f,.50f), "修复货站线路"), ["sign"] = (new(.48f,.55f), "领取货站路签"),
        ["wire"] = (new(.24f,.71f), "搜索线缆柜"),
        ["shovel"] = (new(.65f,.59f), "荧光货单与工具"),
        ["train-inspect"] = (new(.50f,.41f), "检查无人车厢"), ["train-wire"] = (new(.83f,.71f), "导能线 ×2"),
        ["train-supply"] = (new(.91f,.81f), "密封补给"), ["train-parcel"] = (new(.30f,.71f), "居民包裹"),
        ["package"] = (new(.32f,.64f), "带回居民包裹"),
        ["K09"] = (new(.50f,.30f), "迎战巡逻")
    };
    private static readonly Dictionary<string, Vector2[]> ExitPins = new()
    {
        ["A01"] = [new(.45f,.37f), new(.20f,.44f)],
        ["A02"] = [new(.12f,.81f), new(.15f,.37f), new(.49f,.28f), new(.86f,.37f)],
        ["A03"] = [new(.12f,.42f), new(.33f,.28f), new(.66f,.30f), new(.87f,.82f)],
        ["A04"] = [new(.12f,.39f), new(.12f,.83f), new(.89f,.79f), new(.84f,.60f)],
        ["A05"] = [new(.11f,.39f), new(.49f,.31f)],
        ["A06"] = [new(.84f,.40f)], ["A07"] = [new(.68f,.42f)]
    };

    private void Explore()
    {
        var s = Session.State; var place = ChapterContent.Places[s.Location];
        foreach (var action in Session.Actions())
        {
            if (action.Id == "heal")
            {
                var heal = Button("act_heal", "+", () => Change(() => Session.Act("heal")), action.Enabled);
                heal.TooltipText = action.Name + "\n" + action.Description; _root.AddChild(heal); Dock(heal, 278, 26, 316, 68);
                continue;
            }
            var (point, label) = action.Id == "extract"
                ? (s.Location == "A01" ? new Vector2(.69f,.61f) : new Vector2(.74f,.61f), "安全撤离")
                : ActionPins.GetValueOrDefault(action.Id, (new Vector2(.5f,.5f), action.Name));
            // The square's workshop exit occupies the patrol's usual position.
            if (action.Id == "K09" && s.Location == "A02") point = new Vector2(.70f, .25f);
            if (action.Id == "crate" && s.Run.Contains("crate-start")) label = "继续开箱 · 2/2";
            Pin("act_" + action.Id, label, point, () => Change(() => Session.Act(action.Id)),
                action.Name + "\n" + action.Description, action.Enabled, action.Id == "extract",
                action.Id is "K01" or "K02" or "K09" or "E13" ? Danger : null);
        }
        if (Session.CanEquip)
            Pin("field_equipment", "台架 · 调整装备", new(.28f,.59f), () => Toggle("gear"), "免费调整本次携带的装备与战利品。");
        for (var i = 0; i < place.Neighbors.Length; i++)
        {
            var id = place.Neighbors[i]; if (!Session.IsRevealed(id)) continue;
            Pin("move_" + id, ChapterContent.Places[id].Name, ExitPins[s.Location][i],
                () => Change(() => Session.Move(id)), "前往" + ChapterContent.Places[id].Name + "\n移动 1 步。离开当前地点可以避开已抵达的巡逻。", exit: true);
        }
        var nearest = Session.NearestExit();
        var context = Text(s.Warned ? "巡逻已抵达 · 现在可以从出口离开" : $"最近撤离  {ChapterContent.Places[nearest.Place].Name} · {nearest.Distance} 步", 12, s.Warned ? Danger : Muted);
        context.TooltipText = Session.PatrolText; context.MouseFilter = MouseFilterEnum.Pass;
        _root.AddChild(context); Dock(context, 27, 92, 335, 114);
    }

    private void Toast()
    {
        var toast = Panel(_root, new Color("101c22de"), new Color("c4a37066")); toast.Name = "TransientNotice"; toast.MouseFilter = MouseFilterEnum.Ignore;
        Dock(toast, -315, 134, 315, 193, .5f, 0, .5f, 0);
        var label = Text(_toast.Replace("\n", "  "), 14, Ink); label.MaxLinesVisible = 2; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        toast.AddChild(label);
        var remaining = Math.Max(.1, (_toastUntil - Time.GetTicksMsec()) / 1000.0);
        var tween = toast.CreateTween(); tween.TweenInterval(Math.Max(0, remaining - .6));
        tween.TweenProperty(toast, "modulate:a", 0f, .6); tween.TweenCallback(Callable.From(toast.QueueFree));
    }

    private void Debrief()
    {
        var s = Session.State; var ok = s.Phase == ChapterPhase.Extracted;
        var box = Overlay(ok ? "撤离成功" : "紧急回收");
        Buttons["close_panel"].Visible = false;
        Eyebrow(box, ok ? "WELCOME HOME / 余火未熄" : "ANOTHER DAWN / 下一程", ok ? Success : Danger);
        box.AddChild(Text(ok ? "带着收获，归来。" : "灯还亮着。", 36, Ink));
        box.AddChild(Text(s.Summary, 20, Ink)); box.AddChild(Text("仓库 · " + Materials(s.Bank), 17, Accent));
        box.AddChild(Text($"已登记装备 {s.Owned.Count} 件 · 成功撤离 {s.Extractions} 次", 15, Muted));
        box.AddChild(Button("camp", "回到余火站  →", () => Change(Session.ReturnToCamp), true, true));
    }
}
