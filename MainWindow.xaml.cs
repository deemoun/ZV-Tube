using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace YouTubeDownloader
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<YouTubeVideo> videoList = new();
        private readonly string ytDlpPath = "yt-dlp.exe";
        private readonly string downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");

        private ICollectionView? view;
        private Process? searchProcess;
        private bool isSearching = false;
        private CancellationTokenSource? cts;

        public MainWindow()
        {
            InitializeComponent();
            SearchBox.Focus();
            Directory.CreateDirectory(downloadFolder);

            ResultsList.ItemsSource = videoList;
            view = CollectionViewSource.GetDefaultView(videoList);
            view.SortDescriptions.Add(new SortDescription(nameof(YouTubeVideo.view_count), ListSortDirection.Descending));

            OpenFolderButton.IsEnabled = Directory.Exists(downloadFolder);
            OpenFolderButton.Opacity = OpenFolderButton.IsEnabled ? 1.0 : 0.5;

            SetInteractiveUI(false);
            ResultsList.MouseDoubleClick += ResultsList_MouseDoubleClick;
            AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeader_Click));
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
            {
                string sortBy = header.Column.Header switch
                {
                    string h when h.Contains("Название") => nameof(YouTubeVideo.title),
                    string h when h.Contains("Канал") => nameof(YouTubeVideo.uploader),
                    string h when h.Contains("Просмотры") => nameof(YouTubeVideo.view_count),
                    string h when h.Contains("Дата") => nameof(YouTubeVideo.upload_date),
                    _ => null
                };

                if (sortBy == null) return;

                if (view!.SortDescriptions.Count > 0 && view.SortDescriptions[0].PropertyName == sortBy)
                {
                    var current = view.SortDescriptions[0];
                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new SortDescription(sortBy, current.Direction == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending));
                }
                else
                {
                    view!.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new SortDescription(sortBy, ListSortDirection.Descending));
                }

                view.Refresh();
            }
        }

        private void SetInteractiveUI(bool enabled)
        {
            bool hasSelection = enabled && ResultsList.SelectedItem is YouTubeVideo;

            SetButtonState(PlayVideoButton, hasSelection);
            SetButtonState(PlayAudioButton, hasSelection);
            SetButtonState(DownloadButton, hasSelection);
            SetButtonState(DownloadVideoButton, hasSelection);

            ResultsContainer.IsEnabled = enabled;
            ResultsContainer.Opacity = enabled ? 1.0 : 0.5;
        }

        private void SetButtonState(Button button, bool enabled)
        {
            button.IsEnabled = enabled;
            button.Opacity = enabled ? 1.0 : 0.5;
        }

        private void SetSearchButtonToStop()
        {
            SearchButton.Content = "⛔ Стоп";
            SearchButton.Background = Brushes.IndianRed;
            SearchButton.Foreground = Brushes.White;
        }

        private void ResetSearchButton()
        {
            SearchButton.Content = "🔎 Поиск";
            SearchButton.ClearValue(Button.BackgroundProperty);
            SearchButton.ClearValue(Button.ForegroundProperty);
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (isSearching)
            {
                StopSearch();
                return;
            }

            string query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                StatusText.Text = "Введите запрос.";
                return;
            }

            StatusText.Text = "Поиск...";
            videoList.Clear();
            view?.Refresh();
            SetInteractiveUI(false);
            isSearching = true;
            SetSearchButtonToStop();
            cts = new CancellationTokenSource();

            Task.Run(() => PerformSearch(query, cts.Token));
        }

        private void StopSearch()
        {
            cts?.Cancel();
            TryStopProcess();
            ResetSearchButton();
            isSearching = false;

            if (videoList.Count > 0)
                SetInteractiveUI(true);
        }

        private void TryStopProcess()
        {
            if (searchProcess != null && !searchProcess.HasExited)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {searchProcess.Id} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    StatusText.Text = "Поиск остановлен.";
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Ошибка остановки: {ex.Message}";
                }
            }
        }

        private void PerformSearch(string query, CancellationToken token)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = $"ytsearch30:\"{query}\" --print-json --skip-download",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                searchProcess = Process.Start(psi);
                if (searchProcess == null)
                    throw new Exception("yt-dlp не запустился.");

                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), token);
                    if (!token.IsCancellationRequested)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StopSearch();
                            StatusText.Text = "Поиск остановлен по таймауту.";
                        });
                    }
                });

                while (!searchProcess.StandardOutput.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = searchProcess.StandardOutput.ReadLine();
                    if (line?.Trim().StartsWith("{") == true)
                    {
                        try
                        {
                            var video = JsonSerializer.Deserialize<YouTubeVideo>(line);
                            if (video != null)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    videoList.Add(video);
                                    StatusText.Text = $"Добавлено: {videoList.Count}";
                                });
                            }
                        }
                        catch { }
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        ResetSearchButton();
                        isSearching = false;
                        SetInteractiveUI(true);
                        if (videoList.Count == 0)
                            StatusText.Text = "Видео не найдены.";
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ResetSearchButton();
                    isSearching = false;
                    SetInteractiveUI(true);
                    StatusText.Text = $"Ошибка: {ex.Message}";
                });
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SearchButton_Click(sender, e);
        }

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isSearching)
                SetInteractiveUI(true);
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is not YouTubeVideo video)
            {
                StatusText.Text = "Выберите видео.";
                return;
            }

            string url = $"https://www.youtube.com/watch?v={video.id}";
            string safeTitle = string.Join("_", video.title.Split(Path.GetInvalidFileNameChars()));
            string outputPath = Path.Combine(downloadFolder, $"{safeTitle}.%(ext)s");

            StatusText.Text = "Скачивание начато...";

            var psi = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = $"-f bestaudio -o \"{outputPath}\" \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            if (process == null)
            {
                StatusText.Text = "Не удалось запустить загрузку.";
                return;
            }

            await process.WaitForExitAsync();
            StatusText.Text = $"Скачано: {safeTitle}";
        }

        private async void DownloadVideoButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is not YouTubeVideo video)
            {
                StatusText.Text = "Выберите видео.";
                return;
            }

            string url = $"https://www.youtube.com/watch?v={video.id}";
            string safeTitle = string.Join("_", video.title.Split(Path.GetInvalidFileNameChars()));
            string outputPath = Path.Combine(downloadFolder, $"{safeTitle}.%(ext)s");

            StatusText.Text = "Скачивание начато...";

            var psi = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = $"-f bestvideo+bestaudio --merge-output-format mp4 --no-write-thumbnail -o \"{outputPath}\" \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            if (process == null)
            {
                StatusText.Text = "Не удалось запустить загрузку.";
                return;
            }

            await process.WaitForExitAsync();
            StatusText.Text = $"Скачано: {safeTitle}";
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(downloadFolder))
                Directory.CreateDirectory(downloadFolder);

            Process.Start("explorer.exe", downloadFolder);
        }

        private void PlayButtonVideo_Click(object sender, RoutedEventArgs e)
        {
            PlayWithMpv("--no-config");
        }

        private void PlayButtonAudio_Click(object sender, RoutedEventArgs e)
        {
            PlayWithMpv("--no-video");
        }

        private void PlayWithMpv(string extraArg)
        {
            if (ResultsList.SelectedItem is not YouTubeVideo video)
            {
                StatusText.Text = "Выберите видео для воспроизведения.";
                return;
            }

            string url = $"https://www.youtube.com/watch?v={video.id}";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "mpv.exe",
                    Arguments = $"{extraArg} \"{url}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                StatusText.Text = $"Воспроизведение: {video.title}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка воспроизведения: {ex.Message}";
            }
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Проверка, что клик был по элементу списка, а не по заголовку
            if (e.OriginalSource is DependencyObject source)
            {
                var listViewItem = ItemsControl.ContainerFromElement(ResultsList, source) as ListViewItem;
                if (listViewItem == null)
                    return;
            }

            if (ResultsList.SelectedItem is not YouTubeVideo video) return;

            string url = $"https://www.youtube.com/watch?v={video.id}";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Не удалось открыть браузер: {ex.Message}";
            }
        }

    }

    public class YouTubeVideo
    {
        public string id { get; set; }
        public string title { get; set; }
        public string uploader { get; set; }
        public long view_count { get; set; }
        public string upload_date { get; set; }

        public string Title => title;
        public string Uploader => uploader;
        public string Views => view_count.ToString("N0");

        public string UploadDate =>
            string.IsNullOrWhiteSpace(upload_date) || upload_date.Length != 8
                ? ""
                : $"{upload_date[..4]}.{upload_date.Substring(4, 2)}.{upload_date.Substring(6, 2)}";
    }
}
