using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ExpeditionDemo
{
    private void Explore(Control hud)
    {
        var s = Session.State;
        var title = Column(4); hud.AddChild(title); Dock(title, 32, 158, 706, 237);
        title.AddChild(Text(ExpeditionContent.Places[s.Location].Name, 28, Ink));
        title.AddChild(Text(ExpeditionContent.Places[s.Location].Description, 16, Muted));
        var warning = Panel(hud, s.Locked ? new Color("492c29ef") : new Color("1d302deb"));
        Dock(warning, -513, 161, -28, 222, al: 1, ar: 1);
        warning.AddChild(Text((s.Locked ? "!  " : "◇  ") + Session.Threat, 16, s.Locked ? Danger : Accent));

        var detailPanel = Panel(hud, new Color("0b1b24e8")); detailPanel.Name = "InteractionHint";
        Dock(detailPanel, 28, -125, -28, -42, at: 1, ar: 1, ab: 1);
        var detail = Column(3); detailPanel.AddChild(detail);
        var heading = Text("场景交互 · 聚焦或悬停查看后果", 13, Accent); detail.AddChild(heading);
        var description = Text("点击物件旁的行动按钮执行；点击场景边缘的出口移动。地图仅供查看。", 16, Ink); detail.AddChild(description);
        var latest = Text("最近 · " + (s.Log.LastOrDefault() ?? ""), 13, Muted); detail.AddChild(latest);
        void Inspect(string name, string explanation) { heading.Text = name; description.Text = explanation; }

        var sites = new Control { Name = "LocalInteractions", MouseFilter = MouseFilterEnum.Ignore }; hud.AddChild(sites);
        Dock(sites, ExpeditionLocalScene.Left, ExpeditionLocalScene.Top, -ExpeditionLocalScene.Right, -ExpeditionLocalScene.Bottom, ar: 1, ab: 1);
        foreach (var action in Session.Actions())
        {
            var point = ExpeditionLocalScene.ActionPoint(action.Id);
            var fieldEquipment = action.Id == "workshop" && s.Flags.Contains("workshop");
            var id = fieldEquipment ? "field_equipment" : "act_" + action.Id;
            var name = fieldEquipment ? "在工坊调整装备" : action.Name;
            var explanation = fieldEquipment ? "已启用工坊，可以调整装备；换装本身不耗时。" : action.Description;
            var enabled = fieldEquipment || action.Enabled;
            var box = Column(4); box.Name = "Site_" + action.Id; sites.AddChild(box);
            Dock(box, -122, 16, 122, 89, point.X, point.Y, point.X, point.Y);
            var button = Button(id, name, () =>
            {
                if (fieldEquipment) { _inventory = true; Render(); }
                else Change(() => Session.Act(action.Id));
            }, enabled, action.Id is "extract" or "boss");
            button.TooltipText = explanation;
            button.FocusEntered += () => Inspect(name, explanation);
            button.MouseEntered += () => Inspect(name, explanation);
            box.AddChild(button);
            var note = Text(fieldEquipment ? "调整卡组 · 不耗时" : ActionSummary(action), 13, enabled ? Ink : Muted);
            note.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(note);
        }

        var exits = new Control { Name = "LocalExits", MouseFilter = MouseFilterEnum.Ignore }; hud.AddChild(exits);
        Dock(exits, 0, ExpeditionLocalScene.ExitTop, 0, -ExpeditionLocalScene.ExitBottom, ar: 1, ab: 1);
        var neighbors = ExpeditionContent.Places[s.Location].Neighbors;
        for (var i = 0; i < neighbors.Length; i++)
        {
            var id = neighbors[i]; var point = ExpeditionLocalScene.ExitPoint(neighbors.Length, i);
            var name = ExpeditionContent.Places[id].Name;
            var arrow = point.X < .5f ? "← " : "→ ";
            var button = Button("move_" + id, arrow + name + "\n移动 · 1 步", () => Change(() => Session.Move(id)));
            exits.AddChild(button); Dock(button, -87, -15, 87, 57, point.X, point.Y, point.X, point.Y);
            var explanation = "前往" + name + "，消耗 1 步。" + (s.Locked ? "离开广场可以解除当前锁定。" : ExpeditionContent.Places[id].Description);
            button.TooltipText = explanation;
            button.FocusEntered += () => Inspect("区域出口 · " + name, explanation);
            button.MouseEntered += () => Inspect("区域出口 · " + name, explanation);
        }
    }
    private string ActionSummary(ExpeditionAction action)
    {
        var s = Session.State;
        if (!action.Enabled) return action.Id switch
        {
            "heal" => s.Health >= ExpeditionSession.MaxHealth ? "生命已满" : "补给不足",
            "extract" => "需先恢复货运站电源",
            "prepare" => s.Prepared ? "已准备武器牌" : "运输队已击败",
            "blessing" => "已接受本次祝福",
            "intel" => "永久情报已记录",
            _ => "已完成"
        };
        return action.Id switch
        {
            "extract" => "安全撤离 · 不消耗行动",
            "convoy" => s.PatrolEta == 0 && !s.Flags.Contains("patrol") ? "开战 · 巡逻将协战" : "开战 · 获得破甲锤",
            "prepare" => "准备武器牌 · 1 步",
            "safe" => s.Locked ? "继续搜索将被拦截" : s.SafeProgress == 0 ? "制造噪声 · 搜索 1/2" : "材料与随机装备 · 1 步",
            "patrol" => "开战 · 清除巡逻",
            "salvage" => "3 材料 + 1 补给 · 1 步",
            "power" => "开启撤离点 · 1 步",
            "workshop" => "开启换装 · 1 步",
            "blessing" => "发现临时祝福 · 1 步",
            "intel" => "记录永久情报 · 1 步",
            "alarm" => "移除首领支援 · 1 步",
            "boss" => s.Flags.Contains("alarm") ? "开战 · 守卫单独应战" : "开战 · 守卫有支援",
            "heal" => "恢复 14 生命 · 1 步",
            "wait" => "推进局势 · 1 步",
            _ => "查看下方行动说明"
        };
    }
    private void Map(Control hud)
    {
        var s = Session.State;
        var title = Column(5); hud.AddChild(title); Dock(title, 32, 160, 768, 226);
        title.AddChild(Text("铸造区地图", 28, Ink));
        title.AddChild(Text("只读路线图 · 查看不耗时 · 请返回区域场景，通过出口移动", 16, Muted));
        var labels = new Control { Name = "MapLabels", MouseFilter = MouseFilterEnum.Ignore }; hud.AddChild(labels);
        Dock(labels, ExpeditionRegion.Left, ExpeditionRegion.Top, -ExpeditionRegion.Right, -ExpeditionRegion.Bottom, ar: 1, ab: 1);
        foreach (var (id, point) in ExpeditionRegion.Points)
        {
            var current = id == s.Location;
            var panel = Panel(labels, current ? new Color("31402aec") : new Color("10212bdf"), current ? Accent : Outline);
            panel.Name = "MapLabel_" + id; panel.MouseFilter = MouseFilterEnum.Ignore;
            Dock(panel, -89, 37, 89, 92, point.X, point.Y, point.X, point.Y);
            var box = Column(1); box.MouseFilter = MouseFilterEnum.Ignore; panel.AddChild(box);
            var name = Text((current ? "● " : "") + ExpeditionContent.Places[id].Name, 16, current ? Accent : Ink);
            name.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(name);
            var caption = current ? "当前位置" : id == "exit" ? (s.Flags.Contains("power") ? "撤离点 · 已通电" : "撤离点 · 未通电")
                : ExpeditionContent.Places[s.Location].Neighbors.Contains(id) ? "相邻区域" : "区域地标";
            var note = Text(caption, 12, Muted); note.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(note);
        }
        var legend = Panel(hud); Dock(legend, 28, -105, -28, -45, at: 1, ar: 1, ab: 1);
        var row = Row(); legend.AddChild(row);
        var noteText = Text("● 当前位置   /   道路表示连接关系\n地图不提供移动或场景行动。", 16, Ink); noteText.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(noteText);
        row.AddChild(Button("close_map", "返回区域场景  [Esc]", () => { _map = false; Render(); }, true, true));
    }
}
