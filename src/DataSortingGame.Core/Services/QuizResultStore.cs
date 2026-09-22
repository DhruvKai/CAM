using System.Text;
using System.Text.Json;
using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

/// <summary>
/// Append-only JSON Lines store for quiz rounds, kept separate from the sorting game's results so the two
/// leaderboards don't mix. Same crash-safe append/rewrite scheme as <see cref="ResultStore"/>.
/// </summary>
public sealed class QuizResultStore
{
    private static readonly JsonSerializerOptions Json = ConfigLoader.JsonOptions;
    private readonly object _gate = new();

    public QuizResultStore(string path) => Path = path;

    public string Path { get; }

    public IReadOnlyList<QuizResult> LoadAll()
    {
        lock (_gate) return ReadAll();
    }

    public void Append(QuizResult result)
    {
        lock (_gate)
        {
            EnsureDirectory();
            var line = JsonSerializer.Serialize(result, Json) + "\n";
            using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            if (stream.Length > 0 && !EndsWithNewline()) stream.WriteByte((byte)'\n');
            var bytes = new UTF8Encoding(false).GetBytes(line);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
    }

    public bool Delete(Guid id)
    {
        lock (_gate)
        {
            var all = ReadAll();
            var kept = all.Where(r => r.Id != id).ToList();
            if (kept.Count == all.Count) return false;
            Rewrite(kept);
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate) Rewrite([]);
    }

    /// <summary>How many official (leaderboard-counting) quiz rounds this name has already played.</summary>
    public int CountOfficialAttempts(string name)
    {
        var key = PlayerName.Key(name);
        lock (_gate) return ReadAll().Count(r => r.IsOfficial && PlayerName.Key(r.PlayerName) == key);
    }

    private List<QuizResult> ReadAll()
    {
        var results = new List<QuizResult>();
        if (!File.Exists(Path)) return results;

        using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                if (JsonSerializer.Deserialize<QuizResult>(line, Json) is { } r) results.Add(r);
            }
            catch (JsonException)
            {
                // Skip a corrupt or half-written line rather than losing every other result.
            }
        }
        return results;
    }

    private void Rewrite(IEnumerable<QuizResult> results)
    {
        EnsureDirectory();
        var temp = Path + ".tmp";
        File.WriteAllLines(temp, results.Select(r => JsonSerializer.Serialize(r, Json)), new UTF8Encoding(false));
        File.Move(temp, Path, overwrite: true);
    }

    private bool EndsWithNewline()
    {
        using var read = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (read.Length == 0) return true;
        read.Seek(-1, SeekOrigin.End);
        return read.ReadByte() == '\n';
    }

    private void EnsureDirectory()
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }
}
