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
        private readonly string ytDlpPath = "yt-dlp.exe";
        private readonly string downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");

        public MainWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory(downloadFolder);
            SetAllButtonsState(false);

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

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                StatusText.Text = "Введите запрос.";
                return;
            }

            StatusText.Text = "Поиск...";
            ResultsList.ItemsSource = null;
            videoList.Clear();
            SetAllButtonsState(false);

            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = $"ytsearch20:\"{query}\" --print-json --skip-download",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    var process = Process.Start(psi);
                    if (process == null)
                        throw new Exception("yt-dlp не запустился.");

                    var localList = new List<YouTubeVideo>();

                    while (!process.StandardOutput.EndOfStream)
                    {
                        string line = process.StandardOutput.ReadLine();
                        if (line?.Trim().StartsWith("{") == true)
                        {
                            try
                            {
                                var video = JsonSerializer.Deserialize<YouTubeVideo>(line);
                                if (video != null)
                                    localList.Add(video);
                            }
                            catch { }
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
                            var view = CollectionViewSource.GetDefaultView(videoList);
                            ResultsList.ItemsSource = view;

                            // сортировка по убыванию просмотров по умолчанию
                            view.SortDescriptions.Clear();
                            view.SortDescriptions.Add(new SortDescription("view_count", ListSortDirection.Descending));

                            StatusText.Text = $"Найдено видео: {localList.Count}";
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

        // Обработка двойного клика — сортировка или открытие видео
        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject clickedObject)
                return;

            var header = FindAncestor<GridViewColumnHeader>(clickedObject);
            if (header?.Column != null && header.Column.Header is string headerText)
            {
                string sortBy = headerText.Trim() switch
                {
                    "👁 Просмотры" => "view_count",
                    "📅 Дата" => "upload_date",
                    _ => null
                };

                if (!string.IsNullOrEmpty(sortBy))
                {
                    var view = CollectionViewSource.GetDefaultView(ResultsList.ItemsSource);
                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new SortDescription(sortBy, ListSortDirection.Descending));
                    view.Refresh();
                    return;
                }
            }

            // Если клик не по заголовку — откроем видео в браузере
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

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && current is not T)
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as T;
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