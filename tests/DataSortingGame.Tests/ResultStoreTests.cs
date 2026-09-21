using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsg-tests-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
}

public class ResultStoreTests
{
    private static PlayerResult R(string name, int score = 10, bool official = true) => new()
    {
        PlayerName = name, Department = "Ops", Score = score, IsOfficial = official, TotalCards = 2, CorrectCount = 1,
        Answers = [new CardAnswer { CardId = "x", CardLabel = "X", Correct = true }],
    };

    [Fact]
    public void Round_trips_a_result()
    {
        using var dir = new TempDir();
        var store = new ResultStore(Path.Combine(dir.Path, "sub", "results.jsonl"));
        var original = R("Zoë Müller", 42);
        store.Append(original);

        var loaded = Assert.Single(store.LoadAll());
        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal("Zoë Müller", loaded.PlayerName);
        Assert.Equal(42, loaded.Score);
        Assert.Equal("X", loaded.Answers[0].CardLabel);
    }

    [Fact]
    public void Missing_file_loads_as_empty()
    {
        using var dir = new TempDir();
        Assert.Empty(new ResultStore(Path.Combine(dir.Path, "none.jsonl")).LoadAll());
    }

    [Fact]
    public void Corrupt_and_torn_lines_are_skipped_but_later_appends_still_work()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "r.jsonl");
        var store = new ResultStore(path);
        store.Append(R("A"));
        File.AppendAllText(path, "{\"playerName\":\"torn"); // no newline: simulates a crash mid-write
        store.Append(R("B"));

        Assert.Equal(["A", "B"], store.LoadAll().Select(r => r.PlayerName));
    }

    [Fact]
    public void Delete_removes_only_the_chosen_result()
    {
        using var dir = new TempDir();
        var store = new ResultStore(Path.Combine(dir.Path, "r.jsonl"));
        var a = R("A"); var b = R("B");
        store.Append(a); store.Append(b);

        Assert.True(store.Delete(a.Id));
        Assert.False(store.Delete(a.Id));
        Assert.Equal(b.Id, Assert.Single(store.LoadAll()).Id);
    }

    [Fact]
    public void Clear_empties_the_store()
    {
        using var dir = new TempDir();
        var store = new ResultStore(Path.Combine(dir.Path, "r.jsonl"));
        store.Append(R("A"));
        store.Clear();
        Assert.Empty(store.LoadAll());
    }

    [Fact]
    public void Counts_official_attempts_ignoring_case_spacing_and_practice()
    {
        using var dir = new TempDir();
        var store = new ResultStore(Path.Combine(dir.Path, "r.jsonl"));
        store.Append(R("Sam Sample"));
        store.Append(R("sam   sample"));
        store.Append(R("Sam Sample", official: false));
        store.Append(R("Someone Else"));

        Assert.Equal(2, store.CountOfficialAttempts(" SAM SAMPLE "));
        Assert.Equal(0, store.CountOfficialAttempts("Nobody"));
    }

    [Fact]
    public void Concurrent_appends_do_not_lose_results()
    {
        using var dir = new TempDir();
        var store = new ResultStore(Path.Combine(dir.Path, "r.jsonl"));
        Parallel.For(0, 50, i => store.Append(R($"P{i}")));
        Assert.Equal(50, store.LoadAll().Count);
    }
}
