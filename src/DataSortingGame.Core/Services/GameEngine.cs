using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

public static class GameEngine
{
    /// <summary>
    /// Draws a random, duplicate-free set of cards, and makes sure every category appears at least once
    /// so a round never skips a box entirely.
    /// </summary>
    public static IReadOnlyList<Card> DrawCards(IReadOnlyList<Card> pool, int count, Random? rng = null)
    {
        rng ??= Random.Shared;
        count = Math.Clamp(count, 0, pool.Count);
        if (count == 0) return [];

        var chosen = Shuffle(pool, rng).Take(count).ToList();
        EnsureEveryCategoryAppears(chosen, pool, rng);
        return Shuffle(chosen, rng);
    }

    private static void EnsureEveryCategoryAppears(List<Card> chosen, IReadOnlyList<Card> pool, Random rng)
    {
        var categories = pool.Select(c => c.CategoryId).Distinct().ToList();
        if (chosen.Count < categories.Count) return;

        foreach (var missing in categories.Where(cat => chosen.All(c => c.CategoryId != cat)).ToList())
        {
            var incoming = pool.Where(c => c.CategoryId == missing).OrderBy(_ => rng.Next()).First();
            var mostCommon = chosen.GroupBy(c => c.CategoryId).OrderByDescending(g => g.Count()).First();
            var outgoing = mostCommon.OrderBy(_ => rng.Next()).First();
            chosen[chosen.IndexOf(outgoing)] = incoming;
        }
    }

    public static List<T> Shuffle<T>(IEnumerable<T> source, Random rng)
    {
        var list = source.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
