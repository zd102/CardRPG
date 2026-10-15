namespace CardRPG.Core;

public sealed partial class ChapterSession
{
    public bool TurnInBatch(IReadOnlyList<string> quests)
    {
        if (quests.Count == 0 || quests.Distinct().Count() != quests.Count || quests.Any(id => !CanTurnIn(id))) return Reject("所选任务尚未全部满足交付条件。");
        var preview = PreviewCopy();
        foreach (var id in quests) if (!preview.TurnIn(id)) return Reject("连续交付后的奖励空间不足；本次未交付任何任务。");
        foreach (var id in quests) TurnIn(id);
        return true;
    }
    public bool TrackQuest(string id)
    {
        if (id != "" && (!QuestOpen(id) || State.Done.Contains(id))) return false;
        State.TrackedQuest = id; return true;
    }
    public static string QuestItem(string id) => id switch { "Q01" => "T01", "Q02" => "T02", "S01" => "T11", "S08" => "G25", "S09" => "T14", _ => "" };
    public string QuestPlace(string id)
    {
        if (!QuestOpen(id) || State.Done.Contains(id) || CanTurnIn(id)) return "";
        var item = State.Stock.FirstOrDefault(i => i.Kind == QuestItem(id));
        if (item != null && State.Ground.TryGetValue(item.Container, out var ground)) return IsRevealed(ground.Place) ? ground.Place : "";
        var readyToReturn = item != null && IsCarried(item) && (id != "Q02" || item.Calibrated)
            || id == "S07" && State.World.Contains("robot-rescued");
        if (readyToReturn) return AtBase ? "" : NearestExit().Place;
        if (item?.Container == "warehouse" && (id != "Q02" || item.Calibrated)) return "";
        var place = id switch { "Q01" or "S07" => "A03", "Q02" => item != null ? "A04" : "A02", "S01" => "A05", "S08" => IsRevealed("A06") ? "A06" : "A04", "S09" => "A07", _ => "" };
        return place != "" && IsRevealed(place) ? place : "";
    }
    public string[] QuestSteps(string id)
    {
        if (State.Done.Contains(id)) return ["✓ 已完成交付，奖励已入库。"];
        if (id == "S10") return [State.World.Contains("trial-complete") ? "✓ 台架试用已完成" : "○ 参加台架试用并打出借用手套卡", "— 剧场后续尚未开放"];
        var kind = QuestItem(id); var result = new List<string>();
        if (kind != "")
        {
            var item = State.Stock.FirstOrDefault(i => i.Kind == kind);
            var location = item == null ? "尚未获得" : item.Container == "warehouse" ? "已在仓库" : IsCarried(item) ? "已携带" : "仍在地面，未装包";
            result.Add((item == null ? "○ " : "✓ ") + ChapterContent.ItemName(kind) + "：" + location);
            if (id == "Q02") result.Add(item?.Calibrated == true ? "✓ 已校准" : "○ 带到维修工坊校准");
            result.Add(item != null && item.Container is "carry" or "warehouse" && AtBase ? "✓ 已安全带回" : "○ 安全撤离并带回物品");
        }
        if (id == "S07") result.Add(State.World.Contains("robot-rescued") ? "✓ 已解救维修偶" : "○ 在废料场解救维修偶");
        if (id == "S01") result.Add(State.World.Contains("station") ? "✓ 货站线路已修复" : "○ 修复货站线路");
        result.Add(CanTurnIn(id) ? "✓ 可以交付" : "○ 满足条件后在据点交付"); return result.ToArray();
    }
    public (Dictionary<string, int> Gained, Dictionary<string, int> Consumed, int Used) QuestRewardPreview(string id)
    {
        if (!QuestOpen(id) || State.Done.Contains(id)) return ([], [], WarehouseUsed);
        var before = Quantities(State.Stock); var result = QuestResult(id); var after = Quantities(result);
        var gained = after.Where(p => p.Value > before.GetValueOrDefault(p.Key)).ToDictionary(p => p.Key, p => p.Value - before.GetValueOrDefault(p.Key));
        var consumed = before.Where(p => p.Value > after.GetValueOrDefault(p.Key)).ToDictionary(p => p.Key, p => p.Value - after.GetValueOrDefault(p.Key));
        return (gained, consumed, Slots(result.Where(i => i.Container == "warehouse")));
    }
}
