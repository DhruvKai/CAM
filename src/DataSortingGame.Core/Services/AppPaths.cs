namespace DataSortingGame.Core.Services;

public static class AppPaths
{
    private const string AppFolder = "DataSortingGame";

    /// <summary>Per-user data folder holding the results file.</summary>
    public static string DataRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolder);

    public static string ResultsFile => Path.Combine(DataRoot, "results.jsonl");

    /// <summary>
    /// Editable config folder: a "Data" folder next to the exe so it is easy to find, or, when that location
    /// is read-only (e.g. Program Files), a "Data" folder under the per-user data root.
    /// </summary>
    public static string ResolveConfigDir(string baseDirectory)
    {
        var beside = Path.Combine(baseDirectory, "Data");
        return IsWritable(beside) ? beside : Path.Combine(DataRoot, "Data");
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
