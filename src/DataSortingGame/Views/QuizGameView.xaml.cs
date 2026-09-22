using System.Diagnostics;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public partial class QuizGameView : UserControl
{
    private static readonly Brush Neutral = Ui.Brush("#334155");
    private static readonly Brush Good = Ui.Brush("#22C55E");
    private static readonly Brush Bad = Ui.Brush("#EF4444");

    private readonly AppState _state;
    private readonly MainWindow _nav;
    private readonly QuizSession _session;
    private readonly string _name;
    private readonly string _employeeCode;
    private readonly bool _official;
    private readonly bool _openMode;
    private readonly Button[] _optionButtons;
    private readonly Stopwatch _watch = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _answered = true; // true until the first question is shown, so early input is ignored
    private bool _finished;

    public QuizGameView(AppState state, MainWindow nav, QuizSession session, string name, string employeeCode, bool official, bool openMode)
    {
        _state = state;
        _nav = nav;
        _session = session;
        _name = name;
        _employeeCode = employeeCode;
        _official = official;
        _openMode = openMode;
        InitializeComponent();

        _optionButtons = [Option0, Option1, Option2, Option3];

        if (openMode)
        {
            PlayerText.Text = "Open quiz";
            ScorePanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            PlayerText.Text = name;
            PracticeTag.Visibility = official ? Visibility.Collapsed : Visibility.Visible;
        }
        ProgressBar.Visibility = session.Total is null ? Visibility.Collapsed : Visibility.Visible;
        if (session.Total is { } total) ProgressBar.Maximum = total;
        UpdateScore();

        _tick.Tick += (_, _) => OnTick();
        Loaded += (_, _) => { Focus(); _tick.Start(); PullNext(2); };
        Unloaded += (_, _) => _tick.Stop();
    }

    // ---- flow ---------------------------------------------------------------------------------

    private void PullNext(int? difficulty)
    {
        var q = _session.Next(difficulty);
        if (q is null)
        {
            ShowExhausted();
            return;
        }

        _answered = false;
        QuestionText.Text = q.Text;
        for (var i = 0; i < _optionButtons.Length; i++)
        {
            var btn = _optionButtons[i];
            btn.Content = i < q.Options.Count ? $"{(char)('A' + i)}.  {q.Options[i]}" : "";
            btn.Visibility = i < q.Options.Count ? Visibility.Visible : Visibility.Collapsed;
            btn.Background = Neutral;
            btn.Opacity = 1;
            btn.IsEnabled = !_openMode;
        }
        Options.Visibility = Visibility.Visible;
        RevealButton.Visibility = _openMode ? Visibility.Visible : Visibility.Collapsed;
        FeedbackPanel.Visibility = Visibility.Collapsed;
        FinishButton.Visibility = Visibility.Collapsed;
        SetDifficultyButtons(enabled: false);
        HighlightActiveDifficulty();

        ProgressText.Text = ProgressLabel();
        if (_session.Total is not null) ProgressBar.Value = _session.Answered;

        TimerBar.Value = 100;
        _watch.Restart();
    }

    private string ProgressLabel() =>
        _session.Total is { } total ? $"Question {_session.Answered + 1} of {total}" : $"Question {_session.Answered + 1}";

    private void ShowExhausted()
    {
        Options.Visibility = Visibility.Collapsed;
        RevealButton.Visibility = Visibility.Collapsed;
        QuestionText.Text = "No more questions are available. Add more in Admin, or finish up here.";
        SetDifficultyButtons(enabled: false);
        FinishButton.Content = _openMode ? "Back to start" : "See results";
        FinishButton.Visibility = Visibility.Visible;
    }

    private void OnOptionClick(object sender, RoutedEventArgs e)
    {
        if (_openMode) return;
        Answer(int.Parse((string)((Button)sender).Tag));
    }

    private void OnReveal(object sender, RoutedEventArgs e) => Answer(null);

    private void Answer(int? chosenIndex)
    {
        if (_answered || _session.Current is null) return;
        _answered = true;
        _watch.Stop();

        var seconds = _watch.Elapsed.TotalSeconds;
        var outcome = _session.Submit(chosenIndex, seconds);
        HighlightOptions(outcome.CorrectIndex, chosenIndex);

        if (!_openMode)
        {
            UpdateScore();
            if (_state.QuizSettings.SoundEnabled)
                (outcome.Correct ? SystemSounds.Asterisk : SystemSounds.Exclamation).Play();
        }

        ShowFeedback(outcome, chosenIndex is null);
        RevealButton.Visibility = Visibility.Collapsed;

        if (!_openMode && _session.IsFinished)
        {
            FinishButton.Content = "See results";
            FinishButton.Visibility = Visibility.Visible;
        }
        else
        {
            SetDifficultyButtons(enabled: true);
        }
    }

    private void OnPickDifficulty(object sender, RoutedEventArgs e) => PullNext(int.Parse((string)((Button)sender).Tag));

    private void OnTick()
    {
        var limit = _state.QuizSettings.SecondsPerQuestion;
        if (_answered) return;

        var elapsed = _watch.Elapsed.TotalSeconds;
        TimerBar.Value = Math.Max(0, 100 * (1 - elapsed / limit));
        if (elapsed >= limit) Answer(null);
    }

    /// <summary>Records whatever has been answered and moves to the results (also used when the host stops early).</summary>
    public void EndRound()
    {
        if (_finished) return;
        _finished = true;
        _tick.Stop();
        if (_openMode) { _nav.ShowStart(); return; }
        _nav.FinishQuizRound(_session, _name, _employeeCode, _official);
    }

    private void OnFinish(object sender, RoutedEventArgs e) => EndRound();

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        var text = _openMode ? "End this quiz now?" : "End this round now? Your score so far will be recorded.";
        var result = MessageBox.Show(Window.GetWindow(this), text, _state.QuizSettings.EventTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (result == MessageBoxResult.Yes) EndRound();
    }

    // ---- feedback / visuals ---------------------------------------------------------------------

    private void ShowFeedback(QuizAnswerOutcome o, bool timedOutOrRevealed)
    {
        if (_openMode)
        {
            FeedbackTitle.Text = $"Answer: {CorrectOptionText(o.CorrectIndex)}";
            FeedbackTitle.Foreground = (Brush)FindResource("AccentBrush");
        }
        else
        {
            FeedbackTitle.Text = o.Correct
                ? (o.Bonus > 0 ? $"Correct!  +{o.Points} points (includes +{o.Bonus} bonus)" : $"Correct!  +{o.Points} points")
                : timedOutOrRevealed ? $"Time is up. The answer is {CorrectOptionText(o.CorrectIndex)}." : $"Not quite. The answer is {CorrectOptionText(o.CorrectIndex)}.";
            FeedbackTitle.Foreground = (Brush)FindResource(o.Correct ? "GoodBrush" : "BadBrush");
        }
        FeedbackBody.Text = o.Explanation;
        FeedbackPanel.BorderBrush = _openMode ? (Brush)FindResource("AccentBrush") : (Brush)FindResource(o.Correct ? "GoodBrush" : "BadBrush");
        FeedbackPanel.Visibility = Visibility.Visible;
    }

    private string CorrectOptionText(int correctIndex)
    {
        var q = _session.Current;
        return q is not null && correctIndex >= 0 && correctIndex < q.Options.Count
            ? $"{(char)('A' + correctIndex)}. {q.Options[correctIndex]}"
            : "-";
    }

    private void HighlightOptions(int correctIndex, int? chosenIndex)
    {
        for (var i = 0; i < _optionButtons.Length; i++)
        {
            var btn = _optionButtons[i];
            btn.IsEnabled = false;
            if (i == correctIndex) btn.Background = Good;
            else if (i == chosenIndex) btn.Background = Bad;
            else btn.Opacity = 0.5;
        }
    }

    private void SetDifficultyButtons(bool enabled)
    {
        EasyButton.IsEnabled = enabled;
        MediumButton.IsEnabled = enabled;
        TrickyButton.IsEnabled = enabled;
    }

    private void HighlightActiveDifficulty()
    {
        foreach (var wrap in new[] { EasyWrap, MediumWrap, TrickyWrap }) wrap.BorderBrush = Brushes.Transparent;
        var active = _session.Difficulty switch { 1 => EasyWrap, 3 => TrickyWrap, _ => MediumWrap };
        active.BorderBrush = Brushes.White;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_openMode || _answered) return;
        var index = e.Key switch
        {
            >= Key.D1 and <= Key.D4 => e.Key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad4 => e.Key - Key.NumPad1,
            _ => -1,
        };
        if (index >= 0 && index < _optionButtons.Length && _optionButtons[index].IsEnabled)
        {
            Answer(index);
            e.Handled = true;
        }
    }

    private void UpdateScore()
    {
        ScoreText.Text = $"Score {_session.Score}";
        StreakText.Text = _session.Streak >= 2 ? $"Streak x{_session.Streak}" : "";
    }
}
