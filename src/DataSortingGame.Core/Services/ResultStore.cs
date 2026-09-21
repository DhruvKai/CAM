using System.Text;
using System.Text.Json;
using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

/// <summary>
/// Append-only JSON Lines store: one finished round per line. Appending is crash-safe (a torn last line is
/// skipped on load) and needs no database. Deleting rewrites the file via a temp file.
/// </summary>
public sealed class ResultStore
{
    private static readonly JsonSerializerOptions Json = ConfigLoader.JsonOptions;
    private readonly object _gate = new();

    public ResultStore(string path) => Path = path;

    public string Path { get; }

    public IReadOnlyList<PlayerResult> LoadAll()
    {
        lock (_gate) return ReadAll();
    }

    public void Append(PlayerResult result)
    {
        lock (_gate)
        {
            EnsureDirectory();
            var line = JsonSerializer.Serialize(result, Json) + "\n";
            // Start on a fresh line in case an earlier write was cut off mid-record.
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

    /// <summary>How many official (leaderboard-counting) rounds this name has already played.</summary>
    public int CountOfficialAttempts(string name)
    {
        var key = PlayerName.Key(name);
        lock (_gate) return ReadAll().Count(r => r.IsOfficial && PlayerName.Key(r.PlayerName) == key);
    }

    private List<PlayerResult> ReadAll()
    {
        var results = new List<PlayerResult>();
        if (!File.Exists(Path)) return results;

        using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                if (JsonSerializer.Deserialize<PlayerResult>(line, Json) is { } r) results.Add(r);
            }
            catch (JsonException)
            {
                // Skip a corrupt or half-written line rather than losing every other result.
            }
        }
        return results;
    }

    private void Rewrite(IEnumerable<PlayerResult> results)
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
