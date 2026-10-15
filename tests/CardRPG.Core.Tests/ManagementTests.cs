using System.Text.Json;
using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public class ManagementTests
{
    private static ChapterSession Camp() => ChapterSession.FromJson(JsonSerializer.Serialize(new ChapterState
    { ContentVersion = "chapter1-outer-ring-v1", Bank = [20, 20, 20], Cargo = [0, 0, 0], Done = ["Q01", "Q02"], Extractions = 1 }));

    [Fact]
    public void BatchIsAtomicWhenLaterSelectionCannotFitOrHasMoved()
    {
        var game = Camp(); var steel = game.StoredStock.Single(i => i.Kind == "M01"); var wire = game.StoredStock.Single(i => i.Kind == "M02");
        var spring = game.StoredStock.Single(i => i.Kind == "M03"); var before = game.ToJson();
        Assert.False(game.TransferBatch(new Dictionary<long, int> { [steel.Id] = 20, [wire.Id] = 20, [spring.Id] = 20 }, "warehouse", "carry"));
        Assert.Equal(before, game.ToJson());
        Assert.False(game.TransferBatch(new Dictionary<long, int> { [steel.Id] = 1, [9999] = 1 }, "warehouse", "carry"));
        Assert.Equal(before, game.ToJson());
    }

    [Fact]
    public void SameKindBatchesKeepIdentityAndPreviewMatchesCommit()
    {
        var game = Camp(); var steel = game.StoredStock.Single(i => i.Kind == "M01");
        Assert.True(game.Transfer(steel.Id, "carry", 10)); Assert.True(game.Transfer(steel.Id, "carry", 2));
        var second = game.CarriedItems.Single(i => i.Quantity == 2); var first = game.CarriedItems.Single(i => i.Quantity == 10);
        var plan = new Dictionary<long, int> { [second.Id] = 2 }; var before = game.ToJson();
        var preview = game.Preview(s => s.TransferBatch(plan, "carry", "warehouse"));
        Assert.NotNull(preview); Assert.Equal(before, game.ToJson());
        Assert.True(game.TransferBatch(plan, "carry", "warehouse")); Assert.Equal(preview.ToJson(), game.ToJson());
        Assert.Equal(10, game.State.Stock.Single(i => i.Id == first.Id).Quantity);
        Assert.Equal("warehouse", game.State.Stock.Single(i => i.Id == second.Id).Container);
    }

    [Fact]
    public void ProtectionSurvivesSplitAndSaveWhileQuotaCountsProtectedUnitsFirst()
    {
        var game = Camp(); var steel = game.StoredStock.Single(i => i.Kind == "M01");
        Assert.True(game.Transfer(steel.Id, "carry", 6)); var carried = game.CarriedItems.Single();
        Assert.True(game.SetCarryProtection(carried.Id, true)); Assert.True(game.SetCarryQuota("M01", 2));
        Assert.Empty(game.MaterialDepositPlan()); var before = game.ToJson();
        Assert.False(game.TransferBatch(new Dictionary<long, int> { [carried.Id] = 3 }, "carry", "warehouse")); Assert.Equal(before, game.ToJson());
        Assert.True(game.TransferBatch(new Dictionary<long, int> { [carried.Id] = 3 }, "carry", "warehouse", true));
        Assert.True(game.State.Stock.Where(i => i.Id == carried.Id || i.Quantity == 3).All(i => i.KeepOnPerson));
        game = ChapterSession.FromJson(game.ToJson()); Assert.True(game.CarriedItems.Single().KeepOnPerson);
        Assert.True(game.SetCarryProtection(carried.Id, false)); var plan = game.MaterialDepositPlan();
        Assert.Equal(1, plan[carried.Id]); Assert.True(game.StoreCarriedMaterials());
        Assert.Equal(2, game.CarriedItems.Sum(i => i.Quantity));
    }

    [Fact]
    public void PartialPlanIsPureAndMovesOnlyExplicitAcceptedQuantity()
    {
        var game = Camp(); var items = game.StoredStock.ToDictionary(i => i.Id, i => i.Quantity); var before = game.ToJson();
        var fitting = game.FittingTransfer(items, "warehouse", "carry");
        Assert.Equal(before, game.ToJson()); Assert.True(fitting.Values.Sum() < items.Values.Sum());
        Assert.True(game.TransferBatch(fitting, "warehouse", "carry")); Assert.Equal(fitting.Values.Sum(), game.CarriedItems.Sum(i => i.Quantity));
        Assert.True(game.CarryUsed <= game.CarryCapacity);
    }

    [Fact]
    public void CampBatchNeverReachesRemoteWarehouseDuringExploration()
    {
        var game = Camp(); var steel = game.StoredStock.Single(i => i.Kind == "M01"); game.Depart(); var before = game.ToJson();
        Assert.False(game.TransferBatch(new Dictionary<long, int> { [steel.Id] = 1 }, "warehouse", "carry"));
        Assert.Empty(game.MaterialDepositPlan()); Assert.Equal(before, game.ToJson());
    }

    [Fact]
    public void TrialNextStepStopsAskingForCompletedTrial()
    {
        var game = Camp(); Assert.Contains("免费台架试用", game.NextQuestStep("S10"));
        game.State.World.Add("trial-complete"); Assert.Contains("无需重复试用", game.NextQuestStep("S10"));
    }
    [Fact]
    public void DiscardBatchRejectsEntireMixedSelectionAndPreservesProtectedStock()
    {
        var game=Camp();var steel=game.StoredStock.Single(i=>i.Kind=="M01");var wire=game.StoredStock.Single(i=>i.Kind=="M02");
        game.SetCarryProtection(wire.Id,true);var before=game.ToJson();
        Assert.False(game.DiscardBatch(new Dictionary<long,int>{[steel.Id]=2,[wire.Id]=1}));Assert.Equal(before,game.ToJson());
        Assert.True(game.DiscardBatch(new Dictionary<long,int>{[steel.Id]=2}));Assert.Equal(18,game.StoredStock.Single(i=>i.Id==steel.Id).Quantity);
        Assert.True(game.StoredStock.Single(i=>i.Id==wire.Id).KeepOnPerson);
        Assert.False(game.DiscardBatch(new Dictionary<long,int>{[steel.Id]=19}));Assert.Equal(18,game.StoredStock.Single(i=>i.Id==steel.Id).Quantity);
    }
    [Fact]
    public void ExplicitPickupOrderResolvesCompetingMaterialsWithoutMutatingPreview()
    {
        var game=new ChapterSession();Assert.True(game.Depart());Assert.True(game.Move("A03"));Assert.True(game.Act("scrap"));
        var key=game.GroundHere.Single().Key;
        var steel=game.State.Stock.Single(i=>i.Container==key&&i.Kind=="M01");var wire=game.State.Stock.Single(i=>i.Container==key&&i.Kind=="M02");
        // Both commodities now compete for one remaining slot: the planner must obey explicit order.
        game.State.Stock.Add(new ChapterItem{Id=game.State.NextItemId++,Kind="T01",Container="carry"});
        game.State.Stock.Add(new ChapterItem{Id=game.State.NextItemId++,Kind="T02",Container="carry"});
        var before=game.ToJson();var first=game.FittingPickup(key,[wire.Id,steel.Id]);var second=game.FittingPickup(key,[steel.Id,wire.Id]);
        Assert.Equal(before,game.ToJson());Assert.Contains(wire.Id,first.Keys);Assert.DoesNotContain(steel.Id,first.Keys);Assert.Contains(steel.Id,second.Keys);Assert.DoesNotContain(wire.Id,second.Keys);
        var space=game.PickupSpace(wire.Id,wire.Quantity);Assert.Equal(game.CarryCapacity,space.Required);
        Assert.True(game.PickupSelected(key,first));Assert.Contains(game.State.Stock,i=>i.Id==steel.Id&&i.Container==key);
    }
    [Fact]
    public void MaterialDepositFilterDoesNotMoveUnrelatedKinds()
    {
        var game=Camp();foreach(var kind in new[]{"M01","M02"}) {var item=game.StoredStock.Single(i=>i.Kind==kind);Assert.True(game.Transfer(item.Id,"carry",3));}
        Assert.True(game.SetCarryQuota("M01",1));var plan=game.MaterialDepositPlan(["M01"]);Assert.Single(plan);Assert.Equal(2,plan.Single().Value);
        Assert.True(game.TransferBatch(plan,"carry","warehouse"));Assert.Equal(3,game.CarriedItems.Single(i=>i.Kind=="M02").Quantity);Assert.Equal(1,game.CarriedItems.Single(i=>i.Kind=="M01").Quantity);
    }
    [Fact]
    public void BattleReceiptsIdentifyDuplicateEnemyInstancesAndPreviewIsPure()
    {
        var game=Camp();Assert.True(game.StartTrial());var b=game.State.Battle!;
        b.Enemies=[new(){Id="E01",Health=24,MaxHealth=24},new(){Id="E01",Health=24,MaxHealth=24}];
        var index=b.Hand.IndexOf("C01");var before=game.ToJson();var preview=game.Preview(g=>g.PlayCard(index,1));Assert.NotNull(preview);Assert.Equal(before,game.ToJson());
        Assert.True(game.PlayCard(index,1));Assert.Equal(preview.ToJson(),game.ToJson());
        Assert.Contains(game.State.Events,e=>e.Type=="battle"&&e.Text.StartsWith("2号 · 搬运傀儡"));Assert.Equal(24,b.Enemies[0].Health);Assert.True(b.Enemies[1].Health<24);
    }
}
