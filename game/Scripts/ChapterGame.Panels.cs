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
        var focused = _panel is "confirm" or "choice" or "story" or "system" or "loot" || settlement;
        var edge = focused ? Mathf.Max(24, (Size.X - 900) / 2) : 12;
        var dialogHeight = _panel switch
        {
            "confirm" => Mathf.Clamp(240 + (_dialogText.Length / 34 + _dialogText.Count(c => c == '\n')) * 26 * ReadingScale, 320, Size.Y - 48),
            "story" => Mathf.Min(480,Size.Y-48),
            "loot" => Mathf.Min(420,Size.Y-48),
            _ => 0
        };
        if (dialogHeight > 0) Dock(panel, edge, (Size.Y-dialogHeight)/2, -edge, (Size.Y+dialogHeight)/2, ar:1);
        else Dock(panel, edge, focused ? 24 : 12, -edge, focused ? -20 : -12, ar: 1, ab: 1);
        var layout = Column(10); panel.AddChild(layout);
        var head = Row(14); head.Name = "WorkspaceHeader"; layout.AddChild(head);
        var heading = Inline(focused ? title : "余火站 / " + title, focused ? 28 : 20, Ink);
        heading.SizeFlagsVertical = SizeFlags.ShrinkCenter; head.AddChild(heading);
        if (!focused)
        {
            var nav = Row(2); nav.Name = "WorkspaceNavigation"; nav.SizeFlagsHorizontal = SizeFlags.ExpandFill; nav.Alignment = BoxContainer.AlignmentMode.End; head.AddChild(nav);
            foreach (var (key, caption) in new[] { ("prepare", "整备"), ("gear", "装备仓库"), ("character", "卡组"), ("craft", "工坊"), ("journal", "任务"), ("map", "地图"), ("help", "指引") })
            {
                var target = key;
                nav.AddChild(NavigationButton("nav_" + key, caption, _panel == key, () => { if (_panel != target) Toggle(target); }));
            }
        }
        else head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = IconButton("close_panel", "close", _panelHistory.Count > 0 ? "返回上页 · Esc" : "返回场景 · Esc", ClosePanel);
        close.SizeFlagsVertical = SizeFlags.ShrinkCenter; head.AddChild(close);
        Rule(layout);
        var content = Row(12); content.SizeFlagsVertical = SizeFlags.ExpandFill; layout.AddChild(content);
        var body = FlexibleColumn(content); body.Name = "WindowContent";
        if (_actionWarning != "") body.AddChild(Text(_actionWarning, 16, Danger));
        Rule(layout);
        var footer = Row(); layout.AddChild(footer);
        var state = Text(settlement ? "余火未熄" : (Session.State.Phase == ChapterPhase.Camp ? "安全据点" : "行动中 · 撤离后入库") + $"   生命 {Session.State.Health}/42   负重 {Session.CarriedGrams / 1000m:0.##}/20 kg", 13, Muted);
        state.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(state);
        footer.AddChild(Inline(settlement ? "Enter 回营" : _panel == "gear" && _gearFilter == "cargo" ? "单击选物  ·  Shift+Enter 整批取放  ·  Esc 返回" : "Tab 切换  ·  Enter 确认  ·  Esc 返回", 13, Muted));
        return body;
    }

    private void ShowPanel()
    {
        var box = Overlay(_panel switch { "loot" => "战斗胜利 · 战利品仍在地面", "confirm" => _dialogTitle, "choice" => "比较与选择", "system" => "暂停与设置", "story" => "余火记事", "character" => "角色卡组", "gear" => "装备仓库", "craft" => "工坊", "journal" => "任务", "prepare" => "行前整备", "map" => "地图", _ => "指引" });
        switch (_panel)
        {
            case "confirm": Confirmation(box); break;
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
        lead.AddChild(Text("把收获带回来。", 28, ChapterSkin.PaperInk));
        lead.AddChild(Text("首轮只需从废料场取回继电器。无需清空地图，随时可以回入口撤离。", 18, ChapterSkin.PaperMuted));
        lead.AddChild(Text(Session.State.Phase == ChapterPhase.Battle ? "① 看敌方意图 → ② 选牌并比较费用 → ③ 选目标，确认出牌\nSpace 结束回合；Esc 取消选牌。" : "① 点击出发，检查清单 → ② 去废料场拿继电器并装包 → ③ 回入口撤离，在岑砧处交付",16,Ink));
        var legend = new GridContainer { Columns = Size.X < 1200 ? 3 : 4, Visible = false }; legend.AddThemeConstantOverride("h_separation",22); legend.AddThemeConstantOverride("v_separation",12);
        page.AddChild(Button("help_legend", "展开 / 收起图标速查", () => legend.Visible = !legend.Visible)); page.AddChild(legend);
        foreach(var (symbol,label) in new[] { ("heart","生命"),("supply","补给"),("bag","携带物"),("exit","出口 / 撤离"),
            ("action","行动"),("mana","魔法"),("hammer","伤害"),("guard","护盾"),("draw","抽牌"),("eye","区域压力"),("all","全体 / 增援"),("round","回合 / 蓄力"),
            ("steel","废钢"),("wire","导能线"),("spring","精密弹簧"),("lock","资源不足") })
            SymbolValue(legend,symbol,label,Ink,"",16);
        page.AddChild(Text("圆形标记是物件，菱形标记是出口。停留鼠标或用 Tab 聚焦，可查看完整规则；小亮点标记关键物件。",16,Muted));
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
            var copyBox=FlexibleColumn(lesson); copyBox.AddChild(Text(number + "  /  " + title,20,Accent)); copyBox.AddChild(Text(copy,16,Ink));
        }
    }
}
