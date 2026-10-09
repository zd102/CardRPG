using System.Numerics;

namespace CardRPG.Core;

/// <summary>Sweeps the player's circular footprint against solid tiles, including rounded corners.</summary>
internal static class MovementGeometry
{
    public static bool CanTraverse(Vector2 from, Vector2 to, Func<Cell, bool> blocked)
    {
        if (!float.IsFinite(from.X) || !float.IsFinite(from.Y) ||
            !float.IsFinite(to.X) || !float.IsFinite(to.Y)) return false;
        var radius = MazeSession.PlayerRadius;
        var low = Cell.At(Vector2.Min(from, to) - new Vector2(radius));
        var high = Cell.At(Vector2.Max(from, to) + new Vector2(radius));
        for (var y = low.Y; y <= high.Y; y++)
            for (var x = low.X; x <= high.X; x++)
            {
                if (!blocked(new Cell(x, y))) continue;
                if (DistanceToRectangleSquared(from, to, new Vector2(x, y), new Vector2(x + 1, y + 1)) <
                    radius * radius - 0.000001f) return false;
            }
        return true;
    }

    private static float DistanceToRectangleSquared(Vector2 from, Vector2 to, Vector2 low, Vector2 high)
    {
        var direction = to - from;
        var enter = 0f;
        var leave = 1f;
        if (Clip(from.X, direction.X, low.X, high.X, ref enter, ref leave) &&
            Clip(from.Y, direction.Y, low.Y, high.Y, ref enter, ref leave)) return 0;
        var distance = Math.Min(Vector2.DistanceSquared(from, Vector2.Clamp(from, low, high)),
            Vector2.DistanceSquared(to, Vector2.Clamp(to, low, high)));
        distance = Math.Min(distance, DistanceToSegmentSquared(low, from, to));
        distance = Math.Min(distance, DistanceToSegmentSquared(high, from, to));
        distance = Math.Min(distance, DistanceToSegmentSquared(new Vector2(low.X, high.Y), from, to));
        return Math.Min(distance, DistanceToSegmentSquared(new Vector2(high.X, low.Y), from, to));
    }

    private static bool Clip(float origin, float direction, float low, float high, ref float enter, ref float leave)
    {
        if (direction == 0) return origin >= low && origin <= high;
        var a = (low - origin) / direction;
        var b = (high - origin) / direction;
        enter = Math.Max(enter, Math.Min(a, b));
        leave = Math.Min(leave, Math.Max(a, b));
        return enter <= leave;
    }

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 from, Vector2 to)
    {
        var direction = to - from;
        var lengthSquared = direction.LengthSquared();
        var t = lengthSquared == 0 ? 0 : Math.Clamp(Vector2.Dot(point - from, direction) / lengthSquared, 0, 1);
        return Vector2.DistanceSquared(point, from + direction * t);
    }
}
