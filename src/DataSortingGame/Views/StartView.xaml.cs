using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public partial class StartView : UserControl
{
    private readonly AppState _state;
    private readonly MainWindow _nav;

    public StartView(AppState state, MainWindow nav)
    {
        _state = state;
        _nav = nav;
        InitializeComponent();

        var s = state.Settings;
        TitleText.Text = s.EventTitle;
        Subtitle.Text = "Sort each piece of data into the right box. Learn why as you go.";
        HowTo.Text = $"{Math.Min(s.CardsPerRound, state.Config.Cards.Count)} cards per round. Drag a card into a box, click a box, or press its number key.";

        Legend.Columns = state.Categories.Count;
        for (var i = 0; i < state.Categories.Count; i++)
            Legend.Children.Add(BuildLegendItem(state.Categories[i], i));

        Loaded += (_, _) => NameBox.Focus();
    }

    private static Border BuildLegendItem(Core.Models.Category cat, int index)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = $"{index + 1}  {cat.Name}", FontSize = 17, FontWeight = FontWeights.Bold,
            Foreground = Ui.TextOn(cat.Color),
        });
        panel.Children.Add(new TextBlock
        {
            Text = cat.Description, FontSize = 12.5, Margin = new Thickness(0, 3, 0, 0),
            Foreground = Ui.TextOn(cat.Color), Opacity = 0.9,
        });
        return new Border
        {
            Background = Ui.Brush(cat.Color), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10, 14, 12),
            Margin = new Thickness(5, 0, 5, 0), Child = panel,
        };
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        var name = PlayerName.Normalize(NameBox.Text);
        PlayButton.IsEnabled = name.Length > 0;
        AttemptInfo.Text = name.Length == 0 ? "" : DescribeAttempt(name);
    }

    private string DescribeAttempt(string name)
    {
        var max = _state.Settings.MaxOfficialAttempts;
        if (max == 0) return "Every round counts. Your best score is shown on the leaderboard.";

        int used;
        try { used = _state.OfficialAttemptsUsed(name); }
        catch (IOException) { return ""; }

        return used < max
            ? $"This is attempt {used + 1} of {max}. It counts for the leaderboard."
            : "You have used your official attempt. You can still play for practice, but it will not be ranked.";
    }

    private void OnFieldKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender == NameBox) DeptBox.Focus();
        else OnPlay(sender, e);
        e.Handled = true;
    }

    private void OnPlay(object sender, RoutedEventArgs e)
    {
        var name = PlayerName.Normalize(NameBox.Text);
        if (name.Length == 0)
        {
            NameBox.Focus();
            return;
        }
        _nav.StartGame(name, DeptBox.Text);
    }

    private void OnLeaderboard(object sender, RoutedEventArgs e) => _nav.ShowLeaderboard();

    private void OnAdmin(object sender, RoutedEventArgs e) => _nav.RequestAdmin();
}
