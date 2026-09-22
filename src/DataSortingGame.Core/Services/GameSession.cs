using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

public sealed record AnswerOutcome(
    bool Correct,
    bool TimedOut,
    int Points,
    int Bonus,
    string CorrectCategoryId,
    string Explanation);

/// <summary>State of one round: which card is current, the score, and the streak.</summary>
public sealed class GameSession
{
    private readonly IReadOnlyList<Card> _cards;
    private readonly GameSettings _settings;
    private readonly List<CardAnswer> _answers = [];
    private int _index;

    public GameSession(IReadOnlyList<Card> cards, GameSettings settings)
    {
        _cards = cards;
        _settings = settings;
    }

    public int Total => _cards.Count;
    /// <summary>Number of cards already answered.</summary>
    public int Answered => _answers.Count;
    public int Score { get; private set; }
    public int Streak { get; private set; }
    public bool IsFinished => _index >= _cards.Count;
    public Card? Current => IsFinished ? null : _cards[_index];
    public IReadOnlyList<CardAnswer> Answers => _answers;
    public double TotalSeconds => _answers.Sum(a => a.Seconds);

    /// <summary>Records the answer for the current card and advances. Pass null when time ran out.</summary>
    public AnswerOutcome Submit(string? chosenCategoryId, double seconds)
    {
        var card = Current ?? throw new InvalidOperationException("The round is already finished.");
        seconds = Math.Max(0, seconds);

        var correct = chosenCategoryId is not null &&
                      string.Equals(chosenCategoryId, card.CategoryId, StringComparison.Ordinal);
        int points = 0, bonus = 0;

        if (correct)
        {
            Streak++;
            points = _settings.PointsPerCorrect;
            if (_settings.StreakThreshold > 0 && Streak >= _settings.StreakThreshold) bonus += _settings.StreakBonus;
            if (seconds <= _settings.SpeedBonusSeconds) bonus += _settings.SpeedBonus;
            points += bonus;
        }
        else
        {
            Streak = 0;
        }

        Score += points;
        _answers.Add(new CardAnswer
        {
            CardId = card.Id,
            CardLabel = card.Label,
            ChosenCategoryId = chosenCategoryId,
            CorrectCategoryId = card.CategoryId,
            Correct = correct,
            Seconds = Math.Round(seconds, 2),
            Points = points,
        });
        _index++;

        return new AnswerOutcome(correct, chosenCategoryId is null, points, bonus, card.CategoryId, card.Explanation);
    }

    public PlayerResult ToResult(string playerName, string department, string employeeCode, bool isOfficial, DateTime? playedAtUtc = null) => new()
    {
        PlayerName = PlayerName.Normalize(playerName),
        Department = PlayerName.Normalize(department),
        EmployeeCode = PlayerName.Normalize(employeeCode),
        Score = Score,
        CorrectCount = _answers.Count(a => a.Correct),
        TotalCards = _cards.Count,
        TotalSeconds = Math.Round(TotalSeconds, 2),
        PlayedAtUtc = playedAtUtc ?? DateTime.UtcNow,
        IsOfficial = isOfficial,
        Answers = [.. _answers],
    };
}
