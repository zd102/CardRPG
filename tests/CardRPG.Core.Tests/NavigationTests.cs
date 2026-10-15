using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public class NavigationTests
{
    [Fact]
    public void RouteInspectionIsPureAndCannotRevealHiddenPlaces()
    {
        var game = new ChapterSession(); var before = game.ToJson();
        Assert.Empty(game.KnownRoute("A06")); Assert.Empty(game.KnownRoute("A07"));
        var route = game.KnownRoute("A05");
        Assert.Equal("A01", route[0]); Assert.Equal("A05", route[^1]);
        Assert.All(route, place => Assert.True(game.IsRevealed(place)));
        for (var i = 1; i < route.Count; i++) Assert.Contains(route[i], ChapterContent.Places[route[i - 1]].Neighbors);
        Assert.Equal(before, game.ToJson());
        game.State.World.Add("dark-door"); Assert.Equal("A06", game.KnownRoute("A06")[^1]);
    }

    [Fact]
    public void RouteCostRecomputesWeightWithoutInventingPatrolCost()
    {
        var game = new ChapterSession(); var route = game.KnownRoute("A05");
        var steps = route.Count - 1;
        Assert.Equal((steps, steps), game.RouteCost(route));
        game.State.Stock.Add(new ChapterItem { Id = 100, Kind = "M01", Quantity = 1000, Container = "carry" });
        Assert.True(game.Overweight); Assert.Equal((steps, steps * 2), game.RouteCost(route));
        game.State.Warned = true;
        Assert.Equal((steps, steps * 2), game.RouteCost(route));
        Assert.Equal((0, 0), game.RouteCost([]));
    }

    [Fact]
    public void ObjectiveFollowsTaskObjectFromGroundToExtraction()
    {
        var game = new ChapterSession(); Assert.True(game.Depart());
        Assert.Equal("A03", game.QuestPlace("Q01"));
        Assert.True(game.Move("A03")); Assert.True(game.Act("relay"));
        Assert.Equal("A03", game.QuestPlace("Q01"));
        var item = game.State.Stock.Single(i => i.Kind == "T01");
        Assert.True(game.Pickup(item.Id, 1));
        Assert.Equal(game.NearestExit().Place, game.QuestPlace("Q01"));
        Assert.Equal(0, game.GroundQuantityAt("A03"));
    }

    [Fact]
    public void TrackedObjectiveUsesOnlyKnownOpenTasks()
    {
        var game = new ChapterSession(); Assert.Equal("Q01", game.ObjectiveQuest);
        Assert.False(game.TrackQuest("S09")); Assert.Equal("", game.QuestPlace("S09"));
        Assert.True(game.TrackQuest("S07")); Assert.Equal("S07", game.ObjectiveQuest);
        game.State.Done.Add("S07"); Assert.Equal("Q01", game.ObjectiveQuest);
    }

    [Theory]
    [InlineData(19900, false, 1)]
    [InlineData(20000, false, 1)]
    [InlineData(20100, true, 2)]
    public void RouteUsesExactOverweightBoundary(int grams, bool overweight, int cost)
    {
        var game = new ChapterSession();
        game.State.Stock.Add(new ChapterItem { Id = 20, Kind = "M03", Quantity = (grams - game.CarriedGrams) / 100, Container = "carry" });
        Assert.Equal(grams, game.CarriedGrams); Assert.Equal(overweight, game.Overweight);
        Assert.Equal((1, cost), game.RouteCost(game.KnownRoute("A03")));
    }

    [Fact]
    public void EquipmentBlockerSharesCapacityCalculationWithExecution()
    {
        var game = new ChapterSession();
        game.State.Stock.Add(new ChapterItem { Id = 3, Kind = "BP01", Container = "warehouse", Registered = true });
        Assert.True(game.Equip("BP01"));
        game.State.Stock.Add(new ChapterItem { Id = 4, Kind = "M03", Quantity = 60, Container = "carry" });
        var before = game.ToJson();
        Assert.False(game.CanEquipItem("BP01")); Assert.Contains("腾出 2 格", game.EquipBlocker("BP01"));
        Assert.False(game.Equip("BP01")); Assert.Equal(before, game.ToJson());
        game.State.Phase = ChapterPhase.Exploring; game.State.Location = "A03";
        Assert.Contains("台架", game.EquipBlocker("BP01"));
    }
}
