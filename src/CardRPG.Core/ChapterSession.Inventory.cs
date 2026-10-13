namespace CardRPG.Core;

public sealed partial class ChapterSession
{
    public const string InventoryVersion = "chapter1-inventory-v2";
    public const int WeightLimitGrams = 20000;
    public string FailureReason { get; private set; } = "";
    public bool AtBase => State.Phase is ChapterPhase.Camp or ChapterPhase.Extracted or ChapterPhase.Defeated
        || State.Battle?.Id == "trial";
    private static bool IsEquipped(ChapterItem item) => item.Container is "weapon" or "body" or "hands" or "backpack";
    private static bool IsCarried(ChapterItem item) => item.Container == "carry" || IsEquipped(item);
    public int CarryCapacity => Capacity(State.Stock);
    public int CarryUsed => Slots(State.Stock.Where(i => i.Container == "carry"));
    public int WarehouseCapacity => new[] { 48, 72, 96, 128 }[State.WarehouseLevel];
    public int WarehouseUsed => Slots(State.Stock.Where(i => i.Container == "warehouse"));
    public int WarehouseReserved => ReservedSlots(State.Stock);
    public int CarriedGrams => State.Stock.Where(IsCarried).Sum(i => ChapterContent.Items[i.Kind].Grams * i.Quantity);
    public bool Overweight => CarriedGrams > WeightLimitGrams;
    public int SceneActionCost(int normalCost = 1) => normalCost * (Overweight ? 2 : 1);
    public string LoadDescription => $"格位 {CarryUsed}/{CarryCapacity} · 负重 {CarriedGrams / 1000m:0.##}/20 kg"
        + (Overweight ? " · 超重：场景行动值 ×2" : "");
    public IEnumerable<ChapterItem> CarriedItems => State.Stock.Where(i => i.Container == "carry");
    public IEnumerable<ChapterItem> StoredStock => State.Stock.Where(i => i.Container == "warehouse");
    public IEnumerable<KeyValuePair<string, ChapterGround>> GroundHere => State.Ground
        .Where(p => p.Value.Place == State.Location && State.Stock.Any(i => i.Container == p.Key));
    public bool HasPhysical(string kind) => State.Stock.Any(i => i.Kind == kind);
    public bool IsOnPerson(string kind) => State.Stock.Any(i => i.Kind == kind && IsCarried(i));
    public int SafeMaterialCount(string kind) => State.Stock.Where(i => i.Kind == kind && i.Container is "carry" or "warehouse").Sum(i => i.Quantity);

