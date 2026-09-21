using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

public static class GameEngine
{
    public static readonly int[] DefaultMix = [40, 40, 20];

    /// <summary>
    /// Draws a random, duplicate-free set of cards honouring the difficulty mix, and makes sure every
    /// category appears at least once so a round never skips a box entirely.
    /// </summary>
    public static IReadOnlyList<Card> DrawCards(IReadOnlyList<Card> pool, int count, int[]? mix, Random? rng = null)
    {
        rng ??= Random.Shared;
        count = Math.Clamp(count, 0, pool.Count);
        if (count == 0) return [];

        var weights = mix is { Length: 3 } && mix.All(m => m >= 0) && mix.Sum() > 0 ? mix : DefaultMix;
        var shuffled = Shuffle(pool, rng);
        var targets = SplitCount(count, weights);

        var chosen = new List<Card>();
        for (var d = 1; d <= 3; d++)
            chosen.AddRange(shuffled.Where(c => Math.Clamp(c.Difficulty, 1, 3) == d).Take(targets[d - 1]));

        // A tier may be short of cards; top up from whatever is left.
        if (chosen.Count < count)
            chosen.AddRange(shuffled.Where(c => !chosen.Contains(c)).Take(count - chosen.Count));

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

    /// <summary>Splits <paramref name="count"/> across weights using the largest-remainder method.</summary>
    internal static int[] SplitCount(int count, int[] weights)
    {
        double total = weights.Sum();
        var exact = weights.Select(w => count * w / total).ToArray();
        var result = exact.Select(e => (int)Math.Floor(e)).ToArray();
        var leftover = count - result.Sum();
        foreach (var i in Enumerable.Range(0, weights.Length).OrderByDescending(i => exact[i] - result[i]).Take(leftover))
            result[i]++;
        return result;
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
