namespace CardRPG.Core;

public sealed partial class ChapterSession
{
    private bool ValidInventory()
    {
        var s = State;
        if (s.PreparationPresets == null || s.PreparationPresets.Count > 5 || s.PreparationPresets.Any(p => !ValidPreparation(p))
            || s.PreparationPresets.Select(p => p.Name).Distinct().Count() != s.PreparationPresets.Count || s.LastPreparation != null && !ValidPreparation(s.LastPreparation)) return false;
        if (s.RecipePlan == null || s.RecipePlan.Count > 4 || s.RecipePlan.Any(k => k == null || !ChapterContent.Recipes.ContainsKey(k) && k is not ("BP01" or "BP02" or "BP03" or "U01"))
            || s.TrackedQuest == null || s.TrackedQuest != "" && !ChapterContent.Quests.Any(q => q.Id == s.TrackedQuest)) return false;
        if (s.CarryQuotas == null || s.CarryQuotas.Any(p => !ChapterContent.Items.ContainsKey(p.Key) || p.Value is < 0 or > 100000)) return false;
        if (s.Stock == null || s.Ground == null || s.DepartureInventory == null || s.SecuredFirstMaterials == null
            || s.WarehouseLevel is < 0 or > 3 || s.FreeSupplyAllowance is < 0 or > 2 || s.NextItemId < 1
            || s.Stock.Count > 10000 || s.Stock.Any(i => i == null || i.Id <= 0 || i.Id >= s.NextItemId
                || i.Kind == null || !ChapterContent.Items.ContainsKey(i.Kind) || i.Quantity is < 1 or > 100000
                || i.Source == null || i.Container == null || i.Calibrated && i.Kind != "T02"
                || i.FreeSupply && (i.Kind != "U01" || i.Container == "warehouse")
                || i.Registered && !ChapterContent.Gear.ContainsKey(i.Kind)
                || (ChapterContent.Gear.ContainsKey(i.Kind) || i.Kind.StartsWith('T')) && i.Quantity != 1)) return false;
        if (s.Stock.Select(i => i.Id).Distinct().Count() != s.Stock.Count
            || s.Stock.Where(i => ChapterContent.Gear.ContainsKey(i.Kind) || i.Kind.StartsWith('T')).GroupBy(i => i.Kind).Any(g => g.Count() > 1)
            || s.Stock.Where(IsEquipped).GroupBy(i => i.Container).Any(g => g.Count() > 1)
            || s.Stock.Any(i => IsEquipped(i) && (!ChapterContent.Gear.TryGetValue(i.Kind, out var gear) || gear.Slot != i.Container))
            || s.Stock.Any(i => i.Container is not ("carry" or "warehouse") && !IsEquipped(i) && !s.Ground.ContainsKey(i.Container))
            || s.Ground.Any(p => !p.Key.StartsWith("ground:") || p.Value == null || !ChapterContent.Places.ContainsKey(p.Value.Place) || p.Value.Name == null)
            || s.DepartureInventory.Any(p => !s.Stock.Any(i => i.Id == p.Key && i.Registered)
                || p.Value is not ("carry" or "weapon" or "body" or "hands" or "backpack"))
            || s.SecuredFirstMaterials.Any(p => p.Key is not ("M01" or "M02") || p.Value < 0 || p.Value > (p.Key == "M01" ? 4 : 2))) return false;
        if (s.Stock.Where(IsCarried).Sum(i => (long)ChapterContent.Items[i.Kind].Grams * i.Quantity) > int.MaxValue) return false;
        if (!s.LegacyOverCapacity && !Fits(s.Stock, false)) return false;
        if (AtBase && s.Battle?.Id != "trial" && (s.DepartureInventory.Count > 0 || s.Ground.Count > 0 || s.Stock.Any(i => i.FreeSupply))) return false;
        if (!s.Owned.SetEquals(s.Stock.Where(i => i.Registered).Select(i => i.Kind))
            || !s.Loot.SetEquals(s.Stock.Where(i => !i.Registered && IsCarried(i) && ChapterContent.Gear.ContainsKey(i.Kind)).Select(i => i.Kind))
            || !s.Backups.SetEquals(s.Stock.Where(i => i.Registered && i.Container == "carry").Select(i => i.Kind))
            || s.Weapon != s.Stock.FirstOrDefault(i => i.Container == "weapon")?.Kind
            || s.Body != s.Stock.FirstOrDefault(i => i.Container == "body")?.Kind
            || s.Hands != (s.Stock.FirstOrDefault(i => i.Container == "hands")?.Kind ?? "")
            || s.Backpack != (s.Stock.FirstOrDefault(i => i.Container == "backpack")?.Kind ?? "")) return false;
        var carriedTasks = s.Stock.Where(i => i.Container == "carry" && i.Kind.StartsWith('T')).Select(i => i.Kind);
        var storedTasks = s.Stock.Where(i => i.Container == "warehouse" && i.Kind.StartsWith('T')).Select(i => i.Kind);
        if (!s.Items.SetEquals(carriedTasks) || !s.StoredItems.SetEquals(storedTasks)
            || s.Supplies != s.Stock.Where(i => i.Kind == "U01" && i.Container == "carry").Sum(i => i.Quantity)
            || s.Calibrated != s.Stock.Any(i => i.Kind == "T02" && i.Container == "carry" && i.Calibrated)
            || s.StoredCalibration != s.Stock.Any(i => i.Kind == "T02" && i.Container == "warehouse" && i.Calibrated)) return false;
        return s.Bank.SequenceEqual(ChapterContent.MaterialIds.Select(k => s.Stock.Where(i => i.Kind == k && i.Container == "warehouse").Sum(i => i.Quantity)))
            && s.Cargo.SequenceEqual(ChapterContent.MaterialIds.Select(k => s.Stock.Where(i => i.Kind == k && i.Container == "carry").Sum(i => i.Quantity)));
    }

