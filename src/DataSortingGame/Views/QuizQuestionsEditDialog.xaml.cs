using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public partial class QuizQuestionsEditDialog : Window
{
    private readonly AppState _state;
    private readonly ObservableCollection<QuizQuestion> _questions;

    public QuizQuestionsEditDialog(AppState state)
    {
        _state = state;
        InitializeComponent();

        CorrectColumn.ItemsSource = new[] { 1, 2, 3, 4 };
        DifficultyColumn.ItemsSource = new[] { 1, 2, 3 };

        _questions = new ObservableCollection<QuizQuestion>(state.QuizQuestions.Select(Clone));
        Grid.ItemsSource = _questions;
    }

    private static QuizQuestion Clone(QuizQuestion q) => new()
    {
        Id = q.Id, Text = q.Text, Options = [.. Pad(q.Options)], CorrectIndex = q.CorrectIndex,
        Difficulty = q.Difficulty, Explanation = q.Explanation,
    };

    /// <summary>The grid always shows exactly 4 option columns, so every row needs exactly 4 entries.</summary>
    private static List<string> Pad(List<string> options)
    {
        var padded = options.Take(4).ToList();
        while (padded.Count < 4) padded.Add("");
        return padded;
    }

    private void OnAddQuestion(object sender, RoutedEventArgs e)
    {
        var question = new QuizQuestion { Options = ["", "", "", ""], Difficulty = 2, CorrectOption = 1 };
        _questions.Add(question);
        Grid.ScrollIntoView(question);
        Grid.SelectedItem = question;
    }

    private void OnDeleteSelected(object sender, RoutedEventArgs e)
    {
        var selected = Grid.SelectedItems.OfType<QuizQuestion>().ToList();
        if (selected.Count == 0)
        {
            Info("Select one or more questions first.");
            return;
        }
        if (!Confirm($"Delete {selected.Count} question(s)? This cannot be undone.")) return;

        foreach (var question in selected) _questions.Remove(question);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit(DataGridEditingUnit.Cell, true);
        Grid.CommitEdit(DataGridEditingUnit.Row, true);

        var errors = new List<string>();
        var seenIds = new HashSet<string>();
        foreach (var q in _questions)
        {
            q.Id = q.Id.Trim();
            q.Text = q.Text.Trim();
            q.Options = q.Options.Select(o => (o ?? "").Trim()).ToList();
            q.Explanation = (q.Explanation ?? "").Trim();
            q.Difficulty = Math.Clamp(q.Difficulty, 1, 3);

            var name = q.Id.Length > 0 ? q.Id : q.Text.Length > 0 ? q.Text : "(blank row)";
            if (q.Id.Length == 0 || q.Text.Length == 0)
                errors.Add($"'{name}': needs both an id and a question.");
            else if (!seenIds.Add(q.Id))
                errors.Add($"'{q.Id}': duplicate id.");

            if (q.Options.Count(o => o.Length == 0) > 0)
                errors.Add($"'{name}': all 4 options must have text.");
            if (q.CorrectIndex < 0 || q.CorrectIndex >= q.Options.Count)
                errors.Add($"'{name}': pick which option (1-4) is correct.");
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

    private void Info(string text) => MessageBox.Show(this, text, "Manage quiz questions", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Error(string text) => MessageBox.Show(this, text, "Manage quiz questions", MessageBoxButton.OK, MessageBoxImage.Error);
}
