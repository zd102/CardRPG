using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public class ChapterTests
{
    private static void Move(ChapterSession g, params string[] places)
    { foreach (var p in places) Assert.True(g.Move(p), "Move " + p); }
    private static void Act(ChapterSession g, params string[] actions)
    {
        foreach (var a in actions)
        {
            Assert.True(g.Act(a), "Act " + a);
            Collect(g);
        }
    }
    internal static void Collect(ChapterSession g)
    {
        foreach (var item in g.State.Stock.Where(i => g.State.Ground.TryGetValue(i.Container, out var ground) && ground.Place == g.State.Location && ground.FreePickup).ToArray())
            if (g.CanPickup(item.Id, item.Quantity)) Assert.True(g.Pickup(item.Id, item.Quantity));
    }
    public static ChapterSession CompleteOuterRing()
    {
        var g = new ChapterSession(); Assert.True(g.Depart()); Move(g, "A03");
        Act(g, "rescue", "relay", "cloth", "scrap"); Move(g, "A01"); Act(g, "extract");
        Assert.True(g.ReturnToCamp()); Assert.True(g.TurnIn("Q01")); Assert.True(g.TurnIn("S07"));
        Assert.True(g.CraftBackpack("BP01")); Assert.True(g.Equip("BP01")); Assert.True(g.Equip("G10"));
        Assert.True(g.Depart()); Move(g, "A03"); Act(g, "scrap"); Move(g, "A04"); Act(g, "bench", "dark");
        Move(g, "A06"); Act(g, "shovel"); Move(g, "A04"); Assert.True(g.Equip("G25"));
        Move(g, "A02"); Act(g, "crate", "crate", "clamp"); Move(g, "A04"); Act(g, "calibrate");
        Move(g, "A05"); Act(g, "repair", "sign", "wire", "extract");
        Assert.True(g.ReturnToCamp()); Assert.True(g.TurnIn("Q02")); Assert.True(g.TurnIn("S01")); Assert.True(g.TurnIn("S08"));
        return g;
    }

    [Fact]
    public void TwoExpeditionsCompleteMainTasksAndPersistentDiscoveries()
    {
        var g = CompleteOuterRing();
        Assert.True(g.SliceComplete); Assert.Equal(2, g.State.Extractions);
        Assert.Contains("G15", g.State.Owned); Assert.Contains("robot-arrived", g.State.World);
        Assert.True(g.IsRevealed("A06")); Assert.True(g.IsRevealed("A07"));
        Assert.Empty(g.State.StoredItems); Assert.False(g.TurnIn("Q01"));
        Assert.True(g.SetStart("A05")); Assert.True(g.Depart()); Assert.Equal("A05", g.State.Location);
        Assert.Equal(("A05", 0), g.NearestExit()); Assert.Equal(ChapterPhase.Exploring, ChapterSession.FromJson(g.ToJson()).State.Phase);
    }

    [Fact]
    public void TrialIsIsolatedAndCraftingIsAtomicAndOptional()
    {
        var g = CompleteOuterRing(); var health = g.State.Health; var rng = g.State.RandomState;
        var equipped = g.Equipped.ToArray(); var owned = g.State.Owned.Order().ToArray();
        Assert.False(g.Craft("G26")); Assert.True(g.StartTrial());
        g = ChapterSession.FromJson(g.ToJson()); Assert.True(g.PlayCard(0, 0)); Assert.True(g.PlayCard(0, 0));
        Assert.Contains("trial-complete", g.State.World); Assert.DoesNotContain("S10", g.State.Done);
        Assert.True(g.EndTrial()); Assert.Equal(health, g.State.Health); Assert.Equal(rng, g.State.RandomState);
        Assert.Equal(equipped, g.Equipped); Assert.Equal(owned, g.State.Owned.Order());
        var before = g.State.Bank.ToArray(); Assert.True(g.Craft("G26"));
        Assert.Equal(new[] { before[0] - 2, before[1] - 2, before[2] - 1 }, g.State.Bank.Take(3));
        var paid = g.ToJson(); Assert.False(g.Craft("G26")); Assert.Equal(paid, g.ToJson());
        Assert.True(g.Equip("G26")); Assert.Equal(10, g.Deck().Count);
        Assert.True(g.BuyMod("G01", "D01")); Assert.Equal(new[] { "C19", "C01", "C01", "C02" }, g.GearCards("G01"));
        Assert.False(g.BuyMod("G01", "D01")); Assert.True(g.SetMod("G01", ""));
        Assert.Equal(ChapterContent.Gear["G01"].Cards, g.GearCards("G01"));
    }

    [Fact]
    public void TaskItemsRequireExtractionAndFailureRestoresUniqueSources()
    {
        var g = new ChapterSession(); g.Depart(); Move(g, "A03"); Act(g, "rescue", "relay");
        var free = g.State.Stock.Single(i => i.FreeSupply); Assert.True(g.Drop(free.Id, free.Quantity));
        Assert.False(g.TurnIn("Q01")); Move(g, "A04"); Act(g, "dark", "bench");
        Move(g, "A06"); Act(g, "shovel"); Move(g, "A04"); Assert.True(g.Equip("G25"));
        Move(g, "A03"); Act(g, "K02"); g.State.Health = 1; g.EndTurn();
        Assert.Equal(ChapterPhase.Defeated, g.State.Phase); Assert.Equal("G01", g.State.Weapon);
        Assert.Empty(g.State.Loot); Assert.Empty(g.State.Items); Assert.Equal(new int[7], g.State.Cargo);
        Assert.Contains("robot-rescued", g.State.World); Assert.Contains("dark-door", g.State.World);
        Assert.True(g.ReturnToCamp()); Assert.False(g.CanTurnIn("S07")); Assert.True(g.Depart()); Move(g, "A03");
        Assert.Contains(g.Actions(), a => a.Id == "relay"); Assert.DoesNotContain(g.Actions(), a => a.Id == "rescue");
        Act(g, "relay"); Move(g, "A04", "A06"); Act(g, "shovel");
        Assert.Contains("G25", g.State.Loot);
    }

    [Fact]
    public void RetreatPreservesEncounterAndCardPilesAcrossSave()
    {
        var g = new ChapterSession(); g.Depart(); Move(g, "A03"); Act(g, "K02");
        var b = g.State.Battle!; var strike = b.Hand.FindIndex(c => c == "C01");
        Assert.True(g.PlayCard(strike, 0)); var remaining = b.Enemies[0].Health; var mana = b.Mana;
        Assert.True(g.EndTurn(true)); Assert.Equal("A01", g.State.Location); Assert.Equal(36, g.State.Health);
        Assert.Equal(remaining, g.State.Paused["K02"].Enemies[0].Health);
        var loaded = ChapterSession.FromJson(g.ToJson()); Move(loaded, "A03"); Act(loaded, "K02");
        Assert.Equal(remaining, loaded.State.Battle!.Enemies[0].Health); Assert.Equal(2, loaded.State.Battle.Round);
        Assert.Equal(Math.Min(3, mana + 1), loaded.State.Battle.Mana);
        Assert.Equal(8, loaded.State.Battle.Hand.Count + loaded.State.Battle.Draw.Count + loaded.State.Battle.Discard.Count);
    }

    [Fact]
    public void ShieldConversionConsumesAtMostEightAndExtraDrawIsCapped()
    {
        var g = CompleteOuterRing(); g.Equip("G15"); g.Depart(); Move(g, "A03"); Act(g, "K02");
        var b = g.State.Battle!;
        var index = b.Hand.IndexOf("C07");
        while (index < 0) { Assert.True(g.EndTurn()); b = g.State.Battle!; index = b.Hand.IndexOf("C07"); }
        b.Shield = 14; var enemy = b.Enemies[0].Health; Assert.True(g.PlayCard(index, 0));
        Assert.Equal(6, b.Shield); Assert.Equal(enemy - 12, b.Enemies[0].Health);
        var trial = CompleteOuterRing(); trial.StartTrial(); var tb = trial.State.Battle!;
        trial.PlayCard(0, 0); Assert.Equal(2, tb.ExtraDraw); Assert.Equal(6, tb.Hand.Count);
    }

    [Fact]
    public void PressureWarnsThenInterruptsWithoutGrantingTheActionReward()
    {
        var g = new ChapterSession(); g.Depart(); Move(g, "A03"); g.State.Pressure = 23;
        Act(g, "relay"); Assert.Equal(2, g.State.PatrolEta);
        Move(g, "A02", "A03"); Assert.Equal("A02", g.State.PatrolPlace);
        Act(g, "rescue"); Assert.True(g.State.Warned); var before = g.State.Cargo.ToArray();
        Act(g, "scrap"); Assert.Equal(ChapterPhase.Battle, g.State.Phase); Assert.Equal("K09", g.State.Battle!.Id);
        Assert.Equal(before, g.State.Cargo); Assert.DoesNotContain("scrap", g.State.Run);
        g.EndTurn(true); Assert.Equal(ChapterPhase.Exploring, g.State.Phase); Assert.False(g.State.Warned);
    }

    [Fact]
    public void TrainMaterialChoiceAllowsParcelNextRunAndCannotDuplicateReward()
    {
        var g = CompleteOuterRing(); g.SetStart("A01"); g.Depart(); Move(g, "A02", "A07");
        Act(g, "train-inspect", "train-wire"); Assert.False(g.Act("train-wire")); Assert.False(g.Act("train-parcel"));
        Move(g, "A02", "A01"); Act(g, "extract"); g.ReturnToCamp(); var bank = g.State.Bank.ToArray();
        g.Depart(); Move(g, "A02", "A07"); Assert.DoesNotContain(g.Actions(), a => a.Id == "train-wire");
        Act(g, "package"); Move(g, "A02", "A01"); Act(g, "extract"); g.ReturnToCamp();
        Assert.True(g.TurnIn("S09")); Assert.Equal(bank[2] + 2, g.State.Bank[2]); Assert.False(g.TurnIn("S09"));
    }

    [Fact]
    public void FieldEquipmentRequiresActualCarryingAndSnapshotsModifications()
    {
        var g = CompleteOuterRing(); g.BuyMod("G01", "D01"); g.Equip("G25");
        Assert.True(g.ToggleBackup("G01")); g.Depart(); Move(g, "A03", "A04"); Act(g, "bench");
        Assert.True(g.Equip("G01")); Assert.Equal("C19", g.GearCards("G01")[0]);
        Assert.False(g.Equip("G02")); Assert.False(g.BuyMod("G09", "D08")); Assert.False(g.SetMod("G01", ""));
        Assert.True(g.Equip("G25")); Assert.True(g.Equip("G01"));
    }

    [Fact]
    public void SavesAreVersionedAndInvalidInputsAreRejected()
    {
        var g = CompleteOuterRing(); Assert.Equal(g.ToJson(), ChapterSession.FromJson(g.ToJson()).ToJson());
        Assert.Throws<InvalidDataException>(() => ChapterSession.FromJson("{}"));
        Assert.Throws<InvalidDataException>(() => ChapterSession.FromJson(new ExpeditionSession().ToJson()));
        Assert.Throws<InvalidDataException>(() => ChapterSession.FromJson(g.ToJson().Replace(ChapterSession.InventoryVersion, "unknown")));
        g.State.Owned.Add("missing"); Assert.Throws<InvalidDataException>(() => ChapterSession.FromJson(g.ToJson()));
    }

    [Fact]
    public void MapIsBidirectionalAndEveryRevealedPlaceHasAnExit()
    {
        var g = new ChapterSession(); g.State.World.Add("dark-door"); g.State.Done.Add("Q01");
        foreach (var p in ChapterContent.Places.Values)
        {
            foreach (var neighbor in p.Neighbors) Assert.Contains(p.Id, ChapterContent.Places[neighbor].Neighbors);
            g.State.Location = p.Id; Assert.InRange(g.NearestExit().Distance, 0, 3);
        }
        foreach (var gear in ChapterContent.Gear.Values)
        { Assert.Equal(gear.Slot == "backpack" ? 0 : gear.Slot == "hands" ? 2 : 4, gear.Cards.Length); Assert.All(gear.Cards, c => Assert.True(ChapterContent.Cards.ContainsKey(c))); }
    }

    [Fact]
    public void UncalibratedStoredClampCanBeExplicitlyCarriedAndCompletedLater()
    {
        var g = new ChapterSession(); g.Depart(); Move(g, "A03"); Act(g, "relay"); Move(g, "A01"); Act(g, "extract");
        g.ReturnToCamp(); g.TurnIn("Q01"); g.Depart(); Move(g, "A02"); Act(g, "clamp"); Move(g, "A01"); Act(g, "extract");
        g.ReturnToCamp(); Assert.False(g.CanTurnIn("Q02")); Assert.True(g.ToggleCarryItem("T02"));
        g = ChapterSession.FromJson(g.ToJson()); g.Depart(); Assert.Contains("T02", g.State.Items); Assert.DoesNotContain("T02", g.State.StoredItems);
        Move(g, "A03", "A04"); Act(g, "bench", "calibrate"); Move(g, "A03", "A01"); Act(g, "extract");
        g.ReturnToCamp(); Assert.True(g.TurnIn("Q02")); Assert.False(g.ToggleCarryItem("T02"));
    }

    [Fact]
    public void ChangingStarterAfterFailedFirstRunProducesValidSave()
    {
        var g = new ChapterSession(); g.Depart(); Move(g, "A03"); Act(g, "K02"); g.State.Health = 1; g.EndTurn();
        g.ReturnToCamp(); Assert.True(g.SelectStarter("G02"));
        Assert.Equal("G02", ChapterSession.FromJson(g.ToJson()).State.Weapon);
    }

    [Fact]
    public void LeaderVictoryPersistsAndLostEquipmentCanBeRecoveredWithoutAnotherFight()
    {
        var g = CompleteOuterRing(); g.Depart(); Move(g, "A02"); Act(g, "E13");
        var b = g.State.Battle!;
        // Resolve the final hit to test the encounter/unique reward transaction independently of balance.
        b.Enemies[0].Health = 1; b.Enemies[0].Shield = 0; b.Enemies[1].Health = 0;
        var attack = b.Hand.FindIndex(c => ChapterContent.Cards[c].Damage > 0); Assert.True(g.PlayCard(attack, 0)); Collect(g);
        Assert.Contains("G03", g.State.Loot); Assert.Contains("leader-won", g.State.World);
        Move(g, "A03"); Act(g, "K02"); g.State.Health = 1; g.EndTurn(); g.ReturnToCamp(); g.Depart(); Move(g, "A02");
        Assert.DoesNotContain(g.Actions(), a => a.Id == "E13"); Act(g, "leader-tool"); Assert.Contains("G03", g.State.Loot);
        Assert.Equal(new int[7], g.State.Cargo);
    }

    [Fact]
    public void SavedBattleReplaysDeterministicallyAndResonanceDoesNotLoop()
    {
        var g = CompleteOuterRing(); g.Depart(); Move(g, "A03", "A04"); Act(g, "R07"); Move(g, "A03"); Act(g, "K02");
        var copy = ChapterSession.FromJson(g.ToJson());
        var shield = g.State.Battle!.Hand.FindIndex(c => c == "C03"); Assert.True(shield >= 0);
        Assert.True(g.PlayCard(shield, 0)); Assert.True(copy.PlayCard(shield, 0));
        Assert.Equal(11, g.State.Battle.Shield); Assert.Equal(g.ToJson(), copy.ToJson());
        g.EndTurn(); copy.EndTurn(); Assert.Equal(g.ToJson(), copy.ToJson());
    }
}