    private static void MigrateLegacy(ChapterState s)
    {
        if (s.Bank.Length != 3 || s.Cargo.Length != 3 || s.Bank.Concat(s.Cargo).Any(n => n < 0)
            || s.Owned.Concat(s.Loot).Any(k => !ChapterContent.Gear.ContainsKey(k)) || s.Owned.Overlaps(s.Loot)
            || s.Items.Overlaps(s.StoredItems) || s.Backups.Except(s.Owned).Any() || s.Backups.Count > 2
            || s.Supplies is < 0 or > 3 || s.Items.Concat(s.StoredItems).Any(k => k is not ("T01" or "T02" or "T11" or "T14"))
            || s.CarryItems.Except(s.StoredItems).Any()
            || !ChapterContent.Gear.TryGetValue(s.Weapon, out var weapon) || weapon.Slot != "weapon"
            || !ChapterContent.Gear.TryGetValue(s.Body, out var body) || body.Slot != "body"
            || s.Hands != "" && (!ChapterContent.Gear.TryGetValue(s.Hands, out var hands) || hands.Slot != "hands"))
            throw new InvalidDataException("旧档物品数据无效，无法迁移。");
        var session = new ChapterSession(s); var stock = new List<ChapterItem>(); s.NextItemId = 1;
        s.Ground = []; s.DepartureInventory = []; s.SecuredFirstMaterials = [];
        var basePhase = session.AtBase;
        foreach (var kind in s.Owned.Concat(s.Loot).Order())
        {
            var container = kind == s.Weapon ? "weapon" : kind == s.Body ? "body" : kind == s.Hands ? "hands"
                : s.Backups.Contains(kind) || s.Loot.Contains(kind) || !basePhase && s.DepartureGear.ContainsValue(kind) ? "carry" : "warehouse";
            var item = session.AddStock(stock, kind, 1, container, registered: s.Owned.Contains(kind));
            if (!basePhase && item.Registered && (IsCarried(item) || s.DepartureGear.ContainsValue(kind)))
            {
                var original = s.DepartureGear.FirstOrDefault(p => p.Value == kind).Key;
                s.DepartureInventory[item.Id] = original is "weapon" or "body" or "hands" ? original : "carry";
            }
        }
        foreach (var kind in s.StoredItems.Concat(s.Items).Order())
        {
            var container = s.Items.Contains(kind) || basePhase && s.CarryItems.Contains(kind) ? "carry" : "warehouse";
            var item = session.AddStock(stock, kind, 1, container);
            item.Calibrated = kind == "T02" && (s.Items.Contains(kind) ? s.Calibrated : s.StoredCalibration);
        }
        for (var n = 0; n < 3; n++)
        {
            if (s.Bank[n] > 0) session.AddStock(stock, ChapterContent.MaterialIds[n], s.Bank[n], "warehouse");
            if (s.Cargo[n] > 0) session.AddStock(stock, ChapterContent.MaterialIds[n], s.Cargo[n], "carry");
        }
        if (!basePhase && s.Supplies > 0)
        {
            var train = s.CrateSupplyHeld ? 1 : 0;
            if (train > 0) session.AddStock(stock, "U01", 1, "carry", "train-supply");
            if (s.Supplies > train) session.AddStock(stock, "U01", s.Supplies - train, "carry", free: true);
        }
        if (s.FirstSalvageBanked || !basePhase && s.Run.Contains("scrap"))
        { s.SecuredFirstMaterials["M01"] = 4; s.SecuredFirstMaterials["M02"] = 2; }
        s.ContentVersion = InventoryVersion; s.LegacyOverCapacity = true;
        s.Backpack = ""; s.WarehouseLevel = 0; s.FreeSupplyAllowance = 2;
        session.CommitStock(stock);
        if (basePhase) s.DepartureGear.Clear();
    }
}
