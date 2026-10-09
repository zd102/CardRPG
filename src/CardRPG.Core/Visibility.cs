using System.Numerics;

namespace CardRPG.Core;

public static class Visibility
{
    /// <summary>Grid traversal with a conservative corner rule; opaque targets themselves remain visible.</summary>
    public static bool HasLineOfSight(Vector2 origin, Cell target, Func<Cell, bool> opaque)
    {
        var current = Cell.At(origin);
        if (current == target) return true;
        var direction = target.Center - origin;
        var stepX = Math.Sign(direction.X);
        var stepY = Math.Sign(direction.Y);
        var deltaX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1 / direction.X);
        var deltaY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1 / direction.Y);
        var maxX = stepX == 0 ? float.PositiveInfinity :
            ((stepX > 0 ? current.X + 1 : current.X) - origin.X) / direction.X;
        var maxY = stepY == 0 ? float.PositiveInfinity :
            ((stepY > 0 ? current.Y + 1 : current.Y) - origin.Y) / direction.Y;

        for (var steps = 0; steps < 1024; steps++)
        {
            if (MathF.Abs(maxX - maxY) < 0.00001f)
            {
                // Never reveal through the point where two tiles touch.
                if (opaque(new Cell(current.X + stepX, current.Y)) ||
                    opaque(new Cell(current.X, current.Y + stepY))) return false;
                current = new Cell(current.X + stepX, current.Y + stepY);
                maxX += deltaX;
                maxY += deltaY;
            }
            else if (maxX < maxY)
            {
                current = current with { X = current.X + stepX };
                maxX += deltaX;
            }
            else
            {
                current = current with { Y = current.Y + stepY };
                maxY += deltaY;
            }
            if (current == target) return true;
            if (opaque(current)) return false;
        }
        return false;
    }
}
