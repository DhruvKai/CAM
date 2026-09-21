using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;
using Microsoft.Win32;

namespace DataSortingGame.Views;

public sealed record AdminRow(Guid Id, string Name, string Department, string Counts, string Score, string Correct, string Time, string Played);

public partial class AdminView : UserControl
{
    private readonly AppState _state;
    private readonly MainWindow _nav;

    public AdminView(AppState state, MainWindow nav)
    {
        _state = state;
        _nav = nav;
        InitializeComponent();
        Refresh();
        ShowConfigInfo();
    }

    private Window? Owner => Window.GetWindow(this);

    private void Refresh()
    {
        IReadOnlyList<PlayerResult> all;
        try { all = _state.Store.LoadAll(); }
        catch (IOException ex)
        {
            Error($"The results file could not be read: {ex.Message}");
            all = [];
        }

        Grid.ItemsSource = all.OrderByDescending(r => r.PlayedAtUtc).Select(r => new AdminRow(
            r.Id, r.PlayerName, r.Department, r.IsOfficial ? "Yes" : "Practice",
            r.Score.ToString(CultureInfo.InvariantCulture), $"{r.CorrectCount}/{r.TotalCards}", $"{r.TotalSeconds:0.0}s",
            r.PlayedAtUtc.ToLocalTime().ToString("dd MMM HH:mm", CultureInfo.InvariantCulture))).ToList();

        var players = all.Select(r => PlayerName.Key(r.PlayerName)).Distinct().Count();
        Summary.Text = $"{all.Count} {(all.Count == 1 ? "round" : "rounds")} from {players} {(players == 1 ? "player" : "players")}";

        var missed = ResultStats.MostMissed(all);
        MissedText.Text = missed.Count == 0
            ? "Not enough data yet."
            : string.Join("\n", missed.Select(m => $"{m.CardLabel}: {m.MissPercent:0}% missed ({m.Misses}/{m.Attempts})"));
    }

    private void ShowConfigInfo()
    {
        var warnings = _state.Config.Warnings;
        WarningText.Text = warnings.Count == 0 ? "None." : string.Join("\n\n", warnings);
        PathText.Text = $"Results:\n{_state.Store.Path}\n\nSettings folder:\n{_state.ConfigDir}\n\n" +
                        $"{_state.Config.Cards.Count} cards, {_state.Categories.Count} categories loaded.";
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export results",
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"results-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(Owner) != true) return;

        try
        {
            CsvExporter.WriteFile(dialog.FileName, _state.Store.LoadAll());
            Info($"Exported to:\n{dialog.FileName}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error($"Could not write the file. Is it open in another program?\n\n{ex.Message}");
        }
    }

    private void OnDeleteSelected(object sender, RoutedEventArgs e)
    {
        var selected = Grid.SelectedItems.OfType<AdminRow>().ToList();
        if (selected.Count == 0)
        {
            Info("Select one or more rows first.");
            return;
        }
        if (!Confirm($"Delete {selected.Count} selected result(s)? This cannot be undone.")) return;

        try { foreach (var row in selected) _state.Store.Delete(row.Id); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error(ex.Message); }
        Refresh();
    }

    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        if (!Confirm("Delete ALL results and reset the leaderboard? This cannot be undone.\n\nTip: export a CSV first.")) return;

        try { _state.Store.Clear(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error(ex.Message); }
        Refresh();
    }

    private void OnReload(object sender, RoutedEventArgs e)
    {
        try
        {
            _state.Reload();
        }
        catch (ConfigException ex)
        {
            Error($"The settings were not reloaded. The previous settings stay active.\n\n{ex.Message}");
            return;
        }
        _nav.ApplySettings();
        ShowConfigInfo();
        Info("Settings reloaded. They apply to the next round.");
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_state.ConfigDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Error(ex.Message);
        }
    }

    private void OnBack(object sender, RoutedEventArgs e) => _nav.ShowStart();

    private void OnExit(object sender, RoutedEventArgs e)
    {
        if (Confirm("Close the game?")) _nav.ExitApplication();
    }

    private bool Confirm(string text) =>
        MessageBox.Show(Owner, text, "Admin", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void Info(string text) => MessageBox.Show(Owner, text, "Admin", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Error(string text) => MessageBox.Show(Owner, text, "Admin", MessageBoxButton.OK, MessageBoxImage.Error);
}
