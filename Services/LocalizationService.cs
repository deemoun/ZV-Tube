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
            "MenuFile" => "📁 File",
            "MenuActions" => "⚙ Actions",
            "MenuTheme" => "🌓 Theme",
            "MenuAbout" => "📄 About",
            "MenuOpenDownloadFolder" => "📂 Open download folder",
            "MenuPlayAudio" => "🎧 Play audio",
            "MenuPlayVideo" => "🎬 Play video",

            "SearchPrompt" => "What are we searching on YouTube?",
            "SearchWatermark" => "YouTube search...",
            "Search" => "Search",
            "Stop" => "Stop",
            "OpenFolder" => "Open folder",
            "OpenDownloadFolder" => "Open download folder",
            "Exit" => "Exit",

            "PlayAudio" => "Play audio",
            "PlayVideo" => "Play video",
            "DownloadAudio" => "Download audio",
            "DownloadVideo" => "Download video",

            "VideoTitleColumn" => "🎬 Title",
            "VideoChannelColumn" => "📺 Channel",
            "VideoViewsColumn" => "👁 Views",
            "VideoDateColumn" => "📅 Date",
            "SelectionHint" => "Select a video from the list to enable actions.",
            "LogsTitle" => "Search logs",
            "ShowLogs" => "Show logs",
            "HideLogs" => "Hide logs",

            "StatusIdle" => "Results will appear after you search",
            "StatusStoppingSearch" => "Stopping search...",
            "StatusEnterQuery" => "Please enter a query.",
            "StatusSearching" => "Searching...",
            "StatusNoVideosFound" => "No videos found.",
            "StatusSearchCompleted" => "Search completed.",
            "StatusSearchStopped" => "Search stopped due to timeout/cancel. If this is the first run, tool download may still be in progress; try again in a few seconds.",
            "StatusDownloadingAudio" => "Downloading audio...",
            "StatusDownloadingVideo" => "Downloading video...",
            "StatusUnableOpenFolder" => "Unable to open download folder.",
            "StatusUnableStartAudioPlayer" => "Unable to start internal audio player.",
            "StatusUnableStartVideoPlayer" => "Unable to start internal video player.",
            "StatusPlayingAudioPrefix" => "Playing audio",
            "StatusPlayingVideoPrefix" => "Playing video",

            "Theme" => "Theme",
            "Light" => "Light",
            "Dark" => "Dark",
            _ => key
        };
    }
}
