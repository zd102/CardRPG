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
        var bar = DarkPanel(_hud, 14); bar.Name = "Vitals"; Dock(bar, 16, 16, -16, 84, ar: 1);
        var row = Row(18); bar.AddChild(row);
        var status = Column(4); status.CustomMinimumSize = new Vector2(238, 0); row.AddChild(status);
        var values = Row(14); status.AddChild(values);
        SymbolValue(values, "heart", $"{s.Health}/42", s.Health <= 14 ? new Color("ffb29b") : OnNight, "生命；低于三分之一时需要补给。", 18);
        if (s.Phase != ChapterPhase.Battle)
        {
            SymbolValue(values, "supply", s.Supplies.ToString(), OnNight, "补给；探索中可消耗一份恢复生命。", 18);
            SymbolValue(values, "bag", $"{Session.CarryUsed}/{Session.CarryCapacity}", Session.CarryUsed >= Session.CarryCapacity ? Brass : OnNight, CapacityText(Session.CarryUsed, Session.CarryCapacity) + " · " + Session.LoadDescription, 18);
        }
        HealthBar(status, s.Health, 42, s.Health <= 14 ? new Color("e29978") : new Color("94b599"));
        var place = Column(1); place.Name = "LocationPlate"; place.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(place);
        var title = Text(s.Phase is ChapterPhase.Camp or ChapterPhase.Extracted or ChapterPhase.Defeated ? "余火站" : s.Battle?.Id == "trial" ? "台架试用" : ChapterContent.Places[s.Location].Name, 20, OnNight);
        title.HorizontalAlignment = HorizontalAlignment.Center; place.AddChild(title);
        var note = Text(Session.AtBase ? "安全据点" : s.Phase == ChapterPhase.Battle ? "战斗中" : s.Warned ? "巡逻抵达 · 可离开" : "区域压力 " + s.Pressure, 14, Brass);
        note.HorizontalAlignment = HorizontalAlignment.Center; place.AddChild(note);
        var menu = Row(2); row.AddChild(menu);
        foreach (var item in new[] { ("character", "角色", "character"), ("map", "地图", "map"), ("inventory", "背包", "gear"), ("journal", "任务", "journal"), ("craft", "工坊", "craft"), ("help", "指引", "help"), ("quit", "设置", "system") })
        {
            var target = item.Item3;
            var button = DarkButton(item.Item1, _navigationLabels ? item.Item2 : "", () => Toggle(target));
            if (!_navigationLabels)
            {
                button.CustomMinimumSize = new Vector2(40, 40);
                var glyph = Glyph(item.Item1 switch { "inventory" => "bag", "quit" => "settings", _ => item.Item1 }, OnNight, 22);
                button.AddChild(glyph); Dock(glyph, -11, -11, 11, 11, .5f, .5f, .5f, .5f);
            }
            button.TooltipText = item.Item1 == "inventory" ? "装备与背包 · " + KeyLabel("gear") : item.Item2 + (_keys.ContainsKey(target) ? " · " + KeyLabel(target) : "");
            if (_navigationLabels && item.Item1 == "journal" && ChapterContent.Quests.Any(q => Session.CanTurnIn(q.Id))) button.Text = "任务 •";
            menu.AddChild(button);
        }
        if (_panel == "" && s.Phase is ChapterPhase.Camp or ChapterPhase.Exploring)
        {
            var objective = Button("objective", "", () => Toggle("journal")); objective.TooltipText = "当前目标\n" + Session.Objective;
            _hud.AddChild(objective); Dock(objective, 20, 104, 350, 184);
            var copy = Column(4); copy.MouseFilter = MouseFilterEnum.Ignore; objective.AddChild(copy); Dock(copy, 14, 10, -14, -10, ar: 1, ab: 1);
            copy.AddChild(Text("当前目标", 14, Muted)); copy.AddChild(Text(Session.NextObjective, 16, Ink));
        }
    }

    private VBoxContainer Scroll(Node parent, string role = "", string objectId = "")
    {
        var scroll = new ScrollContainer { Name = "FocusRegion_" + _pageScrolls.Count, FocusMode = FocusModeEnum.All, FollowFocus = true, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        if (role != "") scroll.SetMeta("reading_key", _panel + ":" + role + ":" + objectId);
        _scroll ??= scroll;
        _pageScrolls.Add(scroll);
        parent.AddChild(scroll); var box = Column(12); box.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(box);
        if(_panel is "detail" or "recipe-preview" or "receipts")
        {
            box.SizeFlagsHorizontal=SizeFlags.ShrinkCenter;
            void BoundReading() { if(GodotObject.IsInstanceValid(box)&&scroll.Size.X>20)box.CustomMinimumSize=new Vector2(Math.Min(720,scroll.Size.X-18),0); }
            scroll.Resized+=BoundReading;Callable.From(BoundReading).CallDeferred();
        }
        return box;
    }

    private Button Pin(string id, string label, Vector2 point, Action action, string tooltip, bool enabled = true, bool exit = false, Color? tint = null)
    {
        var button = new ChapterHotspot
        {
            Name = id, Caption = UiCopy.Present(label), TooltipText = label + "\n" + tooltip, Disabled = !enabled, Exit = exit, ShowLabel = _labels || _holdLabels,
            Symbol = PinSymbol(id, exit), Important = id is "act_relay" or "act_clamp" or "act_calibrate" || id == "camp_quests" && ChapterContent.Quests.Any(q => Session.CanTurnIn(q.Id)),
            Tint = tint ?? (exit ? Success : Accent), FocusMode = FocusModeEnum.All, LabelSize = (int)Math.Round(14 * ReadingScale),
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        var generation = _renderGeneration;
        button.Pressed += () => { if (generation == _renderGeneration && button.IsInsideTree() && !button.Disabled && !FeedbackBusy) { RecordActivation(); _commandCaption = label; Cue("click"); action(); } };
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
            Dock(button, p.X - 26, p.Y - 26, p.X + 26, p.Y + 26);
            var half = Math.Max(40, button.GetThemeDefaultFont().GetStringSize(button.Caption,fontSize:button.LabelSize).X / 2 + 8);
            var offsets = new List<Vector2> { Vector2.Zero };
            for (var y = 0; y < world.Size.Y - 320; y += button.LabelSize + 20)
                for (var x = 0f; x < world.Size.X; x += half * 2 + 12) offsets.Add(new Vector2(half + 12 + x, 258 + y) - p);
            foreach (var offset in offsets)
            {
                var candidate = p + offset;
                candidate.X = Mathf.Clamp(candidate.X, half, world.Size.X - half);
                candidate.Y = Mathf.Clamp(candidate.Y, 250, world.Size.Y - 130);
                var rect = new Rect2(candidate + new Vector2(-half,31), new Vector2(half*2,button.LabelSize+12));
                if (occupied.Any(other => rect.Intersects(other))) continue;
                button.LabelOffset = candidate-p; break;
            }
            button.SceneAnchor = world.ScenePoint(point) - p + new Vector2(26,26); button.QueueRedraw();
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
        Pin("camp_equipment", UiCopy.Text("UI_7C1F4445897C"), new(.23f, .65f), () => Toggle("gear"),
            string.Join(" / ", Session.Equipped.Select(g => ChapterContent.Gear[g].Name)) + UiCopy.Format("UI_1611C35801F7", Session.Deck().Count));
        var turnIns = ChapterContent.Quests.Count(q => Session.CanTurnIn(q.Id));
        Pin("camp_quests", turnIns > 0 ? UiCopy.Format("UI_740A65622953", turnIns) : UiCopy.Text("UI_6D189FC4960A"), new(.48f, .52f), () => Toggle("journal"),
            s.Done.Contains("Q01") ? UiCopy.Text("UI_EB4A5ED00CA9") : UiCopy.Text("UI_F67515FE7E8B"));
        Pin("camp_preparation", UiCopy.Text("UI_A5CD1B892CC4"), new(.89f, .78f), () => { _gearFilter = "cargo"; Toggle("gear"); },
            UiCopy.Text("UI_AF9FD0F63A22"));
        if (_pinnedTasks.Count > 0)
        {
            var pinned = Button("camp_pinned", UiCopy.Text("UI_0FF70CB19B6C") + _pinnedTasks.Count, () => Toggle("pinned")); _hud!.AddChild(pinned); Dock(pinned, -260, 104, -20, 150, al: 1, ar: 1);
        }
        if (_trackedMaterial != "")
        {
            var needed = Math.Max(0, Session.PlannedMaterialCost(s.RecipePlan).GetValueOrDefault(_trackedMaterial) - Session.SafeMaterialCount(_trackedMaterial));
            var material = Button("tracked_material", ChapterContent.ItemName(_trackedMaterial) + UiCopy.Text("UI_9919DF4B053B") + needed, () => { _materialInfoKind = _trackedMaterial; Toggle("material-info"); }); _hud!.AddChild(material); Dock(material, 20, 174, 370, 220);
        }
        Pin("depart", UiCopy.Text("UI_E46C23BF59B2") + ChapterContent.Places[s.Start].Name, new(.79f, .43f),
            () => Toggle("prepare"), UiCopy.Text("UI_6B7F002CF386") + ChapterContent.Places[s.Start].Name + UiCopy.Text("UI_DD4781161FAA") + Session.LoadDescription, exit: true);
        if (s.Done.Contains("Q02"))
            Pin("camp_trial", UiCopy.Text("UI_1D8E4CC34B64"), new(.35f, .76f), () => Change(Session.StartTrial), UiCopy.Text("UI_01C0CB0F0184"));
        if (s.Done.Contains("S07"))
            Pin("home_robot", UiCopy.Text("UI_58A87C521FFE"), new(.52f, .75f), RobotGesture, UiCopy.Text("UI_2B99904112CF"));
        OpportunityEntry();
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
                var heal = IconButton("act_heal", "supply", action.Name + $"\n生命 {s.Health} → {Math.Min(ChapterSession.MaxHealth, s.Health + 14)} · 实际恢复 {Math.Min(14, ChapterSession.MaxHealth - s.Health)}\n优先消耗配发份 · 推进 {Session.SceneActionCost()} 步" + (s.Warned ? "\n⚠ 巡逻先拦截，本次补给不会消耗。" : ""), () => Change(() => Session.Act("heal")));
                heal.Disabled = !action.Enabled; _hud!.AddChild(heal); Dock(heal, 296, -66, 342, -20, at: 1, ab: 1);
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
            var result = action.Enabled ? Session.Preview(copy => copy.Act(action.Id)) : null;
            var pressureCost = result == null ? 0 : result.State.Pressure - s.Pressure;
            Pin("act_" + action.Id, label, point, () => ExploreAction(action.Id),
                action.Name + "\n" + action.Description + (result?.State.Phase == ChapterPhase.Battle ? "\n将进入战斗" : "\n区域压力 +" + pressureCost) + "\n" + PressureForecast(pressureCost), action.Enabled, action.Id == "extract",
                action.Id is "K01" or "K02" or "K09" or "E13" ? Danger : null);
        }
        if (Session.CanEquip)
            Pin("field_equipment", UiCopy.Text("UI_686F2D40EAF7"), new(.37f,.58f), () => Toggle("gear"), UiCopy.Text("UI_B8EFBE9212A3"));
        for (var i = 0; i < place.Neighbors.Length; i++)
        {
            var id = place.Neighbors[i]; if (!Session.IsRevealed(id)) continue;
            var path = Session.KnownRoute(NavigationTarget); var guided = path.Count > 1 && path[1] == id;
            Pin("move_" + id, (guided ? UiCopy.Text("UI_60293F8F85AA") : "") + ChapterContent.Places[id].Name, ExitPins[s.Location][i],
                () => Change(() => Session.Move(id)), UiCopy.Text("UI_FA96079C3B2F") + ChapterContent.Places[id].Name + UiCopy.Format("UI_B6DE1D18BA58", Session.SceneActionCost()), exit: true);
            if ((guided || s.Warned) && Buttons["move_" + id] is ChapterHotspot marker) { marker.ShowLabel = true; marker.Important = s.Warned; marker.QueueRedraw(); }
        }
        if (Session.GroundHere.Any())
        {
            var loot=Button("field_loot",UiCopy.Text("UI_687A3D26F2BF"),()=>Toggle("loot"));_hud!.AddChild(loot);Dock(loot,-244,-72,-20,-26,1,1,1,1);
        }
        SceneTraces(); OpportunityEntry();
        var load = FlatPanel(_hud!, 8); Dock(load, 20, 194, 350, 252);
        var loadCopy=Column(2);load.AddChild(loadCopy);
        loadCopy.AddChild(Text(CapacityText(Session.CarryUsed, Session.CarryCapacity) + $"\n负重 {Session.CarriedGrams / 1000m:0.##}/20 kg" + (Session.Overweight ? " · 超重" : ""), 14, Session.Overweight ? Danger : Ink));
        if (s.Warned || Session.Overweight || CompactDensity) loadCopy.AddChild(Text(PressureForecast(0).Split('\n')[1],14,Muted));
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
        Dock(toast,left,-94,Mathf.Min(Size.X-270,left+620),-16,at:1,ab:1);
        var label = Text(_toast.Replace("\n", "  "), 14, Ink); label.MaxLinesVisible = 2; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        toast.AddChild(label);
        var remaining = Math.Max(.1, (_toastUntil - Time.GetTicksMsec()) / 1000.0);
        var tween = toast.CreateTween(); tween.TweenInterval(Math.Max(0, remaining - .2));
        tween.TweenProperty(toast, "modulate:a", 0f, .2); tween.TweenCallback(Callable.From(toast.QueueFree));
    }

}