    private static int Capacity(IEnumerable<ChapterItem> stock) => 4 + ChapterContent.BagCapacity(stock.FirstOrDefault(i => i.Container == "backpack")?.Kind ?? "");
    private static int Slots(IEnumerable<ChapterItem> items) => items.GroupBy(i => (i.Kind, i.FreeSupply, i.Calibrated))
        .Sum(g => ((g.Sum(i => i.Quantity) + ChapterContent.Items[g.Key.Kind].Stack - 1) / ChapterContent.Items[g.Key.Kind].Stack) * ChapterContent.Items[g.Key.Kind].Slots);
    private int ReservedSlots(IEnumerable<ChapterItem> stock) => AtBase ? 0 : stock
        .Where(i => State.DepartureInventory.ContainsKey(i.Id)).Sum(i => ChapterContent.Items[i.Kind].Slots);
    private bool Fits(List<ChapterItem> stock, bool permitLegacy = true)
    {
        var carried = Slots(stock.Where(i => i.Container == "carry"));
        var stored = Slots(stock.Where(i => i.Container == "warehouse")) + ReservedSlots(stock);
        return (carried <= Capacity(stock) || permitLegacy && State.LegacyOverCapacity && carried <= CarryUsed)
            && (stored <= WarehouseCapacity || permitLegacy && State.LegacyOverCapacity && stored <= WarehouseUsed + WarehouseReserved);
    }
    private bool Reject(string reason) { FailureReason = reason; return false; }
    private List<ChapterItem> CopyStock() => State.Stock.Select(i => i.Copy()).ToList();
    private ChapterItem AddStock(List<ChapterItem> stock, string kind, int count, string container, string source = "", bool registered = false, bool free = false)
    {
        var item = new ChapterItem { Id = Math.Max(State.NextItemId, stock.Count == 0 ? 1 : stock.Max(i => i.Id) + 1),
            Kind = kind, Quantity = count, Container = container, Source = source, Registered = registered, FreeSupply = free };
        stock.Add(item); return item;
    }
    private void CommitStock(List<ChapterItem> stock)
    {
        State.Stock = stock;
        if (stock.Count > 0) State.NextItemId = Math.Max(State.NextItemId, stock.Max(i => i.Id) + 1);
        SyncInventory(); FailureReason = "";
    }
    private void SyncInventory()
    {
        var stock = State.Stock;
        State.Weapon = stock.FirstOrDefault(i => i.Container == "weapon")?.Kind ?? "";
        State.Body = stock.FirstOrDefault(i => i.Container == "body")?.Kind ?? "";
        State.Hands = stock.FirstOrDefault(i => i.Container == "hands")?.Kind ?? "";
        State.Backpack = stock.FirstOrDefault(i => i.Container == "backpack")?.Kind ?? "";
        State.Owned = stock.Where(i => i.Registered && ChapterContent.Gear.ContainsKey(i.Kind)).Select(i => i.Kind).ToHashSet();
        State.Loot = stock.Where(i => !i.Registered && IsCarried(i) && ChapterContent.Gear.ContainsKey(i.Kind)).Select(i => i.Kind).ToHashSet();
        State.Backups = stock.Where(i => i.Registered && i.Container == "carry" && ChapterContent.Gear.ContainsKey(i.Kind)).Select(i => i.Kind).ToHashSet();
        State.Items = stock.Where(i => i.Container == "carry" && i.Kind.StartsWith('T')).Select(i => i.Kind).ToHashSet();
        State.StoredItems = stock.Where(i => i.Container == "warehouse" && i.Kind.StartsWith('T')).Select(i => i.Kind).ToHashSet();
        State.CarryItems = AtBase ? new(State.Items) : [];
        State.Calibrated = stock.Any(i => i.Kind == "T02" && i.Container == "carry" && i.Calibrated);
        State.StoredCalibration = stock.Any(i => i.Kind == "T02" && i.Container == "warehouse" && i.Calibrated);
        State.Supplies = stock.Where(i => i.Kind == "U01" && i.Container == "carry").Sum(i => i.Quantity);
        State.Bank = ChapterContent.MaterialIds.Select(k => stock.Where(i => i.Kind == k && i.Container == "warehouse").Sum(i => i.Quantity)).ToArray();
        State.Cargo = ChapterContent.MaterialIds.Select(k => stock.Where(i => i.Kind == k && i.Container == "carry").Sum(i => i.Quantity)).ToArray();
        var restored = stock.Where(i => State.DepartureInventory.ContainsKey(i.Id)).Select(i =>
        { var copy = i.Copy(); copy.Container = State.DepartureInventory[i.Id]; return copy; }).ToList();
        if (CarryUsed <= CarryCapacity && WarehouseUsed + WarehouseReserved <= WarehouseCapacity
            && (AtBase || Slots(restored.Where(i => i.Container == "carry")) <= Capacity(restored))) State.LegacyOverCapacity = false;
    }

