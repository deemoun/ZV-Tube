using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ZVTube.Models;

namespace ZVTube.Services;

public class VideoService
{
    private readonly ToolManager toolManager;
    private readonly SettingsService settingsService;

    public VideoService(ToolManager toolManager, SettingsService settingsService)
    {
        this.toolManager = toolManager;
        this.settingsService = settingsService;
    }

    public async Task<List<YouTubeVideo>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var ytDlpPath = await toolManager.EnsureYtDlpAsync();
        var results = new List<YouTubeVideo>();

        var psi = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = $"ytsearch30:\"{query}\" --print-json --skip-download",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start yt-dlp.");

        while (!process.StandardOutput.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line?.TrimStart().StartsWith("{") != true)
            {
                continue;
            }

            var video = JsonSerializer.Deserialize<YouTubeVideo>(line);
            if (video is not null)
            {
                results.Add(video);
            }
        }

        if (!process.HasExited)
        {
            process.Kill(true);
        }

        return results;
    }

    public async Task<string> DownloadAudioAsync(YouTubeVideo video)
    {
        var output = await RunYtDlpAsync($"-f bestaudio --extract-audio --audio-format mp3 -o \"{GetOutputPattern(video)}\" \"{video.Url}\"");
        return output ? $"Download complete: {video.title}" : "Audio download failed.";
    }

    public async Task<string> DownloadVideoAsync(YouTubeVideo video)
    {
        var output = await RunYtDlpAsync($"-f bestvideo+bestaudio --merge-output-format mp4 -o \"{GetOutputPattern(video)}\" \"{video.Url}\"");
        return output ? $"Download complete: {video.title}" : "Video download failed.";
    }

    public string DownloadFolder => settingsService.Load().DownloadFolder;

    public void OpenDownloadFolder() => OpenWithShell(DownloadFolder);

    public void OpenInBrowser(YouTubeVideo video) => OpenWithShell(video.Url);

    public void PlayVideo(YouTubeVideo video)
    {
        if (!TryStartProcess("mpv", $"\"{video.Url}\"") && !TryStartProcess("mpv.exe", $"\"{video.Url}\""))
        {
            OpenWithShell(video.Url);
        }
    }

    public void PlayAudio(YouTubeVideo video)
    {
        if (!TryStartProcess("mpv", $"--no-video \"{video.Url}\"") && !TryStartProcess("mpv.exe", $"--no-video \"{video.Url}\""))
        {
            OpenWithShell(video.Url);
        }
    }

    private async Task<bool> RunYtDlpAsync(string args)
    {
        Directory.CreateDirectory(DownloadFolder);
        var ytDlpPath = await toolManager.EnsureYtDlpAsync();

        var psi = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    private static bool TryStartProcess(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void OpenWithShell(string target)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
    }

    private string GetOutputPattern(YouTubeVideo video)
    {
        var safe = string.Concat(video.title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(DownloadFolder, $"{safe}.%(ext)s");
    }
}
