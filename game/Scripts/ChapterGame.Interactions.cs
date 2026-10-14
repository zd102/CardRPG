using CardRPG.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private int _selectedCard = -1;
    private string _dialogTitle = "", _dialogText = "", _dialogAccept = "确认", _choice = "", _story = "";
    private Action? _dialogAction;
    private static float ReadingScale = 1;
    private bool _reducedMotion, _sound = true, _labels, _turnReminder = true, _battleGuide = true;
    private List<string> _feedback = new(), _lastFeedback = new();
    private ulong _feedbackStarted;
    private Label? _feedbackLabel;
    private PanelContainer? _feedbackPanel;
    private int _lastFeedbackIndex = -1;
    private Vector2 _playedFrom;
    private bool FeedbackBusy => !_reducedMotion && _feedback.Count > 0 && Time.GetTicksMsec() - _feedbackStarted < (ulong)(_feedback.Count * 180);

    public override void _Input(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode == Key.Enter && key.ShiftPressed && _panel == "gear" && _gearFilter == "cargo")
        {
            var item = Session.State.Stock.FirstOrDefault(i => i.Id == _selectedStock);
            if (item != null && !FeedbackBusy)
            {
                if (Session.State.Ground.ContainsKey(item.Container)) Change(() => Session.Pickup(item.Id, item.Quantity));
                else if (Session.State.Phase == ChapterPhase.Camp) Change(() => Session.Transfer(item.Id, item.Container == "carry" ? "warehouse" : "carry", item.Quantity));
                else if (Session.State.Phase == ChapterPhase.Exploring && item.Container == "carry") Change(() => Session.Drop(item.Id, item.Quantity));
            }
            GetViewport().SetInputAsHandled(); return;
        }
        if (_panel == "" && Session.State.Phase == ChapterPhase.Battle && key.Keycode == Key.Space)
        { if (!FeedbackBusy) RequestEndTurn(); GetViewport().SetInputAsHandled(); }
        if (_panel == "" && _selectedCard >= 0 && key.Keycode is Key.Left or Key.Right)
        {
            var living = Session.State.Battle!.Enemies.Select((e, i) => (e, i)).Where(p => p.e.Health > 0).Select(p => p.i).ToArray();
            var n = Array.IndexOf(living, _target); _target = living[(n + (key.Keycode == Key.Right ? 1 : living.Length - 1)) % living.Length];
            Render(); GetViewport().SetInputAsHandled();
        }
    }

    private void Confirm(string title, string copy, Action action, string accept = "确认执行")
    {
        _dialogTitle = title; _dialogText = copy; _dialogAction = action; _dialogAccept = accept;
        OpenNested("confirm");
    }

    private void Confirmation(VBoxContainer box)
    {
        var body = Scroll(box); body.AddChild(Text(_dialogText, 18, Ink));
        var row = Row(16); box.AddChild(row);
        row.AddChild(Button("cancel_action", "取消 · 保持现状", ClosePanel));
        row.AddChild(Button("confirm_action", _dialogAccept, () =>
        { var action = _dialogAction; ClosePanel(); action?.Invoke(); }, primary: true));
    }

    private void SelectCard(int index)
    {
        if (FeedbackBusy || !Session.CanPlay(index, _target)) return;
        _selectedCard = index; Cue("select"); Render();
    }

    private void CommitSelectedCard()
    {
        var index = _selectedCard;
        if (Buttons.TryGetValue("card_" + index, out var card)) _playedFrom = card.Position + card.GetParent<Control>().Position;
        Change(() => Session.PlayCard(index,_target));
    }

    private string CardPreview()
    {
        var b = Session.State.Battle;
        if (b == null || _selectedCard < 0 || _selectedCard >= b.Hand.Count) return "";
        var card = ChapterContent.Cards[b.Hand[_selectedCard]];
        var next = Session.Preview(s => s.PlayCard(_selectedCard, _target));
        if (next == null) return "费用不足或目标无效";
        var lines = new List<string> { card.Name + $" · {card.Action} 行动 / {card.Mana} 魔法", card.Text };
        if (card.Damage > 0)
        {
            foreach (var i in Enumerable.Range(0, b.Enemies.Count).Where(i => b.Enemies[i].Health > 0 && (card.Id == "C18" || i == _target)))
            {
                var enemy = b.Enemies[i]; var after = next.LastResolvedBattle?.Enemies.ElementAtOrDefault(i);
                lines.Add(ChapterSession.EnemyName(enemy.Id) + $"：生命 {enemy.Health} → {after?.Health ?? 0} / 护盾 {enemy.Shield} → {after?.Shield ?? 0}");
            }
        }
        var nb = next.State.Battle;
        if (nb != null) lines.Add($"自身护盾 {b.Shield} → {nb.Shield} · 行动 {b.Action} → {nb.Action} · 魔法 {b.Mana} → {nb.Mana}");
        if (card.Id is "C26" or "C36") lines.Add($"实际额外抽牌 {Math.Min(card.Id == "C36" ? 2 : 1, 2 - b.ExtraDraw)} 张（本回合上限 2）");
        return string.Join("\n", lines);
    }

    private string TurnForecast(bool retreat = false)
    {
        var b = Session.State.Battle;
        if (b == null) return "";
        if (b.Id == "trial") return "试用不消耗真实生命与物品，可免费结束。";
        var next = Session.Preview(s => s.EndTurn(retreat))!;
        var damage = Session.State.Health - next.State.Health;
        var copy = $"本轮敌方攻击 {b.Enemies.Sum(ChapterSession.EnemyDamage)} · 当前护盾 {b.Shield}\n预计失去 {damage} 生命，剩余 {next.State.Health}/{ChapterSession.MaxHealth}。";
        if (next.State.Phase == ChapterPhase.Defeated) copy += "\n本次行动将导致紧急回收，随身未保留物品会丢失。";
        else if (retreat) copy += "\n承伤后退回" + ChapterContent.Places[b.ReturnPlace].Name + "；尚未安全撤离，物品没有入库。";
        return copy;
    }

    private void RequestEndTurn()
    {
        if (FeedbackBusy) return;
        var b = Session.State.Battle;
        if (b == null) return;
        if (_turnReminder && Enumerable.Range(0, b.Hand.Count).Any(i => Session.CanPlay(i, _target)))
            Confirm("仍有可用手牌", TurnForecast() + "\n仍有手牌可以使用。可在设置关闭此提醒。", () => Change(() => Session.EndTurn()), "结束回合");
        else Change(() => Session.EndTurn());
    }

    private void Retreat()
    {
        if (Session.State.Battle?.Id == "trial")
        {
            Change(Session.EndTrial);
            if (Session.State.World.Contains("trial-complete")) { _craftTab = "recipes"; _selectedRecipe = "G26"; Toggle("craft"); }
            return;
        }
        Confirm("战术撤退 · 仍会承伤", TurnForecast(true), () => Change(() => Session.EndTurn(true)), "承受本轮意图并撤退");
    }

    private void ExploreAction(string id)
    {
        if (id.StartsWith("train-") && id != "train-inspect" || id is "R01" or "R07")
        { _choice = id; OpenNested("choice"); return; }
        if (id == "extract")
        {
            var left = Session.State.Stock.Where(i => Session.State.Ground.ContainsKey(i.Container) && !i.Registered && (i.Kind.StartsWith('T') || ChapterContent.Gear.ContainsKey(i.Kind))).ToArray();
            if (left.Length > 0)
            {
                Confirm("有重要物品尚未带走", string.Join("\n", left.Select(i => ChapterContent.Places[Session.State.Ground[i.Container].Place].Name + " · " + ChapterContent.ItemName(i.Kind)))
                    + "\n本次撤离会清除地面遗留物。取消后可打开地图查看地点。", () => Change(() => Session.Act(id)), "确认撤离"); return;
            }
        }
        Change(() => Session.Act(id));
    }

    private void Choices(VBoxContainer box)
    {
        var resonance = _choice.StartsWith('R');
        box.AddChild(Text(resonance ? "本趟最多激活 1 种共鸣 · 离开灰区失效 · 本趟不可替换" : "本次选择 1 项 · 先比较，再确认", 20, Accent));
        box.AddChild(Text(resonance ? "查看和取消免费。" : "所选物品先留在地面，需要装包。选择物资后，居民包裹下一趟仍可回取。", 16, Muted));
        var page = Scroll(box); var choices = Column(12); page.AddChild(choices);
        foreach (var a in Session.Actions().Where(a => resonance ? a.Id is "R01" or "R07" : a.Id is "train-wire" or "train-supply" or "train-parcel"))
        {
            var item = CardBox(choices); var action = a;
            item.AddChild(Button("choose_" + a.Id, (_choice == a.Id ? "◆ " : "○ ") + a.Name, () => { _choice = action.Id; Render(); }));
            item.AddChild(Text(a.Description, 16, Ink));
            var kind = a.Id switch { "train-wire" => "M02", "train-supply" => "U01", "train-parcel" => "T14", _ => "" };
            if (kind != "")
            { var def = ChapterContent.Items[kind]; item.AddChild(Text($"{def.Slots} 格 · {(def.Grams * (kind == "M02" ? 2 : 1)) / 1000m:0.##} kg · " + (kind == "T14" ? "居民委托" : kind == "U01" ? "恢复生命" : "制作材料"), 16, Muted)); }
        }
        if (Session.State.Warned) box.AddChild(Text("巡逻已锁定：确认会先被拦截，选择尚不会生效。", 16, Danger));
        var row = Row(12); box.AddChild(row); row.AddChild(Button("choice_cancel", "暂不选择", ClosePanel));
        row.AddChild(Button("choice_confirm", "确认选择 · 行动值 +" + Session.SceneActionCost(), () => { var id = _choice; ClosePanel(); Change(() => Session.Act(id)); }, primary: true));
    }

    private void SystemMenu(VBoxContainer box)
    {
        var page = Scroll(box);
        page.AddChild(Button("resume", "继续游戏 · Esc", ClosePanel, primary: true));
        page.AddChild(Text("阅读字号", 20, Ink));
        Tabs(page, "font_", [("100", "标准 16"), ("125", "较大 20"), ("150", "大字 24")], ((int)(ReadingScale * 100)).ToString(), value => { ReadingScale = int.Parse(value) / 100f; SavePreferences(); });
        page.AddChild(Text("调整规则、详情与说明的字号；目录和图标保持紧凑，长内容可滚动。", 16, Muted));
        void Setting(string id, string title, bool on, Action toggle) => page.AddChild(Button(id, title + (on ? "：开" : "：关"), () => { toggle(); SavePreferences(); Render(); }));
        Setting("setting_motion", "减少动态效果", _reducedMotion, () => _reducedMotion = !_reducedMotion);
        Setting("setting_sound", "界面与战斗音效", _sound, () => _sound = !_sound);
        Setting("setting_labels", "常显全部场景标签", _labels, () => _labels = !_labels);
        Setting("setting_reminder", "有可用手牌时提醒结束回合", _turnReminder, () => _turnReminder = !_turnReminder);
        Setting("setting_guide", "战斗短引导", _battleGuide, () => _battleGuide = !_battleGuide);
        page.AddChild(Button("system_help", "操作说明", () => OpenNested("help")));
        Rule(page); page.AddChild(Button("save_quit", "保存并退出", RequestQuit));
    }

    private void LoadPreferences()
    {
        var file = new ConfigFile(); if (file.Load("user://ui-settings.cfg") != Error.Ok) return;
        var scale = (float)file.GetValue("ui", "reading", 1f); ReadingScale = scale is 1f or 1.25f or 1.5f ? scale : 1f;
        _reducedMotion = (bool)file.GetValue("ui", "reduced", false); _sound = (bool)file.GetValue("ui", "sound", true);
        _labels = (bool)file.GetValue("ui", "labels", false); _turnReminder = (bool)file.GetValue("ui", "reminder", true);
        _battleGuide = (bool)file.GetValue("ui", "guide", true);
    }
    private void SavePreferences()
    {
        if (_verify) return;
        var file = new ConfigFile(); file.SetValue("ui", "reading", ReadingScale); file.SetValue("ui", "reduced", _reducedMotion);
        file.SetValue("ui", "sound", _sound); file.SetValue("ui", "labels", _labels); file.SetValue("ui", "reminder", _turnReminder); file.SetValue("ui", "guide", _battleGuide);
        if (file.Save("user://ui-settings.cfg") != Error.Ok) _actionWarning = "设置未能写入磁盘，本次运行仍然生效。";
    }

    private void Cue(string category)
    {
        if (!_sound || _verify) return;
        var frequency = category switch { "select" => 620, "hit" => 130, "shield" => 380, "reward" => 880, "danger" => 180, _ => 480 };
        var bytes = new byte[4410];
        for (var i = 0; i < bytes.Length / 2; i++)
        {
            var value = (short)(Math.Sin(i * Math.Tau * frequency / 22050) * 2400 * Math.Sin(Math.PI * i / (bytes.Length / 2)));
            bytes[i * 2] = (byte)value; bytes[i * 2 + 1] = (byte)(value >> 8);
        }
        var player = new AudioStreamPlayer { Stream = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 22050, Data = bytes } };
        AddChild(player); player.Finished += player.QueueFree; player.Play();
    }

    private void FeedbackLayer()
    {
        _feedbackLabel = null; _feedbackPanel = null;
        _lastFeedbackIndex = -1;
        if (_feedback.Count == 0 || Time.GetTicksMsec() - _feedbackStarted > 6000) return;
        if (!_reducedMotion && _feedback[0].StartsWith("打出 ") && Time.GetTicksMsec() - _feedbackStarted < 100)
        {
            var travel = Panel(_root,Surface,Ink); travel.Name = "CardTravel"; travel.ZIndex = 26; travel.MouseFilter = MouseFilterEnum.Ignore;
            travel.Position = _playedFrom; travel.Size = new Vector2(110,90);
            travel.AddChild(Text(_feedback[0].Split('·')[0],16,Ink));
            var flight = travel.CreateTween().SetParallel();
            flight.TweenProperty(travel,"position",new Vector2(560,Size.Y-250),.18);
            flight.TweenProperty(travel,"modulate:a",0f,.18);
            flight.Chain().TweenCallback(Callable.From(travel.QueueFree));
        }
        _feedbackPanel = Panel(_root, Surface, Outline); _feedbackPanel.Name = "BattleFeedback";
        _feedbackPanel.MouseFilter = MouseFilterEnum.Ignore; _feedbackPanel.ZIndex = 25;
        Dock(_feedbackPanel, -350, 96, 350, 140, .5f, 0, .5f, 0);
        _feedbackLabel = Inline("", 16, Ink); _feedbackLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; _feedbackPanel.AddChild(_feedbackLabel);
    }

    public override void _Process(double delta)
    {
        if (_feedbackLabel == null || !GodotObject.IsInstanceValid(_feedbackLabel) || _feedback.Count == 0) return;
        var elapsed = Time.GetTicksMsec() - _feedbackStarted;
        var index = _reducedMotion ? _feedback.Count - 1 : Math.Min(_feedback.Count - 1, (int)(elapsed / 180));
        _feedbackLabel.Text = "结算回放 " + (index + 1) + "/" + _feedback.Count + " · " + _feedback[index];
        if (index != _lastFeedbackIndex)
        {
            _lastFeedbackIndex = index; var note = _feedback[index];
            Cue(note.Contains("护盾 +") ? "shield" : note.Contains("伤害") ? "hit" : note.Contains("抵达") ? "danger" : "select");
            if (!_reducedMotion && Session.State.Battle is { } battle && _panel == "")
            {
                for (var i = 0; i < battle.Enemies.Count; i++)
                    if (note.StartsWith(ChapterSession.EnemyName(battle.Enemies[i].Id)) && Buttons.TryGetValue("target_" + i, out var target))
                    {
                        target.Modulate = new Color(1.3f,.75f,.62f); var tween = target.CreateTween(); tween.TweenProperty(target,"modulate",Colors.White,.16);
                    }
            }
        }
        if (elapsed > (ulong)(_feedback.Count * 180 + 1600)) { _feedbackPanel!.Hide(); _feedback.Clear(); }
    }

    private static readonly Dictionary<string, (string Person, string Title, string Copy)> Stories = new()
    {
        ["Q01"] = ("岑砧 · 余火站工匠", "灯又亮了", "岑砧接过继电器，把最后一截线拧紧。暖光落在工具上。\n“这盏灯，今晚不用省着点了。”\n工作灯与后续委托已开放。"),
        ["robot-rescued"] = ("小维修偶", "松开的夹具", "小维修偶敲了敲你的护臂，举起那把弯扳手，慢慢走向入口。\n救援已永久记录；安全撤离后，可以接它进站。"),
        ["station"] = ("远征记录", "升降车重新启动", "货站深处响起电机声，停住的指示灯亮了起来。\n这里现在可以安全撤离。带回路签交付后，还能从这里出发。"),
        ["S09"] = ("余火站居民", "迟到的包裹", "居民小心展开外面的包布，又把它叠好。\n“没想到还有人记得这节车厢。”\n居民的回礼与据点变化已经保存。"),
        ["Q02"] = ("岑砧 · 余火站工匠", "有自己的工具", "岑砧试了试校准后的夹具，给你让出工作台。\n“接下来，试着配一套合你手的工具。”\n制作、改装与免费台架试用已开放。")
    };

    private void DetectStory(ChapterState before)
    {
        var key = Session.State.Done.Except(before.Done).Concat(Session.State.World.Except(before.World)).FirstOrDefault(Stories.ContainsKey);
        if (key == null) return;
        _story = key; _panelHistory.Push(new(_panel, GetViewport().GuiGetFocusOwner()?.Name.ToString(), [], _gearFilter)); _panel = "story"; Cue("reward");
    }
    private void Story(VBoxContainer box)
    {
        var story = Stories[_story]; var page = Scroll(box);
        page.AddChild(Text(story.Person, 16, Accent)); page.AddChild(Text(story.Title, 28, Ink));
        page.AddChild(Text(story.Copy, 18, Ink));
        box.AddChild(Button("story_continue", "继续 · Esc 跳过 / 可在发现记录回顾", ClosePanel, primary: true));
    }
}