    public bool CanEquipItem(string kind)
    {
        if (!CanEquip || !ChapterContent.Gear.TryGetValue(kind, out var gear) || gear.Slot == "hands" && !State.Done.Contains("Q02")) return false;
        var item = State.Stock.FirstOrDefault(i => i.Kind == kind && (IsCarried(i) || AtBase && i.Container == "warehouse"));
        if (item == null) return false;
        var stock = CopyStock(); var next = stock.Single(i => i.Id == item.Id);
        var old = stock.FirstOrDefault(i => i.Container == gear.Slot);
        if (old == next)
        {
            if (gear.Slot is not ("hands" or "backpack")) return false;
            next.Container = AtBase ? "warehouse" : "carry";
        }
        else
        {
            if (old != null) old.Container = AtBase && next.Container == "warehouse" ? "warehouse" : "carry";
            next.Container = gear.Slot;
        }
        return Fits(stock);
    }
    private bool EquipInventory(string kind)
    {
        if (!CanEquipItem(kind)) return Reject("无法换装：需在据点或已启用台架，并给旧装备和包内物品留足格位。");
        var stock = CopyStock(); var gear = ChapterContent.Gear[kind];
        var item = stock.First(i => i.Kind == kind && (IsCarried(i) || AtBase && i.Container == "warehouse"));
        var old = stock.FirstOrDefault(i => i.Container == gear.Slot);
        if (old == item) item.Container = AtBase ? "warehouse" : "carry";
        else { if (old != null) old.Container = AtBase && item.Container == "warehouse" ? "warehouse" : "carry"; item.Container = gear.Slot; }
        CommitStock(stock); Note("调整装备：" + gear.Name + "。" + LoadDescription); return true;
    }
    public bool CanTransfer(long itemId, string destination, int quantity)
    {
        if (State.Phase != ChapterPhase.Camp || destination is not ("carry" or "warehouse")) return false;
        var item = State.Stock.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.Container is not ("carry" or "warehouse") || item.Container == destination || item.FreeSupply || quantity < 1 || quantity > item.Quantity) return false;
        var stock = CopyStock(); MoveUnits(stock, itemId, destination, quantity); return Fits(stock);
    }
    private void MoveUnits(List<ChapterItem> stock, long itemId, string destination, int quantity)
    {
        var item = stock.Single(i => i.Id == itemId);
        if (quantity == item.Quantity) item.Container = destination;
        else
        {
            item.Quantity -= quantity;
            var part = item.Copy(); part.Id = Math.Max(State.NextItemId, stock.Max(i => i.Id) + 1);
            part.Quantity = quantity; part.Container = destination; stock.Add(part);
        }
    }
    public bool Transfer(long itemId, string destination, int quantity)
    {
        if (!CanTransfer(itemId, destination, quantity)) return Reject("无法转移：检查格位、物品数量与当前所在位置。");
        var stock = CopyStock(); MoveUnits(stock, itemId, destination, quantity); CommitStock(stock);
        Note("物品已" + (destination == "carry" ? "装入行囊。" : "存入仓库。") + LoadDescription); return true;
    }
    public bool SetFreeSupplies(int amount)
    {
        if (State.Phase != ChapterPhase.Camp || amount is < 0 or > 2) return false;
        State.FreeSupplyAllowance = amount; return true;
    }
    public bool CanDepart
    {
        get
        {
            if (State.Phase != ChapterPhase.Camp || CarryUsed > CarryCapacity || WarehouseUsed > WarehouseCapacity) return false;
            var stock = CopyStock();
            if (State.FreeSupplyAllowance > 0) AddStock(stock, "U01", State.FreeSupplyAllowance, "carry", free: true);
            var reserve = stock.Where(i => IsCarried(i) && i.Registered && ChapterContent.Gear.ContainsKey(i.Kind)).Sum(i => ChapterContent.Items[i.Kind].Slots);
            return Slots(stock.Where(i => i.Container == "carry")) <= Capacity(stock) && WarehouseUsed + reserve <= WarehouseCapacity;
        }
    }
    private void BeginInventoryRun()
    {
        State.Ground.Clear(); State.DepartureInventory.Clear();
        foreach (var item in State.Stock.Where(i => IsCarried(i) && i.Registered && ChapterContent.Gear.ContainsKey(i.Kind)))
            State.DepartureInventory[item.Id] = item.Container;
        if (State.FreeSupplyAllowance > 0) AddStock(State.Stock, "U01", State.FreeSupplyAllowance, "carry", free: true);
        CommitStock(State.Stock);
    }

    private void EndFreePickup()
    { foreach (var ground in State.Ground.Values) ground.FreePickup = false; }
    public void ClosePickup() => EndFreePickup();
    private string SpawnGround(string source, string name, params (string Kind, int Quantity)[] contents)
    {
        var container = "ground:" + State.Location + ":" + source;
        if (State.Ground.ContainsKey(container)) return container;
        State.Ground[container] = new() { Place = State.Location, Name = name, FreePickup = true };
        foreach (var (kind, quantity) in contents)
        {
            if (quantity <= 0 || (ChapterContent.Gear.ContainsKey(kind) || kind.StartsWith('T')) && HasPhysical(kind)) continue;
            AddStock(State.Stock, kind, quantity, container, source);
        }
        CommitStock(State.Stock); Note(name + "已打开，物品留在原位。打开行囊选择拾取。"); return container;
    }
    public bool CanPickup(long itemId, int quantity)
    {
        var item = State.Stock.FirstOrDefault(i => i.Id == itemId);
        if (State.Phase != ChapterPhase.Exploring || item == null || !State.Ground.TryGetValue(item.Container, out var ground)
            || ground.Place != State.Location || quantity < 1 || quantity > item.Quantity) return false;
        var stock = CopyStock(); MoveUnits(stock, itemId, "carry", quantity); return Fits(stock);
    }
    public bool Pickup(long itemId, int quantity)
    {
        if (!CanPickup(itemId, quantity)) return Reject("装不下这批物品，先放下物品或减少拾取数量；余物会保留原位。");
        var item = State.Stock.Single(i => i.Id == itemId); var ground = State.Ground[item.Container];
        var cost = ground.FreePickup ? 0 : SceneActionCost();
        if (cost > 0 && InterruptInventoryAction(cost)) return true;
        var stock = CopyStock(); MoveUnits(stock, itemId, "carry", quantity); CommitStock(stock);
        Note($"拾取 {ChapterContent.ItemName(item.Kind)} ×{quantity}。" + LoadDescription);
        if (cost > 0) { EndFreePickup(); Advance(cost: cost); }
        return true;
    }
    public bool PickupAll(string container)
    {
        if (State.Phase != ChapterPhase.Exploring || !State.Ground.TryGetValue(container, out var ground) || ground.Place != State.Location) return false;
        var stock = CopyStock(); var items = stock.Where(i => i.Container == container).ToArray();
        if (items.Length == 0) return false;
        foreach (var item in items) item.Container = "carry";
        if (!Fits(stock)) return Reject("全部拾取会超过格位上限，请逐项选择；未拾取物保留原位。");
        var cost = ground.FreePickup ? 0 : SceneActionCost();
        if (cost > 0 && InterruptInventoryAction(cost)) return true;
        CommitStock(stock); ground.FreePickup = false;
        Note("已收好这处物品。" + LoadDescription);
        if (cost > 0) { EndFreePickup(); Advance(cost: cost); }
        return true;
    }
    public bool Drop(long itemId, int quantity)
    {
        var item = State.Stock.FirstOrDefault(i => i.Id == itemId);
        if (State.Phase != ChapterPhase.Exploring || item?.Container != "carry" || quantity < 1 || quantity > item.Quantity) return false;
        var cost = SceneActionCost();
        if (InterruptInventoryAction(cost)) return true;
        EndFreePickup();
        var container = "ground:" + State.Location + ":left";
        State.Ground.TryAdd(container, new() { Place = State.Location, Name = "留下的物品" });
        var stock = CopyStock(); MoveUnits(stock, itemId, container, quantity); CommitStock(stock);
        Note("物品留在脚边，本次远征可返回拾取。" + LoadDescription); Advance(cost: cost); return true;
    }
    private bool InterruptInventoryAction(int cost)
    {
        if (!State.Warned) return false;
        EndFreePickup(); Advance(false, cost: cost); StartBattle("K09");
        Note("巡逻打断取放，物品位置与数量保持不变。"); return true;
    }
    public bool DiscardAtBase(long itemId, int quantity)
    {
        var item = State.Stock.FirstOrDefault(i => i.Id == itemId);
        if (State.Phase != ChapterPhase.Camp || item == null || item.Container is not ("carry" or "warehouse")
            || item.Kind.StartsWith('T') || quantity < 1 || quantity > item.Quantity) return false;
        var stock = CopyStock(); var discarded = stock.Single(i => i.Id == itemId);
        discarded.Quantity -= quantity; if (discarded.Quantity == 0) stock.Remove(discarded);
        CommitStock(stock); State.Mods.Remove(item.Kind);
        Note("已放弃 " + ChapterContent.ItemName(item.Kind) + " ×" + quantity + "。"); return true;
    }
    public bool ClaimStarter(string kind)
    {
        if (State.Phase != ChapterPhase.Camp || kind is not ("G01" or "G02" or "G09") || HasPhysical(kind)
            || kind == "G02" && State.Extractions == 0) return false;
        var stock = CopyStock(); AddStock(stock, kind, 1, "warehouse", registered: true);
        if (!Fits(stock)) return Reject("仓库没有空间领取公共装备。");
        CommitStock(stock); return true;
    }

    public int[] BackpackCost(string kind) => kind switch
    {
        "BP01" => [2, 0, 0, 2, 0, 0, 0], "BP02" => [3, 0, 1, 4, 0, 2, 0],
        "BP03" => [4, 0, 2, 4, 0, 3, 3], _ => []
    };
    public bool BackpackRecipeOpen(string kind) => kind switch
    { "BP01" => true, "BP02" => State.Done.Contains("Q02"), "BP03" => SliceComplete, _ => false };
    public bool CanCraftBackpack(string kind) => State.Phase == ChapterPhase.Camp && BackpackRecipeOpen(kind)
        && !HasPhysical(kind) && CanProduce(kind, 1, BackpackCost(kind));
    public bool CraftBackpack(string kind)
    {
        if (!CanCraftBackpack(kind)) return Reject("背包配方未开放、材料不足或仓库无成品空间。");
        Produce(kind, 1, BackpackCost(kind)); Note("制作完成：" + ChapterContent.ItemName(kind)); return true;
    }
    public bool CanCraftSupplies => State.Phase == ChapterPhase.Camp && CanProduce("U01", 2, [0, 0, 0, 1, 1, 0, 0]);
    public bool CraftSupplies()
    {
        if (!CanCraftSupplies) return Reject("需要织物 1、药剂 1，以及成品空间。");
        Produce("U01", 2, [0, 0, 0, 1, 1, 0, 0]); Note("制成两份普通密封补给，可存仓或带入。"); return true;
    }
    public int[] WarehouseCost => State.WarehouseLevel switch
    { 0 => [4, 0, 0, 2, 0, 0, 2], 1 => [5, 0, 2, 0, 0, 3, 3], 2 => [6, 0, 2, 3, 0, 3, 4], _ => [] };
    public bool CanUpgradeWarehouse => State.Phase == ChapterPhase.Camp && State.WarehouseLevel < 3
        && (State.WarehouseLevel == 0 ? State.Done.Contains("Q02") : SliceComplete) && Affordable(WarehouseCost);
    public bool UpgradeWarehouse()
    {
        if (!CanUpgradeWarehouse) return Reject("扩仓需要完成整备任务并备齐材料。");
        var stock = CopyStock(); ConsumeMaterials(stock, WarehouseCost); State.WarehouseLevel++;
        CommitStock(stock); Note($"仓库扩建完成：{WarehouseCapacity} 格。"); return true;
    }
    private bool ConsumeMaterials(List<ChapterItem> stock, int[] cost)
    {
        for (var n = 0; n < cost.Length; n++)
        {
            var remaining = cost[n];
            foreach (var item in stock.Where(i => i.Kind == ChapterContent.MaterialIds[n] && i.Container is "carry" or "warehouse").OrderBy(i => i.Container == "warehouse" ? 0 : 1).ToArray())
            {
                var amount = Math.Min(remaining, item.Quantity); item.Quantity -= amount; remaining -= amount;
                if (item.Quantity == 0) stock.Remove(item);
            }
            if (remaining != 0) return false;
        }
        return true;
    }
    private bool CanProduce(string kind, int count, int[] cost)
    {
        var stock = CopyStock(); if (!ConsumeMaterials(stock, cost)) return false;
        AddStock(stock, kind, count, "warehouse", registered: ChapterContent.Gear.ContainsKey(kind));
        return Fits(stock);
    }
    private void Produce(string kind, int count, int[] cost)
    {
        var stock = CopyStock(); ConsumeMaterials(stock, cost);
        AddStock(stock, kind, count, "warehouse", registered: ChapterContent.Gear.ContainsKey(kind)); CommitStock(stock);
    }
    private void Secure(ChapterItem item, int count)
    {
        if (item.Source == "scrap-first") State.SecuredFirstMaterials[item.Kind] = State.SecuredFirstMaterials.GetValueOrDefault(item.Kind) + count;
        if (item.Source.StartsWith("train-")) State.CrateChoiceBanked = true;
        State.FirstSalvageBanked = State.SecuredFirstMaterials.GetValueOrDefault("M01") >= 4 && State.SecuredFirstMaterials.GetValueOrDefault("M02") >= 2;
    }
    private void UseSupply()
    {
        var item = State.Stock.Where(i => i.Kind == "U01" && i.Container == "carry").OrderByDescending(i => i.FreeSupply).ThenBy(i => i.Id).First();
        Secure(item, 1); if (--item.Quantity == 0) State.Stock.Remove(item);
        State.Health = Math.Min(MaxHealth, State.Health + 14); SyncInventory(); Note("补给恢复 14 生命。");
    }
    private void FinishInventory(bool success)
    {
        if (success)
        {
            foreach (var item in State.Stock.Where(IsCarried))
            { Secure(item, item.Quantity); item.Source = ""; if (ChapterContent.Gear.ContainsKey(item.Kind)) item.Registered = true; }
            foreach (var item in State.Stock.Where(i => State.DepartureInventory.ContainsKey(i.Id) && !IsCarried(i))) item.Container = "warehouse";
        }
        else
        {
            foreach (var item in State.Stock.Where(i => State.DepartureInventory.ContainsKey(i.Id))) item.Container = State.DepartureInventory[item.Id];
        }
        State.Stock.RemoveAll(i => i.FreeSupply || State.Ground.ContainsKey(i.Container)
            || !success && IsCarried(i) && !State.DepartureInventory.ContainsKey(i.Id));
        State.DepartureInventory.Clear(); State.Ground.Clear();
        if (success)
        {
            // Only deposit what fits; remaining safe items retain their actual carrying slots.
            foreach (var item in State.Stock.Where(i => i.Container == "carry").ToArray())
            {
                for (var count = item.Quantity; count > 0; count--)
                {
                    var stock = CopyStock(); MoveUnits(stock, item.Id, "warehouse", count);
                    if (Slots(stock.Where(i => i.Container == "warehouse")) > WarehouseCapacity) continue;
                    CommitStock(stock); break;
                }
            }
        }
        CommitStock(State.Stock);
    }
}
