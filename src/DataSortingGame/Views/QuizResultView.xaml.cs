using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DataSortingGame.Core.Models;

namespace DataSortingGame.Views;

public partial class QuizResultView : UserControl
{
    private readonly MainWindow _nav;

    public QuizResultView(AppState state, MainWindow nav, QuizResult result, int? rank, int boardSize)
    {
        _nav = nav;
        InitializeComponent();

        var ratio = result.TotalQuestions == 0 ? 0 : (double)result.CorrectCount / result.TotalQuestions;
        Headline.Text = ratio switch
        {
            >= 1.0 => "Perfect round!",
            >= 0.9 => "Data guardian!",
            >= 0.7 => "Sharp eye!",
            >= 0.5 => "Getting there",
            _ => "A good start",
        };
        var ended = result.Answers.Count < result.TotalQuestions;
        SubLine.Text = $"Well played, {result.PlayerName}." + (ended ? " The round ended early." : "");

        AddTile("Score", result.Score.ToString(CultureInfo.InvariantCulture), "points", "#38BDF8");
        AddTile("Correct", $"{result.CorrectCount} / {result.TotalQuestions}", $"{ratio:P0}", "#22C55E");
        AddTile("Time", FormatTime(result.TotalSeconds), "total", "#A78BFA");
        if (!result.IsOfficial) AddTile("Ranking", "Practice", "not on leaderboard", "#F59E0B");
        else if (rank is { } r) AddTile("Ranking", $"#{r}", $"of {boardSize} players", "#F59E0B");
        else AddTile("Ranking", "-", "a better round of yours is ranked", "#F59E0B");

        BuildReview(state, result);

        Loaded += (_, _) => DoneButton.Focus();
    }

    private static string FormatTime(double seconds) =>
        seconds >= 60 ? $"{(int)seconds / 60}m {(int)seconds % 60:00}s" : $"{seconds:0.0}s";

    private void AddTile(string title, string value, string caption, string accentHex)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 14, Foreground = (Brush)FindResource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = value, FontSize = 36, FontWeight = FontWeights.Bold, Foreground = Ui.Brush(accentHex), HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = caption, FontSize = 13, Foreground = (Brush)FindResource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
        Tiles.Children.Add(new Border
        {
            Background = (Brush)FindResource("PanelBrush"), CornerRadius = new CornerRadius(14), Padding = new Thickness(10, 12, 10, 12),
            Margin = new Thickness(6, 0, 6, 0), Child = panel,
        });
    }

    private void BuildReview(AppState state, QuizResult result)
    {
        var explanations = state.QuizQuestions.ToDictionary(q => q.Id, q => q.Explanation);
        var wrong = result.Answers.Where(a => !a.Correct).ToList();

        if (wrong.Count == 0)
        {
            ReviewHeader.Text = "Every question was answered correctly.";
            return;
        }

        ReviewHeader.Text = wrong.Count == 1 ? "1 question to review" : $"{wrong.Count} questions to review";
        foreach (var a in wrong)
        {
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = a.QuestionText, FontSize = 18, FontWeight = FontWeights.SemiBold });
            body.Children.Add(new TextBlock
            {
                Text = a.ChosenIndex is null ? "No answer was given in time." : "That wasn't the right answer.",
                FontSize = 14, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 4, 0, 0),
            });
            if (explanations.TryGetValue(a.QuestionId, out var why) && why.Length > 0)
                body.Children.Add(new TextBlock { Text = why, FontSize = 14, Foreground = Ui.Brush("#CBD5E1"), Margin = new Thickness(0, 6, 0, 0) });

            ReviewList.Children.Add(new Border
            {
                Background = (Brush)FindResource("PanelBrush"), CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 10, 16, 12),
                Margin = new Thickness(0, 0, 0, 8), Child = body,
            });
        }
    }

    private void OnLeaderboard(object sender, RoutedEventArgs e) => _nav.ShowQuizLeaderboard();

    private void OnDone(object sender, RoutedEventArgs e) => _nav.ShowStart();
}
