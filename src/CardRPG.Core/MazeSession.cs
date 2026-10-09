using System.Numerics;

namespace CardRPG.Core;

/// <summary>Movement, collision, visibility and interactions; no engine or filesystem dependency.</summary>
public sealed class MazeSession
{
    public const float PlayerRadius = 0.23f;
    public const float MoveSpeed = 3.25f;
    public const float SightRadius = 6.5f;
    public const float InteractionRange = 1.35f;
    private readonly HashSet<Cell> _openDoors = [];
    private readonly HashSet<Cell> _collected = [];
    private readonly bool[,] _visible;
    private readonly bool[,] _seen;
    private readonly bool[,] _rememberedOpen;
    private readonly Cell[] _objects;

    public MazeDefinition Map { get; }
    public Vector2 Position { get; private set; }
    public Vector2 Facing { get; private set; } = new(0, -1);
    public bool HasKey { get; private set; }
    public bool SealUnlocked { get; private set; }
    public bool ShortcutUnlocked { get; private set; }
    public bool Won { get; private set; }
    public int TreasureCount { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public int VisibilityRevision { get; private set; }
    public string LastMessage { get; private set; } = "余火仍在。推开北面的木门，寻找离开的路。";
    public string Objective => Won ? "你已走出灰烬回廊" : !HasKey ? "寻找铜钥匙" : !SealUnlocked ? "打开铜锁门" : "寻找并点亮出口";

    public MazeSession(MazeDefinition map)
    {
        Map = map;
        Position = map.Start.Center;
        _visible = new bool[map.Width, map.Height];
        _seen = new bool[map.Width, map.Height];
        _rememberedOpen = new bool[map.Width, map.Height];
        _objects = map.Cells.Where(c => MazeDefinition.IsObject(map[c])).ToArray();
        RefreshVisibility();
    }

    public bool IsVisible(Cell c) => Map.Contains(c) && _visible[c.X, c.Y];
    public bool WasSeen(Cell c) => Map.Contains(c) && _seen[c.X, c.Y];
    public bool RememberedOpen(Cell c) => Map.Contains(c) && _rememberedOpen[c.X, c.Y];
    public bool IsDoorOpen(Cell c) => _openDoors.Contains(c);
    public bool IsCollected(Cell c) => _collected.Contains(c);
    public bool CanInteract(Cell c) => !Won && IsVisible(c) && MazeDefinition.IsObject(Map[c]) &&
        !IsCollected(c) && Vector2.Distance(Position, c.Center) <= InteractionRange;
    public bool BlocksSight(Cell c) => Map[c] == '#' || (MazeDefinition.IsDoor(Map[c]) && !IsDoorOpen(c));
    public bool BlocksMovement(Cell c) => Map[c] is '#' or 'G' || (MazeDefinition.IsDoor(Map[c]) && !IsDoorOpen(c));

    public void Tick(Vector2 direction, float delta)
    {
        if (Won || !float.IsFinite(delta) || delta <= 0) return;
        delta = Math.Min(delta, 0.1f); // Avoid teleporting after a debugger stop or window stall.
        ElapsedSeconds += delta;
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || direction.LengthSquared() == 0) return;
        direction = Vector2.Normalize(direction);
        Facing = direction;
        var displacement = direction * MoveSpeed * delta;
        var count = Math.Max(1, (int)MathF.Ceiling(displacement.Length() / 0.08f));
        var step = displacement / count;
        var original = Position;
        for (var i = 0; i < count; i++)
        {
            var next = Position + step;
            if (MovementGeometry.CanTraverse(Position, next, BlocksMovement))
            {
                Position = next;
                continue;
            }
            // Slide only when the complete movement is blocked; free movement keeps its exact heading.
            next = Position + new Vector2(step.X, 0);
            if (MovementGeometry.CanTraverse(Position, next, BlocksMovement)) Position = next;
            next = Position + new Vector2(0, step.Y);
            if (MovementGeometry.CanTraverse(Position, next, BlocksMovement)) Position = next;
        }
        if (Vector2.DistanceSquared(original, Position) > 0) RefreshVisibility();
    }

