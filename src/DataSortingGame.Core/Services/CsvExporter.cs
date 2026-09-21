using System.Globalization;
using System.Text;
using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

public static class CsvExporter
{
    private static readonly string[] Header =
        ["Name", "Department", "Counts for leaderboard", "Score", "Correct", "Total cards", "Time (s)", "Played at", "Missed cards"];

    public static string ToCsv(IEnumerable<PlayerResult> results)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", Header)).Append("\r\n");
        foreach (var r in results.OrderByDescending(r => r.PlayedAtUtc))
        {
            var missed = string.Join("; ", r.Answers.Where(a => !a.Correct).Select(a => a.CardLabel));
            var fields = new[]
            {
                r.PlayerName,
                r.Department,
                r.IsOfficial ? "Yes" : "Practice",
                r.Score.ToString(CultureInfo.InvariantCulture),
                r.CorrectCount.ToString(CultureInfo.InvariantCulture),
                r.TotalCards.ToString(CultureInfo.InvariantCulture),
                r.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture),
                r.PlayedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                missed,
            };
            sb.Append(string.Join(",", fields.Select(Escape))).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Writes UTF-8 with a BOM so Excel shows non-ASCII names correctly.</summary>
    public static void WriteFile(string path, IEnumerable<PlayerResult> results) =>
        File.WriteAllText(path, ToCsv(results), new UTF8Encoding(true));

    /// <summary>
    /// Quotes a field for CSV and defuses spreadsheet formulas: names are typed by players, so a value like
    /// "=HYPERLINK(...)" must not be executed when the file is opened in Excel.
    /// </summary>
    internal static string Escape(string? value)
    {
        value ??= "";
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        if (value.IndexOfAny([',', '"', '\r', '\n']) >= 0)
            value = "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
