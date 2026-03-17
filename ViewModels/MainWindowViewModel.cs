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
    private bool isSearching;
    private YouTubeVideo? selectedVideo;

    public MainWindowViewModel(VideoService videoService, SettingsService settingsService, LocalizationService localization)
    {
        this.videoService = videoService;
        this.settingsService = settingsService;
        this.localization = localization;
        settings = settingsService.Load();

        Status = localization.Text("StatusIdle");
        SearchCommand = new AsyncRelayCommand(SearchOrStopAsync);
        DownloadAudioCommand = new AsyncRelayCommand(DownloadAudioAsync, () => SelectedVideo is not null);
        DownloadVideoCommand = new AsyncRelayCommand(DownloadVideoAsync, () => SelectedVideo is not null);
        PlayAudioCommand = new RelayCommand(PlayAudio, () => SelectedVideo is not null);
        PlayVideoCommand = new RelayCommand(PlayVideo, () => SelectedVideo is not null);
        OpenFolderCommand = new RelayCommand(videoService.OpenDownloadFolder);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        OpenInBrowserCommand = new RelayCommand(OpenInBrowser, () => SelectedVideo is not null);

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

    public string SearchLabel => localization.Text("Search");
    public string StopLabel => localization.Text("Stop");
    public string OpenFolderLabel => localization.Text("OpenFolder");
    public string SearchPrompt => localization.Text("SearchPrompt");
    public string ThemeLabel => localization.Text("Theme");
    public string PlayAudioLabel => localization.Text("PlayAudio");
    public string PlayVideoLabel => localization.Text("PlayVideo");
    public string DownloadAudioLabel => localization.Text("DownloadAudio");
    public string DownloadVideoLabel => localization.Text("DownloadVideo");

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

    public YouTubeVideo? SelectedVideo
    {
        get => selectedVideo;
        set
        {
            if (SetProperty(ref selectedVideo, value))
            {
                NotifySelectionCommands();
            }
        }
    }

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

    private async Task SearchOrStopAsync()
    {
        if (IsSearching)
        {
            searchCts?.Cancel();
            IsSearching = false;
            Status = "Search stopped.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Status = "Please enter a query.";
            return;
        }

        IsSearching = true;
        Status = "Searching...";
        Videos.Clear();

        searchCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        try
        {
            var items = await videoService.SearchAsync(SearchQuery.Trim(), searchCts.Token);
            foreach (var item in items.OrderByDescending(v => v.view_count))
            {
                Videos.Add(item);
            }

            Status = Videos.Count == 0 ? "No videos found." : "Search completed.";
        }
        catch (OperationCanceledException)
        {
            Status = "Search stopped due to timeout/cancel.";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task DownloadAudioAsync()
    {
        if (SelectedVideo is null) return;
        Status = "Downloading audio...";
        Status = await videoService.DownloadAudioAsync(SelectedVideo);
    }

    private async Task DownloadVideoAsync()
    {
        if (SelectedVideo is null) return;
        Status = "Downloading video...";
        Status = await videoService.DownloadVideoAsync(SelectedVideo);
    }

    private void PlayAudio()
    {
        if (SelectedVideo is null) return;
        videoService.PlayAudio(SelectedVideo);
        Status = $"Playing: {SelectedVideo.title}";
    }

    private void PlayVideo()
    {
        if (SelectedVideo is null) return;
        videoService.PlayVideo(SelectedVideo);
        Status = $"Playing: {SelectedVideo.title}";
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

    private void ApplyTheme()
    {
        Application.Current!.RequestedThemeVariant =
            settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

        RaisePropertyChanged(nameof(CurrentThemeLabel));
    }

    public string CurrentThemeLabel => settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
        ? localization.Text("Dark")
        : localization.Text("Light");

    private void NotifySelectionCommands()
    {
        (DownloadAudioCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (DownloadVideoCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (PlayAudioCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (PlayVideoCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (OpenInBrowserCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }
}
