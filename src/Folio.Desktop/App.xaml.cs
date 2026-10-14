using System.IO;
using System.Windows;
using Folio.Infrastructure.Persistence;

namespace Folio.Desktop;

/// <summary>
/// The WPF application entry point. Fully qualifies <see cref="System.Windows.Application"/>
/// because this project also references the Folio.Application assembly, and
/// "Application" alone would otherwise resolve to that sibling namespace instead of
/// the WPF base class.
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Migrates and (on first run) seeds the database before showing any window. This
    /// is a temporary, minimal composition root — M3 replaces it with the Generic Host,
    /// DI and Serilog. It stays `async void` deliberately: this is WPF's top-level
    /// startup event handler, one of the sanctioned exceptions to "avoid async void",
    /// and awaiting here (instead of `.Result`/`.Wait()`) keeps the UI thread free.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // With no StartupUri, zero windows are open while the database initializes.
        // WPF's default ShutdownMode (OnLastWindowClose) would treat that gap as "the
        // last window closed" and shut the app down before MainWindow ever appears.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            await FolioDbInitializer.InitializeAsync(BuildConnectionString());
        }
        catch (Exception ex)
        {
            // M3 adds the application's global exception handler; this is a stop-gap
            // so a failed first-run migration doesn't fail silently.
            MessageBox.Show(
                $"No se pudo iniciar la base de datos de Folio.\n\n{ex.Message}",
                "Folio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnLastWindowClose;
        mainWindow.Show();
    }

    private static string BuildConnectionString()
    {
        var folioDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Folivora",
            "Folio");
        Directory.CreateDirectory(folioDataFolder);

        var databasePath = Path.Combine(folioDataFolder, "folio.db");
        return $"Data Source={databasePath}";
    }
}
