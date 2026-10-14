using System.Globalization;
using System.Resources;

namespace Folio.Desktop.Resources;

/// <summary>
/// Thin accessor over <c>Strings.resx</c> (SPEC §3: UI strings live in .resx, not
/// hardcoded in XAML). Hand-written rather than relying on the resx designer
/// generator, so both XAML (<c>x:Static</c>) and C# (formatted messages) read from
/// the exact same resource file.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new("Folio.Desktop.Resources.Strings", typeof(Strings).Assembly);

    public static string AppTitle => Get("AppTitle");

    public static string AppSubtitle => Get("AppSubtitle");

    public static string LoginUsername => Get("LoginUsername");

    public static string LoginPassword => Get("LoginPassword");

    public static string LoginSignIn => Get("LoginSignIn");

    public static string LoginInvalidCredentials => Get("LoginInvalidCredentials");

    public static string LoginAccountLockedFormat => Get("LoginAccountLockedFormat");

    public static string LoginAccountInactive => Get("LoginAccountInactive");

    public static string ShellSignOut => Get("ShellSignOut");

    public static string ShellToggleTheme => Get("ShellToggleTheme");

    public static string WelcomeGreetingFormat => Get("WelcomeGreetingFormat");

    private static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
