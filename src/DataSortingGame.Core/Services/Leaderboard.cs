using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

public static class Leaderboard
{
    /// <summary>
    /// Ranks each player's best official result: higher score first, then faster time, then earlier play.
    /// Players with the same score and the same time (to 0.1 s) share a rank; the next rank is skipped.
    /// </summary>
    public static IReadOnlyList<LeaderboardEntry> Rank(IEnumerable<PlayerResult> results)
    {
        var best = results
            .Where(r => r.IsOfficial)
            .GroupBy(r => PlayerName.Key(r.PlayerName))
            .Select(g => g.OrderBy(Order).ThenBy(r => r.PlayedAtUtc).First())
            .OrderBy(Order)
            .ThenBy(r => r.PlayedAtUtc)
            .ToList();

        var entries = new List<LeaderboardEntry>(best.Count);
        for (var i = 0; i < best.Count; i++)
        {
            var rank = i > 0 && IsTie(best[i - 1], best[i]) ? entries[i - 1].Rank : i + 1;
            entries.Add(new LeaderboardEntry(rank, best[i]));
        }
        return entries;
    }

    /// <summary>Rank a result holds on the leaderboard; null for practice rounds or results that are not a player's best.</summary>
    public static int? RankOf(IEnumerable<PlayerResult> results, Guid resultId)
    {
        var entry = Rank(results).FirstOrDefault(e => e.Result.Id == resultId);
        return entry?.Rank;
    }

    private static (int, double) Order(PlayerResult r) => (-r.Score, Math.Round(r.TotalSeconds, 1));

    private static bool IsTie(PlayerResult a, PlayerResult b) => Order(a) == Order(b);
}
