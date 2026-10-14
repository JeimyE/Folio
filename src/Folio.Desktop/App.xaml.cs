using System.Globalization;
using System.IO;
using System.Windows;
using Folio.Desktop.DependencyInjection;
using Folio.Desktop.ErrorHandling;
using Folio.Desktop.Theming;
using Folio.Infrastructure.Common;
using Folio.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Folio.Desktop;

/// <summary>
/// The WPF application entry point and Generic Host composition root. Fully
/// qualifies <see cref="System.Windows.Application"/> because this project also
/// references the Folio.Application assembly, and "Application" alone would
/// otherwise resolve to that sibling namespace instead of the WPF base class.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    /// <summary>
    /// Builds and starts the Generic Host (migrating/seeding the database happens as
    /// part of that, via <c>DatabaseInitializerHostedService</c> — see M2/M3), applies
    /// the saved theme, then shows the login/shell window. Stays `async void`
    /// deliberately: this is WPF's top-level startup event handler, one of the
    /// sanctioned exceptions to "avoid async void", and awaiting here (instead of
    /// `.Result`/`.Wait()`) keeps the UI thread free to render the splash screen.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // With no StartupUri, zero windows are open while the host starts. WPF's
        // default ShutdownMode (OnLastWindowClose) would treat that gap as "the last
        // window closed" and shut the app down before anything ever appears.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var culture = new CultureInfo("es-CR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        GlobalExceptionHandler.Register(this);

        FolioPaths.EnsureDataFolderExists();
        // Two-stage Serilog setup: this bootstrap logger covers anything that happens
        // before the Host (which configures the real sink via UseSerilog) exists.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(FolioPaths.LogsFolder, "log-.txt"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day)
            .CreateBootstrapLogger();

        var splash = new SplashWindow();
        splash.Show();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog((_, loggerConfiguration) => loggerConfiguration
                .WriteTo.File(
                    Path.Combine(FolioPaths.LogsFolder, "log-.txt"),
                    formatProvider: CultureInfo.InvariantCulture,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31))
            .ConfigureServices(services =>
            {
                services.AddFolioInfrastructure();
                services.AddFolioDesktop();
            })
            .Build();

        try
        {
            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start the application host (database migration/seed)");
            MessageBox.Show(
                $"No se pudo iniciar Folio.\n\n{ex.Message}",
                "Folio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            splash.Close();
            Shutdown(-1);
            return;
        }

        _host.Services.GetRequiredService<IThemeService>().ApplyStartupTheme();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnLastWindowClose;
        mainWindow.Show();
        splash.Close();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
