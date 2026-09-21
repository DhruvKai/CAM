namespace DataSortingGame.Core.Models;

/// <summary>A classification box, e.g. "Public". Loaded from categories.json.</summary>
public sealed class Category
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Hex colour such as #2E9E5B.</summary>
    public string Color { get; set; } = "#607D8B";
}

/// <summary>A data-type card the player must classify. Loaded from cards.json.</summary>
public sealed class Card
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>Obviously fake sample value or short context, shown under the label.</summary>
    public string Example { get; set; } = "";
    public string CategoryId { get; set; } = "";
    /// <summary>1 = easy, 2 = medium, 3 = tricky.</summary>
    public int Difficulty { get; set; } = 1;
    public string Explanation { get; set; } = "";
}

/// <summary>Tunable game rules. Loaded from settings.json; every value has a safe default.</summary>
public sealed class GameSettings
{
    public string EventTitle { get; set; } = "Data Sorting Game";
    public int CardsPerRound { get; set; } = 15;
    public int PointsPerCorrect { get; set; } = 10;
    /// <summary>A correct answer earns <see cref="StreakBonus"/> once the streak reaches this length.</summary>
    public int StreakThreshold { get; set; } = 3;
    public int StreakBonus { get; set; } = 2;
    /// <summary>Correct answers given within this many seconds earn <see cref="SpeedBonus"/>.</summary>
    public double SpeedBonusSeconds { get; set; } = 3;
    public int SpeedBonus { get; set; } = 2;
    /// <summary>0 = no per-card time limit.</summary>
    public int SecondsPerCard { get; set; } = 0;
    /// <summary>Attempts per name that count for the leaderboard. 0 = unlimited. Extra plays are practice.</summary>
    public int MaxOfficialAttempts { get; set; } = 1;
    /// <summary>Percent mix of easy / medium / tricky cards in a round.</summary>
    public int[] DifficultyMix { get; set; } = [40, 40, 20];
    public bool ShowFeedback { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool KioskMode { get; set; } = true;
    /// <summary>Seconds of inactivity on the start screen before the leaderboard is shown. 0 = off.</summary>
    public int IdleAttractSeconds { get; set; } = 60;
    public string AdminPin { get; set; } = "1234";
}

/// <summary>One answered card, stored so the security team can see which topics people miss.</summary>
public sealed class CardAnswer
{
    public string CardId { get; set; } = "";
    public string CardLabel { get; set; } = "";
    /// <summary>Null when the player ran out of time.</summary>
    public string? ChosenCategoryId { get; set; }
    public string CorrectCategoryId { get; set; } = "";
    public bool Correct { get; set; }
    public double Seconds { get; set; }
    public int Points { get; set; }
}

/// <summary>One finished round for one player.</summary>
public sealed class PlayerResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PlayerName { get; set; } = "";
    public string Department { get; set; } = "";
    public int Score { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCards { get; set; }
    public double TotalSeconds { get; set; }
    public DateTime PlayedAtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>False for practice rounds, which never appear on the leaderboard.</summary>
    public bool IsOfficial { get; set; } = true;
    public List<CardAnswer> Answers { get; set; } = [];
}

public sealed record LeaderboardEntry(int Rank, PlayerResult Result);
