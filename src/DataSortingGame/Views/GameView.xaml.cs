using System.Diagnostics;
using System.Media;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public partial class GameView : UserControl
{
    private const string DragFormat = "DataSortingGame.Card";

    private readonly AppState _state;
    private readonly MainWindow _nav;
    private readonly GameSession _session;
    private readonly string _name;
    private readonly string _department;
    private readonly string _employeeCode;
    private readonly bool _official;
    private readonly IReadOnlyList<Category> _categories;
    private readonly Dictionary<string, Border> _boxes = [];
    private readonly Stopwatch _cardWatch = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _autoAdvance = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private Point? _dragStart;
    private bool _answered = true; // true until the first card is shown, so early input is ignored
    private bool _finished;

    public GameView(AppState state, MainWindow nav, GameSession session, string name, string department, string employeeCode, bool official)
    {
        _state = state;
        _nav = nav;
        _session = session;
        _name = name;
        _department = department;
        _employeeCode = employeeCode;
        _official = official;
        _categories = state.Categories;
        InitializeComponent();

        PlayerText.Text = name;
        PracticeTag.Visibility = official ? Visibility.Collapsed : Visibility.Visible;
        ProgressBar.Maximum = session.Total;
        TimerBar.Visibility = state.Settings.SecondsPerCard > 0 ? Visibility.Visible : Visibility.Hidden;
        UpdateScore();
        BuildBoxes();

        _tick.Tick += (_, _) => OnTick();
        _autoAdvance.Tick += (_, _) => { _autoAdvance.Stop(); Advance(); };
        Loaded += (_, _) => { Focus(); _tick.Start(); ShowCard(); };
        Unloaded += (_, _) => { _tick.Stop(); _autoAdvance.Stop(); };
    }

    // ---- setup ------------------------------------------------------------------------------

    private void BuildBoxes()
    {
        Boxes.Columns = _categories.Count;
        for (var i = 0; i < _categories.Count; i++)
        {
            var cat = _categories[i];
            var header = new Border
            {
                Background = Ui.Brush(cat.Color), CornerRadius = new CornerRadius(8, 8, 0, 0), Padding = new Thickness(10, 10, 10, 10),
                MinHeight = 58,
                Child = new TextBlock
                {
                    Text = $"{i + 1}  {cat.Name}", FontSize = HeaderFontSize(cat.Name), FontWeight = FontWeights.Bold,
                    Foreground = Ui.TextOn(cat.Color), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                },
            };
            var content = new DockPanel();
            DockPanel.SetDock(header, Dock.Top);
            content.Children.Add(header);

            var box = new Border
            {
                BorderBrush = Ui.Brush(cat.Color), BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(12),
                Background = (Brush)FindResource("PanelBrush"), Margin = new Thickness(5, 0, 5, 0), AllowDrop = true,
                Cursor = Cursors.Hand, Child = content, Tag = cat,
            };
            AutomationProperties.SetName(box, cat.Name);

            box.DragEnter += (_, e) => OnBoxDrag(box, cat, e, entering: true);
            box.DragOver += (_, e) => OnBoxDrag(box, cat, e, entering: true);
            box.DragLeave += (_, _) => SetBoxHover(box, cat, false);
            box.Drop += (_, e) =>
            {
                SetBoxHover(box, cat, false);
                if (e.Data.GetDataPresent(DragFormat)) Answer(cat.Id);
                e.Handled = true;
            };
            box.MouseLeftButtonUp += (_, _) => Answer(cat.Id);
            box.MouseEnter += (_, _) => { if (!_answered) SetBoxHover(box, cat, true); };
            box.MouseLeave += (_, _) => SetBoxHover(box, cat, false);

            _boxes[cat.Id] = box;
            Boxes.Children.Add(box);
        }
    }

    /// <summary>Longer category names (e.g. "Highly Restricted") get a smaller font so they fit the box header cleanly.</summary>
    private static double HeaderFontSize(string name) => name.Length switch
    {
        <= 8 => 22,
        <= 14 => 19,
        _ => 17,
    };

    private void OnBoxDrag(Border box, Category cat, DragEventArgs e, bool entering)
    {
        var accept = !_answered && e.Data.GetDataPresent(DragFormat);
        e.Effects = accept ? DragDropEffects.Move : DragDropEffects.None;
        if (accept) SetBoxHover(box, cat, entering);
        e.Handled = true;
    }

    private void SetBoxHover(Border box, Category cat, bool on) =>
        box.Background = on ? Ui.Brush(cat.Color, 0x55) : (Brush)FindResource("PanelBrush");

    // ---- round flow -------------------------------------------------------------------------

    private void ShowCard()
    {
        if (_session.Current is not { } card) return;

        _autoAdvance.Stop();
        _answered = false;
        CardLabel.Text = card.Label;
        CardExample.Text = card.Example;
        CardBorder.Opacity = 1;
        ProgressText.Text = $"Card {_session.Answered + 1} of {_session.Total}";
        ProgressBar.Value = _session.Answered;
        FeedbackPanel.Visibility = Visibility.Collapsed;
        DragHint.Visibility = Visibility.Visible;
        TimerBar.Value = 100;
        ResetBoxes();

        // FillBehavior.Stop: once finished, the animation lets go so later Opacity changes take effect.
        var pop = new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new BackEase { Amplitude = 0.4 }, FillBehavior = FillBehavior.Stop,
        };
        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        CardBorder.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });

        _cardWatch.Restart();
    }

    private void Answer(string? categoryId)
    {
        if (_answered || _session.Current is null) return;
        _answered = true;
        _cardWatch.Stop();
        _dragStart = null;

        var seconds = _cardWatch.Elapsed.TotalSeconds;
        var outcome = _session.Submit(categoryId, seconds);
        UpdateScore();
        ProgressBar.Value = _session.Answered;
        HighlightAnswer(categoryId, outcome);

        if (_state.Settings.SoundEnabled)
            (outcome.Correct ? SystemSounds.Asterisk : SystemSounds.Exclamation).Play();

        if (_state.Settings.ShowFeedback)
            ShowFeedback(outcome);
        else
            _autoAdvance.Start();
    }

    private void Advance()
    {
        if (_finished) return;
        if (_session.IsFinished) EndRound();
        else ShowCard();
    }

    /// <summary>Records whatever has been answered and moves to the results (also used when a player walks away).</summary>
    public void EndRound()
    {
        if (_finished) return;
        _finished = true;
        _tick.Stop();
        _autoAdvance.Stop();
        _nav.FinishRound(_session, _name, _department, _employeeCode, _official);
    }

    private void OnTick()
    {
        var limit = _state.Settings.SecondsPerCard;
        if (limit <= 0 || _answered) return;

        var elapsed = _cardWatch.Elapsed.TotalSeconds;
        TimerBar.Value = Math.Max(0, 100 * (1 - elapsed / limit));
        if (elapsed >= limit) Answer(null);
    }

    // ---- feedback ---------------------------------------------------------------------------

    private void ShowFeedback(AnswerOutcome o)
    {
        var correctName = _state.CategoryById(o.CorrectCategoryId)?.Name ?? o.CorrectCategoryId;
        var good = o.Correct;

        FeedbackTitle.Text = good
            ? (o.Bonus > 0 ? $"Correct!  +{o.Points} points (includes +{o.Bonus} bonus)" : $"Correct!  +{o.Points} points")
            : o.TimedOut ? $"Time is up. This is {correctName}." : $"Not quite. This is {correctName}.";
        FeedbackTitle.Foreground = (Brush)FindResource(good ? "GoodBrush" : "BadBrush");
        FeedbackBody.Text = o.Explanation;
        FeedbackPanel.BorderBrush = (Brush)FindResource(good ? "GoodBrush" : "BadBrush");
        NextButton.Content = _session.IsFinished ? "See results" : "Next card";

        DragHint.Visibility = Visibility.Collapsed;
        FeedbackPanel.Visibility = Visibility.Visible;
        NextButton.Focus();
    }

    private void HighlightAnswer(string? chosenId, AnswerOutcome o)
    {
        foreach (var (id, box) in _boxes)
        {
            var cat = (Category)box.Tag;
            box.Background = (Brush)FindResource("PanelBrush");
            if (id == o.CorrectCategoryId)
            {
                box.BorderThickness = new Thickness(6);
                box.BorderBrush = Brushes.White;
                box.Effect = new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString(cat.Color), BlurRadius = 26, ShadowDepth = 0, Opacity = 0.9 };
            }
            else if (id == chosenId)
            {
                box.BorderThickness = new Thickness(6);
                box.BorderBrush = (Brush)FindResource("BadBrush");
                box.Opacity = 0.65;
            }
            else
            {
                box.Opacity = 0.45;
            }
        }
        CardBorder.Opacity = 0.55;
    }

    private void ResetBoxes()
    {
        foreach (var box in _boxes.Values)
        {
            var cat = (Category)box.Tag;
            box.BorderThickness = new Thickness(3);
            box.BorderBrush = Ui.Brush(cat.Color);
            box.Background = (Brush)FindResource("PanelBrush");
            box.Effect = null;
            box.Opacity = 1;
        }
    }

    private void UpdateScore()
    {
        ScoreText.Text = $"Score {_session.Score}";
        StreakText.Text = _session.Streak >= 2 ? $"Streak x{_session.Streak}" : "";
    }

    // ---- input ------------------------------------------------------------------------------

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(null);

    private void OnCardMouseMove(object sender, MouseEventArgs e)
    {
        if (_answered || _dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return;

        var now = e.GetPosition(null);
        if (Math.Abs(now.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(now.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _dragStart = null;
        CardBorder.Opacity = 0.5;
        DragDrop.DoDragDrop(CardBorder, new DataObject(DragFormat, _session.Current?.Id ?? ""), DragDropEffects.Move);
        if (!_answered) CardBorder.Opacity = 1; // dropped outside every box: keep the card in play
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var index = e.Key switch
        {
            >= Key.D1 and <= Key.D9 => e.Key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad1,
            _ => -1,
        };

        if (index >= 0 && index < _categories.Count && !_answered)
        {
            Answer(_categories[index].Id);
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Space && _answered && FeedbackPanel.Visibility == Visibility.Visible)
        {
            Advance();
            e.Handled = true;
        }
    }

    private void OnNext(object sender, RoutedEventArgs e) => Advance();

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(Window.GetWindow(this),
            "End this round now? Your score so far will be recorded.", _state.Settings.EventTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (result == MessageBoxResult.Yes) EndRound();
    }
}
