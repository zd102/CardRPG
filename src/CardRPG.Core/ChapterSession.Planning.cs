namespace CardRPG.Core;

public sealed record MaterialBudget(string Kind, int Required, int Warehouse, int Carry, int FromWarehouse, int FromCarry, int ProtectedConsumption)
{
    public int Missing => Math.Max(0, Required - Warehouse - Carry);
    public int Remaining => Math.Max(0, Warehouse + Carry - Required);
}

public sealed partial class ChapterSession
{
    public IReadOnlyList<MaterialBudget> MaterialBudgetFor(int[] cost)
    {
        var result = new List<MaterialBudget>();
        for (var index = 0; index < cost.Length; index++)
        {
            if (cost[index] == 0) continue;
            var kind = ChapterContent.MaterialIds[index];
            var store = StoredStock.Where(i => i.Kind == kind).Sum(i => i.Quantity);
            var carry = CarriedItems.Where(i => i.Kind == kind).Sum(i => i.Quantity);
            var fromStore = Math.Min(cost[index], store); var fromCarry = Math.Min(cost[index] - fromStore, carry);
            var remaining = cost[index]; var protectedCount = 0;
            foreach (var item in State.Stock.Where(i => i.Kind == kind && i.Container is "carry" or "warehouse").OrderBy(i => i.Container == "warehouse" ? 0 : 1))
            { var take = Math.Min(remaining, item.Quantity); if (item.KeepOnPerson) protectedCount += take; remaining -= take; }
            result.Add(new(kind, cost[index], store, carry, fromStore, fromCarry, protectedCount));
        }
        return result;
    }

    public int[] RecipeCost(string kind) => kind.StartsWith("BP") ? BackpackCost(kind) : kind == "U01" ? [0, 0, 0, 1, 1, 0, 0] : ChapterContent.Recipes.ContainsKey(kind) ? Cost(kind) : [];

    public string[] CraftBlockers(string kind)
    {
        if (kind != "U01" && HasPhysical(kind)) return ["已持有此实物，无需重复制作"];
        var reasons = new List<string>();
        if (State.Phase != ChapterPhase.Camp) reasons.Add("回到据点后制作");
        if (kind != "U01" && !(kind.StartsWith("BP") ? BackpackRecipeOpen(kind) : RecipeOpen(kind)))
            reasons.Add(kind == "G26" ? "完成台架试用：预备工具后攻击" : kind == "BP03" ? "完成外环主线和货站任务" : "完成任务：有自己的工具");
        var cost = RecipeCost(kind);
        reasons.AddRange(MaterialBudgetFor(cost).Where(m => m.Missing > 0).Select(m => ChapterContent.ItemName(m.Kind) + "缺 " + m.Missing));
        if (cost.Length > 0)
        {
            var copy = CopyStock(); ConsumeMaterials(copy, cost); AddStock(copy, kind, kind == "U01" ? 2 : 1, "warehouse");
            if (!Fits(copy)) reasons.Add("仓库需腾出成品空间");
        }
        return reasons.ToArray();
    }

    public bool CraftSupplies(int times)
    {
        if (State.Phase != ChapterPhase.Camp || times is < 1 or > 50000) return Reject("制作次数无效或当前不在据点。");
        int[] cost = [0, 0, 0, times, times, 0, 0];
        if (!CanProduce("U01", times * 2, cost)) return Reject("材料或成品空间不足；本次未制作任何补给。");
        Produce("U01", times * 2, cost); NoteStock($"制作补给 {times} 次，获得普通密封补给 {times * 2} 份。"); return true;
    }

    public IReadOnlyDictionary<string, int> PlannedMaterialCost(IEnumerable<string> recipes)
    {
        var cost = new Dictionary<string, int>();
        foreach (var kind in recipes.Distinct())
        {
            var amounts = RecipeCost(kind);
            for (var index = 0; index < amounts.Length; index++) if (amounts[index] > 0) cost[ChapterContent.MaterialIds[index]] = cost.GetValueOrDefault(ChapterContent.MaterialIds[index]) + amounts[index];
        }
        return cost;
    }

    public IReadOnlyList<string> KnownMaterialSources(string kind)
    {
        string[] places = kind switch { "M01" => ["A03", "A02"], "M02" => ["A03", "A05", "A07"], "M03" => ["A02"], "M07" => ["A03", "A02"], "M08" => ["A05"], "M09" => ["A02"], "M10" => ["A05"], _ => [] };
        return places.Where(IsRevealed).ToArray();
    }
}
