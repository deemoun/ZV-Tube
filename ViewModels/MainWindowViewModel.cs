using Avalonia;
using Avalonia.Styling;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ZVTube.Infrastructure;
using ZVTube.Models;
using ZVTube.Services;

namespace ZVTube.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    private readonly VideoService videoService;
    private readonly SettingsService settingsService;
    private readonly LocalizationService localization;
    private readonly AppSettings settings;
    private CancellationTokenSource? searchCts;

    private string searchQuery = string.Empty;
    private string status = string.Empty;
    private string searchLogs = string.Empty;
    private bool isSearching;
    private bool isLogsExpanded = true;
    private YouTubeVideo? selectedVideo;

    public MainWindowViewModel(VideoService videoService, SettingsService settingsService, LocalizationService localization)
    {
        this.videoService = videoService;
        this.settingsService = settingsService;
        this.localization = localization;
        settings = settingsService.Load();

        Status = StatusIdleText;
        SearchCommand = new RelayCommand(SearchOrStop);
        DownloadAudioCommand = new AsyncRelayCommand(DownloadAudioAsync, () => SelectedVideo is not null, HandleCommandError);
        DownloadVideoCommand = new AsyncRelayCommand(DownloadVideoAsync, () => SelectedVideo is not null, HandleCommandError);
        PlayAudioCommand = new AsyncRelayCommand(PlayAudioAsync, () => SelectedVideo is not null, HandleCommandError);
        PlayVideoCommand = new AsyncRelayCommand(PlayVideoAsync, () => SelectedVideo is not null, HandleCommandError);
        OpenFolderCommand = new RelayCommand(OpenDownloadFolder);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        OpenInBrowserCommand = new RelayCommand(OpenInBrowser, () => SelectedVideo is not null);
        ToggleLogsCommand = new RelayCommand(ToggleLogs);

        ApplyTheme();
    }

    public ObservableCollection<YouTubeVideo> Videos { get; } = [];

    public ICommand SearchCommand { get; }
    public ICommand DownloadAudioCommand { get; }
    public ICommand DownloadVideoCommand { get; }
    public ICommand PlayAudioCommand { get; }
    public ICommand PlayVideoCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand OpenInBrowserCommand { get; }
    public ICommand ToggleLogsCommand { get; }

    public string WindowTitle => localization.Text("Title");
    public string FileMenuHeader => localization.Text("MenuFile");
    public string ActionsMenuHeader => localization.Text("MenuActions");
    public string ThemeMenuHeader => localization.Text("MenuTheme");
    public string AboutMenuHeader => localization.Text("MenuAbout");
    public string OpenDownloadFolderMenuItemLabel => localization.Text("MenuOpenDownloadFolder");
    public string PlayAudioMenuItemLabel => localization.Text("MenuPlayAudio");
    public string PlayVideoMenuItemLabel => localization.Text("MenuPlayVideo");

    public string SearchLabel => localization.Text("Search");
    public string StopLabel => localization.Text("Stop");
    public string OpenFolderLabel => localization.Text("OpenFolder");
    public string OpenFolderButtonLabel => $"📁 {OpenFolderLabel}";
    public string SearchPrompt => localization.Text("SearchPrompt");
    public string SearchWatermark => localization.Text("SearchWatermark");

    public string ThemeLabel => localization.Text("Theme");
    public string ThemeButtonLabel => $"{ThemeLabel}: {CurrentThemeLabel}";

    public string PlayAudioLabel => localization.Text("PlayAudio");
    public string PlayVideoLabel => localization.Text("PlayVideo");
    public string DownloadAudioLabel => localization.Text("DownloadAudio");
    public string DownloadVideoLabel => localization.Text("DownloadVideo");

    public string PlayAudioActionLabel => $"🎧 {PlayAudioLabel}";
    public string PlayVideoActionLabel => $"🎬 {PlayVideoLabel}";
    public string DownloadAudioActionLabel => $"💾 {DownloadAudioLabel}";
    public string DownloadVideoActionLabel => $"📹 {DownloadVideoLabel}";

    public string SelectionHintText => localization.Text("SelectionHint");
    public string SearchLogsTitle => localization.Text("LogsTitle");

    public string VideoTitleColumnHeader => localization.Text("VideoTitleColumn");
    public string VideoChannelColumnHeader => localization.Text("VideoChannelColumn");
    public string VideoViewsColumnHeader => localization.Text("VideoViewsColumn");
    public string VideoDateColumnHeader => localization.Text("VideoDateColumn");

    public string StatusIdleText => localization.Text("StatusIdle");
    public string StatusStoppingSearchText => localization.Text("StatusStoppingSearch");
    public string StatusEnterQueryText => localization.Text("StatusEnterQuery");
    public string StatusSearchingText => localization.Text("StatusSearching");
    public string StatusNoVideosFoundText => localization.Text("StatusNoVideosFound");
    public string StatusSearchCompletedText => localization.Text("StatusSearchCompleted");
    public string StatusSearchStoppedText => localization.Text("StatusSearchStopped");
    public string StatusDownloadingAudioText => localization.Text("StatusDownloadingAudio");
    public string StatusDownloadingVideoText => localization.Text("StatusDownloadingVideo");
    public string StatusUnableOpenFolderText => localization.Text("StatusUnableOpenFolder");
    public string StatusUnableStartAudioPlayerText => localization.Text("StatusUnableStartAudioPlayer");
    public string StatusUnableStartVideoPlayerText => localization.Text("StatusUnableStartVideoPlayer");

    public string ShowLogsLabel => localization.Text("ShowLogs");
    public string HideLogsLabel => localization.Text("HideLogs");
    public string LogsToggleText => IsLogsExpanded ? HideLogsLabel : ShowLogsLabel;

    public string SearchButtonText => IsSearching ? $"⛔ {StopLabel}" : $"🔎 {SearchLabel}";

    public string SearchQuery
    {
        get => searchQuery;
        set => SetProperty(ref searchQuery, value);
    }

    public string Status
    {
        get => status;
        set => SetProperty(ref status, value);
    }

    public string SearchLogs
    {
        get => searchLogs;
        set => SetProperty(ref searchLogs, value);
    }

    public YouTubeVideo? SelectedVideo
    {
        get => selectedVideo;
        set
        {
            if (SetProperty(ref selectedVideo, value))
            {
                RaisePropertyChanged(nameof(HasSelection));
                RaisePropertyChanged(nameof(ShowSelectionHint));
                NotifySelectionCommands();
            }
        }
    }

    public bool HasSelection => SelectedVideo is not null;

    public bool ShowSelectionHint => !HasSelection;

    public bool IsSearching
    {
        get => isSearching;
        set
        {
            if (SetProperty(ref isSearching, value))
            {
                RaisePropertyChanged(nameof(SearchButtonText));
            }
        }
    }

    public bool IsLogsExpanded
    {
        get => isLogsExpanded;
        set
        {
            if (SetProperty(ref isLogsExpanded, value))
            {
                RaisePropertyChanged(nameof(LogsToggleText));
            }
        }
    }

    private void SearchOrStop()
    {
        if (IsSearching)
        {
            searchCts?.Cancel();
            Status = StatusStoppingSearchText;
            return;
        }

        _ = RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Status = StatusEnterQueryText;
            return;
        }

        IsSearching = true;
        Status = StatusSearchingText;
        SearchLogs = string.Empty;
        Videos.Clear();

        searchCts?.Dispose();
        searchCts = new CancellationTokenSource(TimeSpan.FromSeconds(180));

        try
        {
            var log = new Progress<string>(message =>
            {
                SearchLogs = string.IsNullOrWhiteSpace(SearchLogs)
                    ? message
                    : $"{SearchLogs}{Environment.NewLine}{message}";
            });

            var resultsProgress = new Progress<YouTubeVideo>(video =>
            {
                Videos.Add(video);
            });

            await videoService.SearchAsync(SearchQuery.Trim(), searchCts.Token, log, resultsProgress);

            Status = Videos.Count == 0 ? StatusNoVideosFoundText : StatusSearchCompletedText;
        }
        catch (OperationCanceledException)
        {
            Status = StatusSearchStoppedText;
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            searchCts?.Dispose();
            searchCts = null;
        }
    }

    private async Task DownloadAudioAsync()
    {
        if (SelectedVideo is null) return;
        Status = StatusDownloadingAudioText;
        Status = await videoService.DownloadAudioAsync(SelectedVideo);
    }

    private async Task DownloadVideoAsync()
    {
        if (SelectedVideo is null) return;
        Status = StatusDownloadingVideoText;
        Status = await videoService.DownloadVideoAsync(SelectedVideo);
    }

    private async Task PlayAudioAsync()
    {
        if (SelectedVideo is null) return;

        var played = await videoService.PlayAudioAsync(SelectedVideo);
        Status = played
            ? $"{localization.Text("StatusPlayingAudioPrefix")}: {SelectedVideo.title}"
            : StatusUnableStartAudioPlayerText;
    }

    private async Task PlayVideoAsync()
    {
        if (SelectedVideo is null) return;

        var played = await videoService.PlayVideoAsync(SelectedVideo);
        Status = played
            ? $"{localization.Text("StatusPlayingVideoPrefix")}: {SelectedVideo.title}"
            : StatusUnableStartVideoPlayerText;
    }

    private void OpenDownloadFolder()
    {
        var opened = videoService.OpenDownloadFolder();
        if (!opened)
        {
            Status = StatusUnableOpenFolderText;
        }
    }

    private void OpenInBrowser()
    {
        if (SelectedVideo is null) return;
        videoService.OpenInBrowser(SelectedVideo);
    }

    private void ToggleTheme()
    {
        settings.Theme = settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        settingsService.Save(settings);
        ApplyTheme();
    }

    private void ToggleLogs()
    {
        IsLogsExpanded = !IsLogsExpanded;
    }

    private void ApplyTheme()
    {
        Application.Current!.RequestedThemeVariant =
            settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

        RaisePropertyChanged(nameof(CurrentThemeLabel));
        RaisePropertyChanged(nameof(ThemeButtonLabel));
    }

    public string CurrentThemeLabel => settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
        ? localization.Text("Dark")
        : localization.Text("Light");

    private void NotifySelectionCommands()
    {
        (DownloadAudioCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (DownloadVideoCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (PlayAudioCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (PlayVideoCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (OpenInBrowserCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void HandleCommandError(Exception ex)
    {
        Status = $"Error: {ex.Message}";
    }
}
