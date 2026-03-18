using System.Text.Json;
using ZVTube.Models;

namespace ZVTube.Services;

public class SettingsService
{
    private readonly string appDirectory;
    private readonly string settingsPath;

    public SettingsService()
    {
        appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZV-Tube");
        settingsPath = Path.Combine(appDirectory, "settings.json");
        Directory.CreateDirectory(appDirectory);
    }

    public string AppDirectory => appDirectory;

    public AppSettings Load()
    {
        if (!File.Exists(settingsPath))
        {
            var defaults = CreateDefaults();
            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(settingsPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? CreateDefaults();

        if (string.IsNullOrWhiteSpace(settings.DownloadFolder))
        {
            settings.DownloadFolder = DefaultDownloadFolder();
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(appDirectory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json);
    }

    private static AppSettings CreateDefaults() => new()
    {
        DownloadFolder = DefaultDownloadFolder(),
        Theme = "Dark",
        Culture = "en"
    };

    private static string DefaultDownloadFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ZV-Tube");
}
