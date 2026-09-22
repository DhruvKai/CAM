using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

public sealed record QuizAnswerOutcome(
    bool Correct,
    bool TimedOut,
    int Points,
    int Bonus,
    int CorrectIndex,
    string Explanation);

/// <summary>
/// State of one quiz round. Unlike <see cref="GameSession"/>, questions are pulled one at a time via
/// <see cref="Next"/> so the difficulty can be switched live (the green/yellow/red picker) between questions.
/// A null <c>totalTarget</c> means an open-ended round (the host-led "open quiz"): there is no fixed length
/// and no score is meaningful, but the same pull/reveal flow still works.
/// </summary>
public sealed class QuizSession
{
    private readonly IReadOnlyList<QuizQuestion> _pool;
    private readonly QuizSettings _settings;
    private readonly int? _totalTarget;
    private readonly Random _rng;
    private readonly List<QuizQuestion> _used = [];
    private readonly List<QuizAnswer> _answers = [];

    public QuizSession(IReadOnlyList<QuizQuestion> pool, QuizSettings settings, int? totalTarget, Random? rng = null)
    {
        _pool = pool;
        _settings = settings;
        _totalTarget = totalTarget;
        _rng = rng ?? Random.Shared;
    }

    public QuizQuestion? Current { get; private set; }
    /// <summary>1 = easy, 2 = medium, 3 = tricky. Defaults to medium.</summary>
    public int Difficulty { get; private set; } = 2;
    public int? Total => _totalTarget;
    public int Answered => _answers.Count;
    public int Score { get; private set; }
    public int Streak { get; private set; }
    public bool IsFinished => _totalTarget is { } t && Answered >= t;
    public IReadOnlyList<QuizAnswer> Answers => _answers;
    public double TotalSeconds => _answers.Sum(a => a.Seconds);

    /// <summary>Pulls a new current question at the given difficulty (or the last-used one). Null once the pool is exhausted.</summary>
    public QuizQuestion? Next(int? difficulty = null)
    {
        Difficulty = difficulty ?? Difficulty;

        var candidates = _pool.Where(q => q.Difficulty == Difficulty && !_used.Contains(q)).ToList();
        if (candidates.Count == 0) candidates = _pool.Where(q => !_used.Contains(q)).ToList();
        if (candidates.Count == 0)
        {
            Current = null;
            return null;
        }

        var pick = candidates[_rng.Next(candidates.Count)];
        _used.Add(pick);
        Current = pick;
        return pick;
    }

    /// <summary>Records the answer for the current question. Pass null when time ran out or the question was only revealed.</summary>
    public QuizAnswerOutcome Submit(int? chosenIndex, double seconds)
    {
        var q = Current ?? throw new InvalidOperationException("There is no current question.");
        seconds = Math.Max(0, seconds);

        var correct = chosenIndex is not null && chosenIndex == q.CorrectIndex;
        int points = 0, bonus = 0;

        if (correct)
        {
            Streak++;
            points = PointsFor(q.Difficulty);
            if (_settings.StreakThreshold > 0 && Streak >= _settings.StreakThreshold) bonus += _settings.StreakBonus;
            points += bonus;
        }
        else
        {
            Streak = 0;
        }

        Score += points;
        _answers.Add(new QuizAnswer
        {
            QuestionId = q.Id,
            QuestionText = q.Text,
            ChosenIndex = chosenIndex,
            CorrectIndex = q.CorrectIndex,
            Correct = correct,
            Seconds = Math.Round(seconds, 2),
            Points = points,
            Difficulty = q.Difficulty,
        });

        return new QuizAnswerOutcome(correct, chosenIndex is null, points, bonus, q.CorrectIndex, q.Explanation);
    }

    /// <summary>Tricky questions are worth more, easy ones less, so switching to easy doesn't inflate the leaderboard.</summary>
    private int PointsFor(int difficulty) => difficulty switch
    {
        1 => Math.Max(1, (int)Math.Round(_settings.PointsPerCorrect * 0.7)),
        3 => (int)Math.Round(_settings.PointsPerCorrect * 1.5),
        _ => _settings.PointsPerCorrect,
    };

    public QuizResult ToResult(string playerName, string employeeCode, bool isOfficial, DateTime? playedAtUtc = null) => new()
    {
        PlayerName = PlayerName.Normalize(playerName),
        EmployeeCode = PlayerName.Normalize(employeeCode),
        Score = Score,
        CorrectCount = _answers.Count(a => a.Correct),
        TotalQuestions = _totalTarget ?? _answers.Count,
        TotalSeconds = Math.Round(TotalSeconds, 2),
        PlayedAtUtc = playedAtUtc ?? DateTime.UtcNow,
        IsOfficial = isOfficial,
        Answers = [.. _answers],
    };
}
