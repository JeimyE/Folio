namespace Folio.Infrastructure.Common;

/// <summary>Centralizes every path Folio reads or writes under <c>%LOCALAPPDATA%</c>.</summary>
public static class FolioPaths
{
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Folivora",
        "Folio");

    public static string LogsFolder => Path.Combine(DataFolder, "logs");

    public static string DatabasePath => Path.Combine(DataFolder, "folio.db");

    public static string ConnectionString => $"Data Source={DatabasePath}";

    public static string SettingsFilePath => Path.Combine(DataFolder, "settings.json");

    /// <summary>Creates <see cref="DataFolder"/> (and therefore its parents) if missing.</summary>
    public static void EnsureDataFolderExists() => Directory.CreateDirectory(DataFolder);
}
