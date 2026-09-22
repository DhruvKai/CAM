using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using DataSortingGame.Core.Models;

namespace DataSortingGame.Core.Services;

public sealed class GameConfig
{
    public required IReadOnlyList<Category> Categories { get; init; }
    public required IReadOnlyList<Card> Cards { get; init; }
    public required GameSettings Settings { get; init; }
    public required IReadOnlyList<QuizQuestion> QuizQuestions { get; init; }
    public required QuizSettings QuizSettings { get; init; }
    /// <summary>Non-fatal problems (skipped cards, bad colours, ...) worth showing to an admin.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>Thrown when a config file is unusable; the message names the file and the problem.</summary>
public sealed class ConfigException(string message, Exception? inner = null) : Exception(message, inner);

public static partial class ConfigLoader
{
    public const int MinCategories = 2;
    public const int MaxCategories = 6;
    private const string FallbackColor = "#607D8B";
    private static readonly string[] FileNames =
        ["categories.json", "cards.json", "settings.json", "quizQuestions.json", "quizSettings.json"];

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Loads config from <paramref name="dir"/>, first re-creating any missing file from the embedded defaults.</summary>
    public static GameConfig Load(string dir)
    {
        EnsureDefaults(dir);
        return Parse(
            ReadFile(dir, "categories.json"),
            ReadFile(dir, "cards.json"),
            ReadFile(dir, "settings.json"),
            ReadFile(dir, "quizQuestions.json"),
            ReadFile(dir, "quizSettings.json"));
    }

    public static GameConfig LoadDefaults() =>
        Parse(ReadDefault("categories.json"), ReadDefault("cards.json"), ReadDefault("settings.json"),
            ReadDefault("quizQuestions.json"), ReadDefault("quizSettings.json"));

    public static void EnsureDefaults(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var name in FileNames)
        {
            var path = Path.Combine(dir, name);
            if (!File.Exists(path)) File.WriteAllText(path, ReadDefault(name));
        }
    }

