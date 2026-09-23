using System.Windows;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public partial class QuizQuestionsEditDialog : Window
{
    private sealed record QuizListItem(QuizQuestion Question, string Title, string Summary);

    private readonly AppState _state;
    private readonly List<QuizQuestion> _questions;
    private QuizQuestion? _selected;
    private bool _loading;
    private bool _suppressSelection;

    public QuizQuestionsEditDialog(AppState state)
    {
        _state = state;
        InitializeComponent();

        _questions = state.QuizQuestions.Select(Clone).ToList();
        RefreshList(_questions.FirstOrDefault());
    }

    private static QuizQuestion Clone(QuizQuestion q) => new()
    {
        Id = q.Id, Text = q.Text, Options = [.. Pad(q.Options)], CorrectIndex = q.CorrectIndex,
        Difficulty = q.Difficulty, Explanation = q.Explanation,
    };

    /// <summary>The editor always shows exactly 4 option boxes, so every question needs exactly 4 entries.</summary>
    private static List<string> Pad(List<string> options)
    {
        var padded = options.Take(4).ToList();
        while (padded.Count < 4) padded.Add("");
        return padded;
    }

    private static QuizListItem BuildItem(QuizQuestion q)
    {
        var diffText = q.Difficulty switch { 1 => "Easy", 3 => "Tricky", _ => "Medium" };
        var options = Pad(q.Options);
        var correct = options[Math.Clamp(q.CorrectIndex, 0, 3)];
        var title = q.Text.Length > 0 ? q.Text : "(untitled question)";
        var summary = diffText + (correct.Length > 0 ? $" · Correct: {correct}" : "") + (q.Id.Length > 0 ? $" · {q.Id}" : "");
        return new QuizListItem(q, title, summary);
    }

    /// <summary>Rebuilds the filtered list, keeping <paramref name="keepSelected"/> selected when still visible.</summary>
    private void RefreshList(QuizQuestion? keepSelected)
    {
        IEnumerable<QuizQuestion> query = _questions;
        var term = SearchBox.Text.Trim();
        if (term.Length > 0)
            query = query.Where(q => q.Text.Contains(term, StringComparison.OrdinalIgnoreCase)
                                      || q.Id.Contains(term, StringComparison.OrdinalIgnoreCase)
                                      || q.Options.Any(o => o.Contains(term, StringComparison.OrdinalIgnoreCase)));

        var items = query.Select(BuildItem).ToList();

        _suppressSelection = true;
        QuestionList.ItemsSource = items;
        QuestionList.SelectedItem = items.FirstOrDefault(i => ReferenceEquals(i.Question, keepSelected)) ?? items.FirstOrDefault();
        _suppressSelection = false;

        CountText.Text = $"{_questions.Count} question{(_questions.Count == 1 ? "" : "s")}";

        var newSelected = (QuestionList.SelectedItem as QuizListItem)?.Question;
        if (!ReferenceEquals(newSelected, _selected)) LoadDetail(newSelected);
    }

    private void LoadDetail(QuizQuestion? question)
    {
        _selected = question;
        if (question == null)
        {
            DetailScroll.Visibility = Visibility.Collapsed;
            EmptyDetailText.Visibility = Visibility.Visible;
            return;
        }

        _loading = true;
        IdBox.Text = question.Id;
        QuestionBox.Text = question.Text;
        var opts = Pad(question.Options);
        OptionABox.Text = opts[0];
        OptionBBox.Text = opts[1];
        OptionCBox.Text = opts[2];
        OptionDBox.Text = opts[3];
        CorrectARadio.IsChecked = question.CorrectIndex == 0;
        CorrectBRadio.IsChecked = question.CorrectIndex == 1;
        CorrectCRadio.IsChecked = question.CorrectIndex == 2;
        CorrectDRadio.IsChecked = question.CorrectIndex == 3;
        EasyRadio.IsChecked = question.Difficulty == 1;
        MediumRadio.IsChecked = question.Difficulty == 2;
        TrickyRadio.IsChecked = question.Difficulty == 3;
        ExplanationBox.Text = question.Explanation;
        DetailHeader.Text = question.Text.Length > 0 ? question.Text : "New question";
        _loading = false;

        DetailScroll.Visibility = Visibility.Visible;
        EmptyDetailText.Visibility = Visibility.Collapsed;
    }

    /// <summary>Copies the detail panel's current values back into the question being edited.</summary>
    private void CommitDetail()
    {
        if (_selected == null) return;
        _selected.Id = IdBox.Text;
        _selected.Text = QuestionBox.Text;
        _selected.Options = [OptionABox.Text, OptionBBox.Text, OptionCBox.Text, OptionDBox.Text];
        _selected.CorrectIndex = CorrectBRadio.IsChecked == true ? 1
            : CorrectCRadio.IsChecked == true ? 2
            : CorrectDRadio.IsChecked == true ? 3
            : 0;
        _selected.Difficulty = TrickyRadio.IsChecked == true ? 3 : MediumRadio.IsChecked == true ? 2 : 1;
        _selected.Explanation = ExplanationBox.Text;
    }

    private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshList(_selected);

    private void OnSelectQuestion(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        CommitDetail();
        LoadDetail((QuestionList.SelectedItem as QuizListItem)?.Question);
    }

    private void OnFieldLostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading || _selected == null) return;
        CommitDetail();
        RefreshList(_selected);
    }

    private void OnCorrectChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || _selected == null) return;
        CommitDetail();
        RefreshList(_selected);
    }

    private void OnDifficultyChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || _selected == null) return;
        CommitDetail();
        RefreshList(_selected);
    }

    private void OnAddQuestion(object sender, RoutedEventArgs e)
    {
        CommitDetail();
        var question = new QuizQuestion { Options = ["", "", "", ""], Difficulty = 2 };
        _questions.Add(question);
        SearchBox.Text = "";
        RefreshList(question);
        QuestionBox.Focus();
    }

    private void OnDeleteCurrent(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var name = _selected.Text.Length > 0 ? _selected.Text : _selected.Id.Length > 0 ? _selected.Id : "this question";
        if (!Confirm($"Delete '{name}'? This cannot be undone.")) return;

        _questions.Remove(_selected);
        _selected = null;
        RefreshList(null);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        CommitDetail();

        var errors = new List<string>();
        var seenIds = new HashSet<string>();
        foreach (var q in _questions)
        {
            q.Id = q.Id.Trim();
            q.Text = q.Text.Trim();
            q.Options = q.Options.Select(o => (o ?? "").Trim()).ToList();
            q.Explanation = (q.Explanation ?? "").Trim();
            q.Difficulty = Math.Clamp(q.Difficulty, 1, 3);

            var name = q.Id.Length > 0 ? q.Id : q.Text.Length > 0 ? q.Text : "(blank question)";
            if (q.Id.Length == 0 || q.Text.Length == 0)
                errors.Add($"'{name}': needs both an id and a question.");
            else if (!seenIds.Add(q.Id))
                errors.Add($"'{q.Id}': duplicate id.");

            if (q.Options.Count(o => o.Length == 0) > 0)
                errors.Add($"'{name}': all 4 options must have text.");
            if (q.CorrectIndex < 0 || q.CorrectIndex >= q.Options.Count)
                errors.Add($"'{name}': pick which option is correct.");
        }

        if (_questions.Count == 0) errors.Add("Add at least one question.");

        if (errors.Count > 0)
        {
            Error(string.Join("\n", errors));
            return;
        }

        try
        {
            ConfigLoader.SaveQuizQuestions(_state.ConfigDir, _questions);
        }
        catch (ConfigException ex)
        {
            Error(ex.Message);
            return;
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private bool Confirm(string text) =>
        MessageBox.Show(this, text, "Manage quiz questions", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void Error(string text) => MessageBox.Show(this, text, "Manage quiz questions", MessageBoxButton.OK, MessageBoxImage.Error);
}
