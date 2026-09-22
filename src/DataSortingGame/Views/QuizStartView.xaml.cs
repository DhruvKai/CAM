using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public partial class QuizStartView : UserControl
{
    private readonly AppState _state;
    private readonly MainWindow _nav;

    public QuizStartView(AppState state, MainWindow nav)
    {
        _state = state;
        _nav = nav;
        InitializeComponent();

        TitleText.Text = state.QuizSettings.EventTitle;
        Subtitle.Text = "Multiple choice questions on data handling and classification.";

        Loaded += (_, _) => NameBox.Focus();
    }

    private void OnOpenQuizChanged(object sender, RoutedEventArgs e)
    {
        var open = OpenQuizBox.IsChecked == true;
        NameFields.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        PlayButton.Content = open ? "Start open quiz" : "Start quiz";
        PlayButton.IsEnabled = open || PlayerName.Normalize(NameBox.Text).Length > 0;
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        var name = PlayerName.Normalize(NameBox.Text);
        PlayButton.IsEnabled = name.Length > 0;
        AttemptInfo.Text = name.Length == 0 ? "" : DescribeAttempt(name);
    }

    private string DescribeAttempt(string name)
    {
        var max = _state.QuizSettings.MaxOfficialAttempts;
        if (max == 0) return "Every round counts. Your best score is shown on the quiz leaderboard.";

        int used;
        try { used = _state.OfficialQuizAttemptsUsed(name); }
        catch (IOException) { return ""; }

        return used < max
            ? $"This is attempt {used + 1} of {max}. It counts for the quiz leaderboard."
            : "You have used your official attempt. You can still play for practice, but it will not be ranked.";
    }

    private void OnFieldKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender == NameBox) EcodeBox.Focus();
        else OnPlay(sender, e);
        e.Handled = true;
    }

    private void OnPlay(object sender, RoutedEventArgs e)
    {
        if (OpenQuizBox.IsChecked == true)
        {
            _nav.StartOpenQuiz();
            return;
        }

        var name = PlayerName.Normalize(NameBox.Text);
        if (name.Length == 0)
        {
            NameBox.Focus();
            return;
        }
        _nav.StartQuiz(name, EcodeBox.Text);
    }

    private void OnLeaderboard(object sender, RoutedEventArgs e) => _nav.ShowQuizLeaderboard();

    private void OnBack(object sender, RoutedEventArgs e) => _nav.ShowStart();
}
