using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Tests;

public class DefaultConfigTests
{
    private static readonly GameConfig Config = ConfigLoader.LoadDefaults();

    [Fact]
    public void Shipped_defaults_load_with_no_warnings() => Assert.Empty(Config.Warnings);

    [Fact]
    public void Shipped_pool_is_big_enough_for_random_rounds()
    {
        Assert.True(Config.Cards.Count >= 60);
        Assert.Equal(4, Config.Categories.Count);
    }

    [Fact]
    public void Every_category_has_a_healthy_number_of_cards()
    {
        foreach (var cat in Config.Categories)
            Assert.True(Config.Cards.Count(c => c.CategoryId == cat.Id) >= 10, cat.Id);
    }

    [Fact]
    public void Every_card_has_an_explanation_and_example()
    {
        foreach (var c in Config.Cards)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Explanation), c.Id);
            Assert.False(string.IsNullOrWhiteSpace(c.Example), c.Id);
        }
    }

    [Fact]
    public void Shipped_content_has_no_brand_or_real_looking_secrets()
    {
        var all = string.Join(' ', ConfigLoader.ReadDefault("cards.json"), ConfigLoader.ReadDefault("categories.json"),
            ConfigLoader.ReadDefault("settings.json"));
        foreach (var banned in new[] { "microsoft", "google", "amazon", "aws", "apple", "gmail", "acme" })
            Assert.DoesNotMatch($@"\b{banned}\b", all.ToLowerInvariant());
    }
}

public class ConfigLoaderTests
{
    private const string Cats = """[{"id":"a","name":"A","color":"#112233"},{"id":"b","name":"B","color":"#445566"}]""";
    private const string OneCard = """[{"id":"c1","label":"L","categoryId":"a","difficulty":2}]""";

    [Fact]
    public void Missing_files_are_recreated_from_defaults()
    {
        using var dir = new TempDir();
        var cfg = ConfigLoader.Load(Path.Combine(dir.Path, "Data"));
        Assert.NotEmpty(cfg.Cards);
        Assert.True(File.Exists(Path.Combine(dir.Path, "Data", "cards.json")));
    }

    [Fact]
    public void Existing_files_are_not_overwritten()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "categories.json"), Cats);
        File.WriteAllText(Path.Combine(dir.Path, "cards.json"), OneCard);
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"), """{"cardsPerRound": 3}""");

        var cfg = ConfigLoader.Load(dir.Path);
        Assert.Single(cfg.Cards);
        Assert.Equal(3, cfg.Settings.CardsPerRound);
    }

    [Fact]
    public void Comments_and_trailing_commas_are_allowed()
    {
        var cfg = ConfigLoader.Parse(Cats, OneCard, "{ // hi\n \"cardsPerRound\": 7, }");
        Assert.Equal(7, cfg.Settings.CardsPerRound);
    }

    [Fact]
    public void Unknown_category_card_is_skipped_with_a_warning()
    {
        var cfg = ConfigLoader.Parse(Cats,
            """[{"id":"ok","label":"L","categoryId":"a"},{"id":"bad","label":"L","categoryId":"zzz"}]""", "{}");
        Assert.Single(cfg.Cards);
        Assert.Contains(cfg.Warnings, w => w.Contains("bad") && w.Contains("zzz"));
    }

    [Fact]
    public void Duplicate_ids_and_bad_colours_are_reported()
    {
        var cfg = ConfigLoader.Parse(
            """[{"id":"a","name":"A","color":"red"},{"id":"b","name":"B"},{"id":"a","name":"Dup"}]""",
            """[{"id":"x","label":"L","categoryId":"a"},{"id":"x","label":"L2","categoryId":"a"}]""", "{}");
        Assert.Equal(2, cfg.Categories.Count);
        Assert.Equal(3, cfg.Warnings.Count); // bad colour, duplicate category, duplicate card
    }

    [Theory]
    [InlineData("""[{"id":"a","name":"A"}]""")] // too few categories
    [InlineData("not json")]
    public void Unusable_categories_throw_a_clear_error(string cats) =>
        Assert.Throws<ConfigException>(() => ConfigLoader.Parse(cats, OneCard, "{}"));

    [Fact]
    public void No_valid_cards_throws() =>
        Assert.Throws<ConfigException>(() => ConfigLoader.Parse(Cats, "[]", "{}"));

    [Fact]
    public void Invalid_json_error_names_the_file()
    {
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Parse(Cats, OneCard, "{ oops"));
        Assert.Contains("settings.json", ex.Message);
    }

    [Fact]
    public void Bad_settings_fall_back_to_safe_values()
    {
        var cfg = ConfigLoader.Parse(Cats, OneCard,
            """{"cardsPerRound":0,"difficultyMix":[0,0,0],"adminPin":"","secondsPerCard":-4}""");
        Assert.Equal(15, cfg.Settings.CardsPerRound);
        Assert.Equal(GameEngine.DefaultMix, cfg.Settings.DifficultyMix);
        Assert.Equal("1234", cfg.Settings.AdminPin);
        Assert.Equal(0, cfg.Settings.SecondsPerCard);
    }
}

public class CsvExporterTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("", "")]
    public void Escape_quotes_and_defuses_formulas(string input, string expected) =>
        Assert.Equal(expected, CsvExporter.Escape(input));

    [Fact]
    public void Csv_has_header_one_row_per_result_and_lists_missed_cards()
    {
        var csv = CsvExporter.ToCsv([new PlayerResult
        {
            PlayerName = "Sam", EmployeeCode = "E123", Department = "Ops", Score = 50, CorrectCount = 4, TotalCards = 5, TotalSeconds = 12.34,
            Answers = [new CardAnswer { CardLabel = "Passwords", Correct = false }, new CardAnswer { CardLabel = "Ads", Correct = true }],
        }]);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Name,Employee code", lines[0]);
        Assert.StartsWith("Sam,E123,Ops,Yes,50,4,5,12.3,", lines[1]);
        Assert.EndsWith(",Passwords", lines[1]);
    }

    [Fact]
    public void File_is_written_with_a_utf8_bom_for_excel()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "out.csv");
        CsvExporter.WriteFile(path, [new PlayerResult { PlayerName = "Zoë" }]);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
    }
}

public class PlayerNameTests
{
    [Theory]
    [InlineData("  Sam   Sample ", "Sam Sample")]
    [InlineData("a\tb\nc", "a b c")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_cleans_whitespace(string? input, string expected) =>
        Assert.Equal(expected, PlayerName.Normalize(input));

    [Fact]
    public void Normalize_truncates_long_names() =>
        Assert.Equal(PlayerName.MaxLength, PlayerName.Normalize(new string('x', 500)).Length);
}