    public static string ReadDefault(string fileName)
    {
        using var stream = typeof(ConfigLoader).Assembly.GetManifestResourceStream("Defaults." + fileName)
                           ?? throw new InvalidOperationException($"Embedded default '{fileName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static GameConfig Parse(string categoriesJson, string cardsJson, string settingsJson,
        string quizQuestionsJson = "[]", string quizSettingsJson = "{}")
    {
        var warnings = new List<string>();
        var categories = ParseCategories(Deserialize<List<Category>>(categoriesJson, "categories.json") ?? [], warnings);
        var cards = ParseCards(Deserialize<List<Card>>(cardsJson, "cards.json") ?? [], categories, warnings);
        var settings = ParseSettings(Deserialize<GameSettings>(settingsJson, "settings.json") ?? new GameSettings(), warnings);
        var quizQuestions = ParseQuizQuestions(Deserialize<List<QuizQuestion>>(quizQuestionsJson, "quizQuestions.json") ?? [], warnings);
        var quizSettings = ParseQuizSettings(Deserialize<QuizSettings>(quizSettingsJson, "quizSettings.json") ?? new QuizSettings(), warnings);

        return new GameConfig
        {
            Categories = categories, Cards = cards, Settings = settings,
            QuizQuestions = quizQuestions, QuizSettings = quizSettings, Warnings = warnings,
        };
    }

    private static List<Category> ParseCategories(List<Category> raw, List<string> warnings)
    {
        var result = new List<Category>();
        foreach (var c in raw)
        {
            c.Id = (c.Id ?? "").Trim();
            c.Name = (c.Name ?? "").Trim();
            if (c.Id.Length == 0 || c.Name.Length == 0)
            {
                warnings.Add("categories.json: a category without an id or name was skipped.");
                continue;
            }
            if (result.Any(x => x.Id == c.Id))
            {
                warnings.Add($"categories.json: duplicate category id '{c.Id}' was skipped.");
                continue;
            }
            if (!HexColor().IsMatch(c.Color ?? ""))
            {
                warnings.Add($"categories.json: '{c.Name}' has an invalid colour '{c.Color}'; using grey. Use the form #RRGGBB.");
                c.Color = FallbackColor;
            }
            result.Add(c);
        }

        if (result.Count < MinCategories || result.Count > MaxCategories)
            throw new ConfigException($"categories.json: found {result.Count} valid categories, but the game needs {MinCategories} to {MaxCategories}.");
        return result;
    }

    private static List<Card> ParseCards(List<Card> raw, List<Category> categories, List<string> warnings)
    {
        var result = new List<Card>();
        foreach (var c in raw)
        {
            c.Id = (c.Id ?? "").Trim();
            c.Label = (c.Label ?? "").Trim();
            var name = c.Id.Length > 0 ? c.Id : c.Label;

            if (c.Id.Length == 0 || c.Label.Length == 0)
                warnings.Add($"cards.json: a card without an id or label was skipped ('{name}').");
            else if (result.Any(x => x.Id == c.Id))
                warnings.Add($"cards.json: duplicate card id '{c.Id}' was skipped.");
            else if (categories.All(cat => cat.Id != c.CategoryId))
                warnings.Add($"cards.json: card '{c.Id}' uses unknown categoryId '{c.CategoryId}' and was skipped.");
            else
            {
                c.Difficulty = Math.Clamp(c.Difficulty, 1, 3);
                c.Example ??= "";
                c.Explanation ??= "";
                result.Add(c);
            }
        }

        if (result.Count == 0)
            throw new ConfigException("cards.json: no valid cards were found.");
        return result;
    }

    private static GameSettings ParseSettings(GameSettings s, List<string> warnings)
    {
        if (s.CardsPerRound < 1) { warnings.Add("settings.json: cardsPerRound must be at least 1; using 15."); s.CardsPerRound = 15; }
        if (s.SecondsPerCard < 0) { warnings.Add("settings.json: secondsPerCard cannot be negative; using 0 (no limit)."); s.SecondsPerCard = 0; }
        if (s.MaxOfficialAttempts < 0) { warnings.Add("settings.json: maxOfficialAttempts cannot be negative; using 1."); s.MaxOfficialAttempts = 1; }
        if (s.IdleAttractSeconds < 0) s.IdleAttractSeconds = 0;

        if (s.DifficultyMix is not { Length: 3 } || s.DifficultyMix.Any(m => m < 0) || s.DifficultyMix.Sum() == 0)
        {
            warnings.Add("settings.json: difficultyMix needs three non-negative numbers (easy, medium, tricky); using 40/40/20.");
            s.DifficultyMix = [.. GameEngine.DefaultMix];
        }
        if (string.IsNullOrWhiteSpace(s.EventTitle)) s.EventTitle = "CAM";
        if (string.IsNullOrWhiteSpace(s.AdminPin))
        {
            warnings.Add("settings.json: adminPin was empty; using 1234. Please change it.");
            s.AdminPin = "1234";
        }
        return s;
    }

    private static List<QuizQuestion> ParseQuizQuestions(List<QuizQuestion> raw, List<string> warnings)
    {
        var result = new List<QuizQuestion>();
        foreach (var q in raw)
        {
            q.Id = (q.Id ?? "").Trim();
            q.Text = (q.Text ?? "").Trim();
            q.Options = (q.Options ?? []).Select(o => (o ?? "").Trim()).ToList();
            var name = q.Id.Length > 0 ? q.Id : q.Text;

            if (q.Id.Length == 0 || q.Text.Length == 0)
                warnings.Add($"quizQuestions.json: a question without an id or text was skipped ('{name}').");
            else if (result.Any(x => x.Id == q.Id))
                warnings.Add($"quizQuestions.json: duplicate question id '{q.Id}' was skipped.");
            else if (q.Options.Count < 2 || q.Options.Any(o => o.Length == 0))
                warnings.Add($"quizQuestions.json: question '{q.Id}' needs at least 2 non-empty options and was skipped.");
            else if (q.CorrectIndex < 0 || q.CorrectIndex >= q.Options.Count)
                warnings.Add($"quizQuestions.json: question '{q.Id}' has an out-of-range correctIndex and was skipped.");
            else
            {
                q.Difficulty = Math.Clamp(q.Difficulty, 1, 3);
                q.Explanation ??= "";
                result.Add(q);
            }
        }
        return result;
    }

    private static QuizSettings ParseQuizSettings(QuizSettings s, List<string> warnings)
    {
        if (s.QuestionsPerRound < 1) { warnings.Add("quizSettings.json: questionsPerRound must be at least 1; using 15."); s.QuestionsPerRound = 15; }
        // The quiz always has a per-question time limit; unlike the sorting game, 0/off is not allowed.
        if (s.SecondsPerQuestion < 5) { warnings.Add("quizSettings.json: secondsPerQuestion must be at least 5; using 20."); s.SecondsPerQuestion = 20; }
        if (s.MaxOfficialAttempts < 0) { warnings.Add("quizSettings.json: maxOfficialAttempts cannot be negative; using 1."); s.MaxOfficialAttempts = 1; }
        if (string.IsNullOrWhiteSpace(s.EventTitle)) s.EventTitle = "CAM";
        return s;
    }

    /// <summary>Overwrites cards.json with the given list. Comments in the previous file are lost.</summary>
    public static void SaveCards(string dir, IEnumerable<Card> cards) =>
        WriteFile(dir, "cards.json", JsonSerializer.Serialize(cards.ToList(), IndentedJsonOptions));

    /// <summary>Overwrites settings.json with the given settings. Comments in the previous file are lost.</summary>
    public static void SaveSettings(string dir, GameSettings settings) =>
        WriteFile(dir, "settings.json", JsonSerializer.Serialize(settings, IndentedJsonOptions));

    /// <summary>Overwrites quizQuestions.json with the given list. Comments in the previous file are lost.</summary>
    public static void SaveQuizQuestions(string dir, IEnumerable<QuizQuestion> questions) =>
        WriteFile(dir, "quizQuestions.json", JsonSerializer.Serialize(questions.ToList(), IndentedJsonOptions));

    /// <summary>Overwrites quizSettings.json with the given settings. Comments in the previous file are lost.</summary>
    public static void SaveQuizSettings(string dir, QuizSettings settings) =>
        WriteFile(dir, "quizSettings.json", JsonSerializer.Serialize(settings, IndentedJsonOptions));

    private static readonly JsonSerializerOptions IndentedJsonOptions = new(JsonOptions) { WriteIndented = true };

    private static string ReadFile(string dir, string name)
    {
        try { return File.ReadAllText(Path.Combine(dir, name)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigException($"{name}: could not be read ({ex.Message}).", ex);
        }
    }

    private static void WriteFile(string dir, string name, string content)
    {
        try { File.WriteAllText(Path.Combine(dir, name), content); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigException($"{name}: could not be written ({ex.Message}).", ex);
        }
    }

    private static T? Deserialize<T>(string json, string fileName)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException ex)
        {
            throw new ConfigException($"{fileName}: invalid JSON near line {(ex.LineNumber ?? 0) + 1} ({ex.Message})", ex);
        }
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")] private static partial Regex HexColor();
}
