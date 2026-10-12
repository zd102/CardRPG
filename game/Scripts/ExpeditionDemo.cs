using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardRPG;

public partial class ExpeditionDemo : Control
{
    private static readonly Color Ink = new("edf0e6");
    private static readonly Color Muted = new("a9bbb7");
    private static readonly Color Accent = new("dfbb7b");
    private static readonly Color Danger = new("f0a08c");
    private static readonly Color Success = new("91c4b2");
    private static readonly Color Surface = new("10212bea");
    private static readonly Color Outline = new("596f727d");
    private Control _root = null!;
    private int _target;
    private bool _inventory;
    private bool _help;
    private bool _map;
    private string _saveWarning = "";
    private bool _verify;
    private ScrollContainer? _body;
    private string _page = "";
    public ExpeditionSession Session { get; private set; } = new();
    public Dictionary<string, Button> Buttons { get; } = new();
    public override void _Ready()
    {
        _verify = OS.GetCmdlineUserArgs().Contains("--verify-expedition");
        Theme = new Theme { DefaultFont = new SystemFont { FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "sans-serif"] }, DefaultFontSize = 16 };
        GetWindow().MinSize = new Vector2I(1100, 760);
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestQuit;
        if (!_verify) Load();
        Render();
        GD.Print("EXPEDITION_READY | action steps, equipment cards, extraction");
        if (_verify) AddChild(new ExpeditionVerification { Game = this });
    }
    public void ReplaceSession(ExpeditionSession session) { Session = session; _target = 0; _inventory = false; _help = false; _map = false; _page = ""; Render(); }
    public void Change(Func<bool> action)
    {
        if (!action()) return;
        if (_target >= Session.State.Enemies.Count || Session.State.Enemies[_target].Health <= 0)
            _target = Math.Max(0, Session.State.Enemies.FindIndex(e => e.Health > 0));
        Save(); Render();
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode == Key.Escape && (_inventory || _help || _map)) { _inventory = false; _help = false; _map = false; Render(); }
        else if (key.Keycode == Key.I) { _inventory = !_inventory; _help = false; _map = false; Render(); }
        else if (key.Keycode == Key.F1) { _help = !_help; _inventory = false; _map = false; Render(); }
        else if (key.Keycode == Key.M) { _map = !_map; _inventory = false; _help = false; Render(); }
        else return;
        GetViewport().SetInputAsHandled();
    }
    public void Render()
    {
        var focus = GetViewport().GuiGetFocusOwner()?.Name.ToString();
        var s = Session.State;
        var page = _map ? "map" : _help ? "help" : _inventory ? "inventory" : s.Phase + ":" + s.Location;
        var scroll = page == _page ? _body?.ScrollVertical ?? 0 : 0;
        _page = page;
        if (_root != null) { RemoveChild(_root); _root.QueueFree(); }
        Buttons.Clear(); _body = null;
        _root = new Control { Name = "Presentation", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_root); _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildWorld(_root);
        BuildHudHeader(_root);
        if (_map) Map(_root);
        else if (_help) Help(Overlay(_root, "行动手册", "close_help", () => { _help = false; Render(); }));
        else if (_inventory) Inventory(Overlay(_root, "装备与卡组", "close_inventory", () => { _inventory = false; Render(); }));
        else switch (s.Phase)
        {
            case ExpeditionPhase.Camp: Camp(_root); break;
            case ExpeditionPhase.Exploring: Explore(_root); break;
            case ExpeditionPhase.Battle: Battle(_root); break;
            case ExpeditionPhase.Reward: Blessing(_root); break;
            default: Debrief(_root); break;
        }
        var footer = Row(); _root.AddChild(footer); Dock(footer, 28, -28, -28, -6, at: 1, ar: 1, ab: 1);
        var hint = Inline(_verify ? "验证模式 · 不读写玩家存档" : "自动保存 · 思考不耗时 · Tab / Enter 操作 · M 地图 · I 装备 · F1 指引", 12, Muted);
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(hint); footer.AddChild(Inline("FOUNDRY / 01", 12, Accent));
        if (_saveWarning != "")
        {
            var error = Panel(_root, new Color("492c28")); Dock(error, 28, 147, -28, 194, ar: 1);
            error.AddChild(Text(_saveWarning, 16, Danger));
        }
        if (_body != null) _body.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, scroll);
        if (focus != null && Buttons.TryGetValue(focus, out var same) && !same.Disabled) same.CallDeferred(Control.MethodName.GrabFocus);
    }
    private void Camp(Control hud)
    {
        var title = Column(12); hud.AddChild(title); Dock(title, 44, 222, -430, 440, ar: 1);
        Eyebrow(title, "区域 01   /   FOUNDRY DISTRICT");
        title.AddChild(Text("废弃铸造区", 48, Ink));
        title.AddChild(Text("深入灰烬，带回回响。", 21, Ink));
        var mission = Panel(hud, new Color("0d1b23d9")); Dock(mission, 40, -211, -430, -64, at: 1, ar: 1, ab: 1);
        var goal = Column(8); mission.AddChild(goal);
        Eyebrow(goal, "远征目标"); goal.AddChild(Text("找到回响护腕，活着把它带回来。", 21, Ink));
        goal.AddChild(Text("绕行废料场，启动工坊，切断熔炉支援。\n路线由你决定，成功撤离后收获才会入库。", 16, Muted));
        var panel = Panel(hud); Dock(panel, -392, 174, -24, -58, al: 1, ar: 1, ab: 1);
        var ready = Column(12); panel.AddChild(ready);
        Heading(ready, "出征整备", "装备决定你的每一张牌");
        var gear = ScrollBody(ready); Equipment(gear, true);
        gear.AddChild(Text($"当前卡组 · {Session.Deck().Count} 张\n{DeckSummary()}", 16, Muted));
        var stats = Row(24); ready.AddChild(stats);
        Metric(stats, "仓库材料", Session.State.StoredMaterials.ToString(), Accent);
        Metric(stats, "成功撤离", Session.State.Extractions + " 次", Success);
        ready.AddChild(Button("depart", "进入铸造区    →", () => Change(Session.Depart), true, true));
        ready.AddChild(Text("满生命出发 · 2 份补给 · 绑定装备保留", 13, Muted));
    }
    private void Battle(Control hud)
    {
        var s = Session.State;
        var encounter = Panel(hud); Dock(encounter, 28, 176, 304, 328);
        var briefing = Column(10); encounter.AddChild(briefing);
        Eyebrow(briefing, "ENCOUNTER / 交战中", Danger);
        briefing.AddChild(Text($"第 {s.Round:00} 回合", 28, Ink));
        briefing.AddChild(Text(s.BattleReason, 16, Muted));
        var tip = Text("点击敌人锁定目标\n再使用下方手牌", 16, Ink); hud.AddChild(tip); Dock(tip, 40, 345, 290, 395);
        for (var i = 0; i < s.Enemies.Count; i++)
        {
            var index = i; var enemy = s.Enemies[i]; var selected = index == _target;
            var x = s.Enemies.Count == 1 ? .63f : i == 0 ? .48f : .77f;
            var target = Column(6); target.Name = "Enemy_" + i; hud.AddChild(target); Dock(target, -130, 174, 130, -334, al: x, ar: x, ab: 1);
            var vitals = Panel(target, new Color("101e26df"), selected ? Accent : Outline);
            var status = Column(5); vitals.AddChild(status);
            status.AddChild(Text($"生命 {enemy.Health}/{enemy.MaxHealth}    护盾 {enemy.Block}", 16, Ink));
            HealthBar(status, enemy.Health, enemy.MaxHealth, Danger);
            target.AddChild(new ExpeditionActor { Name = "Actor", Selected = selected, Boss = enemy.Id == "boss", Defeated = enemy.Health <= 0, Picked = () => { _target = index; Render(); }, CustomMinimumSize = new Vector2(0, 130), SizeFlagsVertical = SizeFlags.ExpandFill });
            target.AddChild(Button("target_" + i, (selected ? "◎  " : "选择  ") + enemy.Name, () => { _target = index; Render(); }, enemy.Health > 0, selected));
            var intent = Text(enemy.Health <= 0 ? "已击败" : $"下一步：攻击 {enemy.Damage}" + (enemy.Id == "boss" ? " · 重建 5 护盾" : ""), 16, enemy.Health <= 0 ? Muted : Danger);
            intent.HorizontalAlignment = HorizontalAlignment.Center; target.AddChild(intent);
        }
        var dock = Panel(hud, new Color("0a161eec")); dock.Name = "HandDock";
        Dock(dock, 24, -318, -24, -38, at: 1, ar: 1, ab: 1);
        var row = Row(14); dock.AddChild(row);
        var cards = Row(10); cards.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(cards);
        for (var i = 0; i < s.Hand.Count; i++)
        {
            var index = i; var card = ExpeditionContent.Cards[s.Hand[i]];
            var playable = s.ActionPoints >= card.ActionCost && s.Mana >= card.ManaCost;
            var p = Panel(cards, playable ? new Color("26392fec") : new Color("17272bef"), playable ? new Color("778768") : Outline);
            p.SizeFlagsHorizontal = SizeFlags.ExpandFill; p.CustomMinimumSize = new Vector2(158, 0);
            var box = Column(7); p.AddChild(box);
            var cost = Row(7); box.AddChild(cost);
            cost.AddChild(new ExpeditionGlyph { Symbol = card.Id, CustomMinimumSize = new Vector2(25, 27), Tint = playable ? Accent : Muted });
            var price = Text(card.ManaCost > 0 ? $"{card.ManaCost} 魔法" : $"{card.ActionCost} 行动", 16, card.ManaCost > 0 ? Success : Accent);
            price.SizeFlagsHorizontal = SizeFlags.ExpandFill; cost.AddChild(price);
            box.AddChild(Text(card.Name, 21, Ink));
            var effect = Text(card.Text, 16, Ink); effect.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(effect);
            box.AddChild(Text("来源 · " + SourceOf(card.Id), 13, Muted));
            box.AddChild(Button("card_" + i, playable ? "使用" : "资源不足", () => Change(() => Session.PlayCard(index, _target)), playable, playable));
        }
        for (var i = s.Hand.Count; i < 5; i++) cards.AddChild(new Control { CustomMinimumSize = new Vector2(158, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var commands = Column(9); commands.CustomMinimumSize = new Vector2(204, 0); row.AddChild(commands);
        var resources = Row(18); commands.AddChild(resources);
        Metric(resources, "行动", $"{s.ActionPoints}/3", Accent);
        Metric(resources, "魔法", $"{s.Mana}/3", Success);
        Metric(resources, "护盾", s.Block.ToString(), Ink);
        commands.AddChild(Text($"抽牌 {s.Draw.Count} / 弃牌 {s.Discard.Count}", 13, Muted));
        commands.AddChild(Button("end_turn", "结束回合    →", () => Change(() => Session.EndTurn()), true, true));
        commands.AddChild(Text("剩余手牌全部弃置", 13, Muted));
        commands.AddChild(Button("retreat", "战术撤退", () => Change(() => Session.EndTurn(true))));
        commands.AddChild(Text("承受攻击后回入口，损失最多 3 材料；受击致死丢失全部未入库收获。", 13, Muted));
    }
    private void Blessing(Control hud)
    {
        var title = Column(10); hud.AddChild(title); Dock(title, 44, 206, 630, 376);
        Eyebrow(title, "RESONANCE / 回响装置");
        title.AddChild(Text("让回响回应你的选择", 38, Ink));
        title.AddChild(Text("仅本次远征生效 · 选择时不推进时间", 16, Muted));
        var choices = Row(20); hud.AddChild(choices); Dock(choices, 40, -285, -40, -62, at: 1, ar: 1, ab: 1);
        var number = 0;
        foreach (var id in Session.State.BlessingOffers)
        {
            var panel = Panel(choices); panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var box = Column(12); panel.AddChild(box);
            Eyebrow(box, $"回响 / {++number:00}");
            box.AddChild(Text(ExpeditionSession.BlessingName(id), 28, Ink));
            var description = Text(ExpeditionSession.BlessingText(id), 21, Success); description.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(description);
            box.AddChild(Button("bless_" + id, "选择此祝福    →", () => Change(() => Session.ChooseBlessing(id)), true, true));
        }
    }
    private void Debrief(Control hud)
    {
        var s = Session.State; var extracted = s.Phase == ExpeditionPhase.Extracted;
        var title = Column(10); hud.AddChild(title); Dock(title, 44, 205, 760, 323);
        Eyebrow(title, extracted ? "EXTRACTION COMPLETE / 撤离成功" : "EXPEDITION LOST / 远征失败", extracted ? Success : Danger);
        title.AddChild(Text(extracted ? "带着回响，归来。" : "灰烬中，仍有下一程。", 38, Ink));
        var panel = Panel(hud); Dock(panel, 40, 347, 618, -75, ab: 1);
        var result = Column(16); panel.AddChild(result);
        result.AddChild(Text(extracted ? "收获已入库" : "远征结束", 28, Ink));
        result.AddChild(Text(s.Summary, 21, Ink));
        var stats = Row(30); result.AddChild(stats);
        Metric(stats, "仓库材料", s.StoredMaterials.ToString(), Accent);
        Metric(stats, "收藏装备", s.Owned.Count + " 种", Ink);
        Metric(stats, "成功撤离", s.Extractions + " 次", Success);
        result.AddChild(Text(extracted ? "调整装备，构筑下一次远征的卡组。" : "本次未撤离装备和材料已丢失；绑定装备与情报保留。", 16, Muted));
        result.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        result.AddChild(Button("camp", "回到据点    →", () => Change(Session.ReturnToCamp), true, true));
    }
    private void Inventory(VBoxContainer content)
    {
        var s = Session.State;
        content.AddChild(Text(CanEquip ? "可以换装 · 更换装备不消耗行动" : "当前仅可查看 · 据点或已启用工坊可以换装", 16, Muted));
        var row = Row(22); content.AddChild(row);
        var left = FlexibleColumn(row, 1);
        Heading(left, "持有装备", "装备即卡组 · 每次换装立即更新供牌");
        Equipment(left);
        var right = FlexibleColumn(row, 1.3f);
        Heading(right, $"当前卡组 · {Session.Deck().Count} 张", "随装备改变 · 每回合抽取 5 张");
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 12); right.AddChild(grid);
        foreach (var group in Session.Deck().GroupBy(x => x))
        {
            var spec = ExpeditionContent.Cards[group.Key]; var p = Panel(grid); p.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var box = Column(8); p.AddChild(box);
            var heading = Row(8); box.AddChild(heading);
            heading.AddChild(new ExpeditionGlyph { Symbol = group.Key, CustomMinimumSize = new Vector2(28, 32) });
            var cardName = Text($"{spec.Name} ×{group.Count()}", 21, Ink); cardName.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(cardName);
            box.AddChild(Text(spec.Text, 16, Muted));
            box.AddChild(Text(spec.ManaCost > 0 ? $"魔法 {spec.ManaCost}" : $"行动 {spec.ActionCost}", 13, Accent));
        }
        var loot = CardBox(right, new Color("292a24"));
        Eyebrow(loot, "未撤离战利品", Accent);
        loot.AddChild(Text(s.Loot.Count == 0 ? "暂无装备收获" : string.Join("\n", s.Loot.GroupBy(x => x).Select(g => $"{ExpeditionContent.Gear[g.Key].Name} ×{g.Count()} · 未入库")), 21, Ink));
        loot.AddChild(Text($"未入库材料 {s.Materials}   /   仓库材料 {s.StoredMaterials}\n新装备即使穿上，战败也会丢失；成功撤离后才绑定。", 16, Muted));
        right.AddChild(Text(s.Intel.Contains("boss") ? "永久情报：铸炉守卫每轮恢复 5 护盾。" : "尚未调查铸炉守卫。", 16, Success));

    }
    private bool CanEquip => Session.State.Phase == ExpeditionPhase.Camp || (Session.State.Phase == ExpeditionPhase.Exploring && Session.State.Location == "workshop" && Session.State.Flags.Contains("workshop"));
    private void Equipment(VBoxContainer parent, bool compact = false)
    {
        var s = Session.State;
        foreach (var id in s.Owned.Concat(s.Loot).Distinct().Where(id => id != "armor"))
        {
            var gear = ExpeditionContent.Gear[id]; var wearing = id == s.Weapon || id == "bracer" && s.WearBracer;
            var p = Panel(parent, wearing ? new Color("242e29") : Surface, wearing ? new Color("8b845f") : Outline);
            var row = Row(12); p.AddChild(row);
            row.AddChild(new ExpeditionGlyph { Symbol = id, CustomMinimumSize = new Vector2(compact ? 36 : 52, 52), Tint = wearing ? Accent : Muted });
            var box = FlexibleColumn(row);
            box.AddChild(Button("equip_" + id, (wearing ? "已穿戴 · " : "穿戴 · ") + gear.Name + (s.Owned.Contains(id) ? "" : "（未撤离）"), () => Change(() => Session.Equip(id)), CanEquip && (!wearing || id == "bracer")));
            box.AddChild(Text(ExpeditionContent.GearCards(id), 16, Muted));
            if (!compact) box.AddChild(Text(gear.Description, 13, Muted));
        }
        parent.AddChild(Text("固定防具 · 铆钉护甲\n坚守 ×3 / 架盾猛击 ×1", 16, Muted));
    }
    private void Help(VBoxContainer content)
    {
        Heading(content, "一次远征，三个决定", "没有倒计时。读懂局势，再做选择。");
        var row = Row(18); content.AddChild(row);
        Guide(row, "01", "去哪里", "点击当前场景边缘的出口移动，每次 1 步。M 打开只读地图；移动必须返回场景。入口营地可安全撤离。\n\n废料场能开启货运站，为深入熔炉后的返程留一条退路。", "route");
        Guide(row, "02", "何时开战", "主动迎战立即锁定敌人；隐蔽接近需要 1 步。\n\n巡逻到达广场后先锁定。此时离开可脱离，继续搜索会被拦截。", "strike");
        Guide(row, "03", "带走还是深入", "新装备和材料需撤离入库。临时祝福仅本局有效。\n\n工坊启用后可换装。失败会丢失未入库收获，绑定装备与情报保留。", "loot");
        var panel = CardBox(content);
        Heading(panel, "战斗速查", "先选择敌人，再使用卡牌；敌人下一步意图始终可见。");
        panel.AddChild(Text("每回合抽 5 张 · 行动点恢复到 3 · 魔法点保留并恢复 1（上限 3）\n回合末剩余手牌全部弃置，普通护盾在下个玩家回合清除。", 16, Ink));
        panel.AddChild(Text("鼠标可完成全部操作 · Tab 聚焦 / Enter 或 Space 执行 · M 地图 · I 装备 · F1 指引 · Esc 返回", 16, Muted));

    }
    private string DeckSummary() => string.Join("  /  ", Session.Deck().GroupBy(x => x).Select(g => $"{ExpeditionContent.Cards[g.Key].Name} ×{g.Count()}"));
    private string SourceOf(string card) => string.Join(" / ", ExpeditionContent.Gear.Values.Where(g => (g.Id == Session.State.Weapon || g.Id == "armor" || g.Id == "bracer" && Session.State.WearBracer) && g.Cards.Contains(card)).Select(g => g.Name));

    private string SavePath => ProjectSettings.GlobalizePath("user://expedition-v1.json");
    private void RequestQuit()
    {
        if (Save()) GetTree().Quit();
        else Render();
    }
    public bool Save()
    {
        if (_verify) return true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            File.WriteAllText(SavePath + ".tmp", Session.ToJson());
            File.Move(SavePath + ".tmp", SavePath, true);
            _saveWarning = ""; return true;
        }
        catch (Exception e) { _saveWarning = "保存失败，进度暂留在内存，请检查磁盘后重试退出：" + e.Message; GD.PushWarning(_saveWarning); return false; }
    }
    private void Load()
    {
        try { if (File.Exists(SavePath)) Session = ExpeditionSession.FromJson(File.ReadAllText(SavePath)); }
        catch (Exception e)
        {
            _saveWarning = "无法读取旧存档，已开启新进度。";
            // Preserve unreadable input before the next successful automatic save.
            try { if (File.Exists(SavePath)) File.Copy(SavePath, SavePath + ".invalid-" + DateTime.UtcNow.Ticks); }
            catch (Exception backup) { throw new IOException("无法保留损坏存档，停止启动以防覆盖。", backup); }
            GD.PushWarning(e.Message);
        }
    }
}
