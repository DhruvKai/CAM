using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Tests;

public class GameEngineTests
{
    private static readonly string[] Cats = ["a", "b", "c", "d"];

    private static List<Card> Pool(int perCategory = 9) =>
    [
        .. from cat in Cats
           from i in Enumerable.Range(0, perCategory)
           select new Card { Id = $"{cat}-{i}", Scenario = $"{cat}{i}", CategoryId = cat },
    ];

    [Fact]
    public void Draw_returns_requested_count_without_duplicates()
    {
        var cards = GameEngine.DrawCards(Pool(), 15, new Random(1));
        Assert.Equal(15, cards.Count);
        Assert.Equal(15, cards.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Draw_is_capped_at_pool_size()
    {
        var pool = Pool(3);
        Assert.Equal(pool.Count, GameEngine.DrawCards(pool, 500, new Random(3)).Count);
    }

    [Fact]
    public void Draw_from_an_empty_pool_is_empty() => Assert.Empty(GameEngine.DrawCards([], 15, new Random(4)));

    [Fact]
    public void Draw_always_includes_every_category()
    {
        for (var seed = 0; seed < 300; seed++)
        {
            var cards = GameEngine.DrawCards(Pool(), 8, new Random(seed));
            Assert.Equal(4, cards.Select(c => c.CategoryId).Distinct().Count());
        }
    }

    [Fact]
    public void Different_seeds_give_different_orders()
    {
        var orders = Enumerable.Range(0, 10)
            .Select(seed => string.Join(",", GameEngine.DrawCards(Pool(), 15, new Random(seed)).Select(c => c.Id)))
            .Distinct().Count();
        Assert.True(orders > 1);
    }
}
