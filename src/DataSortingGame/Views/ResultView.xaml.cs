using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DataSortingGame.Core.Models;

namespace DataSortingGame.Views;

public partial class ResultView : UserControl
{
    private readonly MainWindow _nav;

    public ResultView(AppState state, MainWindow nav, PlayerResult result, int? rank, int boardSize)
    {
        _nav = nav;
        InitializeComponent();

        var ratio = result.TotalCards == 0 ? 0 : (double)result.CorrectCount / result.TotalCards;
        Headline.Text = ratio switch
        {
            >= 1.0 => "Perfect round!",
            >= 0.9 => "Data guardian!",
            >= 0.7 => "Sharp eye!",
            >= 0.5 => "Getting there",
            _ => "A good start",
        };
        var ended = result.Answers.Count < result.TotalCards;
        SubLine.Text = $"Well played, {result.PlayerName}." + (ended ? " The round ended early." : "");

        AddTile("Score", result.Score.ToString(CultureInfo.InvariantCulture), "points", "#38BDF8");
        AddTile("Correct", $"{result.CorrectCount} / {result.TotalCards}", $"{ratio:P0}", "#22C55E");
        AddTile("Time", FormatTime(result.TotalSeconds), "total", "#A78BFA");
        if (!result.IsOfficial) AddTile("Ranking", "Practice", "not on leaderboard", "#F59E0B");
        else if (rank is { } r) AddTile("Ranking", $"#{r}", $"of {boardSize} players", "#F59E0B");
        else AddTile("Ranking", "-", "a better round of yours is ranked", "#F59E0B");

        BuildReview(state, result);

        Loaded += (_, _) =>
        {
            DoneButton.Focus();
            if (ratio >= 1.0 && result.TotalCards > 0) StartConfetti(state);
        };
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

    private void BuildReview(AppState state, PlayerResult result)
    {
        var explanations = state.Config.Cards.ToDictionary(c => c.Id, c => c.Explanation);
        var wrong = result.Answers.Where(a => !a.Correct).ToList();

        if (wrong.Count == 0)
        {
            ReviewHeader.Text = "Every card was sorted correctly.";
            return;
        }

        ReviewHeader.Text = wrong.Count == 1 ? "1 card to review" : $"{wrong.Count} cards to review";
        foreach (var a in wrong)
        {
            var line = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            line.Children.Add(new TextBlock { Text = "You chose ", FontSize = 14, Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center });
            line.Children.Add(Chip(state, a.ChosenCategoryId, "no answer (time ran out)"));
            line.Children.Add(new TextBlock { Text = "   Correct: ", FontSize = 14, Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center });
            line.Children.Add(Chip(state, a.CorrectCategoryId, a.CorrectCategoryId));

            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = a.CardLabel, FontSize = 18, FontWeight = FontWeights.SemiBold });
            body.Children.Add(line);
            if (explanations.TryGetValue(a.CardId, out var why) && why.Length > 0)
                body.Children.Add(new TextBlock { Text = why, FontSize = 14, Foreground = Ui.Brush("#CBD5E1"), Margin = new Thickness(0, 6, 0, 0) });

            ReviewList.Children.Add(new Border
            {
                Background = (Brush)FindResource("PanelBrush"), CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 10, 16, 12),
                Margin = new Thickness(0, 0, 0, 8), Child = body,
            });
        }
    }

    private static Border Chip(AppState state, string? categoryId, string fallback)
    {
        var cat = state.CategoryById(categoryId);
        var color = cat?.Color ?? "#475569";
        return new Border
        {
            Background = Ui.Brush(color), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 1, 8, 2),
            Child = new TextBlock { Text = cat?.Name ?? fallback, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Ui.TextOn(color) },
        };
    }

    private void OnLeaderboard(object sender, RoutedEventArgs e) => _nav.ShowLeaderboard();

    private void OnDone(object sender, RoutedEventArgs e) => _nav.ShowStart();

    // ---- confetti for a perfect round -------------------------------------------------------

    private void StartConfetti(AppState state)
    {
        var rng = Random.Shared;
        var palette = state.Categories.Select(c => c.Color).Append("#38BDF8").Append("#F5C542").ToArray();
        var pieces = new List<(Rectangle rect, double x, double speed, double sway, double spin, double phase)>();

        for (var i = 0; i < 110; i++)
        {
            var size = 7 + rng.NextDouble() * 8;
            var rect = new Rectangle { Width = size, Height = size * 1.6, Fill = Ui.Brush(palette[rng.Next(palette.Length)]), RenderTransformOrigin = new Point(0.5, 0.5) };
            Canvas.SetLeft(rect, 0);
            Canvas.SetTop(rect, -40);
            Confetti.Children.Add(rect);
            pieces.Add((rect, rng.NextDouble() * 1280, 140 + rng.NextDouble() * 200, 20 + rng.NextDouble() * 40, 90 + rng.NextDouble() * 360, rng.NextDouble() * 6.28));
        }

        var start = DateTime.UtcNow;
        var delays = pieces.Select(_ => rng.NextDouble() * 1.5).ToArray();

        void OnFrame(object? s, EventArgs e)
        {
            var t = (DateTime.UtcNow - start).TotalSeconds;
            var alive = false;
            for (var i = 0; i < pieces.Count; i++)
            {
                var (rect, x, speed, sway, spin, phase) = pieces[i];
                var local = t - delays[i];
                if (local < 0) { alive = true; continue; }
                var y = -30 + speed * local;
                if (y > 760) { rect.Visibility = Visibility.Collapsed; continue; }
                alive = true;
                Canvas.SetLeft(rect, x + Math.Sin(local * 2 + phase) * sway);
                Canvas.SetTop(rect, y);
                rect.RenderTransform = new RotateTransform(spin * local);
            }
            if (!alive) Stop();
        }

        void Stop()
        {
            CompositionTarget.Rendering -= OnFrame;
            Confetti.Children.Clear();
        }

        CompositionTarget.Rendering += OnFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }
}
