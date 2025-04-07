using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
        private List<YouTubeVideo> videoList = new();
        private ICollectionView? view;
        private readonly string ytDlpPath = "yt-dlp.exe";
        private readonly string downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");

        private Process? searchProcess;
        private bool isSearching = false;
        private DateTime lastResultTime;

        public MainWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory(downloadFolder);
            SetAllButtonsState(false);

            ResultsList.ItemsSource = videoList;
            view = CollectionViewSource.GetDefaultView(ResultsList.ItemsSource);
            view.SortDescriptions.Add(new SortDescription("view_count", ListSortDirection.Descending));

            if (Directory.Exists(downloadFolder))
            {
                OpenFolderButton.IsEnabled = true;
                OpenFolderButton.Opacity = 1.0;
            }

            ResultsList.MouseDoubleClick += ResultsList_MouseDoubleClick;
        }

        private void SetAllButtonsState(bool enabled)
        {
            SetButtonState(PlayVideoButton, enabled);
            SetButtonState(PlayAudioButton, enabled);
            SetButtonState(DownloadButton, enabled);
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

                ResetSearchButton();
                isSearching = false;
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
            SetAllButtonsState(false);

            isSearching = true;
            SetSearchButtonToStop();
            lastResultTime = DateTime.Now;

            Task.Run(() =>
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

                    while (!searchProcess.StandardOutput.EndOfStream)
                    {
                        // Таймаут 60 секунд без новых результатов
                        if ((DateTime.Now - lastResultTime).TotalSeconds > 60)
                        {
                            try
                            {
                                if (!searchProcess.HasExited)
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "taskkill",
                                        Arguments = $"/PID {searchProcess.Id} /T /F",
                                        CreateNoWindow = true,
                                        UseShellExecute = false
                                    });
                                }
                                Dispatcher.Invoke(() =>
                                {
                                    StatusText.Text = "Поиск остановлен по таймауту (60 сек).";
                                    ResetSearchButton();
                                    isSearching = false;
                                });
                                return;
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    StatusText.Text = $"Ошибка при автоостановке: {ex.Message}";
                                    ResetSearchButton();
                                    isSearching = false;
                                });
                                return;
                            }
                        }

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
                                        view?.Refresh();
                                        lastResultTime = DateTime.Now;
                                        StatusText.Text = $"Добавлено: {videoList.Count}";

                                        if (videoList.Count > 0)
                                            SetAllButtonsState(true);
                                    });
                                }
                            }
                            catch { }
                        }
                    }

                    searchProcess.WaitForExit();

                    Dispatcher.Invoke(() =>
                    {
                        if (videoList.Count == 0)
                            StatusText.Text = "Видео не найдены.";

                        ResetSearchButton();
                        isSearching = false;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = $"Ошибка: {ex.Message}";
                        ResetSearchButton();
                        isSearching = false;
                    });
                }
            });
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SearchButton_Click(sender, e);
        }

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool selected = ResultsList.SelectedIndex >= 0;
            SetButtonState(PlayVideoButton, selected);
            SetButtonState(PlayAudioButton, selected);
            SetButtonState(DownloadButton, selected);
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            int index = ResultsList.SelectedIndex;
            if (index < 0 || index >= videoList.Count)
            {
                StatusText.Text = "Выберите видео для загрузки.";
                return;
            }

            var video = videoList[index];
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
            int index = ResultsList.SelectedIndex;
            if (index < 0 || index >= videoList.Count)
            {
                StatusText.Text = "Выберите видео для воспроизведения.";
                return;
            }

            var video = videoList[index];
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
            int index = ResultsList.SelectedIndex;
            if (index >= 0 && index < videoList.Count)
            {
                string url = $"https://www.youtube.com/watch?v={videoList[index].id}";
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

        public string UploadDate
        {
            get
            {
                if (string.IsNullOrWhiteSpace(upload_date) || upload_date.Length != 8)
                    return "";
                return $"{upload_date[..4]}.{upload_date.Substring(4, 2)}.{upload_date.Substring(6, 2)}";
            }
        }
    }
}