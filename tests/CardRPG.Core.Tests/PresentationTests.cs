using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public class PresentationTests
{
    [Theory]
    [InlineData("C01", 8, 0)]
    [InlineData("C06", 17, 0)]
    [InlineData("C07", 12, 0)]
    [InlineData("C18", 10, 0)]
    [InlineData("C25", 0, 9)]
    [InlineData("C36", 0, 0)]
    public void PreviewIsPureAndMatchesExecutionIncludingSpecialRules(string card, int damage, int shieldGain)
    {
        var game = new ChapterSession(); game.Depart(); game.Move("A03"); game.Act("K02");
        var b = game.State.Battle!; b.Hand = [card]; b.Draw = ["C01", "C02", "C03"]; b.Shield = 8;
        b.Enemies[0].Health = 24; b.Enemies[0].Shield = 4; game.State.Resonance = "R07";
        var before = game.ToJson(); var preview = game.Preview(g => g.PlayCard(0, 0));
        Assert.NotNull(preview); Assert.Equal(before, game.ToJson()); Assert.Empty(game.BattleFeedback);
        Assert.True(game.PlayCard(0, 0)); Assert.Equal(preview!.ToJson(), game.ToJson());
        if (damage > 0) Assert.Equal(24 - damage + (card == "C06" ? 0 : 4), game.State.Battle!.Enemies[0].Health);
        if (shieldGain > 0) Assert.Equal(8 + shieldGain, game.State.Battle!.Shield);
    }

    [Theory]
    [InlineData(42, 0, false)]
    [InlineData(3, 0, true)]
    [InlineData(3, 20, false)]
    public void RetreatForecastMatchesSurvivalAndDoesNotSpendAnything(int health, int shield, bool defeated)
    {
        var game = new ChapterSession(); game.Depart(); game.Move("A03"); game.Act("K02");
        game.State.Health = health; game.State.Battle!.Shield = shield;
        var before = game.ToJson(); var preview = game.Preview(g => g.EndTurn(true));
        Assert.Equal(before, game.ToJson()); Assert.NotNull(preview);
        Assert.Equal(defeated, preview!.State.Phase == ChapterPhase.Defeated);
        Assert.True(game.EndTurn(true)); Assert.Equal(preview.ToJson(), game.ToJson());
    }

    [Fact]
    public void BatchPickupPreviewsExactRemainderAndPaysOneAction()
    {
        var game = new ChapterSession(); game.Depart(); game.Move("A03");
        game.Act("relay"); var relay = game.State.Stock.Single(i => i.Kind == "T01"); game.Pickup(relay.Id, 1);
        game.Act("cloth"); var cloth = game.State.Stock.Single(i => i.Kind == "M07"); game.Pickup(cloth.Id, 2);
        game.Act("scrap"); var ground = game.GroundHere.Single(g => g.Value.Name.Contains("首获")).Key;
        game.ClosePickup(); var before = game.ToJson(); var plan = game.FittingPickup(ground);
        Assert.Equal(before, game.ToJson()); Assert.Single(plan);
        var pressure = game.State.Pressure; var remaining = game.State.Stock.Where(i => i.Container == ground).Sum(i => i.Quantity) - plan.Values.Sum();
        Assert.True(game.PickupSelected(ground, plan)); Assert.Equal(pressure + 1, game.State.Pressure);
        Assert.Equal(remaining, game.State.Stock.Where(i => i.Container == ground).Sum(i => i.Quantity));
        Assert.Equal(game.ToJson(), ChapterSession.FromJson(game.ToJson()).ToJson());
    }

    [Fact]
    public void BatchPickupInterruptedByPatrolMovesNothing()
    {
        var game = new ChapterSession(); game.Depart(); game.Move("A03"); game.Act("scrap"); game.ClosePickup();
        var ground = game.GroundHere.Single().Key; var plan = game.FittingPickup(ground);
        var stock = game.State.Stock.Select(i => (i.Id, i.Container, i.Quantity)).ToArray();
        game.State.Warned = true; game.State.PatrolPlace = "A03";
        Assert.True(game.PickupSelected(ground, plan)); Assert.Equal(ChapterPhase.Battle, game.State.Phase);
        Assert.Equal(stock, game.State.Stock.Select(i => (i.Id, i.Container, i.Quantity)));
    }

    [Fact]
    public void DepartureSummaryIncludesFreeSuppliesAndReservedEquipment()
    {
        var game = new ChapterSession(); var before = game.ToJson(); var projected = game.DepartureLoad();
        Assert.Equal(before, game.ToJson()); Assert.True(game.Depart());
        Assert.Equal(projected.Slots, game.CarryUsed); Assert.Equal(projected.Grams, game.CarriedGrams);
        Assert.Equal(projected.Reserved, game.WarehouseReserved);
    }

    [Fact]
    public void ReportsPersistAndSeparateLostItemsFromRestoredEquipment()
    {
        var game = new ChapterSession(); game.Depart(); game.Move("A03"); game.Act("relay"); ChapterTests.Collect(game);
        game.Act("rescue"); game.Act("K02"); game.State.Health = 1; game.EndTurn();
        var report = game.State.LastReport!; Assert.False(report.Success); Assert.Equal(1, report.Lost["T01"]);
        Assert.DoesNotContain("G01", report.Lost.Keys); Assert.Contains("守备铁锤", report.Preserved);
        Assert.Contains("robot-rescued", report.Changes); Assert.Empty(report.Brought);
        Assert.Equal(game.ToJson(), ChapterSession.FromJson(game.ToJson()).ToJson());
        game.ReturnToCamp(); game.Depart(); Assert.Null(game.State.LastReport);
    }

    [Fact]
    public void SuccessfulReportDoesNotCountFreeSuppliesAsLoot()
    {
        var game = new ChapterSession(); game.Depart(); game.Move("A03"); game.Act("relay"); ChapterTests.Collect(game);
        game.Move("A01"); game.Act("extract"); var report = game.State.LastReport!;
        Assert.True(report.Success); Assert.Equal(1, report.Gained["T01"]); Assert.DoesNotContain("U01", report.Gained.Keys);
        Assert.Contains("G01", report.Brought.Keys); Assert.DoesNotContain("G01", report.Gained.Keys);
        Assert.Contains("G02", report.NewGear);
    }

    [Fact]
    public void LegacyActiveRunDoesNotInventDepartureDeltas()
    {
        var game = new ChapterSession(); game.Depart(); game.State.World.Add("station");
        game.State.HasDepartureReportBaseline = false; game.State.DepartureQuantities.Clear(); game.State.DepartureWorld.Clear();
        game = ChapterSession.FromJson(game.ToJson()); Assert.True(game.Act("extract"));
        Assert.False(game.State.LastReport!.BaselineKnown); Assert.Empty(game.State.LastReport.Gained); Assert.Empty(game.State.LastReport.Changes);
    }

    [Fact]
    public void LethalPiercingPreviewRetainsUnconsumedEnemyShield()
    {
        var game = new ChapterSession(); game.Depart(); game.Move("A03"); game.Act("K02");
        game.State.Battle!.Hand = ["C06"]; game.State.Battle.Enemies[0].Health = 10; game.State.Battle.Enemies[0].Shield = 4;
        var next = game.Preview(g => g.PlayCard(0,0))!;
        Assert.Equal(ChapterPhase.Exploring,next.State.Phase); Assert.Equal(4,next.LastResolvedBattle!.Enemies[0].Shield); Assert.Equal(0,next.LastResolvedBattle.Enemies[0].Health);
        Assert.Equal(10,game.State.Battle.Enemies[0].Health);
    }

    [Fact]
    public void MalformedReportIsRejectedInsteadOfBreakingSettlementUi()
    {
        var game = new ChapterSession(); game.Depart(); game.Act("extract");
        game.State.LastReport!.Gained = null!;
        Assert.Throws<InvalidDataException>(() => ChapterSession.FromJson(game.ToJson()));
    }
}
