using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

public sealed record QuestionMissRate(string QuestionText, int Attempts, int Misses)
{
    public double MissPercent => Attempts == 0 ? 0 : 100.0 * Misses / Attempts;
}

public static class QuizResultStats
{
    /// <summary>Questions players get wrong most often, useful for follow-up awareness messages.</summary>
    public static IReadOnlyList<QuestionMissRate> MostMissed(IEnumerable<QuizResult> results, int top = 5, int minAttempts = 2) =>
        results.SelectMany(r => r.Answers)
            .GroupBy(a => a.QuestionText)
            .Select(g => new QuestionMissRate(g.Key, g.Count(), g.Count(a => !a.Correct)))
            .Where(m => m.Attempts >= minAttempts && m.Misses > 0)
            .OrderByDescending(m => m.MissPercent).ThenByDescending(m => m.Attempts).ThenBy(m => m.QuestionText)
            .Take(top)
            .ToList();
}
