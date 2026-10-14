namespace CardRPG.Core;

public enum ChapterPhase { Camp, Exploring, Battle, Extracted, Defeated }
public sealed class ChapterItem
{
    public long Id { get; set; }
    public string Kind { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string Container { get; set; } = "warehouse";
    public string Source { get; set; } = "";
    public bool Registered { get; set; }
    public bool Calibrated { get; set; }
    public bool FreeSupply { get; set; }
    public ChapterItem Copy() => (ChapterItem)MemberwiseClone();
}
public sealed class ChapterGround
{
    public string Place { get; set; } = "";
    public string Name { get; set; } = "";
    public bool FreePickup { get; set; }
}
public sealed class ChapterEnemy
{
    public string Id { get; set; } = "E01";
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Shield { get; set; }
    public int Cycle { get; set; }
    public bool Signaled { get; set; }
}
public sealed class ChapterBattle
{
    public string Id { get; set; } = "";
    public string ReturnPlace { get; set; } = "A01";
    public int Round { get; set; }
    public int Action { get; set; }
    public int Mana { get; set; } = 1;
    public int Shield { get; set; }
    public int ExtraDraw { get; set; }
    public bool KeepShield { get; set; }
    public bool ShieldResonated { get; set; }
    public bool BeginPending { get; set; }
    public bool TrialToolUsed { get; set; }
    public bool BonusSecured { get; set; }
    public uint TrialRandomState { get; set; }
    public List<string> Equipment { get; set; } = [];
    public List<ChapterEnemy> Enemies { get; set; } = [];
    public List<string> Hand { get; set; } = [];
    public List<string> Draw { get; set; } = [];
    public List<string> Discard { get; set; } = [];
}
public sealed class ChapterState
{
    public ChapterRunReport? LastReport { get; set; }
    public Dictionary<string, int> DepartureQuantities { get; set; } = [];
    public HashSet<string> DepartureWorld { get; set; } = [];
    public bool HasDepartureReportBaseline { get; set; }
    public string ContentVersion { get; set; } = "chapter1-inventory-v2";
    public List<ChapterItem> Stock { get; set; } =
    [
        new() { Id = 1, Kind = "G01", Container = "weapon", Registered = true },
        new() { Id = 2, Kind = "G09", Container = "body", Registered = true }
    ];
    public long NextItemId { get; set; } = 3;
    public Dictionary<string, ChapterGround> Ground { get; set; } = [];
    public Dictionary<long, string> DepartureInventory { get; set; } = [];
    public Dictionary<string, int> SecuredFirstMaterials { get; set; } = [];
    public int WarehouseLevel { get; set; }
    public int FreeSupplyAllowance { get; set; } = 2;
    public bool LegacyOverCapacity { get; set; }
    public string Backpack { get; set; } = "";
    public ChapterPhase Phase { get; set; }
    public string Location { get; set; } = "A01";
    public string Previous { get; set; } = "A01";
    public string Start { get; set; } = "A01";
    public string Weapon { get; set; } = "G01";
    public string Body { get; set; } = "G09";
    public string Hands { get; set; } = "";
    public int Health { get; set; } = 42;
    public int Supplies { get; set; }
    public int Pressure { get; set; }
    public int Runs { get; set; }
    public int Extractions { get; set; }
    public int[] Bank { get; set; } = new int[7];
    public int[] Cargo { get; set; } = new int[7];
    public uint RandomState { get; set; } = 1701;
    public HashSet<string> Owned { get; set; } = ["G01", "G09"];
    public HashSet<string> Loot { get; set; } = [];
    public HashSet<string> Items { get; set; } = [];
    public HashSet<string> StoredItems { get; set; } = [];
    public HashSet<string> CarryItems { get; set; } = [];
    public HashSet<string> Done { get; set; } = [];
    public HashSet<string> World { get; set; } = [];
    public HashSet<string> Run { get; set; } = [];
    public HashSet<string> Intel { get; set; } = [];
    public HashSet<string> Backups { get; set; } = [];
    public Dictionary<string, string> DepartureMods { get; set; } = [];
    public Dictionary<string, string> DepartureGear { get; set; } = [];
    public HashSet<string> BoughtMods { get; set; } = [];
    public Dictionary<string, string> Mods { get; set; } = [];
    public string Resonance { get; set; } = "";
    public int PatrolEta { get; set; } = -1;
    public string PatrolPlace { get; set; } = "";
    public bool Warned { get; set; }
    public bool FirstSalvageBanked { get; set; }
    public bool CrateChoiceBanked { get; set; }
    public bool CrateSupplyHeld { get; set; }
    public string CrateSelection { get; set; } = "";
    public bool Calibrated { get; set; }
    public bool StoredCalibration { get; set; }
    public ChapterBattle? Battle { get; set; }
    public Dictionary<string, ChapterBattle> Paused { get; set; } = [];
    public string Summary { get; set; } = "";
    public List<string> Log { get; set; } = ["余火站的灯快熄了。先去废料场带回继电器；那里还传来轻轻的敲击声。"];
}
