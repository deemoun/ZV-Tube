using System;
using System.Diagnostics;
using System.IO;
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

            ExecuteProcess("yt-dlp.exe", $"-f bestaudio -o \"{outputPath}\" \"{url}\"", statusText, $"Скачано: {safeTitle}");
        }

        public void DownloadVideo(YouTubeVideo video, TextBlock statusText)
        {
            string url = $"https://www.youtube.com/watch?v={video.id}";
            string safeTitle = GetSafeFileName(video.title);
            string outputPath = Path.Combine(DownloadFolder, $"{safeTitle}.%(ext)s");

            ExecuteProcess("yt-dlp.exe", $"-f bestvideo+bestaudio --merge-output-format mp4 -o \"{outputPath}\" \"{url}\"", statusText, $"Скачано: {safeTitle}");
        }

        public void PlayVideo(YouTubeVideo video, TextBlock statusText)
        {
            ExecuteProcess("mpv.exe", $"--no-config \"https://www.youtube.com/watch?v={video.id}\"", statusText, $"Воспроизведение: {video.title}");
        }

        public void PlayAudio(YouTubeVideo video, TextBlock statusText)
        {
            ExecuteProcess("mpv.exe", $"--no-video \"https://www.youtube.com/watch?v={video.id}\"", statusText, $"Воспроизведение: {video.title}");
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
                statusText.Text = $"Не удалось открыть браузер: {ex.Message}";
            }
        }

        private void ExecuteProcess(string fileName, string arguments, TextBlock statusText, string successMessage)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });

                process?.WaitForExit();
                statusText.Text = successMessage;
            }
            catch (Exception ex)
            {
                statusText.Text = $"Ошибка: {ex.Message}";
            }
        }

        private string GetSafeFileName(string title)
        {
            return string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        }
    }
}