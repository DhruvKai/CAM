using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;
using DataSortingGame.Views;

namespace DataSortingGame;

public partial class MainWindow : Window
{
    private const int GameIdleSeconds = 180;
    private const int ResultIdleSeconds = 60;
    private const int AdminIdleSeconds = 300;

    private readonly AppState _state;
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _lastInput = DateTime.UtcNow;
    private bool _allowClose;

    public MainWindow(AppState state)
    {
        _state = state;
        InitializeComponent();
        ApplySettings();

        PreviewMouseMove += (_, _) => Touch();
        PreviewMouseDown += (_, _) => OnActivity();
        PreviewTouchDown += (_, _) => OnActivity();
        PreviewKeyDown += (_, e) => { OnActivity(); if (e.Key == Key.Escape) OnEscape(); };
        _idleTimer.Tick += (_, _) => CheckIdle();
        _idleTimer.Start();

        Application.Current.SessionEnding += (_, _) => _allowClose = true;
        ShowStart();
    }

    /// <summary>Applies title and kiosk mode; called at startup and after the admin reloads the config.</summary>
    public void ApplySettings()
    {
        Title = _state.Settings.EventTitle;
        if (_state.Settings.KioskMode)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
        }
    }

    // ---- navigation -------------------------------------------------------------------------

    public void ShowStart() => Navigate(new StartView(_state, this));

    public void ShowLeaderboard(bool attract = false) => Navigate(new LeaderboardView(_state, this, attract));

    public void StartGame(string name, string department, string employeeCode)
    {
        var official = _state.NextRoundIsOfficial(name);
        var cards = GameEngine.DrawCards(_state.Config.Cards, _state.Settings.CardsPerRound, _state.Settings.DifficultyMix);
        var session = new GameSession(cards, _state.Settings);
        Navigate(new GameView(_state, this, session, name, department, employeeCode, official));
    }

    public void ShowQuizStart() => Navigate(new QuizStartView(_state, this));

    public void ShowQuizLeaderboard() => Navigate(new QuizLeaderboardView(_state, this));

    public void StartQuiz(string name, string employeeCode)
    {
        var official = _state.NextQuizRoundIsOfficial(name);
        var session = new QuizSession(_state.QuizQuestions, _state.QuizSettings, _state.QuizSettings.QuestionsPerRound);
        Navigate(new QuizGameView(_state, this, session, name, employeeCode, official, openMode: false));
    }

    /// <summary>The host-led "open quiz": a group plays together, no name is recorded and nothing is scored.</summary>
    public void StartOpenQuiz()
    {
        var session = new QuizSession(_state.QuizQuestions, _state.QuizSettings, totalTarget: null);
        Navigate(new QuizGameView(_state, this, session, "", "", official: false, openMode: true));
    }

    /// <summary>Saves a finished (or abandoned) quiz round and shows the quiz result screen.</summary>
    public void FinishQuizRound(QuizSession session, string name, string employeeCode, bool official)
    {
        if (session.Answered == 0)
        {
            // Nothing was answered, so nothing was revealed: don't burn the player's official attempt.
            ShowStart();
            return;
        }

        var result = session.ToResult(name, employeeCode, official);
        try
        {
            _state.QuizStore.Append(result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this,
                $"Your result could not be saved ({ex.Message}). Please tell the event organiser.",
                Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        IReadOnlyList<QuizResult> all;
        try { all = _state.QuizStore.LoadAll(); }
        catch (IOException) { all = [result]; }

        var board = QuizLeaderboard.Rank(all);
        Navigate(new QuizResultView(_state, this, result, QuizLeaderboard.RankOf(all, result.Id), board.Count));
    }

    /// <summary>Saves a finished (or abandoned) round and shows the result screen.</summary>
    public void FinishRound(GameSession session, string name, string department, string employeeCode, bool official)
    {
        if (session.Answered == 0)
        {
            // Nothing was answered, so nothing was revealed: don't burn the player's official attempt.
            ShowStart();
            return;
        }

        var result = session.ToResult(name, department, employeeCode, official);
        try
        {
            _state.Store.Append(result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this,
                $"Your result could not be saved ({ex.Message}). Please tell the event organiser.",
                Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        IReadOnlyList<PlayerResult> all;
        try { all = _state.Store.LoadAll(); }
        catch (IOException) { all = [result]; }

        var board = Leaderboard.Rank(all);
        Navigate(new ResultView(_state, this, result, Leaderboard.RankOf(all, result.Id), board.Count));
    }

    public void RequestAdmin()
    {
        var dialog = new PinDialog(_state.Settings.AdminPin) { Owner = this };
        if (dialog.ShowDialog() == true) Navigate(new AdminView(_state, this));
    }

    /// <summary>Closes the app for real (kiosk mode otherwise blocks Alt+F4).</summary>
    public void ExitApplication()
    {
        _allowClose = true;
        Close();
    }

    /// <summary>Escape is a global exit shortcut, so a stray press can't close the kiosk unconfirmed.</summary>
    private void OnEscape()
    {
        var result = MessageBox.Show(this, "Exit the game?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (result == MessageBoxResult.Yes) ExitApplication();
    }

    private void Navigate(UserControl view)
    {
        Host.Content = view;
        Touch();
        view.Focus();
    }

    // ---- kiosk behaviour --------------------------------------------------------------------

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_state.Settings.KioskMode && !_allowClose) e.Cancel = true;
        base.OnClosing(e);
    }

    private void Touch() => _lastInput = DateTime.UtcNow;

    private void OnActivity()
    {
        Touch();
        // Any deliberate input wakes the screensaver-style leaderboard.
        if (Host.Content is LeaderboardView { IsAttract: true }) ShowStart();
    }

    private void CheckIdle()
    {
        var idle = (DateTime.UtcNow - _lastInput).TotalSeconds;
        var attract = _state.Settings.IdleAttractSeconds;

        switch (Host.Content)
        {
            case StartView when attract > 0 && idle >= attract:
                ShowLeaderboard(attract: true);
                break;
            case GameView game when idle >= GameIdleSeconds:
                game.EndRound(); // walked away mid-round: record what was answered and free the kiosk
                break;
            case QuizGameView quiz when idle >= GameIdleSeconds:
                quiz.EndRound(); // walked away mid-round: record what was answered and free the kiosk
                break;
            case ResultView when idle >= ResultIdleSeconds:
            case QuizResultView when idle >= ResultIdleSeconds:
            case QuizStartView when idle >= ResultIdleSeconds:
            case QuizLeaderboardView when idle >= ResultIdleSeconds:
            case LeaderboardView { IsAttract: false } when idle >= ResultIdleSeconds:
                ShowStart();
                break;
            case AdminView when idle >= AdminIdleSeconds:
                ShowStart();
                break;
        }
    }
}
