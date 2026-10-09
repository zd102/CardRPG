using System.Numerics;

namespace CardRPG.Core;

/// <summary>Click navigation uses only observed terrain; execution still uses normal collision and interaction rules.</summary>
public sealed class MouseNavigator(MazeSession session)
{
    private readonly List<Vector2> _waypoints = [];
    private int _next;
    private Cell? _interaction;
    private const float ArrivalTolerance = 0.00001f;
    public Vector2? Destination { get; private set; }
    public bool IsActive => Destination.HasValue;
    public string? Feedback { get; private set; }
    public IEnumerable<Vector2> RemainingPath => _waypoints.Skip(_next);

    public void Cancel()
    {
        _waypoints.Clear();
        _next = 0;
        _interaction = null;
        Destination = null;
        Feedback = null;
    }

    public bool Request(Cell target) => Request(target.Center);

    public bool Request(Vector2 point)
    {
        Cancel();
        if (session.Won) return false;
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)) return Reject("请选择有效的地面位置。");
        var target = Cell.At(point);
        if (!session.WasSeen(target)) return Reject("那里仍是未知的黑暗，请先靠近探索。");
        var interact = session.IsVisible(target) && MazeDefinition.IsObject(session.Map[target]) && !session.IsCollected(target);
        if (interact && session.CanInteract(target)) return session.Interact(target);
        if (!interact && !KnownWalkable(target)) return Reject("这里无法落脚，请选择已探索的道路。");

        if (!interact) point = FitDestination(point, target);
        if (TryConnect(session.Position, point, interact ? target : null, out var direct))
        {
            _waypoints.Add(direct);
            Destination = interact ? target.Center : point;
            _interaction = interact ? target : null;
            return true;
        }

        // Tile centres only supply a search graph around obstacles. The executed route is
        // pulled into clear, arbitrary-angle segments from the actual position to the click.
        var start = Cell.At(session.Position);
        var queue = new PriorityQueue<Cell, float>();
        var previous = new Dictionary<Cell, Cell> { [start] = start };
        var costs = new Dictionary<Cell, float> { [start] = Vector2.Distance(session.Position, start.Center) };
        var closed = new HashSet<Cell>();
        queue.Enqueue(start, costs[start] + Heuristic(start.Center));
        Cell? endpoint = null;
        var finish = point;
        while (queue.TryDequeue(out var current, out _))
        {
            if (!closed.Add(current)) continue;
            if (TryConnect(current.Center, point, interact ? target : null, out finish))
            {
                endpoint = current;
                break;
            }
            foreach (var next in Neighbours(current))
            {
                if (closed.Contains(next) || !KnownWalkable(next) || !CanTravel(current.Center, next.Center)) continue;
                var cost = costs[current] + Vector2.Distance(current.Center, next.Center);
                if (costs.TryGetValue(next, out var existing) && cost >= existing) continue;
                previous[next] = current;
                costs[next] = cost;
                queue.Enqueue(next, cost + Heuristic(next.Center));
            }
        }
        if (endpoint is not Cell end) return Reject("已知道路暂时无法到达那里，请换个位置观察。");
        var route = new List<Vector2>();
        for (var c = end; ; c = previous[c])
        {
            route.Add(c.Center);
            if (c == start) break;
        }
        route.Reverse();
        route.Add(finish);
        var origin = session.Position;
        for (var index = 0; index < route.Count;)
        {
            var next = route.Count - 1;
            while (next > index && !CanTravel(origin, route[next])) next--;
            _waypoints.Add(route[next]);
            origin = route[next];
            index = next + 1;
        }
        Destination = interact ? target.Center : point;
        _interaction = interact ? target : null;
        return true;

        float Heuristic(Vector2 position) => interact
            ? Math.Max(0, Vector2.Distance(position, target.Center) - MazeSession.InteractionRange)
            : Vector2.Distance(position, point);
    }

    public void Tick(float delta)
    {
        if (!float.IsFinite(delta) || delta <= 0) return;
        var remaining = Math.Min(delta, 0.1f);
        while (IsActive && !session.Won && _next < _waypoints.Count)
        {
            var difference = _waypoints[_next] - session.Position;
            if (difference.Length() <= ArrivalTolerance) { _next++; continue; }
            if (remaining < 0.00001f) break;
            if (!CanTravel(session.Position, _waypoints[_next]))
            {
                StopAtObstacle();
                break;
            }
            var duration = Math.Min(remaining, difference.Length() / MazeSession.MoveSpeed);
            var before = session.Position;
            session.Tick(difference, duration);
            remaining -= duration;
            if (before == session.Position)
            {
                StopAtObstacle();
                break;
            }
        }
        if (IsActive && _next == _waypoints.Count)
        {
            var interaction = _interaction;
            Cancel();
            if (interaction is Cell target && !session.Interact(target))
                Feedback = "已抵达附近。请重新观察并点击物件。";
        }
        session.Tick(Vector2.Zero, remaining);
    }

    private void StopAtObstacle()
    {
        Cancel();
        Feedback = "前方暂时无法通行，请重新选择道路。";
    }

    private bool CanTravel(Vector2 from, Vector2 to) =>
        MovementGeometry.CanTraverse(from, to, cell => !KnownWalkable(cell));

    private bool TryConnect(Vector2 from, Vector2 point, Cell? interaction, out Vector2 finish)
    {
        finish = point;
        if (interaction is Cell target)
        {
            var offset = from - target.Center;
            var range = MazeSession.InteractionRange - 0.01f;
            finish = offset.Length() <= range ? from : target.Center + Vector2.Normalize(offset) * range;
            if (!Visibility.HasLineOfSight(finish, target, KnownOpaque)) return false;
        }
        return CanTravel(from, finish);
    }

    private Vector2 FitDestination(Vector2 point, Cell cell)
    {
        if (CanTravel(point, point)) return point;
        // A click against a wall settles at the edge of the standable area, never at a grid centre.
        var safe = cell.Center;
        var blocked = point;
        for (var i = 0; i < 24; i++)
        {
            var middle = (safe + blocked) / 2;
            if (CanTravel(middle, middle)) safe = middle;
            else blocked = middle;
        }
        return safe;
    }

    private bool KnownWalkable(Cell cell) => session.WasSeen(cell) && session.Map[cell] is not ('#' or 'G') &&
        (!MazeDefinition.IsDoor(session.Map[cell]) || KnownOpen(cell));

    private bool KnownOpaque(Cell cell) => !session.WasSeen(cell) || session.Map[cell] == '#' ||
        (MazeDefinition.IsDoor(session.Map[cell]) && !KnownOpen(cell));

    private bool KnownOpen(Cell cell) => session.IsVisible(cell) ? session.IsDoorOpen(cell) : session.RememberedOpen(cell);

    private bool Reject(string message) { Feedback = message; return false; }

    private static IEnumerable<Cell> Neighbours(Cell c)
    {
        for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
                if (x != 0 || y != 0) yield return new Cell(c.X + x, c.Y + y);
    }
}
