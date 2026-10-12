using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardRPG;

public partial class ExpeditionDemo : Control
{
    private static readonly Color Ink = new("edf0e8");
    private static readonly Color Muted = new("a9bfc0");
    private static readonly Color Accent = new("e9bb72");
    private static readonly Color Danger = new("f1a28d");
    private VBoxContainer _root = null!;
    private int _target;
    private bool _inventory;
    private bool _help;
    private string _saveWarning = "";
    private bool _verify;
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

    public void ReplaceSession(ExpeditionSession session) { Session = session; _target = 0; _inventory = false; _help = false; Render(); }
    public void Change(Func<bool> action)
    {
        if (!action()) return;
        _target = Math.Max(0, Session.State.Enemies.FindIndex(e => e.Health > 0));
        Save(); Render();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode == Key.Escape && (_inventory || _help)) { _inventory = false; _help = false; Render(); }
        else if (key.Keycode == Key.I) { _inventory = !_inventory; _help = false; Render(); }
        else if (key.Keycode == Key.F1) { _help = !_help; _inventory = false; Render(); }
        else return;
        GetViewport().SetInputAsHandled();
    }

    public void Render()
    {
        var focus = GetViewport().GuiGetFocusOwner()?.Name.ToString();
        if (_root != null) { RemoveChild(_root); _root.QueueFree(); }
        Buttons.Clear();
        _root = new VBoxContainer { OffsetLeft = 28, OffsetTop = 20, OffsetRight = -28, OffsetBottom = -20, AnchorRight = 1, AnchorBottom = 1 };
        _root.AddThemeConstantOverride("separation", 12); AddChild(_root);
        var top = Row(); _root.AddChild(top);
        var titles = Column(); titles.SizeFlagsHorizontal = SizeFlags.ExpandFill; top.AddChild(titles);
        titles.AddChild(Text("灰烬远征", 28, Ink));
        titles.AddChild(Text("装备构筑  /  行动探索  /  搜打撤", 16, Muted));
        top.AddChild(Button("inventory", _inventory ? "返回场景  [I]" : "装备与战利品  [I]", () => { _inventory = !_inventory; _help = false; Render(); }));
        top.AddChild(Button("help", _help ? "关闭指引" : "行动指引  [F1]", () => { _help = !_help; _inventory = false; Render(); }));
        top.AddChild(Button("quit", "保存并退出", RequestQuit));
        var s = Session.State;
        var status = Panel(_root, new Color("1c3035"));
        status.AddChild(Text($"生命 {s.Health}/{ExpeditionSession.MaxHealth}     补给 {s.Supplies}     未入库材料 {s.Materials}     装备 {s.Loot.Count}     行动 {s.Steps}  ·  {Session.ThreatTierText}", 16, Ink));
        if (_saveWarning != "") _root.AddChild(Text(_saveWarning, 16, Danger));
        var body = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _root.AddChild(body);
        var content = Column(); content.SizeFlagsHorizontal = SizeFlags.ExpandFill; body.AddChild(content);
        if (_help) Help(content);
        else if (_inventory) Inventory(content);
        else switch (s.Phase)
        {
            case ExpeditionPhase.Camp: Camp(content); break;
            case ExpeditionPhase.Exploring: Explore(content); break;
            case ExpeditionPhase.Battle: Battle(content); break;
            case ExpeditionPhase.Reward: Blessing(content); break;
            default: Debrief(content); break;
        }
        var bottom = Row(); _root.AddChild(bottom);
        var note = Text(_verify ? "验证模式 · 不读写玩家存档" : "每次行动后自动保存  ·  阅读与思考不会推进时间  ·  Tab / Enter 可操作", 16, Muted);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill; bottom.AddChild(note);
        var edition = Text("FOUNDRY / 01", 16, Accent);
        edition.AutowrapMode = TextServer.AutowrapMode.Off;
        bottom.AddChild(edition);
        if (focus != null && Buttons.TryGetValue(focus, out var same) && !same.Disabled) same.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Camp(VBoxContainer content)
    {
        var row = Row(); content.AddChild(row);
        var left = Column(); left.SizeFlagsHorizontal = SizeFlags.ExpandFill; left.SizeFlagsStretchRatio = 1.3f; row.AddChild(left);
        left.AddChild(Text("下一站 · 废弃铸造区", 28, Accent));
        left.AddChild(new IndustrialBackdrop { CustomMinimumSize = new Vector2(560, 285), SizeFlagsHorizontal = SizeFlags.ExpandFill });
        left.AddChild(Text("找到回响护腕，活着把它带回来。", 21, Ink));
        left.AddChild(Text("可绕行、调查、切断支援，也可直接迎战。战利品在撤离前都有风险。\n每次远征重新生成临时祝福与搜索奖励；仓库装备长期保留。", 16, Muted));
        var right = Column(); right.CustomMinimumSize = new Vector2(330, 0); right.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(right);
        var p = Panel(right); var box = Column(); p.AddChild(box);
        box.AddChild(Text("出征整备", 21, Ink));
        box.AddChild(Text($"仓库材料 {Session.State.StoredMaterials}  ·  成功撤离 {Session.State.Extractions} 次", 16, Muted));
        Equipment(box);
        box.AddChild(Text($"当前卡组 {Session.Deck().Count} 张\n{DeckSummary()}", 16, Muted));
        box.AddChild(Button("depart", "进入铸造区  →", () => Change(Session.Depart), true, true));
        box.AddChild(Text("满生命出发，携带 2 份补给。\n基础绑定装备不会因战败丢失。", 16, Muted));
    }

    private void Explore(VBoxContainer content)
    {
        var s = Session.State;
        var warning = Panel(content, s.Locked ? new Color("49312b") : new Color("243237"));
        warning.AddChild(Text(Session.Threat, 16, s.Locked ? Danger : Accent));
        var row = Row(); content.AddChild(row);
        var left = Column(); left.SizeFlagsHorizontal = SizeFlags.ExpandFill; left.SizeFlagsStretchRatio = 1.6f; row.AddChild(left);
        left.AddChild(Text("区域路线 · 点击相邻地点移动 1 步", 16, Muted));
        var map = new Control { CustomMinimumSize = new Vector2(620, 205), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddChild(map);
        var background = new IndustrialBackdrop { IsMap = true }; map.AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        string[] ids = ["gate", "square", "scrap", "workshop", "furnace", "exit"];
        Vector2[] points = [new(.10f, .5f), new(.34f, .24f), new(.34f, .76f), new(.61f, .24f), new(.86f, .24f), new(.75f, .76f)];
        for (var i = 0; i < ids.Length; i++)
        {
            var id = ids[i]; var current = id == s.Location;
            var canMove = ExpeditionContent.Places[s.Location].Neighbors.Contains(id);
            var b = Button("move_" + id, (current ? "● " : "") + ExpeditionContent.Places[id].Name, () => Change(() => Session.Move(id)), canMove, current);
            b.AnchorLeft = b.AnchorRight = points[i].X; b.AnchorTop = b.AnchorBottom = points[i].Y;
            b.OffsetLeft = -56; b.OffsetRight = 56; b.OffsetTop = -22; b.OffsetBottom = 22;
            b.TooltipText = current ? "当前位置" : canMove ? "移动消耗 1 步。离开广场能解除当前锁定。" : "需要沿连接路线前往";
            map.AddChild(b);
        }
        var scene = new IndustrialBackdrop { Location = s.Location, CustomMinimumSize = new Vector2(620, 160), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddChild(scene);
        var sceneTitle = Text(ExpeditionContent.Places[s.Location].Name, 21, Accent);
        sceneTitle.AutowrapMode = TextServer.AutowrapMode.Off;
        sceneTitle.Position = new Vector2(16, 12); scene.AddChild(sceneTitle);
        left.AddChild(Text(ExpeditionContent.Places[s.Location].Description, 16, Muted));
        left.AddChild(Text("最近发生", 16, Accent));
        left.AddChild(Text(string.Join("\n", s.Log.TakeLast(3)), 16, Muted));
        var right = Column(); right.CustomMinimumSize = new Vector2(365, 0); right.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(right);
        right.AddChild(Text("场景行动", 21, Ink));
        foreach (var action in Session.Actions())
        {
            var box = Column(); var panel = Panel(right); panel.AddChild(box);
            box.AddChild(Button("act_" + action.Id, action.Name, () => Change(() => Session.Act(action.Id)), action.Enabled, action.Id is "extract" or "boss"));
            box.AddChild(Text(action.Description, 16, action.Enabled ? Muted : new Color("809395")));
        }
        if (s.Location == "workshop" && s.Flags.Contains("workshop"))
            right.AddChild(Button("field_equipment", "在工坊调整装备", () => { _inventory = true; Render(); }));
        if (s.Location == "furnace" && s.Intel.Contains("boss")) right.AddChild(Text("已知机制：守卫每轮重建 5 护盾；破甲重击可绕过。", 16, Accent));
        right.AddChild(Text($"祝福：{ExpeditionSession.BlessingName(s.Blessing)}\n{ExpeditionSession.BlessingText(s.Blessing)}", 16, Accent));
    }

    private void Battle(VBoxContainer content)
    {
        var s = Session.State;
        content.AddChild(Text($"第 {s.Round} 回合  /  {s.BattleReason}", 21, Accent));
        content.AddChild(Text("点击敌人选择目标，再点击卡牌出牌。敌人意图公开；场外巡逻暂停。", 16, Muted));
        var enemies = Row(); content.AddChild(enemies);
        for (var i = 0; i < s.Enemies.Count; i++)
        {
            var index = i; var enemy = s.Enemies[i];
            var p = Panel(enemies, index == _target ? new Color("3b4035") : new Color("203035"));
            p.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var b = Column(); p.AddChild(b);
            b.AddChild(Button("target_" + i, (index == _target ? "◎ " : "") + enemy.Name, () => { _target = index; Render(); }, enemy.Health > 0));
            b.AddChild(Text($"生命 {enemy.Health}/{enemy.MaxHealth}   护盾 {enemy.Block}", 21, Ink));
            b.AddChild(Text(enemy.Health <= 0 ? "已击败" : $"意图：攻击 {enemy.Damage}" + (enemy.Id == "boss" ? "，随后重建 5 护盾" : ""), 16, Danger));
        }
        content.AddChild(Text($"行动点 {s.ActionPoints}/3     魔法点 {s.Mana}/3     护盾 {s.Block}     抽牌堆 {s.Draw.Count}  /  弃牌堆 {s.Discard.Count}", 21, Ink));
        var cards = new HBoxContainer(); cards.AddThemeConstantOverride("separation", 10); content.AddChild(cards);
        for (var i = 0; i < s.Hand.Count; i++)
        {
            var index = i; var card = ExpeditionContent.Cards[s.Hand[i]];
            var p = Panel(cards, new Color("23383c")); p.SizeFlagsHorizontal = SizeFlags.ExpandFill; p.CustomMinimumSize = new Vector2(190, 235);
            var box = Column(); p.AddChild(box);
            box.AddChild(Text(card.ManaCost > 0 ? $"魔法  {card.ManaCost}" : $"行动  {card.ActionCost}", 16, Accent));
            box.AddChild(Text(card.Name, 21, Ink));
            var description = Text(card.Text, 16, Muted); description.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(description);
            box.AddChild(Text("来源 · " + SourceOf(card.Id), 16, Muted));
            box.AddChild(Button("card_" + i, "使用", () => Change(() => Session.PlayCard(index, _target)), s.ActionPoints >= card.ActionCost && s.Mana >= card.ManaCost, true));
        }
        var controls = Row(); content.AddChild(controls);
        controls.AddChild(Button("end_turn", "结束回合 · 弃掉剩余手牌", () => Change(() => Session.EndTurn()), true, true));
        controls.AddChild(Button("retreat", "战术撤退 · 承受本轮攻击后回入口", () => Change(() => Session.EndTurn(true))));
        content.AddChild(Text("撤退成功损失最多 3 材料，不发战斗奖励。若本轮受击致死，按战败丢失全部未撤离收获。", 16, Muted));
        content.AddChild(Text(string.Join("\n", s.Log.TakeLast(3)), 16, Muted));
    }

    private void Blessing(VBoxContainer content)
    {
        content.AddChild(Text("回响装置 · 选择本次远征的祝福", 28, Accent));
        content.AddChild(Text("装备形成基础卡组，临时祝福改变这一次的打法。选择时不推进时间。", 16, Muted));
        foreach (var id in Session.State.BlessingOffers)
        {
            var panel = Panel(content); var box = Column(); panel.AddChild(box);
            box.AddChild(Text(ExpeditionSession.BlessingName(id), 21, Ink));
            box.AddChild(Text(ExpeditionSession.BlessingText(id), 16, Muted));
            box.AddChild(Button("bless_" + id, "接受祝福", () => Change(() => Session.ChooseBlessing(id)), true, true));
        }
    }

    private void Debrief(VBoxContainer content)
    {
        content.AddChild(new IndustrialBackdrop { CustomMinimumSize = new Vector2(0, 230), SizeFlagsHorizontal = SizeFlags.ExpandFill });
        content.AddChild(Text(Session.State.Phase == ExpeditionPhase.Extracted ? "收获已入库" : "远征结束", 28, Accent));
        content.AddChild(Text(Session.State.Summary, 21, Ink));
        content.AddChild(Text($"仓库：{Session.State.StoredMaterials} 材料  /  {Session.State.Owned.Count} 种装备", 16, Muted));
        content.AddChild(Button("camp", "回到据点 · 准备下一次远征", () => Change(Session.ReturnToCamp), true, true));
    }

    private void Inventory(VBoxContainer content)
    {
        var s = Session.State;
        content.AddChild(Text("装备与卡组", 28, Accent));
        content.AddChild(Text("据点和已启用工坊可以换装。其他地点、战斗和结算期间只允许查看。\n新发现的装备即使穿上，战败也会丢失；成功撤离后才绑定。", 16, Muted));
        Equipment(content);
        content.AddChild(Text($"当前卡组 · {Session.Deck().Count} 张", 21, Ink));
        content.AddChild(Text(DeckSummary(), 16, Muted));
        content.AddChild(Text("未撤离战利品", 21, Accent));
        content.AddChild(Text(s.Loot.Count == 0 ? "暂无装备收获" : string.Join("\n", s.Loot.GroupBy(x => x).Select(g => $"{ExpeditionContent.Gear[g.Key].Name} ×{g.Count()} · 未入库")), 16, Ink));
        content.AddChild(Text($"未入库材料 {s.Materials} / 仓库材料 {s.StoredMaterials}\n{(s.Intel.Contains("boss") ? "永久情报：铸炉守卫每轮恢复 5 护盾。" : "尚未调查铸炉守卫。")}", 16, Muted));
        content.AddChild(Button("close_inventory", "返回", () => { _inventory = false; Render(); }));
    }

    private void Equipment(VBoxContainer box)
    {
        var s = Session.State;
        var allowed = s.Phase == ExpeditionPhase.Camp || (s.Phase == ExpeditionPhase.Exploring && s.Location == "workshop" && s.Flags.Contains("workshop"));
        foreach (var id in s.Owned.Concat(s.Loot).Distinct().Where(id => id != "armor"))
        {
            var gear = ExpeditionContent.Gear[id]; var wearing = id == s.Weapon || id == "bracer" && s.WearBracer;
            box.AddChild(Button("equip_" + id, (wearing ? "已穿戴 · " : "穿戴 · ") + gear.Name + (s.Owned.Contains(id) ? "" : "（未撤离）"), () => Change(() => Session.Equip(id)), allowed && (!wearing || id == "bracer"), wearing));
            box.AddChild(Text(ExpeditionContent.GearCards(id), 16, Muted));
        }
        box.AddChild(Text("固定防具 · 铆钉护甲：坚守 ×3、架盾猛击 ×1", 16, Muted));
    }

    private void Help(VBoxContainer content)
    {
        content.AddChild(Text("一次远征，三个决定", 28, Accent));
        content.AddChild(Text("01  去哪里\n点击地图相邻地点移动，每次 1 步。入口营地始终可以安全撤离。\n\n02  何时开战\n直接迎战立即锁定敌人；隐蔽接近需要 1 步。巡逻到达广场后先锁定，再给一次应对机会。离开可解除锁定，继续搜索会被拦截。\n\n03  带走还是深入\n新装备和材料需撤离入库。祝福仅本局有效；工坊可更换装备。废料场能开启货运站，给深入熔炉后的返程留下退路。", 21, Ink));
        content.AddChild(Text("战斗：每回合抽 5 张，行动点恢复至 3，魔法点保留并恢复 1（上限 3），回合末手牌全部弃置。选择敌人后出牌，普通护盾下个玩家回合清除。\n\n所有行动均由点击确认，不限思考时间。按钮支持 Tab 聚焦、Enter / Space 执行。I 查看装备，F1 查看指引，Esc 返回。\n\nDemo 范围：单角色、固定区域、两种基础武器；属性加点、随机词缀和制作树尚未实现。", 16, Muted));
        content.AddChild(Button("close_help", "返回远征", () => { _help = false; Render(); }));
    }

    private string DeckSummary() => string.Join("  /  ", Session.Deck().GroupBy(x => x).Select(g => $"{ExpeditionContent.Cards[g.Key].Name} ×{g.Count()}"));
    private string SourceOf(string card) => string.Join(" / ", ExpeditionContent.Gear.Values.Where(g => (g.Id == Session.State.Weapon || g.Id == "armor" || g.Id == "bracer" && Session.State.WearBracer) && g.Cards.Contains(card)).Select(g => g.Name));
    private static VBoxContainer Column() { var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 10); return v; }
    private static HBoxContainer Row() { var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 18); return h; }
    private static Label Text(string value, int size, Color color)
    {
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color); return label;
    }
    private static StyleBoxFlat Style(Color background, Color border)
    {
        return new StyleBoxFlat { BgColor = background, BorderColor = border, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10 };
    }
    private static PanelContainer Panel(Node parent, Color? background = null)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Style(background ?? new Color("1b2b31"), new Color("395056"))); parent.AddChild(panel); return panel;
    }
    private Button Button(string id, string label, Action action, bool enabled = true, bool primary = false)
    {
        var button = new Button { Name = id, Text = label, Disabled = !enabled, CustomMinimumSize = new Vector2(0, 44), FocusMode = FocusModeEnum.All };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", primary ? new Color("172329") : Ink);
        button.AddThemeColorOverride("font_hover_color", Ink);
        button.AddThemeColorOverride("font_disabled_color", new Color("9baaa8"));
        button.AddThemeStyleboxOverride("normal", Style(primary ? Accent : new Color("2b4147"), primary ? Accent : new Color("50696c")));
        button.AddThemeStyleboxOverride("hover", Style(new Color("47636a"), Accent));
        button.AddThemeStyleboxOverride("pressed", Style(new Color("18272e"), Accent));
        button.AddThemeStyleboxOverride("disabled", Style(new Color("233033"), new Color("3b4c50")));
        var focus = Style(Colors.Transparent, Accent); focus.BorderWidthBottom = focus.BorderWidthLeft = focus.BorderWidthRight = focus.BorderWidthTop = 2;
        button.AddThemeStyleboxOverride("focus", focus);
        button.Pressed += () => { if (!button.Disabled) action(); };
        Buttons[id] = button; return button;
    }

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
