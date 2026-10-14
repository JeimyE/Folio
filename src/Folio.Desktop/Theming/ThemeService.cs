using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Folio.Infrastructure.Common;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Folio.Desktop.Theming;

public sealed class ThemeService : IThemeService
{
    public event EventHandler<AppTheme>? ThemeChanged;

    private static readonly Uri LightDictionaryUri = new("pack://application:,,,/Resources/Theme/Folio.Light.xaml");
    private static readonly Uri DarkDictionaryUri = new("pack://application:,,,/Resources/Theme/Folio.Dark.xaml");

    // Must stay in sync with FolioPrimaryColor in Folio.Light.xaml / Folio.Dark.xaml.
    private static readonly Color LightPrimaryColor = (Color)ColorConverter.ConvertFromString("#3F6B4A");
    private static readonly Color DarkPrimaryColor = (Color)ColorConverter.ConvertFromString("#7FA57A");

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public void ApplyStartupTheme()
    {
        var saved = LoadSavedTheme();
        Apply(saved ?? DetectSystemTheme(), persist: false);
    }

    public void ToggleTheme()
    {
        var next = CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        Apply(next, persist: true);
    }

    private void Apply(AppTheme theme, bool persist)
    {
        CurrentTheme = theme;

        var wpfUiTheme = theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;

        // updateAccent: false — otherwise ApplicationThemeManager pulls Windows' own
        // system accent color (showing up as blue, not Bosque green) on every call.
        // backdrop: None — Mica requires DWM composition that isn't reliable on every
        // machine (it went blank gray here when re-applied at runtime); a flat, solid
        // background is correct for a business app and never depends on that.
        // ApplicationAccentColorManager.Apply sets WPF-UI's accent — used by button
        // "Primary" appearance, selection highlights, etc. — to Folio's own primary.
        ApplicationThemeManager.Apply(wpfUiTheme, WindowBackdropType.None, updateAccent: false);
        ApplicationAccentColorManager.Apply(theme == AppTheme.Dark ? DarkPrimaryColor : LightPrimaryColor, wpfUiTheme);

        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d =>
            d.Source == LightDictionaryUri || d.Source == DarkDictionaryUri);
        if (existing is not null)
        {
            dictionaries.Remove(existing);
        }

        dictionaries.Add(new ResourceDictionary { Source = theme == AppTheme.Dark ? DarkDictionaryUri : LightDictionaryUri });

        if (persist)
        {
            SaveTheme(theme);
        }

        ThemeChanged?.Invoke(this, theme);
    }

    private static AppTheme DetectSystemTheme() =>
        ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark ? AppTheme.Dark : AppTheme.Light;

    private static AppTheme? LoadSavedTheme()
    {
        if (!File.Exists(FolioPaths.SettingsFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(FolioPaths.SettingsFilePath);
            return JsonSerializer.Deserialize<FolioSettings>(json)?.Theme;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private static void SaveTheme(AppTheme theme)
    {
        FolioPaths.EnsureDataFolderExists();
        File.WriteAllText(FolioPaths.SettingsFilePath, JsonSerializer.Serialize(new FolioSettings { Theme = theme }));
    }

    private sealed class FolioSettings
    {
        public AppTheme Theme { get; set; }
    }
}
