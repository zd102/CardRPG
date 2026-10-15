using System.Text.Json;
using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public class PlanningTests
{
    private static ChapterSession Camp() => ChapterSession.FromJson(JsonSerializer.Serialize(new ChapterState
    { ContentVersion = "chapter1-outer-ring-v1", Bank = [20,20,20], Cargo = [0,0,0], Done = ["Q01", "Q02", "S01"], Owned = ["G01", "G02", "G09", "BP01"], Extractions = 1 }));

    [Fact]
    public void MaterialSourcesAndRemainingCountsMatchActualCraft()
    {
        var game = Camp(); var steel = game.StoredStock.Single(i => i.Kind == "M01"); game.Transfer(steel.Id, "carry", 19);
        var plan = game.MaterialBudgetFor(game.RecipeCost("G10")); var before = game.ToJson();
        Assert.True(plan.Single(p => p.Kind == "M01").FromCarry > 0);
        Assert.Equal(before, game.ToJson()); Assert.True(game.Craft("G10"));
        foreach (var item in plan)
        {
            Assert.Equal(item.Warehouse - item.FromWarehouse, game.StoredStock.Where(i => i.Kind == item.Kind).Sum(i => i.Quantity));
            Assert.Equal(item.Carry - item.FromCarry, game.CarriedItems.Where(i => i.Kind == item.Kind).Sum(i => i.Quantity));
            Assert.Equal(item.Remaining, game.SafeMaterialCount(item.Kind));
        }
    }

    [Fact]
    public void RecipePlanCombinesSharedCostWithoutDuplicatingInventory()
    {
        var game = Camp(); var before = game.ToJson(); var sum = game.PlannedMaterialCost(["G10", "G15", "G10"]);
        Assert.Equal(game.RecipeCost("G10")[0] + game.RecipeCost("G15")[0], sum["M01"]);
        Assert.Equal(before, game.ToJson());
    }

    [Fact]
    public void PresetRestoresOnlyRequestedScopeAndReplenishesDifference()
    {
        var game = Camp(); game.Equip("BP01");
        var steel = game.StoredStock.Single(i => i.Kind == "M01"); game.Transfer(steel.Id, "carry", 3); game.SavePreparation("采集");
        var preset = game.State.PreparationPresets.Single(); var carried = game.CarriedItems.Single(i => i.Kind == "M01");
        game.Transfer(carried.Id, "warehouse", 2); game.Equip("G02");
        var before = game.ToJson(); var next = game.Preview(g => g.ApplyPreparation(preset, false, true));
        Assert.NotNull(next); Assert.Equal(before, game.ToJson()); Assert.Equal("G02", next.State.Weapon);
        Assert.Equal(3, next.CarriedItems.Where(i => i.Kind == "M01").Sum(i => i.Quantity));
        Assert.True(game.ApplyPreparation(preset, false, true)); Assert.Equal(next.ToJson(), game.ToJson());
        Assert.True(game.ApplyPreparation(preset, true, false)); Assert.Equal("G01", game.State.Weapon);
        Assert.Equal(3, game.CarriedItems.Where(i => i.Kind == "M01").Sum(i => i.Quantity));
        Assert.Equal(game.ToJson(), ChapterSession.FromJson(game.ToJson()).ToJson());
    }

    [Fact]
    public void MissingPresetItemsDoNotPartiallyChangeEquipmentOrMaterials()
    {
        var game = Camp(); var preset = game.CurrentPreparation("缺件"); preset.Equipment["hands"] = "G26"; preset.Carry["M01"] = 2;
        var before = game.ToJson(); Assert.False(game.ApplyPreparation(preset, true, true)); Assert.Equal(before, game.ToJson());
        game.Depart(); before = game.ToJson(); Assert.False(game.ApplyPreparation(preset, false, true)); Assert.Equal(before, game.ToJson());
    }

    [Fact]
    public void QuestTrackingAndRewardPreviewAreConsistentAndPure()
    {
        var game = new ChapterSession(); Assert.True(game.TrackQuest("Q01")); Assert.Contains("追踪", game.NextObjective);
        Assert.False(game.TrackQuest("S10")); game.Depart(); game.Move("A03"); game.Act("relay"); ChapterTests.Collect(game); game.Move("A01"); game.Act("extract"); game.ReturnToCamp();
        var before = game.ToJson(); var forecast = game.QuestRewardPreview("Q01"); Assert.Equal(before, game.ToJson());
        var counts = game.State.Stock.GroupBy(i => i.Kind).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        Assert.True(game.TurnIn("Q01"));
        foreach (var (kind, count) in forecast.Gained) Assert.Equal(counts.GetValueOrDefault(kind) + count, game.State.Stock.Where(i => i.Kind == kind).Sum(i => i.Quantity));
        Assert.Equal(forecast.Used, game.WarehouseUsed); Assert.DoesNotContain("追踪", game.NextObjective);
    }
}
