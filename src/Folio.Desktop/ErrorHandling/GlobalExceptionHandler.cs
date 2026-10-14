using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Serilog;

namespace Folio.Desktop.ErrorHandling;

/// <summary>
/// Catches exceptions that would otherwise crash the app silently or with the default
/// Windows "stopped working" dialog: UI-thread exceptions (recoverable — the app stays
/// open), background-thread exceptions (not recoverable, just logged before the
/// process dies), and unobserved <see cref="Task"/> exceptions (logged and marked
/// observed so they don't escalate).
/// </summary>
public static class GlobalExceptionHandler
{
    public static void Register(System.Windows.Application app)
    {
        app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception on the UI thread");

        MessageBox.Show(
            $"Ocurrió un error inesperado y la acción no se pudo completar.\n\n{e.Exception.Message}",
            "Folio",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception outside the UI thread; the process will terminate");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
