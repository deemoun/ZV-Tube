using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using YouTubeDownloader.Models;

namespace YouTubeDownloader.Services
{
    public class VideoService
    {
        public string DownloadFolder { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");

        public void DownloadAudio(YouTubeVideo video, TextBlock statusText)
        {
            string url = $"https://www.youtube.com/watch?v={video.id}";
            string safeTitle = GetSafeFileName(video.title);
            string outputPath = Path.Combine(DownloadFolder, $"{safeTitle}.%(ext)s");

            RunYtDlp($"-f bestaudio --extract-audio --audio-format mp3 -o \"{outputPath}\" \"{url}\"", statusText, $"Download complete: {video.title}");
        }

        public void DownloadVideo(YouTubeVideo video, TextBlock statusText)
        {
            string url = $"https://www.youtube.com/watch?v={video.id}";
            string safeTitle = GetSafeFileName(video.title);
            string outputPath = Path.Combine(DownloadFolder, $"{safeTitle}.%(ext)s");

            RunYtDlp($"-f bestvideo+bestaudio --merge-output-format mp4 -o \"{outputPath}\" \"{url}\"", statusText, $"Download complete: {video.title}");
        }

        public void PlayVideo(YouTubeVideo video, TextBlock statusText)
        {
            RunExternal("mpv.exe", $"\"https://www.youtube.com/watch?v={video.id}\"", statusText, $"Playing: {video.title}");
        }

        public void PlayAudio(YouTubeVideo video, TextBlock statusText)
        {
            RunExternal("mpv.exe", $"--no-video \"https://www.youtube.com/watch?v={video.id}\"", statusText, $"Playing: {video.title}");
        }

        public void OpenDownloadFolder()
        {
            Process.Start("explorer.exe", DownloadFolder);
        }

        public void OpenInBrowser(YouTubeVideo video, TextBlock statusText)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://www.youtube.com/watch?v={video.id}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                statusText.Text = $"Failed to open browser: {ex.Message}";
            }
        }

        private void RunExternal(string fileName, string arguments, TextBlock statusText, string successMessage)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                });

                statusText.Text = successMessage;
            }
            catch (Exception ex)
            {
                statusText.Text = $"Failed to start: {ex.Message}";
            }
        }

        private async void RunYtDlp(string arguments, TextBlock statusText, string successMessage)
        {
            if (!File.Exists("yt-dlp.exe"))
            {
                statusText.Text = "Error: yt-dlp.exe not found.";
                return;
            }

            if (!File.Exists("ffmpeg.exe"))
            {
                statusText.Text = "Error: ffmpeg.exe not found.";
                return;
            }

            Directory.CreateDirectory(DownloadFolder);

            var psi = new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            try
            {
                var process = new Process { StartInfo = psi };

                process.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(e.Data)) return;

                    output.AppendLine(e.Data);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (e.Data.Contains("Destination", StringComparison.OrdinalIgnoreCase) ||
                            e.Data.Contains("Downloading", StringComparison.OrdinalIgnoreCase))
                        {
                            statusText.Text = "Downloading...";
                        }
                        else if (e.Data.Contains("Merger", StringComparison.OrdinalIgnoreCase) ||
                                 e.Data.Contains("Converting", StringComparison.OrdinalIgnoreCase) ||
                                 e.Data.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase))
                        {
                            statusText.Text = "Converting...";
                        }
                    });
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        error.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (process.ExitCode == 0)
                    {
                        statusText.Text = successMessage;
                    }
                    else
                    {
                        statusText.Text = $"Download error:\n{error.ToString()}";
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    statusText.Text = $"Exception: {ex.Message}";
                });
            }
        }

        private string GetSafeFileName(string title)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(c, '_');
            }
            return title;
        }
    }
}