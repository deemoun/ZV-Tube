using System.Diagnostics;
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
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("--print-json");
        psi.ArgumentList.Add("--skip-download");
        psi.ArgumentList.Add("--no-warnings");
        psi.ArgumentList.Add($"ytsearch20:{query}");

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start yt-dlp.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
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

            if (process.ExitCode != 0)
            {
                var stderrLines = stderr
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

                var details = stderrLines.Count > 0 ? string.Join(Environment.NewLine, stderrLines.TakeLast(6)) : "Unknown yt-dlp error.";
                throw new InvalidOperationException($"yt-dlp search failed (exit code {process.ExitCode}). {details}");
            }

            return results;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                await process.WaitForExitAsync();
            }
        }
    }

    public async Task<string> DownloadAudioAsync(YouTubeVideo video)
    {
        var output = await RunYtDlpAsync(video, "-f bestaudio --extract-audio --audio-format mp3");
        return output ? $"Download complete: {video.title}" : "Audio download failed.";
    }

    public async Task<string> DownloadVideoAsync(YouTubeVideo video)
    {
        var output = await RunYtDlpAsync(video, "-f bestvideo+bestaudio --merge-output-format mp4");
        return output ? $"Download complete: {video.title}" : "Video download failed.";
    }

    public string DownloadFolder => settingsService.Load().DownloadFolder;

    public bool OpenDownloadFolder()
    {
        Directory.CreateDirectory(DownloadFolder);
        return TryOpenWithShell(DownloadFolder);
    }

    public bool OpenInBrowser(YouTubeVideo video) => TryOpenWithShell(video.Url);

    public void PlayVideo(YouTubeVideo video)
    {
        if (!TryStartProcess("mpv", $"\"{video.Url}\"") && !TryStartProcess("mpv.exe", $"\"{video.Url}\""))
        {
            TryOpenWithShell(video.Url);
        }
    }

    public void PlayAudio(YouTubeVideo video)
    {
        if (!TryStartProcess("mpv", $"--no-video \"{video.Url}\"") && !TryStartProcess("mpv.exe", $"--no-video \"{video.Url}\""))
        {
            TryOpenWithShell(video.Url);
        }
    }

    private async Task<bool> RunYtDlpAsync(YouTubeVideo video, string modeArgs)
    {
        Directory.CreateDirectory(DownloadFolder);
        var tools = await toolManager.EnsureToolsAsync();

        var psi = new ProcessStartInfo
        {
            FileName = tools.YtDlpPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        foreach (var arg in modeArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            psi.ArgumentList.Add(arg);
        }

        psi.ArgumentList.Add("--ffmpeg-location");
        psi.ArgumentList.Add(tools.FfmpegDirectory);
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(GetOutputPattern(video));
        psi.ArgumentList.Add(video.Url);

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

    private static bool TryOpenWithShell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GetOutputPattern(YouTubeVideo video)
    {
        var safe = string.Concat(video.title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(DownloadFolder, $"{safe}.%(ext)s");
    }
}
