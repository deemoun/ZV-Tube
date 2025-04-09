using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using YouTubeDownloader.Models;

namespace YouTubeDownloader.Services
{
    public class SearchService
    {
        private readonly ObservableCollection<YouTubeVideo> videoList;
        private Process? searchProcess;
        private CancellationTokenSource? cts;

        public bool IsSearching { get; private set; } = false;

        public SearchService(ObservableCollection<YouTubeVideo> videoList)
        {
            this.videoList = videoList;
        }

        public void StartSearch(string query, TextBlock statusText, Action<bool> setInteractiveUI, Action resetSearchButton)
        {
            IsSearching = true;
            cts = new CancellationTokenSource();
            Task.Run(() => PerformSearch(query, cts.Token, statusText, setInteractiveUI, resetSearchButton));
        }

        private void PerformSearch(string query, CancellationToken token, TextBlock statusText, Action<bool> setInteractiveUI, Action resetSearchButton)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    Arguments = $"ytsearch30:\"{query}\" --print-json --skip-download",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                searchProcess = Process.Start(psi);
                if (searchProcess == null)
                {
                    throw new Exception("yt-dlp не запустился. Проверьте путь к yt-dlp.exe.");
                }

                while (!searchProcess.StandardOutput.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = searchProcess.StandardOutput.ReadLine();
                    if (line?.Trim().StartsWith("{") == true)
                    {
                        var video = JsonSerializer.Deserialize<YouTubeVideo>(line);
                        if (video != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                videoList.Add(video);
                                statusText.Text = $"Добавлено: {videoList.Count}";
                            });
                        }
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    resetSearchButton();
                    setInteractiveUI(videoList.Count > 0);
                    statusText.Text = videoList.Count > 0 ? "Поиск завершен." : "Видео не найдены.";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    resetSearchButton();
                    setInteractiveUI(false);
                    statusText.Text = $"Ошибка: {ex.Message}";
                });
            }
            finally
            {
                IsSearching = false;
                cts?.Dispose();
                cts = null;
            }
        }

        public void StopSearch(TextBlock statusText, Action resetSearchButton, Action<bool> setUI)
        {
            if (!IsSearching) return;

            IsSearching = false;

            try
            {
                cts?.Cancel();

                if (searchProcess != null && !searchProcess.HasExited)
                {
                    searchProcess.Kill(true);
                    searchProcess.Dispose();
                }
            }
            catch { /* intentionally ignored */ }

            Application.Current.Dispatcher.Invoke(() =>
            {
                statusText.Text = "Поиск остановлен.";
                resetSearchButton();
                setUI(false);
            });
        }
    }
}