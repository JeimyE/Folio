namespace Folio.Desktop;

/// <summary>
/// Shown while the Generic Host starts (migrating/seeding the database on first run).
/// Closed as soon as the login screen is ready to show.
/// </summary>
public partial class SplashWindow : System.Windows.Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }
}
