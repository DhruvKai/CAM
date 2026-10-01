using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Tests;

public class DefaultConfigTests
{
    private static readonly GameConfig Config = ConfigLoader.LoadDefaults();

    private static GameConfig LoadSamples() => ConfigLoader.Parse(
        ConfigLoader.ReadDefault("categories.json"), ConfigLoader.ReadDefault(ConfigLoader.CardsSampleFileName), "{}",
        ConfigLoader.ReadDefault(ConfigLoader.QuizQuestionsSampleFileName));

    [Fact]
    public void Shipped_defaults_load_with_no_warnings() => Assert.Empty(Config.Warnings);

    [Fact]
    public void Shipped_cards_and_questions_are_blank_for_the_organiser_to_fill()
    {
        Assert.Empty(Config.Cards);
        Assert.Empty(Config.QuizQuestions);
    }

    [Fact]
    public void Shipped_categories_are_the_four_classifications() =>
        Assert.Equal(["Public", "Internal", "Confidential", "Highly Restricted"], Config.Categories.Select(c => c.Name));

    [Fact]
    public void Games_are_named_label_legends_and_cyber_trivia()
    {
        Assert.Equal("Label Legends", Config.Settings.EventTitle);
        Assert.Equal("Cyber Trivia", Config.QuizSettings.EventTitle);
    }

    [Fact]
    public void Card_sample_is_valid_and_covers_every_category()
    {
        var cfg = LoadSamples();
        Assert.Empty(cfg.Warnings);
        foreach (var cat in cfg.Categories)
            Assert.Contains(cfg.Cards, c => c.CategoryId == cat.Id);
        Assert.All(cfg.Cards, c => Assert.False(string.IsNullOrWhiteSpace(c.Why), c.Id));
    }

    [Fact]
    public void Quiz_sample_is_valid_and_covers_every_difficulty()
    {
        var cfg = LoadSamples();
        Assert.Empty(cfg.Warnings);
        Assert.Equal([1, 2, 3], cfg.QuizQuestions.Select(q => q.Difficulty).Distinct().Order());
    }

    [Fact]
    public void Shipped_content_has_no_brand_or_real_looking_secrets()
    {
        var all = string.Join(' ', ConfigLoader.ReadDefault("cards.json"), ConfigLoader.ReadDefault("categories.json"),
            ConfigLoader.ReadDefault("settings.json"), ConfigLoader.ReadDefault(ConfigLoader.CardsSampleFileName),
            ConfigLoader.ReadDefault(ConfigLoader.QuizQuestionsSampleFileName));
        foreach (var banned in new[] { "microsoft", "google", "amazon", "aws", "apple", "gmail", "acme" })
            Assert.DoesNotMatch($@"\b{banned}\b", all.ToLowerInvariant());
    }
}

public class ConfigLoaderTests
{
    private const string Cats = """[{"id":"a","name":"A","color":"#112233"},{"id":"b","name":"B","color":"#445566"}]""";
    private const string OneCard = """[{"id":"c1","scenario":"S","categoryId":"a","why":"W"}]""";

