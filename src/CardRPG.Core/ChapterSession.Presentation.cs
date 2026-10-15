using System.Text.Json;

namespace CardRPG.Core;

/// <summary>Read-only forecasts use the same rules as execution, on an isolated state copy.</summary>
public sealed partial class ChapterSession
{
    public ChapterSession PreviewCopy() => new(JsonSerializer.Deserialize<ChapterState>(ToJson())!);

    public ChapterSession? Preview(Func<ChapterSession, bool> action)
    {
        var copy = PreviewCopy();
        return action(copy) ? copy : null;
    }

    public string CraftReason(string kind)
    {
        if (State.Phase != ChapterPhase.Camp) return "回到据点后制作";
        if (HasPhysical(kind)) return "已持有";
        if (kind.StartsWith("BP") ? !BackpackRecipeOpen(kind) : !RecipeOpen(kind))
            return kind == "G26" ? "完成台架试用：预备工具后攻击" : kind == "BP03" ? "完成外环主线和货站任务" : "完成任务：有自己的工具";
        var cost = kind.StartsWith("BP") ? BackpackCost(kind) : Cost(kind);
        var missing = cost.Select((n, i) => (n, i)).Where(p => p.n > SafeMaterialCount(ChapterContent.MaterialIds[p.i]))
            .Select(p => ChapterContent.MaterialName(p.i) + "缺 " + (p.n - SafeMaterialCount(ChapterContent.MaterialIds[p.i]))).ToArray();
        if (missing.Length > 0) return string.Join(" · ", missing);
        return CanProduce(kind, 1, cost) ? "" : "材料足够；仓库需腾出成品空间";
    }

    public int AvailableMaterial(int index) => SafeMaterialCount(ChapterContent.MaterialIds[index]);
    public (int Slots, int Capacity, int Grams, int Reserved) DepartureLoad()
    {
        var stock = CopyStock();
        if (State.FreeSupplyAllowance > 0) AddStock(stock, "U01", State.FreeSupplyAllowance, "carry", free: true);
        return (Slots(stock.Where(i => i.Container == "carry")), Capacity(stock), stock.Where(IsCarried).Sum(i => ChapterContent.Items[i.Kind].Grams * i.Quantity),
            stock.Where(i => IsCarried(i) && i.Registered && ChapterContent.Gear.ContainsKey(i.Kind)).Sum(i => ChapterContent.Items[i.Kind].Slots));
    }

    public string NextObjective => State.TrackedQuest != "" && QuestOpen(State.TrackedQuest) && !State.Done.Contains(State.TrackedQuest)
        ? "追踪：" + ChapterContent.Quests.Single(q => q.Id == State.TrackedQuest).Name + " · " + NextQuestStep(State.TrackedQuest) : MainObjective;
    public string MainObjective => !State.Done.Contains("Q01")
        ? State.StoredItems.Contains("T01") ? "回到岑砧处交付继电器"
            : State.Items.Contains("T01") ? "继电器已装包，回入口安全撤离" : "去废料场取继电器，再回入口撤离"
        : !State.Done.Contains("Q02")
            ? State.StoredCalibration ? "回到岑砧处交付校准夹具"
                : State.Calibrated ? "夹具已校准，带回据点交付"
                : State.Items.Contains("T02") || State.StoredItems.Contains("T02") ? "携带夹具去维修工坊校准" : "去货运广场拿取工坊夹具"
            : !State.Done.Contains("S01") ? State.StoredItems.Contains("T11") ? "交付货站路签，解锁前进站" : State.Items.Contains("T11") ? "路签已装包，安全撤离后交付" : "修复货站线路，带回路签" : "外环已完成，可继续探索与整备";

    public List<string> ExitRoute()
    {
        var paths = new Queue<List<string>>(); paths.Enqueue([State.Location]);
        var seen = new HashSet<string> { State.Location };
        while (paths.Count > 0)
        {
            var path = paths.Dequeue(); var last = path[^1];
            if (last == "A01" || last == "A05" && State.World.Contains("station")) return path;
            foreach (var next in ChapterContent.Places[last].Neighbors.Where(IsRevealed))
                if (seen.Add(next)) paths.Enqueue([.. path, next]);
        }
        return [];
    }

    // A batch has one explicit cost and one patrol interruption, just like PickupAll.
    public Dictionary<long, int> FittingPickup(string container, IReadOnlyList<long>? priority = null)
    {
        var stock = CopyStock(); var result = new Dictionary<long, int>();
        var rank = (priority ?? Array.Empty<long>()).Distinct().Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
        foreach (var item in State.Stock.Where(i => i.Container == container).OrderBy(i => rank.GetValueOrDefault(i.Id, int.MaxValue)).ThenByDescending(i => i.Kind.StartsWith('T')).ThenByDescending(i => i.KeepOnPerson).ThenBy(i => i.Id))
        {
            var low = 0; var high = item.Quantity;
            while (low < high)
            {
                var count = low + (high - low + 1) / 2;
                var candidate = stock.Select(i => i.Copy()).ToList(); MoveUnits(candidate, item.Id, "carry", count);
                if (Fits(candidate)) low = count; else high = count - 1;
            }
            if (low > 0) { result[item.Id] = low; MoveUnits(stock, item.Id, "carry", low); }
        }
        return result;
    }

