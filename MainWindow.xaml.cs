using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YouTubeDownloader
{
    public partial class MainWindow : Window
    {
        private List<YouTubeVideo> videoList = new();
        private readonly string ytDlpPath = "yt-dlp.exe";
        private readonly string downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");

        public MainWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory(downloadFolder);
            SetAllButtonsState(false);
        }

        private void SetAllButtonsState(bool enabled)
        {
            SetButtonState(PlayVideoButton, enabled);
            SetButtonState(PlayAudioButton, enabled);
            SetButtonState(DownloadButton, enabled);
            SetButtonState(OpenFolderButton, enabled);
        }

        private void SetButtonState(Button button, bool enabled)
        {
            button.IsEnabled = enabled;
            button.Opacity = enabled ? 1.0 : 0.5;
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                StatusText.Text = "Введите запрос.";
                return;
            }

            StatusText.Text = "Поиск...";
            ResultsList.Items.Clear();
            videoList.Clear();
            SetAllButtonsState(false);

            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = $"ytsearch10:\"{query}\" --print-json --skip-download",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    var process = Process.Start(psi);
                    if (process == null)
                        throw new Exception("yt-dlp не запустился.");

                    var localList = new List<YouTubeVideo>();
                    var localDisplay = new List<string>();

                    while (!process.StandardOutput.EndOfStream)
                    {
                        string line = process.StandardOutput.ReadLine();
                        if (line?.Trim().StartsWith("{") == true)
                        {
                            try
                            {
                                var video = JsonSerializer.Deserialize<YouTubeVideo>(line);
                                if (video != null)
                                {
                                    localList.Add(video);
                                    localDisplay.Add($"{video.title} — {video.uploader} — {video.view_count:N0} просмотров");
                                }
                            }
                            catch
                            {
                                // игнорируем ошибки
                            }
                        }
                    }

                    process.WaitForExit();

                    Dispatcher.Invoke(() =>
                    {
                        if (localList.Count == 0)
                        {
                            StatusText.Text = "Видео не найдены.";
                            SetAllButtonsState(false);
                        }
                        else
                        {
                            videoList = localList;
                            foreach (var item in localDisplay)
                            {
                                ResultsList.Items.Add(item);
                            }
                            StatusText.Text = $"Найдено видео: {localList.Count}";

                            // Папку можно открыть — остальное только после выбора
                            SetButtonState(OpenFolderButton, true);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        StatusText.Text = $"Ошибка: {ex.Message}");
                }
            });
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
            }
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
            if (Directory.Exists(downloadFolder))
            {
                Process.Start("explorer.exe", downloadFolder);
            }
            else
            {
                StatusText.Text = "Папка загрузки не найдена.";
            }
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
    }

    public class YouTubeVideo
    {
        public string id { get; set; }
        public string title { get; set; }
        public string uploader { get; set; }
        public long view_count { get; set; }
    }
}
