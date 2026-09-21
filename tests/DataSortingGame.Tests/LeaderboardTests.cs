using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Tests;

public class LeaderboardTests
{
    private static PlayerResult R(string name, int score, double seconds, bool official = true, int minute = 0) => new()
    {
        PlayerName = name, Score = score, TotalSeconds = seconds, IsOfficial = official,
        PlayedAtUtc = new DateTime(2026, 10, 1, 9, minute, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void Ranks_by_score_then_time()
    {
        var board = Leaderboard.Rank([R("Slow", 100, 60), R("Fast", 100, 30), R("Top", 120, 90)]);
        Assert.Equal(["Top", "Fast", "Slow"], board.Select(e => e.Result.PlayerName));
        Assert.Equal([1, 2, 3], board.Select(e => e.Rank));
    }

    [Fact]
    public void Practice_rounds_are_excluded()
    {
        var board = Leaderboard.Rank([R("A", 50, 10), R("B", 200, 10, official: false)]);
        Assert.Single(board);
        Assert.Equal("A", board[0].Result.PlayerName);
    }

    [Fact]
    public void Only_a_players_best_result_is_listed_and_names_are_case_insensitive()
    {
        var board = Leaderboard.Rank([R("sam", 80, 40), R("SAM ", 100, 50), R("Kim", 90, 30)]);
        Assert.Equal(2, board.Count);
        Assert.Equal(100, board[0].Result.Score);
    }

    [Fact]
    public void Equal_score_and_time_share_a_rank_and_next_rank_is_skipped()
    {
        var board = Leaderboard.Rank([R("A", 100, 30.01), R("B", 100, 30.04, minute: 1), R("C", 90, 20)]);
        Assert.Equal([1, 1, 3], board.Select(e => e.Rank));
    }

    [Fact]
    public void RankOf_is_null_for_practice()
    {
        var practice = R("A", 10, 10, official: false);
        Assert.Null(Leaderboard.RankOf([practice, R("B", 5, 5)], practice.Id));
    }

    [Fact]
    public void Most_missed_orders_by_miss_rate()
    {
        PlayerResult Round(params (string label, bool ok)[] a) => new()
        {
            Answers = [.. a.Select(x => new CardAnswer { CardLabel = x.label, Correct = x.ok })],
        };
        var stats = ResultStats.MostMissed([
            Round(("Easy", true), ("Hard", false)),
            Round(("Easy", true), ("Hard", false)),
            Round(("Easy", false), ("Once", false)),
        ]);
        Assert.Equal("Hard", stats[0].CardLabel);
        Assert.DoesNotContain(stats, s => s.CardLabel == "Once"); // only seen once
    }
}
