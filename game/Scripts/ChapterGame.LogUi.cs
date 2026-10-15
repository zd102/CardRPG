using CardRPG.Core;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace CardRPG;

public partial class ChapterGame
{
    private readonly HashSet<string> _expandedLogGroups = new();
    private void JournalLog(VBoxContainer box)
    {
        Tabs(box, "log_", [("all", "全部"), ("battle", "战斗"), ("stock", "物资"), ("world", "探索"), ("quest", "任务")], _logFilter, key => _logFilter = key);
        var page = Scroll(box); var s = Session.State;
        page.AddChild(Button("recent_receipts", UiCopy.Format("UI_FBE948826912", _receipts.Count(r => r.Success), _receipts.Count(r => !r.Success)), () => OpenNested("receipts")));
        foreach (var report in s.ReportHistory.Concat(s.LastReport == null ? [] : new[] { s.LastReport }).Reverse())
        {
            page.AddChild(Button("report_history_" + report.Run, (report.Run == 0 ? UiCopy.Text("UI_2FC95325D52C") : UiCopy.Format("UI_5BE055DDD65A", report.Run)) + (report.Success ? UiCopy.Text("UI_6538174F79A4") : UiCopy.Text("UI_8FD34C031A48")), () =>
            {
                _detailItemId = 0; _detailTitle = UiCopy.Text("UI_F5BEFCA8921F");
                _detailText = (report.Success ? UiCopy.Text("UI_7FE3CD3E4050") : UiCopy.Text("UI_715D0F6332BE")) + (report.BaselineKnown ? "" : UiCopy.Text("UI_0E2D07722C33"))
                    + UiCopy.Text("UI_D40F035AAF43") + ReportItems(report.Gained) + UiCopy.Text("UI_76EA43ADEC93") + ReportItems(report.Lost)
                    + UiCopy.Text("UI_17AB98A67BB5") + string.Join("、", report.Preserved) + UiCopy.Text("UI_99794C46EE5B") + string.Join("\n", report.Changes.Select(WorldChange)); OpenNested("detail");
            }));
        }
        foreach (var group in s.Events.AsEnumerable().Reverse().Where(e => _logFilter == "all" || e.Type == _logFilter).GroupBy(e => (e.Run, e.Place, e.Type)))
        {
            var key = $"{group.Key.Run}:{group.Key.Place}:{group.Key.Type}";
            var place = group.Key.Place == "camp" ? "余火站" : ChapterContent.Places[group.Key.Place].Name;
            var type = group.Key.Type switch { "battle" => "战斗", "stock" => "物资", "quest" => "任务", _ => "探索" };
            page.AddChild(Button("log_group_" + key, (_expandedLogGroups.Contains(key) ? "− " : "+ ") + UiCopy.Format("UI_FFC7F7151030", group.Key.Run, place, type, group.Count()), () => { if (!_expandedLogGroups.Add(key)) _expandedLogGroups.Remove(key); Render(); }));
            if (_expandedLogGroups.Contains(key)) foreach (var entry in group.OrderBy(e => e.Id))
                page.AddChild(Text(UiCopy.Format("UI_580A44125CED", entry.Pressure) + (entry.Round > 0 ? UiCopy.Format("UI_A52BAB8A19BF", entry.Round) : "") + " · " + entry.Text, 16, Ink));
        }
        var structuredTexts = s.Events.Select(e => e.Text).ToHashSet(); var legacy = s.Log.Where(note => !structuredTexts.Contains(note)).ToArray();
        if (_logFilter == "all" && legacy.Length > 0)
        {
            page.AddChild(Text(UiCopy.Text("UI_58F6D6819E71"), 16, Muted));
            foreach (var note in legacy) page.AddChild(Text(note, 16, Ink));
        }
        if (_lastFeedback.Count > 0)
        {
            page.AddChild(Button("log_battle_feedback", UiCopy.Text("UI_962B1772733D"), () => { _detailTitle = UiCopy.Text("UI_75A62C22B362"); _detailText = string.Join("\n", _lastFeedback); OpenNested("detail"); }));
        }
    }
    private static string ReportItems(IReadOnlyDictionary<string, int> items) => items.Count == 0 ? "无" : string.Join("\n", items.Select(p => ChapterContent.ItemName(p.Key) + " ×" + p.Value));
    private void DeliverQuest(string id)
    {
        Change(() => Session.TurnIn(id));
        if (Session.State.Done.Contains(id)) { _selectedQuest = id; _questFilter = "done"; Render(); }
    }
    private void RequestQuestBatch()
    {
        _batchQuestSelection.Clear(); foreach (var quest in ChapterContent.Quests.Where(q => Session.CanTurnIn(q.Id))) _batchQuestSelection.Add(quest.Id);
        _batchQuestCompleted.Clear(); _batchQuestRemaining.Clear(); _batchQuestRunning = false; OpenNested("quest-batch");
    }
}
