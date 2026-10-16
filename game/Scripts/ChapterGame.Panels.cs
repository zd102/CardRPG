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
            DisableFocus(_hud); _hud.Visible = false;
        }
        var shade = new ColorRect { Name = "ModalShade", Color = new Color("102720b8"), MouseFilter = MouseFilterEnum.Stop };
        _root.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = FlatPanel(_root, CompactDensity ? 18 : 24); panel.Name = "FieldManual";
        var settlement = _panel == "" && Session.State.Phase is ChapterPhase.Extracted or ChapterPhase.Defeated;
        var focused = _panel is "startup" or "prepare" or "confirm" or "choice" or "story" or "system" or "loot" or "pickup-plan" or "space-plan" or "quantity" or "stock-batches" or "detail" or "recipe-preview" or "keys" or "keybind" || settlement;
        var edge = focused ? Mathf.Max(24, (Size.X - (_panel is "prepare" or "choice" or "loot" or "space-plan" ? 1080 : 920)) / 2) : 24;
        var height = _panel switch
        {
            "confirm" => Mathf.Clamp(240 + (_dialogText.Length / 32 + _dialogText.Count(c => c == '\n')) * 26 * ReadingScale, 340, Size.Y - 64),
            "story" => Mathf.Min(420 * ReadingScale, Size.Y - 64),
            "loot" => Mathf.Min(600 * ReadingScale, Size.Y - 64),
            "quantity" => Mathf.Min(580, Size.Y - 64),
            "stock-batches" => Mathf.Min(480 * ReadingScale, Size.Y - 64),
            _ => Size.Y - (focused ? 64 : 48)
        };
        Dock(panel, edge, (Size.Y - height) / 2, -edge, (Size.Y + height) / 2, ar: 1);
        if (_panel == "story") shade.Color = new Color("10272070");
        var layout = Column(8); panel.AddChild(layout);
        var head = Column(8); head.Name = "WorkspaceHeader"; layout.AddChild(head);
        var titleRow = Row(14); head.AddChild(titleRow);
        var heading = Inline(title, 28, Ink); heading.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.SizeFlagsVertical = SizeFlags.ShrinkCenter; titleRow.AddChild(heading);
        if (!focused)
        {
            titleRow.AddChild(QuietButton("workspace_density", CompactDensity ? "紧凑布局" : "舒展布局", TogglePageDensity));
        }
        var close = IconButton("close_panel", "close", _panelHistory.Count > 0 ? "返回上页 · Esc" : "返回场景 · Esc", ClosePanel);
        close.SizeFlagsVertical = SizeFlags.ShrinkCenter; titleRow.AddChild(close);
        if (_panel == "startup") { close.Visible = false; close.FocusMode = FocusModeEnum.None; }
        if (!focused)
        {
            var nav = Row(6); nav.Name = "WorkspaceNavigation"; head.AddChild(nav);
            foreach (var (key, caption) in new[] { ("prepare", "出发"), ("character", "角色与卡组"), ("gear", "背包与装备"), ("craft", "工坊"), ("journal", "任务"), ("map", "地图"), ("help", "指引") })
            {
                var target = key;
                nav.AddChild(NavigationButton("nav_" + key, caption, _panel == key, () => { if (_panel != target) Toggle(target); }));
            }
            if (Size.X >= 1100)
            {
                head.RemoveChild(nav); titleRow.AddChild(nav); titleRow.MoveChild(nav, 1);
                heading.SizeFlagsHorizontal = SizeFlags.Fill;
                nav.SizeFlagsHorizontal = SizeFlags.ExpandFill; nav.Alignment = BoxContainer.AlignmentMode.End;
            }
        }
        Rule(layout);
        if (_saveWarning != "") { var warning = FlatPanel(layout); warning.Name = "SaveFailure"; SaveFailureContent(warning); }
        if (Session.State.Phase == ChapterPhase.Battle && !focused) layout.AddChild(Text("战斗暂停 · 查看不会消耗行动", 14, Muted));
        var body = Column(8); body.Name = "WindowContent"; body.SizeFlagsVertical = SizeFlags.ExpandFill; layout.AddChild(body);
        if (_actionWarning != "" && _saveWarning == "") body.AddChild(Text(_actionWarning, 16, Danger));
        if (_panel != "story" && _panel != "startup")
        {
            var footer = Row(); layout.AddChild(footer);
            var context = Text(settlement ? "远征记录已保存" : Session.AtBase ? "余火站 · 安全据点" : ChapterContent.Places[Session.State.Location].Name + " · 随时可返回场景", 14, Muted);
            context.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(context);
            _modalityHint = Inline(InputHelp, 14, Muted); footer.AddChild(_modalityHint);
        }
        return body;
    }

    private void ShowPanel()
    {
        var box = Overlay(_panel == "stock-batches" ? "物品来源" : _panel == "recipe-preview" ? "配方预览" : PanelTitle(_panel));
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
            case "stock-batches": StockBatchesPanel(box); break;
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

    private string PanelTitle(string panel) => panel switch { "startup" => "开始与存档", "measurements" => "操作流程记录", "pickup-plan" => "自定义装包", "space-plan" => "腾出空间", "quest-batch" => "逐项交付", "opportunities" => "新机会与发现", "pinned" => "常用入口", "receipts" => "最近操作回执", "material-info" => "材料用途与来源", "" => "场景", "keys" => "键位速查与重绑定", "keybind" => "设置新按键", "presets" => "整备预设", "plan" => "材料计划", "detail" => _detailTitle, "loot" => "发现物资", "confirm" => _dialogTitle, "quantity" => "选择取放数量", "choice" => "比较与选择", "system" => "暂停与设置", "story" => "余火记事", "character" => "角色与卡组", "gear" => Session.AtBase ? "背包与仓库" : "随身物品", "craft" => "工坊", "journal" => "任务", "prepare" => "准备远征", "map" => "地图", _ => "指引" };

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
            ("01", "探索与巡逻", "通过场景出口或地图的前往按钮移动，每次一站。移动、搜物和补给通常增加 1 点区域压力，超过 20 kg 时消耗翻倍。巡逻先预警，抵达后仍可离开；继续停留才会被拦截。阅读与思考免费。"),
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
