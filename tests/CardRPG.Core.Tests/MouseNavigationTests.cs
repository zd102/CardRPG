using System.Numerics;
using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public sealed class MouseNavigationTests
{
    private static MazeSession Session(params string[] rows) => new(new MazeDefinition("Mouse test", rows));
    private static void Finish(MouseNavigator navigator, MazeSession session)
    {
        for (var frame = 0; frame < 1200 && navigator.IsActive; frame++)
        {
            navigator.Tick(1f / 60);
            Assert.True(session.CanOccupy(session.Position));
            AssertClearOfSolids(session);
        }
        Assert.False(navigator.IsActive);
        Assert.Null(navigator.Feedback);
    }

    private static void AssertClearOfSolids(MazeSession session)
    {
        foreach (var cell in session.Map.Cells.Where(session.BlocksMovement))
        {
            var closest = Vector2.Clamp(session.Position, new Vector2(cell.X, cell.Y), new Vector2(cell.X + 1, cell.Y + 1));
            Assert.True(Vector2.Distance(session.Position, closest) >= MazeSession.PlayerRadius - 0.0001f);
        }
    }

    private static MazeSession OpenRoom() => Session(
        "##########", "#........#", "#........#", "#........#", "#...P....#",
        "#........#", "#........#", "#........#", "##########");

    [Theory]
    [InlineData(6.83f, 3.17f)]
    [InlineData(2.123f, 5.714f)]
    [InlineData(4.505f, 4.497f)]
    [InlineData(4.51f, 4.5f)]
    [InlineData(4.5f, 4.5f)]
    public void FreeClickFollowsAStraightLineAndStopsAtTheExactPoint(float x, float y)
    {
        var session = OpenRoom();
        var start = session.Position;
        var target = new Vector2(x, y);
        var navigator = new MouseNavigator(session);
        Assert.True(navigator.Request(target));
        for (var frame = 0; frame < 240 && navigator.IsActive; frame++)
        {
            navigator.Tick(1f / 60);
            var travel = session.Position - start;
            var direction = target - start;
            Assert.InRange(MathF.Abs(travel.X * direction.Y - travel.Y * direction.X), 0, 0.00005f);
            Assert.True(travel.Length() <= direction.Length() + 0.00001f);
        }
        Assert.False(navigator.IsActive);
        Assert.Null(navigator.Feedback);
        Assert.InRange(Vector2.Distance(session.Position, target), 0, 0.00001f);
    }

    [Fact]
    public void ReplacingRouteTurnsFromTheCurrentPositionWithoutRecentering()
    {
        var session = OpenRoom();
        var navigator = new MouseNavigator(session);
        navigator.Request(new Vector2(6.8f, 3.2f));
        navigator.Tick(0.1f);
        var origin = session.Position;
        var target = new Vector2(3.123f, 5.876f);
        Assert.True(navigator.Request(target));
        navigator.Tick(0.02f);
        var expected = origin + Vector2.Normalize(target - origin) * MazeSession.MoveSpeed * 0.02f;
        Assert.InRange(Vector2.Distance(session.Position, expected), 0, 0.00001f);
        Finish(navigator, session);
        Assert.InRange(Vector2.Distance(session.Position, target), 0, 0.00001f);
    }

    [Fact]
    public void ClickAlongWallStopsAtAStandablePointWithoutSnappingToCentre()
    {
        var session = Session("########", "#P.....#", "########");
        var navigator = new MouseNavigator(session);
        Assert.True(navigator.Request(new Vector2(4.13f, 1.01f)));
        Finish(navigator, session);
        Assert.InRange(session.Position.Y, 1 + MazeSession.PlayerRadius - 0.0001f, 1.24f);
        Assert.InRange(session.Position.X, 4.12f, 4.35f);
    }

    [Fact]
    public void SmoothingPreservesBodyClearanceAtWallCorners()
    {
        var session = Session("########", "#......#", "#.P#...#", "#......#", "########");
        var navigator = new MouseNavigator(session);
        // Observe the upper passage and then move off-centre beside the corner.
        foreach (var point in new[] { new Vector2(2.6f, 1.5f), new Vector2(2.73f, 2.6f) })
        {
            Assert.True(navigator.Request(point));
            Finish(navigator, session);
        }
        var target = new Vector2(4.18f, 1.63f);
        Assert.True(navigator.Request(target));
        Finish(navigator, session);
        Assert.InRange(Vector2.Distance(session.Position, target), 0, 0.00001f);
    }

    [Fact]
    public void ANewlyClosedDoorStopsAnExistingContinuousRoute()
    {
        var session = Session("########", "#PD....#", "########");
        session.Interact();
        var navigator = new MouseNavigator(session);
        Assert.True(navigator.Request(new Vector2(5.17f, 1.63f)));
        session.Interact();
        for (var frame = 0; frame < 240 && navigator.IsActive; frame++)
        {
            navigator.Tick(1f / 60);
            AssertClearOfSolids(session);
        }
        Assert.False(navigator.IsActive);
        Assert.NotNull(navigator.Feedback);
        Assert.True(session.Position.X < 2);
    }

    [Fact]
    public void ClickMovesToTileCentreWithoutOvershooting()
    {
        var session = Session("########", "#P.....#", "########");
        var navigator = new MouseNavigator(session);
        Assert.True(navigator.Request(new Cell(5, 1)));
        Finish(navigator, session);
        Assert.InRange(Vector2.Distance(session.Position, new Cell(5, 1).Center), 0, 0.004f);
        var stopped = session.Position;
        navigator.Tick(0.1f);
        Assert.Equal(stopped, session.Position);
    }

    [Fact]
    public void NavigationTurnsAroundWallUsingOnlyKnownFloor()
    {
        var session = Session("########", "#......#", "#.P#...#", "#......#", "########");
        // First look along the upper corridor, then return behind the corner.
        foreach (var destination in new[] { new Cell(2, 1), new Cell(2, 2) })
        {
            for (var frame = 0; frame < 120 && Vector2.Distance(session.Position, destination.Center) > 0.003f; frame++)
            {
                var direction = destination.Center - session.Position;
                session.Tick(direction, Math.Min(1f / 60, direction.Length() / MazeSession.MoveSpeed));
            }
            Assert.InRange(Vector2.Distance(session.Position, destination.Center), 0, 0.004f);
        }
        Assert.True(session.WasSeen(new Cell(4, 1)));
        Assert.False(session.IsVisible(new Cell(4, 1)));
        var navigator = new MouseNavigator(session);
        Assert.True(navigator.Request(new Cell(4, 1)));
        Assert.All(navigator.RemainingPath, point => Assert.True(session.WasSeen(Cell.At(point))));
        Finish(navigator, session);
        Assert.InRange(Vector2.Distance(session.Position, new Cell(4, 1).Center), 0, 0.004f);
    }

    [Fact]
    public void ClickingUnknownTerrainDoesNotRevealOrTraverseIt()
    {
        var session = Session("############", "#P.......K.#", "############");
        var navigator = new MouseNavigator(session);
        Assert.False(navigator.Request(new Cell(9, 1)));
        Assert.False(session.WasSeen(new Cell(9, 1)));
        Assert.Empty(navigator.RemainingPath);
        Assert.False(session.HasKey);
    }

    [Fact]
    public void VisibleKeyBehindBarsCannotBeApproachedThroughUnknownRoute()
    {
        var session = Session("########", "#P.G.K.#", "########");
        var navigator = new MouseNavigator(session);
        Assert.True(session.IsVisible(new Cell(5, 1)));
        Assert.False(navigator.Request(new Cell(5, 1)));
        Assert.False(session.HasKey);
    }

    [Fact]
    public void ClickingDistantVisibleObjectApproachesAndInteracts()
    {
        var session = Session("########", "#P...K.#", "########");
        var navigator = new MouseNavigator(session);
        Assert.True(navigator.Request(new Cell(5, 1)));
        Assert.False(session.HasKey);
        Finish(navigator, session);
        Assert.True(session.HasKey);
        Assert.InRange(Vector2.Distance(session.Position, new Cell(5, 1).Center), 0, MazeSession.InteractionRange);
    }

    [Fact]
    public void MouseInteractsWithClickedObjectInsteadOfNearestObject()
    {
        var session = Session("########", "#PKC...#", "########");
        var navigator = new MouseNavigator(session);
        Assert.True(navigator.Request(new Cell(3, 1)));
        Finish(navigator, session);
        Assert.Equal(1, session.TreasureCount);
        Assert.False(session.HasKey);
    }

    [Fact]
    public void ExplicitInteractionRejectsOutOfRangeHiddenCollectedAndFloorTargets()
    {
        var session = Session("#########", "#PK..D.C#", "#########");
        Assert.False(session.Interact(new Cell(7, 1)));
        Assert.False(session.Interact(new Cell(5, 1)));
        Assert.False(session.Interact(new Cell(1, 1)));
        Assert.True(session.Interact(new Cell(2, 1)));
        Assert.False(session.Interact(new Cell(2, 1)));
    }

    [Fact]
    public void CancelStopsMovementAndPendingInteraction()
    {
        var session = Session("########", "#P...K.#", "########");
        var navigator = new MouseNavigator(session);
        navigator.Request(new Cell(5, 1));
        navigator.Tick(0.1f);
        navigator.Cancel();
        var stopped = session.Position;
        for (var i = 0; i < 60; i++) navigator.Tick(1f / 60);
        Assert.Equal(stopped, session.Position);
        Assert.False(session.HasKey);
        Assert.Empty(navigator.RemainingPath);
    }

    [Fact]
    public void ANewClickReplacesTheOldInteractionIntent()
    {
        var session = Session("########", "#P...K.#", "########");
        var navigator = new MouseNavigator(session);
        navigator.Request(new Cell(5, 1));
        navigator.Tick(0.1f);
        navigator.Request(new Cell(2, 1));
        Finish(navigator, session);
        Assert.False(session.HasKey);
        Assert.InRange(Vector2.Distance(session.Position, new Cell(2, 1).Center), 0, 0.004f);
    }

    [Fact]
    public void ClickingRememberedGroundDoesNotOpenAClosedDoorAlongTheWay()
    {
        var session = Session("########", "#PD..K.#", "########");
        session.Interact();
        session.Interact();
        Assert.True(session.WasSeen(new Cell(4, 1)));
        var navigator = new MouseNavigator(session);
        Assert.False(navigator.Request(new Cell(4, 1)));
        Assert.False(session.IsDoorOpen(new Cell(2, 1)));
    }

    [Fact]
    public void ClickingADoorStillHonoursLocksAndOneSidedLatch()
    {
        var locked = Session("######", "#PL..#", "######");
        Assert.True(new MouseNavigator(locked).Request(new Cell(2, 1)));
        Assert.False(locked.IsDoorOpen(new Cell(2, 1)));
        var shortcut = Session("######", "#PS..#", "######");
        Assert.True(new MouseNavigator(shortcut).Request(new Cell(2, 1)));
        Assert.False(shortcut.ShortcutUnlocked);
    }

    [Fact]
    public void ClickingDoorTogglesCollisionAndVisibility()
    {
        var session = Session("########", "#PD..K.#", "########");
        var navigator = new MouseNavigator(session);
        var door = new Cell(2, 1);
        Assert.True(navigator.Request(door));
        Assert.False(session.BlocksMovement(door));
        Assert.True(session.IsVisible(new Cell(5, 1)));
        Assert.True(navigator.Request(door));
        Assert.True(session.BlocksMovement(door));
        Assert.False(session.IsVisible(new Cell(5, 1)));
    }

    [Fact]
    public void ClickingOpenDoorFromItsCentreDoesNotTrapPlayer()
    {
        var session = Session("#######", "#PD...#", "#######");
        session.Interact();
        for (var i = 0; i < 18; i++) session.Tick(Vector2.UnitX, 1f / 60);
        var navigator = new MouseNavigator(session);
        navigator.Request(new Cell(2, 1));
        Assert.True(session.IsDoorOpen(new Cell(2, 1)));
        Assert.True(session.CanOccupy(session.Position));
    }

    [Fact]
    public void MouseCanCompleteTheExitButCannotMoveAfterWinning()
    {
        var session = Session("########", "#P...E.#", "########");
        var navigator = new MouseNavigator(session);
        navigator.Request(new Cell(5, 1));
        Finish(navigator, session);
        Assert.True(session.Won);
        Assert.False(navigator.Request(new Cell(2, 1)));
    }
}
