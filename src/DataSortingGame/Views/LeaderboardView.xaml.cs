using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public sealed record LeaderRow(string Rank, string Name, string Department, string Correct, string Time, string Score,
    Brush RankBrush, Brush RowBrush);

public partial class LeaderboardView : UserControl
{
    private static readonly Brush Gold = Ui.Brush("#F5C542");
    private static readonly Brush Silver = Ui.Brush("#C0C7D1");
    private static readonly Brush Bronze = Ui.Brush("#CD7F32");
    private static readonly Brush Plain = Ui.Brush("#94A3B8");

    private readonly MainWindow _nav;
    private readonly DispatcherTimer _pager = new() { Interval = TimeSpan.FromSeconds(7) };

    public LeaderboardView(AppState state, MainWindow nav, bool attract)
    {
        _nav = nav;
        IsAttract = attract;
        InitializeComponent();

        IReadOnlyList<LeaderboardEntry> board;
        try { board = Leaderboard.Rank(state.Store.LoadAll()); }
        catch (IOException) { board = []; }

        var panel = (Brush)FindResource("PanelBrush");
        Rows.ItemsSource = board.Select(e => new LeaderRow(
            e.Rank.ToString(CultureInfo.InvariantCulture),
            e.Result.PlayerName,
            e.Result.Department,
            $"{e.Result.CorrectCount}/{e.Result.TotalCards}",
            $"{e.Result.TotalSeconds:0.0}s",
            e.Result.Score.ToString(CultureInfo.InvariantCulture),
            e.Rank switch { 1 => Gold, 2 => Silver, 3 => Bronze, _ => Plain },
            panel)).ToList();
        EmptyText.Visibility = board.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = attract ? Visibility.Collapsed : Visibility.Visible;
        AttractText.Visibility = attract ? Visibility.Visible : Visibility.Collapsed;

        if (attract)
        {
            // Page through a long list so everyone gets to see their name.
            _pager.Tick += (_, _) => NextPage();
            Loaded += (_, _) => _pager.Start();
            Unloaded += (_, _) => _pager.Stop();
        }
        else
        {
            Loaded += (_, _) => BackButton.Focus();
        }
    }

    /// <summary>True when shown automatically while the kiosk is idle; any input returns to the start screen.</summary>
    public bool IsAttract { get; }

    private void NextPage()
    {
        if (Scroller.ScrollableHeight <= 0) return;
        var next = Scroller.VerticalOffset + Scroller.ViewportHeight - 20;
        Scroller.ScrollToVerticalOffset(next >= Scroller.ScrollableHeight && Scroller.VerticalOffset >= Scroller.ScrollableHeight - 1 ? 0 : next);
    }

    private void OnBack(object sender, RoutedEventArgs e) => _nav.ShowStart();
}
