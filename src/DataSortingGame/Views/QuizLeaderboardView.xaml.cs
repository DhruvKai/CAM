using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public sealed record QuizLeaderRow(string Rank, string Name, string EmployeeCode, string Correct, string Time, string Score,
    Brush RankBrush, Brush RowBrush);

public partial class QuizLeaderboardView : UserControl
{
    private static readonly Brush Gold = Ui.Brush("#F5C542");
    private static readonly Brush Silver = Ui.Brush("#C0C7D1");
    private static readonly Brush Bronze = Ui.Brush("#CD7F32");
    private static readonly Brush Plain = Ui.Brush("#94A3B8");

    private readonly MainWindow _nav;

    public QuizLeaderboardView(AppState state, MainWindow nav)
    {
        _nav = nav;
        InitializeComponent();

        IReadOnlyList<QuizLeaderboardEntry> board;
        try { board = QuizLeaderboard.Rank(state.QuizStore.LoadAll()); }
        catch (IOException) { board = []; }

        var panel = (Brush)FindResource("PanelBrush");
        Rows.ItemsSource = board.Select(e => new QuizLeaderRow(
            e.Rank.ToString(CultureInfo.InvariantCulture),
            e.Result.PlayerName,
            e.Result.EmployeeCode,
            $"{e.Result.CorrectCount}/{e.Result.TotalQuestions}",
            $"{e.Result.TotalSeconds:0.0}s",
            e.Result.Score.ToString(CultureInfo.InvariantCulture),
            e.Rank switch { 1 => Gold, 2 => Silver, 3 => Bronze, _ => Plain },
            panel)).ToList();
        EmptyText.Visibility = board.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) => BackButton.Focus();
    }

    private void OnBack(object sender, RoutedEventArgs e) => _nav.ShowStart();
}
