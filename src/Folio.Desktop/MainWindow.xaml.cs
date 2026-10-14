using Folio.Desktop.Theming;
using Folio.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace Folio.Desktop;

/// <summary>
/// The application's only window. Its content switches between the login screen and
/// the authenticated shell via <see cref="ShellCoordinatorViewModel.CurrentViewModel"/>.
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(ShellCoordinatorViewModel viewModel, IThemeService themeService)
    {
        InitializeComponent();
        DataContext = viewModel;

        // The title bar doesn't pick up the app's theme on its own — it has its own
        // ApplicationTheme property, unrelated to ApplicationThemeManager's. Sync it
        // now (ApplyStartupTheme already ran before this window was ever created) and
        // on every later theme change.
        SyncTitleBarTheme(themeService.CurrentTheme);
        themeService.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => themeService.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, AppTheme theme) => SyncTitleBarTheme(theme);

    private void SyncTitleBarTheme(AppTheme theme) =>
        RootTitleBar.ApplicationTheme = theme == AppTheme.Dark
            ? Wpf.Ui.Appearance.ApplicationTheme.Dark
            : Wpf.Ui.Appearance.ApplicationTheme.Light;
}
