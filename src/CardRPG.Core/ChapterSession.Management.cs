namespace CardRPG.Core;

/// <summary>Inventory commands shared by buttons, shortcuts, menus and drag/drop.</summary>
public sealed partial class ChapterSession
{
    public bool SetCarryProtection(long id, bool keep)
    {
        var item = State.Stock.FirstOrDefault(i => i.Id == id);
        if (State.Phase != ChapterPhase.Camp || item == null || item.Container is not ("carry" or "warehouse"))
            return Reject("请回到据点设置携行保留。");
        item.KeepOnPerson = keep;
        NoteStock(ChapterContent.ItemName(item.Kind) + (keep ? "：已标记保留随身。" : "：已取消保留随身。")); return true;
    }

    public bool SetCarryQuota(string kind, int count)
    {
        if (State.Phase != ChapterPhase.Camp || !ChapterContent.Items.ContainsKey(kind) || count is < 0 or > 100000) return false;
        if (count == 0) State.CarryQuotas.Remove(kind); else State.CarryQuotas[kind] = count;
        return true;
    }

    public Dictionary<long, int> MaterialDepositPlan(IEnumerable<string>? kinds = null)
    {
        var plan = new Dictionary<long, int>();
        if (State.Phase != ChapterPhase.Camp) return plan;
        foreach (var group in CarriedItems.Where(i => ChapterContent.MaterialIds.Contains(i.Kind) && (kinds == null || kinds.Contains(i.Kind))).GroupBy(i => i.Kind))
        {
            var remaining = Math.Max(0, State.CarryQuotas.GetValueOrDefault(group.Key) - group.Where(i => i.KeepOnPerson).Sum(i => i.Quantity));
            foreach (var item in group.Where(i => !i.KeepOnPerson).OrderBy(i => i.Id))
            {
                var keep = Math.Min(item.Quantity, remaining); remaining -= keep;
                if (item.Quantity > keep) plan[item.Id] = item.Quantity - keep;
            }
        }
        return plan;
    }

    public bool TransferBatch(IReadOnlyDictionary<long, int> quantities, string source, string destination, bool overrideProtection = false)
    {
        if (State.Phase != ChapterPhase.Camp || source is not ("carry" or "warehouse") || destination is not ("carry" or "warehouse") || source == destination || quantities.Count == 0)
            return Reject("营地批量取放只在随身和仓库之间执行。");
        var stock = CopyStock();
        foreach (var (id, count) in quantities)
        {
            var item = stock.FirstOrDefault(i => i.Id == id);
            if (item == null || item.Container != source || count < 1 || count > item.Quantity || item.FreeSupply)
                return Reject("物品位置或数量已改变，请重新查看清单。");
            if (source == "carry" && item.KeepOnPerson && !overrideProtection)
                return Reject("所选物品标记为保留随身，请明确允许本次移走或取消标记。");
            MoveUnits(stock, id, destination, count);
        }
        if (!Fits(stock))
        {
            var carry=Slots(stock.Where(i=>i.Container=="carry"));var capacity=Capacity(stock);var reserved=ReservedSlots(stock);var stored=Slots(stock.Where(i=>i.Container=="warehouse"))+reserved;
            return Reject($"空间不足：随身需 {carry}/{capacity} 格，还缺 {Math.Max(0,carry-capacity)} 格；仓库含返还预留 {reserved} 格共需 {stored}/{WarehouseCapacity}，还缺 {Math.Max(0,stored-WarehouseCapacity)} 格。本次未移动任何物品。");
        }
        CommitStock(stock);
        NoteStock($"已将 {quantities.Count} 批、{quantities.Values.Sum()} 件物品" + (destination == "carry" ? "装入随身。" : "存入仓库。"));
        return true;
    }

    public Dictionary<long, int> FittingTransfer(IReadOnlyDictionary<long, int> quantities, string source, string destination)
    {
        var result = new Dictionary<long, int>();
        if (State.Phase != ChapterPhase.Camp) return result;
        foreach (var (id, count) in quantities)
        {
            var item = State.Stock.FirstOrDefault(i => i.Id == id);
            if (item == null || item.Container != source || item.KeepOnPerson && source == "carry") continue;
            var low = 0; var high = Math.Min(item.Quantity, count);
            while (low < high)
            {
                var mid = low + (high - low + 1) / 2;
                var test = new Dictionary<long, int>(result) { [id] = mid };
                if (Preview(s => s.TransferBatch(test, source, destination)) != null) low = mid; else high = mid - 1;
            }
            if (low > 0) result[id] = low;
        }
        return result;
    }

    public int MaximumTransfer(long id, string destination)
    {
        var item = State.Stock.FirstOrDefault(i => i.Id == id);
        if (item == null) return 0;
        var low = 0; var high = item.Quantity;
        while (low < high)
        {
            var mid = low + (high - low + 1) / 2;
            var valid = State.Phase == ChapterPhase.Camp ? CanTransfer(id, destination, mid) : CanPickup(id, mid);
            if (valid) low = mid; else high = mid - 1;
        }
        return low;
    }

    public bool DiscardBatch(IReadOnlyDictionary<long, int> quantities)
    {
        if (State.Phase != ChapterPhase.Camp || quantities.Count == 0) return Reject("请在据点选择可放弃的实物。");
        var copy = CopyStock();
        foreach (var (id, count) in quantities)
        {
            var item = copy.FirstOrDefault(i => i.Id == id);
            if (item == null || item.Container is not ("carry" or "warehouse") || item.KeepOnPerson || item.FreeSupply || item.Kind.StartsWith('T') || count < 1 || count > item.Quantity)
                return Reject("物品已变化，或包含受保护、任务、配发物品；本次未放弃任何物品。");
            item.Quantity -= count; if (item.Quantity == 0) copy.Remove(item);
        }
        var removedGear = State.Stock.Where(i => quantities.ContainsKey(i.Id) && ChapterContent.Gear.ContainsKey(i.Kind)).Select(i => i.Kind).ToArray();
        CommitStock(copy); foreach (var kind in removedGear.Where(k => !HasPhysical(k))) State.Mods.Remove(kind);
        NoteStock($"已按确认清单永久放弃 {quantities.Count} 批、{quantities.Values.Sum()} 件物品。"); return true;
    }
    public (int Required, int Capacity) PickupSpace(long id, int count)
    {
        var copy = CopyStock(); var item = copy.FirstOrDefault(i => i.Id == id);
        if (item == null || count < 1 || count > item.Quantity) return (CarryUsed, CarryCapacity);
        MoveUnits(copy, id, "carry", count); return (Slots(copy.Where(i => i.Container == "carry")), Capacity(copy));
    }

    public string NextQuestStep(string id) => State.Done.Contains(id) ? "任务已完成，可查看交付记录。" : id switch
    {
        "S10" => State.World.Contains("trial-complete") ? "台架试用已完成；剧场后续尚未开放，无需重复试用。" : "在据点工坊进行免费台架试用，并打出借用的检修手套卡牌。",
        "Q01" => MainObjective,
        "Q02" => State.Calibrated || State.StoredCalibration ? "将校准夹具安全带回并交付。" : "到广场取夹具，再携带到工坊启动台架并校准。",
        "S01" => "在货站修复线路、领取路签，安全带回交付。",
        "S07" => State.World.Contains("robot-rescued") ? "维修偶已获救，安全撤离后在据点交付。" : "在废料场救出维修偶，然后安全撤离。",
        "S08" => "沿已发现的荧光线索探索工坊，带回工具。",
        "S09" => "探索无人车厢，取回居民包裹并安全撤离。",
        _ => "按任务说明准备物品。"
    };
}
