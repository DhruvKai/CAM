using System.Windows.Media;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame;

/// <summary>Everything the screens share: loaded config and the results store.</summary>
public sealed class AppState
{
    private AppState(string configDir, GameConfig config, ResultStore store)
    {
        ConfigDir = configDir;
        Config = config;
        Store = store;
    }

    public string ConfigDir { get; }
    public GameConfig Config { get; private set; }
    public ResultStore Store { get; }

    public GameSettings Settings => Config.Settings;
    public IReadOnlyList<Category> Categories => Config.Categories;

    /// <exception cref="ConfigException">A config file is unusable.</exception>
    public static AppState Create()
    {
        var dir = AppPaths.ResolveConfigDir(AppContext.BaseDirectory);
        return new AppState(dir, ConfigLoader.Load(dir), new ResultStore(AppPaths.ResultsFile));
    }

    /// <summary>Re-reads the config files. On failure the previous config stays active and the exception is thrown.</summary>
    public void Reload() => Config = ConfigLoader.Load(ConfigDir);

    public Category? CategoryById(string? id) => Categories.FirstOrDefault(c => c.Id == id);

    public int OfficialAttemptsUsed(string name) => Store.CountOfficialAttempts(name);

    /// <summary>Whether the next round for this name would count for the leaderboard.</summary>
    public bool NextRoundIsOfficial(string name) =>
        Settings.MaxOfficialAttempts == 0 || OfficialAttemptsUsed(name) < Settings.MaxOfficialAttempts;
}

public static class Ui
{
    public static SolidColorBrush Brush(string hex, byte? alpha = null)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        if (alpha is { } a) color.A = a;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>Black or white, whichever reads better on the given background.</summary>
    public static SolidColorBrush TextOn(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
        return Brush(luminance > 0.6 ? "#0F172A" : "#FFFFFF");
    }
}
