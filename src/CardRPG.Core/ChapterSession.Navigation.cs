namespace CardRPG.Core;

public sealed partial class ChapterSession
{
    // Only discovered graph edges participate. Inspecting a path never advances the world.
    public List<string> KnownRoute(string destination, string? origin = null)
    {
        origin ??= AtBase ? State.Start : State.Location;
        if (!IsRevealed(origin) || !IsRevealed(destination)) return [];
        var queue = new Queue<List<string>>(); queue.Enqueue([origin]);
        var seen = new HashSet<string> { origin };
        while (queue.TryDequeue(out var path))
        {
            if (path[^1] == destination) return path;
            foreach (var next in ChapterContent.Places[path[^1]].Neighbors.Where(IsRevealed))
                if (seen.Add(next)) queue.Enqueue([.. path, next]);
        }
        return [];
    }

    public (int Steps, int Pressure) RouteCost(IReadOnlyCollection<string> route, bool departure = false)
    {
        var steps = Math.Max(0, route.Count - 1);
        var overweight = departure ? DepartureLoad().Grams > WeightLimitGrams : Overweight;
        return (steps, steps * (overweight ? 2 : 1));
    }

    public string ObjectiveQuest => State.TrackedQuest != "" && QuestOpen(State.TrackedQuest) && !State.Done.Contains(State.TrackedQuest)
        ? State.TrackedQuest : new[] { "Q01", "Q02", "S01" }.FirstOrDefault(id => !State.Done.Contains(id)) ?? "";

    public int GroundQuantityAt(string place) => IsRevealed(place)
        ? State.Stock.Where(i => State.Ground.TryGetValue(i.Container, out var g) && g.Place == place).Sum(i => i.Quantity) : 0;
}
