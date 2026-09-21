using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Tests;

public class GameSessionTests
{
    private static GameSession NewSession(int cards = 5, GameSettings? settings = null) =>
        new(Enumerable.Range(1, cards).Select(i => new Card
        {
            Id = $"c{i}", Label = $"Card {i}", CategoryId = "right", Explanation = $"why {i}",
        }).ToList(), settings ?? new GameSettings());

    [Fact]
    public void Correct_answer_scores_base_points_plus_speed_bonus()
    {
        var s = NewSession();
        var o = s.Submit("right", 1.0);
        Assert.True(o.Correct);
        Assert.Equal(12, o.Points); // 10 + 2 speed
        Assert.Equal(12, s.Score);
    }

    [Fact]
    public void Slow_correct_answer_gets_no_speed_bonus()
    {
        var o = NewSession().Submit("right", 9);
        Assert.Equal(10, o.Points);
        Assert.Equal(0, o.Bonus);
    }

    [Fact]
    public void Wrong_answer_scores_nothing_and_resets_streak()
    {
        var s = NewSession();
        s.Submit("right", 9);
        s.Submit("right", 9);
        var wrong = s.Submit("other", 1);
        Assert.False(wrong.Correct);
        Assert.Equal(0, wrong.Points);
        Assert.Equal(0, s.Streak);
        Assert.Equal(20, s.Score);
    }

    [Fact]
    public void Streak_bonus_starts_at_threshold()
    {
        var s = NewSession();
        Assert.Equal(0, s.Submit("right", 9).Bonus);
        Assert.Equal(0, s.Submit("right", 9).Bonus);
        Assert.Equal(2, s.Submit("right", 9).Bonus); // third in a row
    }

    [Fact]
    public void Timeout_counts_as_wrong()
    {
        var s = NewSession();
        var o = s.Submit(null, 20);
        Assert.False(o.Correct);
        Assert.True(o.TimedOut);
        Assert.Null(s.Answers[0].ChosenCategoryId);
    }

    [Fact]
    public void Outcome_carries_correct_category_and_explanation()
    {
        var o = NewSession().Submit("wrong", 1);
        Assert.Equal("right", o.CorrectCategoryId);
        Assert.Equal("why 1", o.Explanation);
    }

    [Fact]
    public void Session_finishes_and_then_refuses_more_answers()
    {
        var s = NewSession(2);
        s.Submit("right", 1);
        Assert.False(s.IsFinished);
        s.Submit("right", 1);
        Assert.True(s.IsFinished);
        Assert.Null(s.Current);
        Assert.Throws<InvalidOperationException>(() => s.Submit("right", 1));
    }

    [Fact]
    public void ToResult_summarises_the_round()
    {
        var s = NewSession(3);
        s.Submit("right", 1);
        s.Submit("nope", 2);
        s.Submit("right", 3);
        var r = s.ToResult("  Sam   Sample ", " Ops ", isOfficial: true);

        Assert.Equal("Sam Sample", r.PlayerName);
        Assert.Equal("Ops", r.Department);
        Assert.Equal(2, r.CorrectCount);
        Assert.Equal(3, r.TotalCards);
        Assert.Equal(6.0, r.TotalSeconds);
        Assert.Equal(3, r.Answers.Count);
        Assert.Equal(s.Score, r.Score);
    }

    [Fact]
    public void Streak_bonus_can_be_switched_off()
    {
        var s = NewSession(5, new GameSettings { StreakThreshold = 0, SpeedBonus = 0 });
        for (var i = 0; i < 5; i++) Assert.Equal(10, s.Submit("right", 9).Points);
    }
}
