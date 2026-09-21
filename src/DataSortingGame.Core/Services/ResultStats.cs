using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

public sealed record CardMissRate(string CardLabel, int Attempts, int Misses)
{
    public double MissPercent => Attempts == 0 ? 0 : 100.0 * Misses / Attempts;
}

public static class ResultStats
{
    /// <summary>Cards players get wrong most often, useful for follow-up awareness messages.</summary>
    public static IReadOnlyList<CardMissRate> MostMissed(IEnumerable<PlayerResult> results, int top = 5, int minAttempts = 2) =>
        results.SelectMany(r => r.Answers)
            .GroupBy(a => a.CardLabel)
            .Select(g => new CardMissRate(g.Key, g.Count(), g.Count(a => !a.Correct)))
            .Where(m => m.Attempts >= minAttempts && m.Misses > 0)
            .OrderByDescending(m => m.MissPercent).ThenByDescending(m => m.Attempts).ThenBy(m => m.CardLabel)
            .Take(top)
            .ToList();
}
