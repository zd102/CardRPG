namespace CardRPG.Core;

public sealed partial class ChapterSession
{
    public ChapterPreparationPreset CurrentPreparation(string name) => new()
    {
        Name = name,
        Equipment = new[] { "weapon", "body", "hands", "backpack" }.ToDictionary(slot => slot, slot => State.Stock.FirstOrDefault(i => i.Container == slot)?.Kind ?? ""),
        Mods = Equipped.ToDictionary(kind => kind, kind => State.Mods.GetValueOrDefault(kind, "")),
        Carry = CarriedItems.Where(i => !i.FreeSupply).GroupBy(i => i.Kind).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity)),
        FreeSupplies = State.FreeSupplyAllowance
    };
    public bool SavePreparation(string name)
    {
        name = name.Trim();
        if (State.Phase != ChapterPhase.Camp || name.Length is < 1 or > 24 || name.Any(char.IsControl)) return Reject("请在据点输入 1–24 字的预设名称。");
        var old = State.PreparationPresets.FindIndex(p => p.Name == name);
        if (old < 0 && State.PreparationPresets.Count >= 5) return Reject("最多保存 5 个预设，请删除或覆盖一个已有预设。");
        var preset = CurrentPreparation(name);
        if (old < 0) State.PreparationPresets.Add(preset); else State.PreparationPresets[old] = preset;
        return true;
    }
    public bool RemovePreparation(string name) => State.Phase == ChapterPhase.Camp && State.PreparationPresets.RemoveAll(p => p.Name == name) > 0;

    public bool ApplyPreparation(ChapterPreparationPreset preset, bool equipment, bool cargo)
    {
        if (State.Phase != ChapterPhase.Camp) return Reject("整备预设仅在据点使用；野外不能访问仓库。");
        if (!equipment && !cargo || !ValidPreparation(preset)) return Reject("请选择装备或携行范围，并检查预设内容。");
        var stock = CopyStock(); var mods = new Dictionary<string, string>(State.Mods); var missing = new List<string>();
        if (equipment)
        {
            foreach (var (slot, kind) in preset.Equipment)
            {
                var old = stock.FirstOrDefault(i => i.Container == slot);
                if (kind == "") { if (old != null) old.Container = "warehouse"; continue; }
                if (slot == "hands" && !State.Done.Contains("Q02")) { missing.Add("手部尚未解锁"); continue; }
                var item = stock.FirstOrDefault(i => i.Kind == kind && (IsCarried(i) || i.Container == "warehouse"));
                if (item == null) { missing.Add("缺少 " + ChapterContent.ItemName(kind)); continue; }
                if (old != item)
                {
                    if (old != null) old.Container = item.Container == "warehouse" ? "warehouse" : "carry";
                    item.Container = slot;
                }
                var mod = preset.Mods.GetValueOrDefault(kind, "");
                if (mod != "" && !State.BoughtMods.Contains(kind + ":" + mod)) missing.Add(ChapterContent.ItemName(kind) + " 尚未购买预设中的改装");
                else mods[kind] = mod;
            }
        }
        if (cargo)
        {
            foreach (var (kind, target) in preset.Carry)
            {
                var needed = Math.Max(0, target - stock.Where(i => i.Kind == kind && i.Container == "carry" && !i.FreeSupply).Sum(i => i.Quantity));
                foreach (var item in stock.Where(i => i.Kind == kind && i.Container == "warehouse" && !i.FreeSupply).OrderBy(i => i.Id).ToArray())
                {
                    var amount = Math.Min(needed, item.Quantity); if (amount == 0) break;
                    MoveUnits(stock, item.Id, "carry", amount); needed -= amount;
                }
                if (needed > 0) missing.Add(ChapterContent.ItemName(kind) + " 仍缺 " + needed);
            }
        }
        if (missing.Count > 0) return Reject(string.Join("；", missing) + "。预设未应用，当前物品保持原位。");
        if (!Fits(stock)) return Reject("预设应用后的随身或仓库容量不足；本次没有部分换装或取物。");
        var preview = PreviewCopy(); preview.CommitStock(stock); preview.State.Mods = mods;
        if (cargo) preview.State.FreeSupplyAllowance = preset.FreeSupplies;
        if (!preview.CanDepart) return Reject("预设应用后无法容纳出发配发或返还预留，请先调整携行数量或仓库空间。");
        CommitStock(stock); State.Mods = mods;
        if (cargo) State.FreeSupplyAllowance = preset.FreeSupplies;
        NoteStock("已应用整备预设：" + preset.Name + (equipment && cargo ? "（装备与携行）" : equipment ? "（仅装备）" : "（仅补齐携行）")); return true;
    }

    private static bool ValidPreparation(ChapterPreparationPreset p) => p != null && p.Name != null && p.Name.Length <= 24 && !p.Name.Any(char.IsControl)
        && p.Equipment != null && p.Equipment.Count == 4 && new[] { "weapon", "body", "hands", "backpack" }.All(p.Equipment.ContainsKey)
        && p.Equipment.All(pair => pair.Value != null && (pair.Value == "" ? pair.Key is "hands" or "backpack" : ChapterContent.Gear.TryGetValue(pair.Value, out var gear) && gear.Slot == pair.Key))
        && p.Mods != null && p.Mods.All(pair => ChapterContent.Gear.ContainsKey(pair.Key) && (pair.Value == "" || CompatibleMod(pair.Key, pair.Value)))
        && p.Carry != null && p.Carry.Count <= ChapterContent.Items.Count && p.Carry.All(pair => ChapterContent.Items.ContainsKey(pair.Key) && pair.Value is >= 1 and <= 100000)
        && p.FreeSupplies is >= 0 and <= 2;
}
