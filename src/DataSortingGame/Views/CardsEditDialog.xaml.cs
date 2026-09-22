using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public partial class CardsEditDialog : Window
{
    private readonly AppState _state;
    private readonly ObservableCollection<Card> _cards;

    public CardsEditDialog(AppState state)
    {
        _state = state;
        InitializeComponent();

        CategoryColumn.ItemsSource = state.Categories;
        DifficultyColumn.ItemsSource = new[] { 1, 2, 3 };

        _cards = new ObservableCollection<Card>(state.Config.Cards.Select(Clone));
        Grid.ItemsSource = _cards;
    }

    private static Card Clone(Card c) => new()
    {
        Id = c.Id, Label = c.Label, Example = c.Example, CategoryId = c.CategoryId,
        Difficulty = c.Difficulty, Explanation = c.Explanation,
    };

    private void OnAddCard(object sender, RoutedEventArgs e)
    {
        var card = new Card { CategoryId = _state.Categories.FirstOrDefault()?.Id ?? "", Difficulty = 1 };
        _cards.Add(card);
        Grid.ScrollIntoView(card);
        Grid.SelectedItem = card;
    }

    private void OnDeleteSelected(object sender, RoutedEventArgs e)
    {
        var selected = Grid.SelectedItems.OfType<Card>().ToList();
        if (selected.Count == 0)
        {
            Info("Select one or more cards first.");
            return;
        }
        if (!Confirm($"Delete {selected.Count} card(s)? This cannot be undone.")) return;

        foreach (var card in selected) _cards.Remove(card);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit(DataGridEditingUnit.Cell, true);
        Grid.CommitEdit(DataGridEditingUnit.Row, true);

        if (_cards.Count == 0)
        {
            Error("Add at least one card.");
            return;
        }

        var errors = new List<string>();
        var seenIds = new HashSet<string>();
        foreach (var card in _cards)
        {
            card.Id = card.Id.Trim();
            card.Label = card.Label.Trim();
            card.Example = (card.Example ?? "").Trim();
            card.Explanation = (card.Explanation ?? "").Trim();
            card.Difficulty = Math.Clamp(card.Difficulty, 1, 3);

            var name = card.Id.Length > 0 ? card.Id : card.Label.Length > 0 ? card.Label : "(blank row)";
            if (card.Id.Length == 0 || card.Label.Length == 0)
                errors.Add($"'{name}': needs both an id and a label.");
            else if (!seenIds.Add(card.Id))
                errors.Add($"'{card.Id}': duplicate id.");

            if (_state.Categories.All(cat => cat.Id != card.CategoryId))
                errors.Add($"'{name}': choose a category.");
        }

        if (errors.Count > 0)
        {
            Error(string.Join("\n", errors));
            return;
        }

        try
        {
            ConfigLoader.SaveCards(_state.ConfigDir, _cards);
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
        MessageBox.Show(this, text, "Manage cards", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void Info(string text) => MessageBox.Show(this, text, "Manage cards", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Error(string text) => MessageBox.Show(this, text, "Manage cards", MessageBoxButton.OK, MessageBoxImage.Error);
}
