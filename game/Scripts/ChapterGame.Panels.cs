using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private VBoxContainer Overlay(string title)
    {
        if (_hud != null)
        {
            void DisableFocus(Node node)
            {
                if (node is Control control) control.FocusMode = FocusModeEnum.None;
                foreach (var child in node.GetChildren()) DisableFocus(child);
            }
            DisableFocus(_hud);
            _hud.Visible = false;
        }
        var shade = new ColorRect { Name = "ModalShade", Color = new Color("030b09b8"), MouseFilter = MouseFilterEnum.Stop };
        _root.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = FlatPanel(_root, 16); panel.Name = "FieldManual";
        var settlement = _panel == "" && Session.State.Phase is ChapterPhase.Extracted or ChapterPhase.Defeated;
        var focused = _panel is "startup" or "confirm" or "choice" or "story" or "system" or "loot" or "quantity" or "detail" or "recipe-preview" or "keys" or "keybind" || settlement;
        var edge = focused ? Mathf.Max(24, (Size.X - 900) / 2) : 12;
        var dialogHeight = _panel switch
        {
            "confirm" => Mathf.Clamp(240 + (_dialogText.Length / 34 + _dialogText.Count(c => c == '\n')) * 26 * ReadingScale, 320, Size.Y - 48),
            "story" => Mathf.Min(280 + Stories.GetValueOrDefault(_story).Copy?.Length / 18f * 24 * ReadingScale ?? 480,Size.Y-48),
            "loot" => Mathf.Min(520,Size.Y-48),
            "quantity" or "detail" or "recipe-preview" => Mathf.Min(600, Size.Y - 48),
            _ => 0
        };
        if (dialogHeight > 0) Dock(panel, edge, (Size.Y-dialogHeight)/2, -edge, (Size.Y+dialogHeight)/2, ar:1);
        else Dock(panel, edge, focused ? 24 : 12, -edge, focused ? -20 : -12, ar: 1, ab: 1);
        if (_panel == "story") { Dock(panel,-Mathf.Min(580,Size.X*.53f),Mathf.Max(24,(Size.Y-dialogHeight)/2),-24,Mathf.Min(Size.Y-24,(Size.Y+dialogHeight)/2),1,0,1,0); shade.Color = new Color("030b0930"); }
        var layout = Column(10); panel.AddChild(layout);
        var head = Row(14); head.Name = "WorkspaceHeader"; layout.AddChild(head);
        var heading = Inline(focused ? title : (Session.AtBase ? UiCopy.Text("UI_916C57AF9A41") : ChapterContent.Places[Session.State.Location].Name) + " / " + title, focused ? 28 : 20, Ink);
        heading.SizeFlagsVertical = SizeFlags.ShrinkCenter; head.AddChild(heading);
        if (!focused)
        {
            var nav = Row(2); nav.Name = "WorkspaceNavigation"; nav.SizeFlagsHorizontal = SizeFlags.ExpandFill; nav.Alignment = BoxContainer.AlignmentMode.End; head.AddChild(nav);
            foreach (var (key, caption) in new[] { ("prepare", "整备"), ("gear", "装备仓库"), ("character", "角色与卡组"), ("craft", "工坊"), ("journal", "任务"), ("map", "地图"), ("help", "指引") })
            {
                var target = key;
                nav.AddChild(NavigationButton("nav_" + key, caption, _panel == key, () => { if (_panel != target) Toggle(target); }));
            }
        }
        else head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = IconButton("close_panel", "close", _panelHistory.Count > 0 ? "返回上页 · Esc" : "返回场景 · Esc", ClosePanel);
        close.SizeFlagsVertical = SizeFlags.ShrinkCenter; head.AddChild(close);
        if (_panel == "startup") { close.Visible = false; close.FocusMode = FocusModeEnum.None; }
        Rule(layout);
        if (_panelHistory.TryPeek(out var origin)) layout.AddChild(Text(UiCopy.Text("UI_FA5640E8F543") + PanelTitle(origin.Panel) + UiCopy.Text("UI_19366196CC81"), 14, Muted));
        if (_saveWarning != "") { var warning = FlatPanel(layout); warning.Name = "SaveFailure"; SaveFailureContent(warning); }
        if (Session.State.Phase == ChapterPhase.Battle && !focused) layout.AddChild(Text(UiCopy.Text("UI_3D8820C78663"), 16, Muted));
        var content = Row(12); content.SizeFlagsVertical = SizeFlags.ExpandFill; layout.AddChild(content);
        var body = FlexibleColumn(content); body.Name = "WindowContent";
        if (_actionWarning != "" && _saveWarning == "") body.AddChild(Text(_actionWarning, 16, Danger));
        Rule(layout);
        var footer = Row(); layout.AddChild(footer);
        var state = Text(settlement ? UiCopy.Text("UI_8C8DD23CECD1") : (Session.State.Phase == ChapterPhase.Camp ? UiCopy.Text("UI_225F739C6987") : UiCopy.Text("UI_BAB3AAAE4703")) + UiCopy.Format("UI_508BEF3266ED", Session.State.Health, Session.CarriedGrams / 1000m), 13, Muted);
        state.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(state);
        _modalityHint = Inline(settlement ? UiCopy.Text("UI_794E3FAE12AB") : InputHelp, 13, Muted); footer.AddChild(_modalityHint);
        if (_lastSavedAt != "" && _saveWarning == "") state.Text += UiCopy.Text("UI_5EBAF5664C2F") + _lastSavedAt;
        return body;
    }

    private void ShowPanel()
    {
        var box = Overlay(_panel == "recipe-preview" ? "配方预览" : PanelTitle(_panel));
        switch (_panel)
        {
            case "startup": Startup(box); break;
            case "measurements": MeasurementPage(box); break;
            case "pickup-plan": PickupPlan(box); break;
            case "space-plan": SpacePlan(box); break;
            case "material-info": MaterialInfo(box); break;
            case "pinned": PinnedTasks(box); break;
            case "receipts": RecentReceipts(box); break;
            case "quest-batch": QuestBatchPage(box); break;
            case "opportunities": OpportunityPage(box); break;
            case "confirm": Confirmation(box); break;
            case "plan": RecipePlan(box); break;
            case "presets": PreparationPresets(box); break;
            case "keys": KeyMap(box); break;
            case "keybind": box.AddChild(Text(UiCopy.Text("UI_09C7636E22A7") + ShortcutName(_bindingAction) + UiCopy.Text("UI_EFE6831AFBC8"), 20, Ink)); break;
            case "quantity": QuantityPanel(box); break;
            case "detail": ItemDetail(box); break;
            case "recipe-preview": MaterialRecipePreview(box); break;
            case "loot": LootSummary(box); break;
            case "choice": Choices(box); break;
            case "system": SystemMenu(box); break;
            case "story": Story(box); break;
            case "prepare": Preparation(box); break;
            case "character": CharacterOverview(box); break;
            case "gear": Inventory(box); break;
            case "craft": Crafting(box); break;
            case "journal": Journal(box); break;
            case "map": Map(box); break;
            default: Handbook(box); break;
        }
    }

    private string PanelTitle(string panel) => panel switch { "startup" => "开始与存档", "measurements" => "操作流程记录", "pickup-plan" => "自定义装包", "space-plan" => "腾出空间", "quest-batch" => "逐项交付", "opportunities" => "新机会与发现", "pinned" => "常用入口", "receipts" => "最近操作回执", "material-info" => "材料用途与来源", "" => "场景", "keys" => "键位速查与重绑定", "keybind" => "设置新按键", "presets" => "整备预设", "plan" => "材料计划", "detail" => _detailTitle, "loot" => "战斗胜利 · 战利品仍在地面", "confirm" => _dialogTitle, "quantity" => "选择取放数量", "choice" => "比较与选择", "system" => "暂停与设置", "story" => "余火记事", "character" => "角色与卡组", "gear" => "装备仓库", "craft" => "工坊", "journal" => "任务", "prepare" => "行前整备", "map" => "地图", _ => "指引" };

    private void SaveFailureContent(Node parent)
    {
        var row = Row(12); parent.AddChild(row);
        var copy = Text(_saveWarning + (_actionWarning != "" ? "\n" + _actionWarning : ""), 16, Danger); copy.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(copy);
        row.AddChild(Button("retry_save", UiCopy.Text("UI_630C9076E621"), () => { if (Save()) _actionWarning = "当前进度已保存；已撤回的操作需要重新执行。"; Render(); }));
    }

    private void Tabs(Node parent, string prefix, (string Id, string Label)[] tabs, string selected, Action<string> select)
    {
        var row = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("h_separation", 6); row.AddThemeConstantOverride("v_separation", 6); parent.AddChild(row);
        foreach (var (id, caption) in tabs)
        {
            var key = id;
            row.AddChild(NavigationButton(prefix + id, caption, id == selected, () => { select(key); _scroll = null; Render(); }));
        }
    }

    private void Handbook(VBoxContainer box)
    {
        var scroll = Scroll(box);
        var page = Column(12); page.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; page.CustomMinimumSize = new Vector2(Mathf.Min(720,Size.X-240),0); scroll.AddChild(page);
        var lead = Paper(page);
        lead.AddChild(Text(UiCopy.Text("UI_0A97D4E2BC34"), 28, ChapterSkin.PaperInk));
        lead.AddChild(Text(UiCopy.Text("UI_88FC5003D973"), 18, ChapterSkin.PaperMuted));
        lead.AddChild(Text(Session.State.Phase == ChapterPhase.Battle ? UiCopy.Text("UI_EB2B2949415B") + KeyLabel("endturn") + UiCopy.Text("UI_DAE6C0045A79") : UiCopy.Text("UI_75DC5C04F809"),16,Ink));
        var legend = new GridContainer { Columns = Size.X < 1200 ? 3 : 4, Visible = false }; legend.AddThemeConstantOverride("h_separation",22); legend.AddThemeConstantOverride("v_separation",12);
        page.AddChild(Button("help_legend", UiCopy.Text("UI_6C5427137F01"), () => legend.Visible = !legend.Visible)); page.AddChild(legend);
        foreach(var (symbol,label) in new[] { ("heart","生命"),("supply","补给"),("bag","携带物"),("exit","出口 / 撤离"),
            ("action","行动"),("mana","魔法"),("hammer","伤害"),("guard","护盾"),("draw","抽牌"),("eye","区域压力"),("all","全体 / 增援"),("round","回合 / 蓄力"),
            ("steel","废钢"),("wire","导能线"),("spring","精密弹簧"),("lock","资源不足") })
            SymbolValue(legend,symbol,label,Ink,"",16);
        page.AddChild(Text(UiCopy.Text("UI_0D8F9AFF586B"),16,Muted));
        foreach (var (number, title, copy) in new[]
        {
            ("01", "探索与巡逻", "通过场景出口移动，每次 1 步。搜物、设施与补给通常也用 1 步。负重超过 20 kg 时场景行动值翻倍。巡逻先预警，抵达后仍可离开；继续停留才会被拦截。阅读与思考免费。"),
            ("02", "装备决定卡组", "据点可换装和制作，备用件数由背包格位决定，穿戴与随身实物都计入负重。启动维修工坊的台架后可换随身装备。新物品需要撤离登记；永久修复与救援立即保存。"),
            ("03", "每回合做出选择", "每回合 3 行动，抽 5 张牌，恢复 1 魔法（上限 3）。选牌查看预览，再选择敌人并确认出牌；Esc 取消。Space 始终表示结束回合，敌人执行公开意图。额外抽牌最多 2 张。"),
            ("04", "护盾与撤退", "护盾下回合清除，稳固最多保留 4。盾压消耗实际护盾，穿盾直接扣生命。战术撤退仍承受本轮意图，存活后退回进入位置，敌人状态保留。")
        })
        {
            var card = CardBox(page); var lesson=Row(18); card.AddChild(lesson);
            Illustration(lesson,number switch {"01"=>"T11","02"=>"G01","03"=>"enemy-E01",_=>"G09"},96,98);
            var copyBox=FlexibleColumn(lesson); copyBox.AddChild(Text(number + "  /  " + title,20,Accent)); copyBox.AddChild(Text(copy.Replace("Space", KeyLabel("endturn")),16,Ink));
        }
    }
}
