using System.Windows;
using System.Windows.Input;

namespace DataSortingGame.Views;

public partial class PinDialog : Window
{
    private readonly string _pin;

    public PinDialog(string pin)
    {
        _pin = pin;
        InitializeComponent();
        Loaded += (_, _) => PinBox.Focus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnOk(sender, e);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (PinBox.Password == _pin)
        {
            DialogResult = true;
            return;
        }
        ErrorText.Text = "Incorrect PIN.";
        PinBox.Clear();
        PinBox.Focus();
    }
}
