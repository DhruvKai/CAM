using System.Text.RegularExpressions;

namespace DataSortingGame.Core.Services;

public static partial class PlayerName
{
    public const int MaxLength = 60;

    /// <summary>Trims, collapses runs of whitespace and strips control characters.</summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var cleaned = ControlChars().Replace(name, " ");
        cleaned = Whitespace().Replace(cleaned, " ").Trim();
        return cleaned.Length > MaxLength ? cleaned[..MaxLength].TrimEnd() : cleaned;
    }

    /// <summary>Case-insensitive identity used to count attempts and pick a player's best result.</summary>
    public static string Key(string? name) => Normalize(name).ToUpperInvariant();

    [GeneratedRegex(@"\p{C}")] private static partial Regex ControlChars();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
}
