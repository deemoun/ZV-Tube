namespace ZVTube.Services;

public class LocalizationService
{
    private readonly SettingsService settingsService;

    public LocalizationService(SettingsService settingsService)
    {
        this.settingsService = settingsService;
    }

    public string Text(string key)
    {
        // Future-ready: currently only English catalog.
        return key switch
        {
            "Title" => "ZV Tube",
            "SearchPrompt" => "What are we searching on YouTube?",
            "Search" => "Search",
            "Stop" => "Stop",
            "OpenFolder" => "Open folder",
            "OpenDownloadFolder" => "Open download folder",
            "Exit" => "Exit",
            "PlayAudio" => "Play audio",
            "PlayVideo" => "Play video",
            "DownloadAudio" => "Download audio",
            "DownloadVideo" => "Download video",
            "StatusIdle" => "Results will appear after you search",
            "Theme" => "Theme",
            "Light" => "Light",
            "Dark" => "Dark",
            _ => key
        };
    }
}
