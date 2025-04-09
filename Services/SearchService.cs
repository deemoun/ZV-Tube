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
            if (IsSearching)
                return;

            IsSearching = true;
            cts = new CancellationTokenSource();

            videoList.Clear();
            Application.Current.Dispatcher.Invoke(() => statusText.Text = "Поиск...");

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

                // Авто-таймаут через 60 сек
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(60), token);
                        if (!token.IsCancellationRequested)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                StopSearch(statusText, resetSearchButton, setInteractiveUI);
                                statusText.Text = "Поиск остановлен по таймауту.";
                            });
                        }
                    }
                    catch { }
                });

                if (searchProcess == null)
                    throw new Exception("yt-dlp не запустился.");

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
                    if (!token.IsCancellationRequested)
                    {
                        resetSearchButton();
                        setInteractiveUI(videoList.Count > 0);
                        statusText.Text = videoList.Count > 0 ? "Поиск завершён." : "Видео не найдены.";
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    statusText.Text = $"Ошибка: {ex.Message}";
                    resetSearchButton();
                    setInteractiveUI(false);
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
            if (!IsSearching)
                return;

            IsSearching = false;
            cts?.Cancel();

            try
            {
                if (searchProcess != null && !searchProcess.HasExited)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {searchProcess.Id} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    statusText.Text = $"Ошибка остановки: {ex.Message}";
                });
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                resetSearchButton();
                setUI(videoList.Count > 0);
                statusText.Text = "Поиск остановлен.";
            });
        }
    }
}