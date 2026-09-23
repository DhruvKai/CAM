using System.Windows;
using System.Windows.Media;
using DataSortingGame.Core.Models;
using DataSortingGame.Core.Services;

namespace DataSortingGame.Views;

public partial class CardsEditDialog : Window
{
    private sealed record CardListItem(Card Card, string Label, string Summary, SolidColorBrush ColorBrush);

    private readonly AppState _state;
    private readonly List<Card> _cards;
    private Card? _selected;
    private bool _loading;
    private bool _suppressSelection;

    public CardsEditDialog(AppState state)
    {
        _state = state;
        InitializeComponent();

        CategoryCombo.ItemsSource = state.Categories;
        _cards = state.Config.Cards.Select(Clone).ToList();
        RefreshList(_cards.FirstOrDefault());
    }

    private static Card Clone(Card c) => new()
    {
        Id = c.Id, Label = c.Label, Example = c.Example, CategoryId = c.CategoryId,
        Difficulty = c.Difficulty, Explanation = c.Explanation,
    };

    private CardListItem BuildItem(Card c)
    {
        var category = _state.Categories.FirstOrDefault(cat => cat.Id == c.CategoryId);
        var diffText = c.Difficulty switch { 1 => "Easy", 3 => "Tricky", _ => "Medium" };
        var label = c.Label.Length > 0 ? c.Label : "(untitled card)";
        var summary = $"{category?.Name ?? "No category"} · {diffText}" + (c.Id.Length > 0 ? $" · {c.Id}" : "");
        return new CardListItem(c, label, summary, Ui.Brush(category?.Color ?? "#607D8B"));
    }

    /// <summary>Rebuilds the filtered list, keeping <paramref name="keepSelected"/> selected when still visible.</summary>
    private void RefreshList(Card? keepSelected)
    {
        IEnumerable<Card> query = _cards;
        var term = SearchBox.Text.Trim();
        if (term.Length > 0)
            query = query.Where(c => c.Label.Contains(term, StringComparison.OrdinalIgnoreCase)
                                      || c.Id.Contains(term, StringComparison.OrdinalIgnoreCase)
                                      || c.Example.Contains(term, StringComparison.OrdinalIgnoreCase));

        var items = query.OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase).Select(BuildItem).ToList();

        _suppressSelection = true;
        CardList.ItemsSource = items;
        CardList.SelectedItem = items.FirstOrDefault(i => ReferenceEquals(i.Card, keepSelected)) ?? items.FirstOrDefault();
        _suppressSelection = false;

        CountText.Text = $"{_cards.Count} card{(_cards.Count == 1 ? "" : "s")}";

        var newSelected = (CardList.SelectedItem as CardListItem)?.Card;
        if (!ReferenceEquals(newSelected, _selected)) LoadDetail(newSelected);
    }

    private void LoadDetail(Card? card)
    {
        _selected = card;
        if (card == null)
        {
            DetailScroll.Visibility = Visibility.Collapsed;
            EmptyDetailText.Visibility = Visibility.Visible;
            return;
        }

        _loading = true;
        IdBox.Text = card.Id;
        LabelBox.Text = card.Label;
        ExampleBox.Text = card.Example;
        ExplanationBox.Text = card.Explanation;
        CategoryCombo.SelectedValue = card.CategoryId;
        EasyRadio.IsChecked = card.Difficulty == 1;
        MediumRadio.IsChecked = card.Difficulty == 2;
        TrickyRadio.IsChecked = card.Difficulty == 3;
        DetailHeader.Text = card.Label.Length > 0 ? card.Label : "New card";
        _loading = false;

        DetailScroll.Visibility = Visibility.Visible;
        EmptyDetailText.Visibility = Visibility.Collapsed;
    }

    /// <summary>Copies the detail panel's current values back into the card being edited.</summary>
    private void CommitDetail()
    {
        if (_selected == null) return;
        _selected.Id = IdBox.Text;
        _selected.Label = LabelBox.Text;
        _selected.Example = ExampleBox.Text;
        _selected.Explanation = ExplanationBox.Text;
        _selected.CategoryId = (string?)CategoryCombo.SelectedValue ?? "";
        _selected.Difficulty = TrickyRadio.IsChecked == true ? 3 : MediumRadio.IsChecked == true ? 2 : 1;
    }

    private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshList(_selected);

    private void OnSelectCard(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        CommitDetail();
        LoadDetail((CardList.SelectedItem as CardListItem)?.Card);
    }

    private void OnFieldLostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading || _selected == null) return;
        CommitDetail();
        RefreshList(_selected);
    }

    private void OnCategoryChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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

    private void OnAddCard(object sender, RoutedEventArgs e)
    {
        CommitDetail();
        var card = new Card { CategoryId = _state.Categories.FirstOrDefault()?.Id ?? "", Difficulty = 1 };
        _cards.Add(card);
        SearchBox.Text = "";
        RefreshList(card);
        LabelBox.Focus();
    }

    private void OnDeleteCurrent(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var name = _selected.Label.Length > 0 ? _selected.Label : _selected.Id.Length > 0 ? _selected.Id : "this card";
        if (!Confirm($"Delete '{name}'? This cannot be undone.")) return;

        _cards.Remove(_selected);
        _selected = null;
        RefreshList(null);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        CommitDetail();

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

            var name = card.Id.Length > 0 ? card.Id : card.Label.Length > 0 ? card.Label : "(blank card)";
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

    private void Error(string text) => MessageBox.Show(this, text, "Manage cards", MessageBoxButton.OK, MessageBoxImage.Error);
}
