using System.Text.Json;

namespace CardRPG.Core;

public sealed partial class ChapterSession
{
    public const int MaxHealth = 42;
    public ChapterState State { get; }
    public ChapterSession(ChapterState? state = null) => State = state ?? new();
    public bool SliceComplete => State.Done.Contains("Q02") && State.Done.Contains("S01");
    public bool CanEquip => State.Phase == ChapterPhase.Camp ||
        State.Phase == ChapterPhase.Exploring && State.Location == "A04" && State.Run.Contains("bench");
    public IEnumerable<string> Equipped => new[] { State.Weapon, State.Body, State.Hands }.Where(x => x != "");
    public string ToJson() => JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true });

    public static ChapterSession FromJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("ContentVersion", out var version)
                || version.GetString() != "chapter1-outer-ring-v1")
                throw new InvalidDataException("这不是受支持的第一章存档，旧 Demo 存档需单独保留。");
            var s = JsonSerializer.Deserialize<ChapterState>(json) ?? throw new InvalidDataException("存档为空。");
            var session = new ChapterSession(s);
            if (!session.ValidState()) throw new InvalidDataException("章节存档版本不符或数据损坏。");
            return session;
        }
        catch (Exception e) when (e is JsonException or NullReferenceException or ArgumentException or InvalidOperationException)
        { throw new InvalidDataException("章节存档数据不完整。", e); }
    }

    private bool ValidState()
    {
        var s = State;
        if (s.ContentVersion != "chapter1-outer-ring-v1" || !Enum.IsDefined(s.Phase) || s.Health is < 0 or > MaxHealth
            || s.Supplies is < 0 or > 3 || s.Pressure < 0 || s.RandomState == 0 || s.Runs < 0 || s.Extractions < 0
            || !ChapterContent.Places.ContainsKey(s.Location) || !ChapterContent.Places.ContainsKey(s.Previous)
            || s.Start is not ("A01" or "A05") || s.Start == "A05" && !s.Done.Contains("S01")
            || s.Bank.Length != 3 || s.Cargo.Length != 3 || s.Bank.Concat(s.Cargo).Any(x => x < 0)
            || s.PatrolEta < -1 || s.PatrolPlace is not ("" or "A02" or "A03")
            || s.Resonance is not ("" or "R01" or "R07")
            || s.CrateSelection is not ("" or "wire" or "supply" or "parcel")) return false;
        if (s.Owned.Concat(s.Loot).Concat(s.Backups).Any(g => !ChapterContent.Gear.ContainsKey(g))
            || s.Backups.Count > 2 || s.Backups.Except(s.Owned).Any()
            || s.Items.Concat(s.StoredItems).Any(i => i is not ("T01" or "T02" or "T11" or "T14"))
            || s.CarryItems.Any(i => i != "T02" || !s.StoredItems.Contains(i) || s.StoredCalibration)
            || s.Items.Intersect(s.StoredItems).Any() || s.Owned.Overlaps(s.Loot)
            || !IsSlot(s.Weapon, "weapon") || !IsSlot(s.Body, "body") || s.Hands != "" && !IsSlot(s.Hands, "hands")
            || Equipped.Any(g => !s.Owned.Contains(g) && !s.Loot.Contains(g))
            || s.Hands != "" && !s.Done.Contains("Q02")) return false;
        if (s.Mods.Any(p => !s.Owned.Contains(p.Key) || p.Value != "" && (!CompatibleMod(p.Key, p.Value) || !s.BoughtMods.Contains(p.Key + ":" + p.Value)))
            || s.DepartureMods.Any(p => !ChapterContent.Gear.ContainsKey(p.Key) || p.Value != "" && !CompatibleMod(p.Key, p.Value))) return false;
        if (s.World == null || s.Run == null || s.Intel == null || s.Log == null || s.Done == null || s.Paused == null
            || s.DepartureGear == null || s.BoughtMods == null || s.Summary == null) return false;
        if (s.DepartureGear.Any(p => p.Value != "" && !s.Owned.Contains(p.Value))
            || s.DepartureGear.TryGetValue("weapon", out var weapon) && !IsSlot(weapon, "weapon")
            || s.DepartureGear.TryGetValue("body", out var body) && !IsSlot(body, "body")
            || s.DepartureGear.TryGetValue("hands", out var hands) && hands != "" && !IsSlot(hands, "hands")) return false;
        if (s.Phase == ChapterPhase.Battle != (s.Battle != null)) return false;
        if (s.Battle != null && (!ValidBattle(s.Battle) || s.Battle.BeginPending)) return false;
        if (s.Paused.Any(p => p.Key != p.Value.Id || !ValidBattle(p.Value) || !p.Value.BeginPending || p.Key == "trial")) return false;
        return true;
    }
    private static bool ValidBattle(ChapterBattle b) => b.Id is "K01" or "K02" or "K09" or "E13" or "trial"
        && ChapterContent.Places.ContainsKey(b.ReturnPlace) && b.Round > 0 && b.Action is >= 0 and <= 3 && b.Mana is >= 0 and <= 3
        && b.Shield >= 0 && b.ExtraDraw is >= 0 and <= 2 && b.Enemies.Count is >= 1 and <= 3
        && b.Enemies.All(e => e.Id is "E01" or "E02" or "E03" or "E13" or "dummy"
            && e.MaxHealth > 0 && e.Health >= 0 && e.Health <= e.MaxHealth && e.Shield is >= 0 and <= 20 && e.Cycle is >= 0 and <= 2)
        && b.Hand.Concat(b.Draw).Concat(b.Discard).All(ChapterContent.Cards.ContainsKey)
        && b.Equipment.All(ChapterContent.Gear.ContainsKey) && b.Hand.Count + b.Draw.Count + b.Discard.Count is 8 or 10;

    private bool IsSlot(string id, string slot) => ChapterContent.Gear.TryGetValue(id, out var gear) && gear.Slot == slot;
    public string[] GearCards(string id)
    {
        var cards = ChapterContent.Gear[id].Cards.ToArray();
        var mods = State.Phase == ChapterPhase.Camp ? State.Mods : State.DepartureMods;
        if (!mods.TryGetValue(id, out var mod) || mod == "") return cards;
        var from = mod == "D01" ? "C01" : "C03";
        var index = Array.IndexOf(cards, from);
        if (index >= 0) cards[index] = mod == "D01" ? "C19" : "C26";
        return cards;
    }
    public List<string> Deck() => Equipped.SelectMany(GearCards).ToList();
    public string CardList(string gear) => string.Join("、", GearCards(gear).GroupBy(c => c).Select(g => ChapterContent.Cards[g.Key].Name + " ×" + g.Count()));
    public bool SelectStarter(string id)
    {
        if (State.Phase != ChapterPhase.Camp || State.Extractions != 0 || id is not ("G01" or "G02")) return false;
        State.Owned.Remove(id == "G01" ? "G02" : "G01");
        State.Owned.Add(id); State.Weapon = id; State.Backups.RemoveWhere(x => !State.Owned.Contains(x));
        State.DepartureGear.Clear(); State.DepartureMods.Clear();
        Note("出征工具：" + ChapterContent.Gear[id].Name); return true;
    }
    public bool Equip(string id)
    {
        if (!CanEquip || !ChapterContent.Gear.TryGetValue(id, out var g) || !State.Owned.Contains(id) && !State.Loot.Contains(id)) return false;
        if (State.Phase != ChapterPhase.Camp && !Equipped.Contains(id) && !State.Backups.Contains(id) && !State.Loot.Contains(id) && !State.DepartureGear.ContainsValue(id)) return false;
        if (g.Slot == "hands" && !State.Done.Contains("Q02")) return false;
        switch (g.Slot)
        {
            case "weapon": State.Weapon = id; break;
            case "body": State.Body = id; break;
            case "hands": State.Hands = State.Hands == id ? "" : id; break;
        }
        State.Backups.RemoveWhere(x => Equipped.Contains(x));
        Note("调整装备：" + g.Name + $"。当前牌组 {Deck().Count} 张。"); return true;
    }
    public bool ToggleBackup(string id)
    {
        if (State.Phase != ChapterPhase.Camp || !State.Owned.Contains(id) || Equipped.Contains(id)) return false;
        if (State.Backups.Remove(id)) return true;
        if (State.Backups.Count == 2) return false;
        State.Backups.Add(id); return true;
    }
    public bool SetStart(string id)
    {
        if (State.Phase != ChapterPhase.Camp || id != "A01" && !(id == "A05" && State.Done.Contains("S01"))) return false;
        State.Start = id; return true;
    }
    public bool ToggleCarryItem(string id)
    {
        if (State.Phase != ChapterPhase.Camp || id != "T02" || !State.StoredItems.Contains(id) || State.StoredCalibration) return false;
        if (!State.CarryItems.Remove(id)) State.CarryItems.Add(id);
        return true;
    }
    public bool RecipeOpen(string id) => ChapterContent.Recipes.ContainsKey(id) && State.Done.Contains("Q02")
        && (id != "G26" || State.World.Contains("trial-complete"));
    public bool CanCraft(string id) => State.Phase == ChapterPhase.Camp && RecipeOpen(id) && !State.Owned.Contains(id)
        && Affordable(Cost(id));
    private static int[] Cost(string id)
    { var r = ChapterContent.Recipes[id]; return [r.Steel, r.Wire, r.Spring]; }
    public bool Craft(string id)
    {
        if (!CanCraft(id)) return false;
        Pay(Cost(id)); State.Owned.Add(id); Note("制作完成：" + ChapterContent.Gear[id].Name); return true;
    }
    public static bool CompatibleMod(string gear, string mod) => mod switch
    { "D01" => gear is "G01" or "G03", "D08" => gear is "G09" or "G10", _ => false };
    public bool BuyMod(string gear, string mod)
    {
        if (State.Phase != ChapterPhase.Camp || !State.Done.Contains("Q02") || !State.Owned.Contains(gear)
            || !CompatibleMod(gear, mod) || State.BoughtMods.Contains(gear + ":" + mod) || !Affordable([2, 0, 1])) return false;
        Pay([2, 0, 1]); State.BoughtMods.Add(gear + ":" + mod); State.Mods[gear] = mod;
        Note("改装完成：" + ChapterContent.Gear[gear].Name + "。替换一张牌，牌数不变。"); return true;
    }
    public bool SetMod(string gear, string mod)
    {
        if (State.Phase != ChapterPhase.Camp || !State.Owned.Contains(gear)
            || mod != "" && !State.BoughtMods.Contains(gear + ":" + mod)) return false;
        State.Mods[gear] = mod; return true;
    }
    private bool Affordable(int[] cost) => cost.Select((x, i) => State.Bank[i] >= x).All(x => x);
    private void Pay(int[] cost) { for (var i = 0; i < 3; i++) State.Bank[i] -= cost[i]; }
    private static void Add(int[] destination, int steel, int wire, int spring)
    { destination[0] += steel; destination[1] += wire; destination[2] += spring; }
    private void Note(string text) { State.Log.Add(text); if (State.Log.Count > 100) State.Log.RemoveAt(0); }
    private int Next(int max)
    {
        var n = State.RandomState; n ^= n << 13; n ^= n >> 17; n ^= n << 5; State.RandomState = n;
        return (int)(n % (uint)max);
    }
    private void Shuffle(List<string> cards)
    { for (var i = cards.Count - 1; i > 0; i--) { var j = Next(i + 1); (cards[i], cards[j]) = (cards[j], cards[i]); } }
}
