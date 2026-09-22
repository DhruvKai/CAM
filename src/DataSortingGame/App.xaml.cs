using System.Windows;
using DataSortingGame.Core.Services;

namespace DataSortingGame;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppState state;
        try
        {
            state = AppState.Create();
        }
        catch (ConfigException ex)
        {
            MessageBox.Show(
                $"The game could not start because a settings file has a problem:\n\n{ex.Message}\n\n" +
                $"Fix the file in:\n{AppPaths.ResolveConfigDir(AppContext.BaseDirectory)}\n\n" +
                "or delete it to restore the default.",
                "CAM", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        new MainWindow(state).Show();
    }
}
