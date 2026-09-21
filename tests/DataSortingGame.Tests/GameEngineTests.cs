using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Tests;

public class GameEngineTests
{
    private static readonly string[] Cats = ["a", "b", "c", "d"];

    private static List<Card> Pool(int perCategoryPerDifficulty = 3) =>
    [
        .. from cat in Cats
           from d in Enumerable.Range(1, 3)
           from i in Enumerable.Range(0, perCategoryPerDifficulty)
           select new Card { Id = $"{cat}-{d}-{i}", Label = $"{cat}{d}{i}", CategoryId = cat, Difficulty = d },
    ];

    [Fact]
    public void Draw_returns_requested_count_without_duplicates()
    {
        var cards = GameEngine.DrawCards(Pool(), 15, [40, 40, 20], new Random(1));
        Assert.Equal(15, cards.Count);
        Assert.Equal(15, cards.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Draw_honours_difficulty_mix()
    {
        // 20 cards at 50/30/20 -> 10 easy, 6 medium, 4 tricky. Balancing may swap a card, so allow a small drift.
        var cards = GameEngine.DrawCards(Pool(), 20, [50, 30, 20], new Random(2));
        Assert.InRange(cards.Count(c => c.Difficulty == 1), 8, 12);
        Assert.InRange(cards.Count(c => c.Difficulty == 3), 2, 6);
    }

    [Fact]
    public void Draw_is_capped_at_pool_size()
    {
        var pool = Pool(1);
        Assert.Equal(pool.Count, GameEngine.DrawCards(pool, 500, null, new Random(3)).Count);
    }

    [Fact]
    public void Draw_falls_back_when_a_difficulty_tier_is_empty()
    {
        var easyOnly = Pool().Where(c => c.Difficulty == 1).ToList();
        Assert.Equal(10, GameEngine.DrawCards(easyOnly, 10, [40, 40, 20], new Random(4)).Count);
    }

    [Fact]
    public void Draw_uses_default_mix_when_mix_is_invalid()
    {
        Assert.Equal(10, GameEngine.DrawCards(Pool(), 10, [0, 0, 0], new Random(5)).Count);
        Assert.Equal(10, GameEngine.DrawCards(Pool(), 10, [1, 2], new Random(5)).Count);
    }

    [Fact]
    public void Draw_always_includes_every_category()
    {
        for (var seed = 0; seed < 300; seed++)
        {
            var cards = GameEngine.DrawCards(Pool(), 8, [40, 40, 20], new Random(seed));
            Assert.Equal(4, cards.Select(c => c.CategoryId).Distinct().Count());
        }
    }

    [Fact]
    public void Different_seeds_give_different_orders()
    {
        var orders = Enumerable.Range(0, 10)
            .Select(seed => string.Join(",", GameEngine.DrawCards(Pool(), 15, [40, 40, 20], new Random(seed)).Select(c => c.Id)))
            .Distinct().Count();
        Assert.True(orders > 1);
    }

    [Theory]
    [InlineData(15, new[] { 40, 40, 20 }, new[] { 6, 6, 3 })]
    [InlineData(10, new[] { 1, 1, 1 }, new[] { 4, 3, 3 })]
    [InlineData(1, new[] { 40, 40, 20 }, new[] { 1, 0, 0 })]
    public void SplitCount_always_sums_to_count(int count, int[] weights, int[] expected)
    {
        var split = GameEngine.SplitCount(count, weights);
        Assert.Equal(count, split.Sum());
        Assert.Equal(expected, split);
    }
}
