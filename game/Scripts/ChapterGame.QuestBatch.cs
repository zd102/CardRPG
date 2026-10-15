using CardRPG.Core;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private readonly HashSet<string> _batchQuestSelection = new();
    private readonly Queue<string> _batchQuestRemaining = new();
    private readonly List<string> _batchQuestCompleted = new();
    private bool _batchQuestRunning, _batchQuestScheduled;
    private void QuestBatchPage(VBoxContainer box)
    {
        box.AddChild(Text(UiCopy.Text("UI_70760299E2E4"), 18, Ink));
        var page = Scroll(box);
        foreach (var quest in ChapterContent.Quests.Where(q => Session.CanTurnIn(q.Id) || _batchQuestCompleted.Contains(q.Id)))
        {
            if (_batchQuestCompleted.Contains(quest.Id)) { page.AddChild(Text(UiCopy.Text("UI_FBEF1B77BF92") + quest.Name, 16, Success)); continue; }
            var check = new CheckBox { Name = "BatchQuest_" + quest.Id, Text = quest.Name, ButtonPressed = _batchQuestSelection.Contains(quest.Id) };
            check.AddThemeColorOverride("font_color", Ink); check.AddThemeColorOverride("font_pressed_color", Ink); page.AddChild(check);
            check.Toggled += on => { if (on) _batchQuestSelection.Add(quest.Id); else _batchQuestSelection.Remove(quest.Id); };
            var rewards = Session.QuestRewardPreview(quest.Id); page.AddChild(Text(UiCopy.Text("UI_409B2C7954C3") + ReportItems(rewards.Consumed) + UiCopy.Text("UI_D5E253118E0C") + ReportItems(rewards.Gained) + UiCopy.Format("UI_43FADFA4234E", rewards.Used, Session.WarehouseCapacity), 16, Ink));
        }
        if (_batchQuestRemaining.Count > 0) page.AddChild(Text(UiCopy.Text("UI_416376A6782F") + string.Join("、", _batchQuestRemaining.Select(id => ChapterContent.Quests.Single(q => q.Id == id).Name)), 16, Muted));
        box.AddChild(Button("quest_batch_begin", UiCopy.Text("UI_A0F118CABA12"), () =>
        {
            var ids = ChapterContent.Quests.Where(q => _batchQuestSelection.Contains(q.Id) && Session.CanTurnIn(q.Id)).Select(q => q.Id).ToArray();
            if (ids.Length == 0) { _actionWarning = "请先选择目前可交付的任务。"; Render(); return; }
            var preview = Session.Preview(s => s.TurnInBatch(ids));
            if (preview == null) { _actionWarning = "连续交付后的奖励空间不足，请减少所选任务或先整理空间。尚未交付任何任务。"; Render(); return; }
            Confirm(UiCopy.Text("UI_E3D71FC3EE33"), string.Join("\n", ids.Select(id => ChapterContent.Quests.Single(q => q.Id == id).Name)) + UiCopy.Format("UI_78FC8B553437", Session.WarehouseUsed, preview.WarehouseUsed, preview.WarehouseCapacity), () =>
            { _batchQuestRemaining.Clear(); foreach (var id in ids) _batchQuestRemaining.Enqueue(id); _batchQuestCompleted.Clear(); _batchQuestRunning = true; ContinueQuestBatch(); }, UiCopy.Text("UI_21CCA776724C"));
        }));
        if (_batchQuestRemaining.Count > 0) box.AddChild(Button("quest_batch_resume", UiCopy.Text("UI_C4E584873979"), () => { _batchQuestRunning = true; ContinueQuestBatch(); }));
        box.AddChild(Button("quest_batch_cancel", UiCopy.Text("UI_C2F5A44CFB2F"), () => { _batchQuestRunning = false; ClosePanel(); }));
    }
    private void ContinueQuestBatch()
    {
        if (!_batchQuestRunning || _batchQuestRemaining.Count == 0 || Session.State.Phase != ChapterPhase.Camp) { _batchQuestRunning = false; return; }
        var id = _batchQuestRemaining.Peek();
        if (!Session.CanTurnIn(id)) { _batchQuestRunning = false; _actionWarning = "条件或奖励空间已变化。已交付 " + _batchQuestCompleted.Count + " 项；剩余项未执行。"; _panel = "quest-batch"; Render(); return; }
        Change(() => Session.TurnIn(id));
        if (!Session.State.Done.Contains(id)) { _batchQuestRunning = false; _actionWarning = "本项未保存，后续项没有执行。"; Render(); return; }
        _batchQuestRemaining.Dequeue(); _batchQuestCompleted.Add(id); _batchQuestSelection.Remove(id);
        if (_panel != "story") ScheduleQuestBatch();
    }
    private async void ScheduleQuestBatch()
    {
        if (_batchQuestScheduled || !_batchQuestRunning) return;
        _batchQuestScheduled = true;
        var sessionGeneration=_sessionGeneration;
        await ToSignal(GetTree().CreateTimer(.025), SceneTreeTimer.SignalName.Timeout);
        if(sessionGeneration!=_sessionGeneration)return;
        _batchQuestScheduled = false;
        if (!_batchQuestRunning) return;
        if (_panel != "quest-batch") { _batchQuestRunning = false; return; }
        if (_batchQuestRemaining.Count == 0) { _batchQuestRunning = false; _panel = "quest-batch"; Render(); return; }
        ContinueQuestBatch();
    }
}
