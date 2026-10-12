using CardRPG.Core;
using Godot;
using System;
using System.Linq;

namespace CardRPG;

public partial class ExpeditionDemo
{
    // HUD anchors share the same canvas as the scene; only individual drawers scroll.
    private static void Dock(Control node, float left, float top, float right, float bottom,
        float al = 0, float at = 0, float ar = 0, float ab = 0)
    {
        node.AnchorLeft = al; node.AnchorTop = at; node.AnchorRight = ar; node.AnchorBottom = ab;
        node.OffsetLeft = left; node.OffsetTop = top; node.OffsetRight = right; node.OffsetBottom = bottom;
    }
    private void BuildWorld(Control parent)
    {
        var s = Session.State;
        Control scene = _map
            ? new ExpeditionRegion { Location = s.Location, Powered = s.Flags.Contains("power") }
            : s.Phase == ExpeditionPhase.Exploring
            ? new ExpeditionLocalScene { Location = s.Location, SiteIds = Session.Actions().Select(a => a.Id).ToArray(), Powered = s.Flags.Contains("power") }
            : new IndustrialBackdrop { Location = s.Phase switch
            {
                ExpeditionPhase.Reward => "echo", ExpeditionPhase.Extracted => "exit",
                ExpeditionPhase.Defeated => "furnace", ExpeditionPhase.Battle => s.Location, _ => "gate"
            } };
        scene.Name = "WorldScene"; parent.AddChild(scene); scene.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new TextureRect
        {
            Name = "SceneShade", MouseFilter = MouseFilterEnum.Ignore,
            Texture = new GradientTexture2D
            {
                FillFrom = Vector2.Zero, FillTo = Vector2.Down,
                Gradient = new Gradient
                {
                    Colors = [new Color("030b13df"), new Color("06131c12"), new Color("06131c00"), new Color("040d17bf")],
                    Offsets = [0, .25f, .65f, 1]
                }
            }
        };
        parent.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }
    private void BuildHudHeader(Control hud)
    {
        var s = Session.State;
        var top = Row(12); hud.AddChild(top); Dock(top, 28, 20, -28, 65, ar: 1);
        top.AddChild(new ExpeditionGlyph { Symbol = "echo", CustomMinimumSize = new Vector2(36, 44) });
        var title = Column(0); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; top.AddChild(title);
        title.AddChild(Text("灰烬远征", 25, Ink)); title.AddChild(Text("A S H E N   E X P E D I T I O N", 11, Accent));
        top.AddChild(Button("map", _map ? "关闭地图  [M]" : "区域地图  [M]", () => { _map = !_map; _inventory = false; _help = false; Render(); }));
        top.AddChild(Button("inventory", _inventory ? "返回场景  [I]" : "装备与战利品  [I]", () => { _inventory = !_inventory; _help = false; _map = false; Render(); }));
        top.AddChild(Button("help", _help ? "关闭指引  [F1]" : "行动指引  [F1]", () => { _help = !_help; _inventory = false; _map = false; Render(); }));
        top.AddChild(Button("quit", "保存并退出", RequestQuit));
        var vitals = Panel(hud, new Color("0b1b23db")); vitals.Name = "VitalsHud"; Dock(vitals, 28, 86, 636, 142);
        var row = Row(24); vitals.AddChild(row);
        Metric(row, "生命", $"{s.Health}/{ExpeditionSession.MaxHealth}", s.Health <= 14 ? Danger : Success, "guard");
        Metric(row, "补给", s.Supplies.ToString("00"), Ink, "supply");
        Metric(row, "未入库", $"{s.Materials} 材料 · {s.Loot.Count} 装备", Accent, "loot");
        Metric(row, "行动", s.Steps.ToString("00"), Ink, "route");
        var place = Text(s.Phase == ExpeditionPhase.Camp ? "据点 · 安全区域" : Session.ThreatTierText, 16, s.Phase == ExpeditionPhase.Camp ? Success : Accent);
        place.HorizontalAlignment = HorizontalAlignment.Right; hud.AddChild(place); Dock(place, -460, 98, -30, 136, al: 1, ar: 1);
    }
    private VBoxContainer ScrollBody(Node parent)
    {
        _body = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill
        };
        parent.AddChild(_body);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_right", 10); _body.AddChild(margin);
        var content = Column(12); content.SizeFlagsHorizontal = SizeFlags.ExpandFill; margin.AddChild(content); return content;
    }
    private VBoxContainer Overlay(Control hud, string title, string closeId, Action close)
    {
        var shade = new ColorRect { Color = new Color("040a11a3"), MouseFilter = MouseFilterEnum.Ignore };
        hud.AddChild(shade); Dock(shade, 0, 151, 0, 0, ar: 1, ab: 1);
        var panel = Panel(hud, new Color("101f28f5")); panel.Name = "Overlay"; Dock(panel, 48, 164, -48, -52, ar: 1, ab: 1);
        var layout = Column(16); panel.AddChild(layout);
        var head = Row(); layout.AddChild(head);
        var label = Text(title, 28, Ink); label.SizeFlagsHorizontal = SizeFlags.ExpandFill; head.AddChild(label);
        head.AddChild(Button(closeId, "返回场景  [Esc]", close));
        return ScrollBody(layout);
    }
}