    public bool CanOccupy(Vector2 position) => MovementGeometry.CanTraverse(position, position, BlocksMovement);

    public void RefreshVisibility()
    {
        foreach (var c in Map.Cells)
        {
            var visible = Vector2.DistanceSquared(Position, c.Center) <= SightRadius * SightRadius &&
                Visibility.HasLineOfSight(Position, c, BlocksSight);
            _visible[c.X, c.Y] = visible;
            if (!visible) continue;
            _seen[c.X, c.Y] = true;
            _rememberedOpen[c.X, c.Y] = IsDoorOpen(c);
        }
        VisibilityRevision++;
    }

    public Cell? InteractionTarget()
    {
        if (Won) return null;
        Cell? best = null;
        var score = float.MaxValue;
        foreach (var c in _objects)
        {
            if (!IsVisible(c) || IsCollected(c)) continue;
            var distance = Vector2.Distance(Position, c.Center);
            if (distance > InteractionRange) continue;
            var alignment = distance < 0.001f ? 0 : Vector2.Dot(Facing, (c.Center - Position) / distance);
            var candidate = distance - alignment * 0.2f;
            if (candidate < score) { best = c; score = candidate; }
        }
        return best;
    }

    public string InteractionLabel(Cell c) => Map[c] switch
    {
        'D' => IsDoorOpen(c) ? "关闭木门" : "推开木门",
        'S' => IsDoorOpen(c) ? "关闭捷径门" : ShortcutUnlocked ? "打开捷径门" : "检查门闩",
        'L' => IsDoorOpen(c) ? "关闭铜锁门" : SealUnlocked || HasKey ? "打开铜锁门" : "检查铜锁门",
        'K' => "拾取铜钥匙",
        'C' => "打开旧宝箱",
        'B' => "靠近余火",
        'N' => "阅读石碑",
        'E' => "点亮出口",
        _ => "查看"
    };

    public bool Interact()
    {
        var target = InteractionTarget();
        return target is Cell c && Interact(c);
    }

    public bool Interact(Cell c)
    {
        if (!CanInteract(c)) return false;
        var tile = Map[c];
        if (MazeDefinition.IsDoor(tile))
        {
            if (IsDoorOpen(c))
            {
                var nearest = Vector2.Clamp(Position, new Vector2(c.X, c.Y), new Vector2(c.X + 1, c.Y + 1));
                if (Vector2.Distance(Position, nearest) < PlayerRadius + 0.02f)
                    LastMessage = "先离开门洞，再关门。";
                else { _openDoors.Remove(c); LastMessage = "门合上了，另一侧再次隐入黑暗。"; }
            }
            else if (tile == 'L' && !HasKey && !SealUnlocked)
                LastMessage = "铜锁仍在。需要一把铜钥匙。";
            else if (tile == 'S' && !ShortcutUnlocked && Position.X <= c.Center.X)
                LastMessage = "门闩在另一侧。也许有路能绕过去。";
            else
            {
                _openDoors.Add(c);
                if (tile == 'S') ShortcutUnlocked = true;
                if (tile == 'L') SealUnlocked = true;
                LastMessage = tile == 'S' ? "门闩松开了。这里原来通向入口庭院。" : tile == 'L' ? "铜钥匙转动，通向深处的门打开了。" : "木门打开了。转角之后仍是一片未知。";
            }
        }
        else switch (tile)
        {
            case 'K': HasKey = true; _collected.Add(c); LastMessage = "获得铜钥匙。它应该能打开某处的铜锁。"; break;
            case 'C': TreasureCount++; _collected.Add(c); LastMessage = "获得一枚旧银币。探索总会留下一点收获。"; break;
            case 'N': LastMessage = "石碑：沿北廊寻找铜钥匙。深处的门闩，会把你带回余火旁。"; break;
            case 'B': LastMessage = "余火温暖着庭院。已经走过的地形会留在记忆中。"; break;
            case 'E': Won = true; LastMessage = "出口亮起。你把陌生的迷宫，走成了一张熟悉的地图。"; break;
        }
        RefreshVisibility();
        return true;
    }
}
