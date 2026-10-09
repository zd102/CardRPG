using System.Numerics;
using CardRPG.Core;
using Xunit;

namespace CardRPG.Core.Tests;

public sealed class MazeTests
{
    private static MazeSession Session(params string[] rows) => new(new MazeDefinition("Test", rows));
    private static void Move(MazeSession session, Vector2 direction, int frames = 60)
    {
        for (var i = 0; i < frames; i++) session.Tick(direction, 1f / 60);
    }

    [Fact]
    public void StraightAndDiagonalMovementHaveEqualSpeed()
    {
        string[] rows = ["############", "#..........#", "#..........#", "#..........#", "#...P......#", "#..........#", "#..........#", "#..........#", "############"];
        var straight = Session(rows);
        var diagonal = Session(rows);
        var start = straight.Position;
        Move(straight, Vector2.UnitX, 30);
        Move(diagonal, Vector2.One, 30);
        Assert.InRange(MathF.Abs(Vector2.Distance(start, straight.Position) - Vector2.Distance(start, diagonal.Position)), 0, 0.001f);
    }

    [Fact]
    public void PlayerCannotTunnelThroughWallsEvenAfterLongFrame()
    {
        var session = Session("########", "#P.#...#", "########");
        for (var i = 0; i < 60; i++) session.Tick(Vector2.UnitX, 10);
        Assert.True(session.Position.X <= 3 - MazeSession.PlayerRadius + 0.001f);
        Assert.True(session.CanOccupy(session.Position));
    }

    [Fact]
    public void ClosedDoorBlocksSightAndMovementThenRevealsOnlyLineOfSight()
    {
        var session = Session("########", "#PD.K..#", "########");
        var door = new Cell(2, 1);
        var key = new Cell(4, 1);
        Assert.True(session.IsVisible(door));
        Assert.False(session.IsVisible(key));
        Assert.True(session.BlocksMovement(door));
        Assert.Equal(door, session.InteractionTarget());
        session.Interact();
        Assert.True(session.IsDoorOpen(door));
        Assert.False(session.BlocksMovement(door));
        Assert.True(session.IsVisible(key));
    }

    [Fact]
    public void DiagonalWallSeamDoesNotRevealHiddenObject()
    {
        var session = Session("#####", "#P#.#", "##K.#", "#...#", "#####");
        Assert.False(session.IsVisible(new Cell(2, 2)));
        Assert.Null(session.InteractionTarget());
    }

    [Fact]
    public void LShapedCorridorHidesTheFarSideOfCorner()
    {
        var session = Session("########", "#P.....#", "######.#", "#....K.#", "########");
        Assert.False(session.IsVisible(new Cell(5, 3)));
    }

    [Fact]
    public void BarsAreTransparentButSolidAndDoNotAllowRemoteInteraction()
    {
        var session = Session("########", "#P.G.K.#", "########");
        Assert.True(session.IsVisible(new Cell(5, 1)));
        Assert.Null(session.InteractionTarget());
        Move(session, Vector2.UnitX, 120);
        Assert.True(session.Position.X < 3);
        Assert.False(session.HasKey);
    }

    [Fact]
    public void SightRadiusDoesNotRevealDistantTerrain()
    {
        var session = Session("############", "#P.......K.#", "############");
        Assert.False(session.IsVisible(new Cell(9, 1)));
        Assert.False(session.WasSeen(new Cell(9, 1)));
    }

    [Fact]
    public void ClosingDoorHidesObjectsButKeepsExploredTerrain()
    {
        var session = Session("########", "#PD.K..#", "########");
        session.Interact();
        Assert.True(session.IsVisible(new Cell(4, 1)));
        session.Interact();
        Assert.False(session.IsVisible(new Cell(4, 1)));
        Assert.True(session.WasSeen(new Cell(4, 1)));
    }

    [Fact]
    public void PlayerStandingInDoorwayPreventsClosingDoor()
    {
        var session = Session("########", "#PD....#", "########");
        session.Interact();
        Move(session, Vector2.UnitX, 18);
        session.Interact();
        Assert.True(session.IsDoorOpen(new Cell(2, 1)));
        Assert.True(session.CanOccupy(session.Position));
    }

    [Fact]
    public void ShortcutCannotBeOpenedFromCourtyardSide()
    {
        var session = Session("######", "#PS..#", "######");
        session.Interact();
        Assert.False(session.ShortcutUnlocked);
        Assert.False(session.IsDoorOpen(new Cell(2, 1)));
    }

    [Fact]
    public void ShortcutCanBeOpenedFromDeepSide()
    {
        var session = Session("######", "#.SP.#", "######");
        session.Interact();
        Assert.True(session.ShortcutUnlocked);
        Assert.True(session.IsDoorOpen(new Cell(2, 1)));
    }

    [Fact]
    public void KeyIsRequiredForCopperDoorAndPickupCannotRepeat()
    {
        var locked = Session("#######", "#PL...#", "#######");
        locked.Interact();
        Assert.False(locked.SealUnlocked);
        var session = Session("########", "#PKL...#", "########");
        session.Interact();
        Assert.True(session.HasKey);
        Assert.True(session.IsCollected(new Cell(2, 1)));
        Move(session, Vector2.UnitX, 18);
        session.Interact();
        Assert.True(session.SealUnlocked);
        Assert.True(session.IsDoorOpen(new Cell(3, 1)));
    }

    [Fact]
    public void ChestCanOnlyBeCollectedOnce()
    {
        var session = Session("######", "#PC..#", "######");
        session.Interact();
        session.Interact();
        Assert.Equal(1, session.TreasureCount);
    }

    [Fact]
    public void ExitRequiresInteractionAndStopsMovementAfterCompletion()
    {
        var session = Session("######", "#PE..#", "######");
        Assert.False(session.Won);
        session.Interact();
        var position = session.Position;
        var time = session.ElapsedSeconds;
        Move(session, Vector2.UnitX);
        Assert.True(session.Won);
        Assert.Equal(position, session.Position);
        Assert.Equal(time, session.ElapsedSeconds);
    }

    [Theory]
    [InlineData("####", "#P#", "####")]
    [InlineData("####", "#PP#", "####")]
    [InlineData("####", "#PZ#", "####")]
    [InlineData("#.##", "#P.#", "####")]
    public void MalformedLayoutIsRejected(string a, string b, string c)
        => Assert.Throws<ArgumentException>(() => new MazeDefinition("Invalid", [a, b, c]));

    [Fact]
    public void DemoContainsRequiredLandmarksAndDoesNotRevealTheKeyAtSpawn()
    {
        var map = MazeDefinition.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "micro-maze.json")));
        var session = new MazeSession(map);
        foreach (var symbol in "PDLSGKCE") Assert.Single(map.Cells, c => map[c] == symbol);
        Assert.False(session.IsVisible(map.Cells.Single(c => map[c] == 'K')));
        Assert.False(session.WasSeen(map.Cells.Single(c => map[c] == 'E')));
        Assert.True(session.CanOccupy(session.Position));
    }
}