    [Fact]
    public void Missing_files_are_recreated_from_defaults()
    {
        using var dir = new TempDir();
        var data = Path.Combine(dir.Path, "Data");
        var cfg = ConfigLoader.Load(data);
        Assert.Empty(cfg.Cards);
        Assert.True(File.Exists(Path.Combine(data, "cards.json")));
        Assert.True(File.Exists(Path.Combine(data, ConfigLoader.CardsSampleFileName)));
        Assert.True(File.Exists(Path.Combine(data, ConfigLoader.QuizQuestionsSampleFileName)));
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
    public void Card_fields_are_read()
    {
        var card = Assert.Single(ConfigLoader.Parse(Cats, OneCard, "{}").Cards);
        Assert.Equal(("c1", "S", "a", "W"), (card.Id, card.Scenario, card.CategoryId, card.Why));
    }

    [Fact]
    public void Category_can_be_given_by_its_name()
    {
        var cfg = ConfigLoader.Parse(
            """[{"id":"restricted","name":"Highly Restricted"},{"id":"public","name":"Public"}]""",
            """[{"id":"x","scenario":"S","categoryId":"Highly Restricted"},{"id":"y","scenario":"S","categoryId":"PUBLIC"}]""", "{}");
        Assert.Empty(cfg.Warnings);
        Assert.Equal(["restricted", "public"], cfg.Cards.Select(c => c.CategoryId));
    }

    [Fact]
    public void Unknown_category_card_is_skipped_with_a_warning()
    {
        var cfg = ConfigLoader.Parse(Cats,
            """[{"id":"ok","scenario":"S","categoryId":"a"},{"id":"bad","scenario":"S","categoryId":"zzz"}]""", "{}");
        Assert.Single(cfg.Cards);
        Assert.Contains(cfg.Warnings, w => w.Contains("bad") && w.Contains("zzz"));
    }

    [Fact]
    public void Card_without_a_scenario_is_skipped_with_a_warning()
    {
        var cfg = ConfigLoader.Parse(Cats, """[{"id":"old","label":"Old format","categoryId":"a"}]""", "{}");
        Assert.Empty(cfg.Cards);
        Assert.Contains(cfg.Warnings, w => w.Contains("old"));
    }

    [Fact]
    public void Duplicate_ids_and_bad_colours_are_reported()
    {
        var cfg = ConfigLoader.Parse(
            """[{"id":"a","name":"A","color":"red"},{"id":"b","name":"B"},{"id":"a","name":"Dup"}]""",
            """[{"id":"x","scenario":"S","categoryId":"a"},{"id":"x","scenario":"S2","categoryId":"a"}]""", "{}");
        Assert.Equal(2, cfg.Categories.Count);
        Assert.Equal(3, cfg.Warnings.Count); // bad colour, duplicate category, duplicate card
    }

    [Theory]
    [InlineData("""[{"id":"a","name":"A"}]""")] // too few categories
    [InlineData("not json")]
    public void Unusable_categories_throw_a_clear_error(string cats) =>
        Assert.Throws<ConfigException>(() => ConfigLoader.Parse(cats, OneCard, "{}"));

    [Fact]
    public void Empty_card_list_is_allowed() =>
        Assert.Empty(ConfigLoader.Parse(Cats, "[]", "{}").Cards);

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
            """{"cardsPerRound":0,"adminPin":"","secondsPerCard":-4,"eventTitle":" "}""");
        Assert.Equal(15, cfg.Settings.CardsPerRound);
        Assert.Equal("1234", cfg.Settings.AdminPin);
        Assert.Equal(0, cfg.Settings.SecondsPerCard);
        Assert.Equal("Label Legends", cfg.Settings.EventTitle);
    }

    [Theory]
    [InlineData("""["Only one"]""", 0)]
    [InlineData("""["A","B","C","D","E"]""", 0)]
    [InlineData("""["A",""]""", 0)]
    [InlineData("""["True","False"]""", 2)]
    public void Invalid_quiz_questions_are_skipped_with_a_warning(string options, int correctIndex)
    {
        var json = $$"""[{"id":"q","text":"T","options":{{options}},"correctIndex":{{correctIndex}}}]""";
        var cfg = ConfigLoader.Parse(Cats, OneCard, "{}", json);
        Assert.Empty(cfg.QuizQuestions);
        Assert.Contains(cfg.Warnings, w => w.Contains("'q'"));
    }

    [Fact]
    public void Two_option_quiz_question_is_accepted()
    {
        var cfg = ConfigLoader.Parse(Cats, OneCard, "{}",
            """[{"id":"tf","text":"True or false?","options":["True","False"],"correctIndex":1,"difficulty":3}]""");
        Assert.Equal(1, Assert.Single(cfg.QuizQuestions).CorrectIndex);
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