    public bool PickupSelected(string container, IReadOnlyDictionary<long, int> quantities)
    {
        if (State.Phase != ChapterPhase.Exploring || !State.Ground.TryGetValue(container, out var ground)
            || ground.Place != State.Location || quantities.Count == 0) return false;
        var stock = CopyStock();
        foreach (var (id, count) in quantities)
        {
            var item = stock.FirstOrDefault(i => i.Id == id);
            if (item == null || item.Container != container || count <= 0 || count > item.Quantity) return false;
            MoveUnits(stock, id, "carry", count);
        }
        if (!Fits(stock)) return Reject("空间已变化，请重新选择要拾取的物品。");
        var cost = ground.FreePickup ? 0 : SceneActionCost();
        if (cost > 0 && InterruptInventoryAction(cost)) return true;
        CommitStock(stock); ground.FreePickup = false;
        NoteStock("已按确认清单装包，余物保留原位。" + LoadDescription);
        if (cost > 0) { EndFreePickup(); Advance(cost: cost); }
        return true;
    }

    public bool StoreCarriedMaterials()
    {
        return TransferBatch(MaterialDepositPlan(), "carry", "warehouse");
    }

    private static Dictionary<string, int> Quantities(IEnumerable<ChapterItem> items) => items.GroupBy(i => i.Kind).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

    private void BeginReport()
    {
        if (State.LastReport != null) { State.ReportHistory.Add(State.LastReport); if (State.ReportHistory.Count > 10) State.ReportHistory.RemoveAt(0); }
        State.LastReport = null;
        State.HasDepartureReportBaseline = true;
        State.DepartureQuantities = Quantities(State.Stock.Where(i => IsCarried(i) && !i.FreeSupply));
        State.DepartureWorld = [.. State.World];
    }

    private void BeginReturnReport(bool success)
    {
        var carried = State.Stock.Where(i => IsCarried(i) && !i.FreeSupply).ToArray();
        var total = Quantities(carried);
        State.LastReport = new ChapterRunReport
        {
            Run = State.Runs,
            Success = success,
            BaselineKnown = State.HasDepartureReportBaseline,
            Brought = success ? total : [],
            Gained = success && State.HasDepartureReportBaseline ? total.Where(p => p.Value > State.DepartureQuantities.GetValueOrDefault(p.Key))
                .ToDictionary(p => p.Key, p => p.Value - State.DepartureQuantities.GetValueOrDefault(p.Key)) : [],
            Lost = success ? [] : Quantities(carried.Where(i => !State.DepartureInventory.ContainsKey(i.Id))),
            Left = State.Stock.Where(i => State.Ground.ContainsKey(i.Container) && !State.DepartureInventory.ContainsKey(i.Id))
                .Select(i => ChapterContent.Places[State.Ground[i.Container].Place].Name + " · " + ChapterContent.ItemName(i.Kind) + " ×" + i.Quantity).ToList(),
            Preserved = State.Stock.Where(i => State.DepartureInventory.ContainsKey(i.Id)).Select(i => ChapterContent.ItemName(i.Kind)).Distinct().ToList(),
            Changes = State.HasDepartureReportBaseline ? State.World.Except(State.DepartureWorld).ToList() : []
        };
    }

    private bool ValidPresentation()
    {
        if (State.Events == null || State.Events.Count > 200 || State.NextLogId < 1 || State.Events.Any(e => e == null || e.Id < 1 || e.Id >= State.NextLogId || e.Type is not ("world" or "stock" or "battle" or "quest") || e.Text == null || e.Place == null || e.Place != "camp" && !ChapterContent.Places.ContainsKey(e.Place) || e.Run < 0 || e.Pressure < 0 || e.Round < 0)
            || State.Events.Select(e => e.Id).Distinct().Count() != State.Events.Count || State.ReportHistory == null || State.ReportHistory.Count > 10
            || State.QuestReceipts == null || State.QuestReceipts.Any(p => !ChapterContent.Quests.Any(q => q.Id == p.Key) || p.Value == null || p.Value.Gained == null || p.Value.Consumed == null || p.Value.Gained.Concat(p.Value.Consumed).Any(i => !ChapterContent.Items.ContainsKey(i.Key) || i.Value < 0))) return false;
        if (State.DepartureQuantities == null || State.DepartureWorld == null
            || State.DepartureQuantities.Any(p => !ChapterContent.Items.ContainsKey(p.Key) || p.Value < 0)) return false;
        if (State.ReportHistory.Any(r => !ValidReport(r))) return false;
        return State.LastReport == null || ValidReport(State.LastReport);
    }
    private static bool ValidReport(ChapterRunReport report)
    {
        if (report == null || report.Run < 0) return false;
        return report.Brought != null && report.Gained != null && report.Lost != null && report.Left != null
            && report.Preserved != null && report.Changes != null && report.NewGear != null
            && report.Brought.Concat(report.Gained).Concat(report.Lost).All(p => ChapterContent.Items.ContainsKey(p.Key) && p.Value >= 0)
            && report.NewGear.All(ChapterContent.Gear.ContainsKey);
    }
}

public sealed class ChapterRunReport
{
    public int Run { get; set; }
    public bool Success { get; set; }
    public bool BaselineKnown { get; set; }
    public List<string> NewGear { get; set; } = [];
    public Dictionary<string, int> Brought { get; set; } = [];
    public Dictionary<string, int> Gained { get; set; } = [];
    public Dictionary<string, int> Lost { get; set; } = [];
    public List<string> Left { get; set; } = [];
    public List<string> Preserved { get; set; } = [];
    public List<string> Changes { get; set; } = [];
}
