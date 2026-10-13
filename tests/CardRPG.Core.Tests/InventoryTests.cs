using System.Text.Json;
using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public class InventoryTests
{
    private static ChapterSession Camp()
    {
        // Legacy fixture also exercises material conversion and unique equipment ownership.
        var state = new ChapterState { ContentVersion = "chapter1-outer-ring-v1", Extractions = 1,
            Owned = ChapterContent.Gear.Keys.ToHashSet(), Bank = [20, 20, 20], Cargo = [0, 0, 0],
            Done = ["Q01", "Q02", "S01"], World = ["station"], Supplies = 2 };
        return ChapterSession.FromJson(JsonSerializer.Serialize(state));
    }
    private static ChapterItem Item(ChapterSession g, string kind, string container = "carry") => g.State.Stock.Single(i => i.Kind == kind && i.Container == container);
    private static void Pick(ChapterSession g, string kind, int count)
    {
        var item = g.State.Stock.First(i => i.Kind == kind && g.State.Ground.TryGetValue(i.Container, out var ground) && ground.Place == g.State.Location);
        Assert.True(g.Pickup(item.Id, count));
    }
    private static void Saved(ChapterSession g) => Assert.Equal(g.ToJson(), ChapterSession.FromJson(g.ToJson()).ToJson());

    [Fact]
    public void BarePocketsAndBackpacksShareSlotsAndNeverProvideCards()
    {
        var g = Camp(); Assert.Equal(4, g.CarryCapacity); Assert.Equal(8, g.Deck().Count);
        Assert.True(g.Equip("BP01")); Assert.Equal(12, g.CarryCapacity); Assert.Equal(8, g.Deck().Count);
        Assert.True(g.ToggleBackup("BP02")); Assert.Equal(2, g.CarryUsed);
        Assert.True(g.Equip("BP02")); Assert.Equal(16, g.CarryCapacity); Assert.Equal(2, g.CarryUsed);
        Assert.True(g.Equip("BP02")); Assert.Equal(4, g.CarryCapacity); Assert.Equal(2, g.CarryUsed);
        Saved(g);
    }

    [Fact]
    public void MoreThanTwoBackupsAreAllowedButCapacityAndDepartureSuppliesStillApply()
    {
        var g = Camp(); Assert.True(g.Equip("BP01"));
        foreach (var kind in new[] { "G02", "G03", "G10", "G15", "G25", "G26", "BP02" }) Assert.True(g.ToggleBackup(kind));
        Assert.Equal(7, g.State.Backups.Count); Assert.Equal(12, g.CarryUsed);
        var before = g.ToJson(); Assert.False(g.ToggleBackup("BP03")); Assert.Equal(before, g.ToJson());
        Assert.False(g.CanDepart); Assert.False(g.Depart());
        Assert.True(g.SetFreeSupplies(0)); Assert.True(g.CanDepart); Assert.True(g.Depart()); Saved(g);
    }

    [Fact]
    public void FullPocketsLeaveLootAtItsSourceAndPartialPickupSurvivesRevisitingAndSaving()
    {
        var g = new ChapterSession(); g.Depart(); g.Move("A03");
        Assert.True(g.Act("relay")); Pick(g, "T01", 1);
        Assert.True(g.Act("scrap")); Pick(g, "M01", 2); Pick(g, "M02", 1);
        Assert.Equal(4, g.CarryUsed); var before = g.ToJson();
        Assert.True(g.Act("cloth")); var cloth = g.State.Stock.Single(i => i.Kind == "M07");
        Assert.False(g.Pickup(cloth.Id, 2)); Assert.DoesNotContain("M07", g.CarriedItems.Select(i => i.Kind));
        g.Move("A01"); g.Move("A03"); g = ChapterSession.FromJson(g.ToJson());
        Assert.Equal(2, g.State.Stock.Single(i => i.Id == cloth.Id).Quantity);
        var free = g.State.Stock.Single(i => i.FreeSupply); Assert.True(g.Drop(free.Id, 2));
        var pressure = g.State.Pressure; Assert.True(g.Pickup(cloth.Id, 2)); Assert.Equal(pressure + 1, g.State.Pressure);
        Assert.False(g.Act("relay")); Assert.False(g.Act("scrap")); Saved(g);
    }

    [Fact]
    public void MaterialStackElevenTakesTwoSlotsAndSplittingConservesQuantity()
    {
        var g = Camp(); var steel = Item(g, "M01", "warehouse");
        Assert.True(g.Transfer(steel.Id, "carry", 10)); Assert.Equal(1, g.CarryUsed);
        Assert.True(g.Transfer(steel.Id, "carry", 1)); Assert.Equal(2, g.CarryUsed);
        Assert.Equal(20, g.State.Stock.Where(i => i.Kind == "M01").Sum(i => i.Quantity));
        Assert.Equal(11, g.State.Cargo[0]); Saved(g);
    }

    [Theory]
    [InlineData("move", 2)]
    [InlineData("crate", 4)]
    [InlineData("repair", 2)]
    [InlineData("heal", 2)]
    [InlineData("battle", 2)]
    [InlineData("interrupt", 4)]
    public void OverweightDoublesEverySceneActionIncludingNoiseAndInterruption(string action, int expected)
    {
        var g = Camp(); g.Equip("BP01"); var steel = Item(g, "M01", "warehouse");
        Assert.True(g.Transfer(steel.Id, "carry", 20)); Assert.True(g.Depart()); Assert.True(g.Overweight);
        g.State.Pressure = 0;
        if (action == "move") Assert.True(g.Move("A03"));
        if (action == "crate") { g.State.Location = "A02"; Assert.True(g.Act("crate")); }
        if (action == "repair") { g.State.Location = "A05"; g.State.World.Remove("station"); Assert.True(g.Act("repair")); }
        if (action == "heal") { g.State.Health = 30; Assert.True(g.Act("heal")); }
        if (action == "battle") { g.State.Location = "A03"; Assert.True(g.Act("K02")); }
        if (action == "interrupt") { g.State.Location = "A02"; g.State.PatrolPlace = "A02"; g.State.Warned = true; Assert.True(g.Act("crate")); Assert.DoesNotContain("crate-start", g.State.Run); }
        Assert.Equal(expected, g.State.Pressure); Saved(g);
    }

    [Fact]
    public void WeightIsStrictlyGreaterThanLimitAndAnOverweightDropPaysItsStartingCost()
    {
        var g = Camp(); g.Equip("BP01"); g.SetFreeSupplies(0);
        // Worn hammer + armor + pack = 11 kg, fifteen steel = 9 kg.
        Assert.True(g.Transfer(Item(g, "M01", "warehouse").Id, "carry", 15));
        Assert.Equal(20000, g.CarriedGrams); Assert.False(g.Overweight);
        Assert.True(g.Transfer(Item(g, "M01", "warehouse").Id, "carry", 1)); Assert.True(g.Overweight);
        g.Depart(); var part = g.CarriedItems.Single(i => i.Kind == "M01" && i.Quantity == 1);
        Assert.True(g.Drop(part.Id, 1)); Assert.Equal(2, g.State.Pressure); Assert.False(g.Overweight);
        g.Move("A03"); Assert.Equal(3, g.State.Pressure); Saved(g);
    }

    [Fact]
    public void FreeBrowsingExtractionAndCombatDoNotAcquireWeightSurcharges()
    {
        var g = Camp(); g.Equip("BP01"); g.Transfer(Item(g, "M01", "warehouse").Id, "carry", 20); g.Depart();
        var before = g.ToJson(); _ = g.Actions(); _ = g.LoadDescription; _ = g.NearestExit(); Assert.Equal(before, g.ToJson());
        Assert.Equal(0, g.SceneActionCost(0)); g.Move("A03"); g.Act("K02"); var pressure = g.State.Pressure;
        var b = g.State.Battle!; var card = b.Hand.FindIndex(c => c == "C01"); var points = b.Action;
        Assert.True(g.PlayCard(card, 0)); Assert.Equal(points - 1, b.Action); Assert.Equal(pressure, g.State.Pressure);
        Assert.True(g.EndTurn(true)); g.State.Location = "A01"; Assert.True(g.Act("extract")); Assert.Equal(pressure, g.State.Pressure); Saved(g);
    }

    [Fact]
    public void ChangingToSmallerPackIsAtomicAndOldEquipmentStaysPhysicallyCarried()
    {
        var g = Camp(); g.Equip("BP02");
        foreach (var kind in new[] { "BP01", "G02", "G03", "G10", "G15", "G25", "G26" }) g.ToggleBackup(kind);
        g.Depart(); g.Move("A03"); g.Move("A04"); g.Act("bench");
        Assert.Equal(13, g.CarryUsed); var before = g.ToJson(); Assert.False(g.Equip("BP01")); Assert.Equal(before, g.ToJson());
        Assert.True(g.Drop(Item(g, "G15").Id, 1)); Assert.True(g.Equip("BP01")); Assert.Equal(12, g.CarryUsed);
        Assert.Contains(g.CarriedItems, i => i.Kind == "BP02"); Saved(g);
    }

    [Fact]
    public void DroppedRegisteredGearCannotBeRemotelyEquippedAndReturnsOnFailure()
    {
        var g = Camp(); g.Equip("BP01"); g.ToggleBackup("G02"); g.Depart();
        Assert.True(g.Drop(Item(g, "G02").Id, 1)); g.Move("A03"); g.Move("A04"); g.Act("bench");
        Assert.False(g.Equip("G02"));
        g.Move("A03"); g.Act("K02"); g.State.Health = 1; g.EndTurn();
        Assert.Equal(ChapterPhase.Defeated, g.State.Phase); Assert.Equal("BP01", g.State.Backpack);
        Assert.Contains(g.CarriedItems, i => i.Kind == "G02"); Assert.Empty(g.State.Ground); Assert.DoesNotContain(g.State.Cargo, n => n != 0); Saved(g);
    }

    [Fact]
    public void PickupThatCrossesWeightLimitChangesTheNextActionNotTheFreeInitialPickup()
    {
        var g = Camp(); g.Equip("BP01"); g.Transfer(Item(g,"M01","warehouse").Id,"carry",13); g.Depart();
        g.Move("A03"); g.Act("scrap"); var pressure = g.State.Pressure; Assert.False(g.Overweight);
        Pick(g,"M01",4); Assert.True(g.Overweight); Assert.Equal(pressure,g.State.Pressure);
        g.Move("A01"); Assert.Equal(pressure+2,g.State.Pressure);
    }

    [Fact]
    public void FullWarehouseDoesNotLoseExtractedItemsAndQuestRewardsRequireOutputSpace()
    {
        var g = Camp(); g.Equip("BP01"); g.Depart(); g.Move("A03"); g.Act("cloth");
        g.State.Stock.Single(i => i.Kind == "M07").Quantity = 61;
        Pick(g,"M07",61);
        // Fill only unreserved storage with large material counts; preserve the serialized mirrors.
        var steel = Item(g,"M01","warehouse");
        while (g.WarehouseUsed + g.WarehouseReserved < g.WarehouseCapacity) steel.Quantity += 10;
        g.State.Bank[0] = steel.Quantity;
        g.Move("A01"); g.Act("extract"); Assert.Equal(ChapterPhase.Extracted,g.State.Phase);
        Assert.Equal(61,g.State.Stock.Where(i=>i.Kind=="M07").Sum(i=>i.Quantity));
        Assert.Single(g.CarriedItems); Assert.Equal(1,g.CarriedItems.Single().Quantity);
        Assert.True(g.WarehouseUsed <= g.WarehouseCapacity); Assert.True(g.CarryUsed <= g.CarryCapacity); Saved(g);
        g.ReturnToCamp();
        // Force a genuinely full warehouse to check atomic reward rejection.
        steel = Item(g,"M01","warehouse");
        while (g.WarehouseUsed < g.WarehouseCapacity) steel.Quantity += 10;
        g.State.Bank[0] = steel.Quantity;
        g.State.World.Add("robot-arrived");
        var wire = Item(g,"M02","warehouse"); wire.Quantity = 20; g.State.Bank[1] = 20;
        var before=g.ToJson(); Assert.False(g.TurnIn("S07")); Assert.Equal(before,g.ToJson());
    }

    [Fact]
    public void FirstSourcePartialExtractionOnlySettlesTheQuantitiesActuallyBroughtBack()
    {
        var g=new ChapterSession(); g.Depart(); g.Move("A03"); g.Act("scrap"); Pick(g,"M01",2);
        g.Move("A01"); g.Act("extract"); g.ReturnToCamp();
        Assert.Equal(2,g.State.SecuredFirstMaterials["M01"]); Assert.False(g.State.FirstSalvageBanked);
        g.Depart(); g.Move("A03"); g.Act("scrap");
        Assert.Equal(2,g.State.Stock.Single(i=>i.Kind=="M01" && i.Container.StartsWith("ground:")).Quantity);
        Assert.Equal(2,g.State.Stock.Single(i=>i.Kind=="M02" && i.Container.StartsWith("ground:")).Quantity);
        Saved(g);
    }

    [Fact]
    public void LegacyOverCapacityRetainsAllAssetsAndBlocksAnotherDepartureUntilSorted()
    {
        var state=new ChapterState { ContentVersion="chapter1-outer-ring-v1", Bank=[1000,1000,1000], Cargo=[0,0,0], Supplies=2 };
        var g=ChapterSession.FromJson(JsonSerializer.Serialize(state)); Assert.True(g.State.LegacyOverCapacity);
        Assert.Equal(3000,g.State.Bank.Sum()); Assert.False(g.Depart()); Saved(g);
        foreach(var item in g.StoredStock.ToArray()) Assert.True(g.DiscardAtBase(item.Id,item.Quantity));
        Assert.False(g.State.LegacyOverCapacity); Assert.True(g.Depart()); Saved(g);
    }

    [Fact]
    public void SaveRejectsDuplicateInstancesInvalidContainersNegativeWeightInputsAndUnflaggedOverflow()
    {
        foreach(var mutate in new Action<ChapterState>[]
        {
            s=>s.Stock.Add(s.Stock[0].Copy()), s=>s.Stock[0].Container="unknown",
            s=>s.Stock[0].Quantity=-1, s=>s.Stock[0].Kind="missing",
            s=>s.WarehouseLevel=9, s=>s.DepartureInventory[999]="weapon"
        })
        {
            var g=new ChapterSession(); mutate(g.State);
            Assert.Throws<InvalidDataException>(()=>ChapterSession.FromJson(g.ToJson()));
        }
    }

    [Fact]
    public void LegacyOversizedBackupLayoutRemainsRecoverableAfterDroppingGearAndDefeat()
    {
        var state = new ChapterState { ContentVersion = "chapter1-outer-ring-v1", Phase = ChapterPhase.Exploring,
            Runs = 1, Owned = ["G01", "G02", "G03", "G09", "G10"], Weapon = "G03", Body = "G09",
            Backups = ["G02", "G10"], Bank = [0,0,0], Cargo = [0,0,0], Supplies = 2,
            DepartureGear = new() { ["weapon"]="G01", ["body"]="G09", ["backup1"]="G02", ["backup2"]="G10", ["hands"]="" } };
        // G03 was registered but not the departure weapon; use the old carried layout as-is.
        state.DepartureGear["backup2"] = "G03"; state.Backups = ["G02", "G03"];
        var g = ChapterSession.FromJson(JsonSerializer.Serialize(state));
        Assert.True(g.State.LegacyOverCapacity); Saved(g);
        foreach (var item in g.CarriedItems.Where(i=>i.Registered).ToArray()) Assert.True(g.Drop(item.Id,1));
        g.Move("A03"); g.Act("K02"); g.State.Health = 1; g.EndTurn();
        Saved(g); Assert.Equal(ChapterPhase.Defeated,g.State.Phase);
    }

    [Fact]
    public void NewPackWornInFieldIsLostAndDeparturePackIsRestoredOnDefeat()
    {
        var g = Camp(); g.Equip("BP02");
        var old = Item(g,"BP01","warehouse"); Assert.True(g.DiscardAtBase(old.Id,1));
        g.Depart(); g.Move("A03"); g.Move("A04"); g.Act("bench"); g.Act("bag-cache"); Pick(g,"BP01",1);
        Assert.True(g.Equip("BP01")); Assert.DoesNotContain("BP01",g.State.Owned);
        g.Move("A03"); g.Act("K02"); g.State.Health = 1; g.EndTurn();
        Assert.Equal("BP02",g.State.Backpack); Assert.False(g.HasPhysical("BP01")); Saved(g);
    }

    [Fact]
    public void GroundPickupCannotBeDoneRemotelyAndInterceptionPreservesTheObject()
    {
        var g = new ChapterSession(); g.Depart(); g.Move("A03"); g.Act("cloth");
        var item = g.State.Stock.Single(i=>i.Kind=="M07");
        g.Move("A02"); Assert.False(g.Pickup(item.Id,2)); g.Move("A03");
        g.State.Warned = true; g.State.PatrolPlace = "A03";
        Assert.True(g.Pickup(item.Id,2)); Assert.Equal(ChapterPhase.Battle,g.State.Phase);
        Assert.Equal(item.Container,g.State.Stock.Single(i=>i.Id==item.Id).Container); Assert.DoesNotContain(g.CarriedItems,i=>i.Kind=="M07"); Saved(g);
    }

    [Fact]
    public void UnusedOrdinarySupplyPersistsButFreeSupplyDoesNotAccumulateAcrossRuns()
    {
        var g = new ChapterSession(); g.Depart(); g.Move("A03"); g.Act("supplies"); Pick(g,"U01",1);
        Assert.Equal(3,g.State.Supplies); Assert.Equal(2,g.CarryUsed);
        g.Move("A01"); g.Act("extract"); g.ReturnToCamp();
        Assert.Single(g.State.Stock,i=>i.Kind=="U01"); Assert.DoesNotContain(g.State.Stock,i=>i.FreeSupply);
        var stored=Item(g,"U01","warehouse"); Assert.True(g.Transfer(stored.Id,"carry",1));
        g.Depart(); Assert.Equal(3,g.State.Supplies); g.State.Health=30; g.Act("heal");
        Assert.Equal(1,g.State.Stock.Single(i=>i.FreeSupply).Quantity);
        g.Act("extract"); g.ReturnToCamp(); Assert.Single(g.State.Stock,i=>i.Kind=="U01"); Saved(g);
    }
}
