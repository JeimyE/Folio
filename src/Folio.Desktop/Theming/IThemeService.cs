namespace Folio.Desktop.Theming;

/// <summary>Applies and persists the light/dark choice for both WPF-UI's own controls
/// and Folio's "Bosque" brand palette.</summary>
public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    /// <summary>Raised every time the theme changes, including from <see cref="ApplyStartupTheme"/>.
    /// A window created after that initial call (every window in this app) uses this to
    /// sync its own title bar / native chrome, which WPF-UI does not do on its own.</summary>
    event EventHandler<AppTheme>? ThemeChanged;

    /// <summary>Applies the saved preference, or the system theme if none was saved yet.</summary>
    void ApplyStartupTheme();

    void ToggleTheme();
}
