using System.Diagnostics;
using System.Runtime.InteropServices;
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

    public async Task<List<YouTubeVideo>> SearchAsync(
        string query,
        CancellationToken cancellationToken,
        IProgress<string>? logger = null,
        IProgress<YouTubeVideo>? resultProgress = null)
    {
        logger?.Report("Ensuring yt-dlp is available...");
        var ytDlpPath = await toolManager.EnsureYtDlpAsync();
        logger?.Report($"Using yt-dlp: {ytDlpPath}");
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
        psi.ArgumentList.Add("--ignore-errors");
        psi.ArgumentList.Add("--no-warnings");
        psi.ArgumentList.Add($"ytsearch20:{query}");

        logger?.Report($"Running search for '{query}'...");

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start yt-dlp.");
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            while (!process.StandardOutput.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line) || !line.TrimStart().StartsWith("{"))
                {
                    continue;
                }

                var video = JsonSerializer.Deserialize<YouTubeVideo>(line);
                if (video is null)
                {
                    continue;
                }

                results.Add(video);
                resultProgress?.Report(video);
            }

            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;

            logger?.Report($"yt-dlp exited with code {process.ExitCode}. Parsed {results.Count} videos.");

            var stderrLines = stderr
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (stderrLines.Count > 0)
            {
                foreach (var line in stderrLines.TakeLast(6))
                {
                    logger?.Report($"yt-dlp: {line}");
                }
            }

            if (process.ExitCode != 0 && results.Count == 0)
            {
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
        var result = await RunYtDlpAsync(video, args =>
        {
            args.Add("-f");
            args.Add("bestaudio/best");
            args.Add("-x");
            args.Add("--audio-format");
            args.Add("mp3");
            args.Add("--audio-quality");
            args.Add("0");
        });

        return BuildDownloadStatus(result, "Audio");
    }

    public async Task<string> DownloadVideoAsync(YouTubeVideo video)
    {
        var result = await RunYtDlpAsync(video, _ => { }, requireFfmpeg: true);

        return BuildDownloadStatus(result, "Video");
    }

    public string DownloadFolder => settingsService.Load().DownloadFolder;

    public bool OpenDownloadFolder()
    {
        Directory.CreateDirectory(DownloadFolder);
        return TryOpenWithShell(DownloadFolder);
    }

    public bool OpenInBrowser(YouTubeVideo video) => TryOpenWithShell(video.Url);

    public async Task<bool> PlayVideoAsync(YouTubeVideo video)
    {
        var ytDlpPath = await toolManager.EnsureYtDlpAsync();
        var streamUrl = await ResolveStreamUrlAsync(ytDlpPath, video.Url, "best[acodec!=none][vcodec!=none]/best");
        return !string.IsNullOrWhiteSpace(streamUrl) && TryPlayWithInstalledPlayer(streamUrl, audioOnly: false);
    }

    public async Task<bool> PlayAudioAsync(YouTubeVideo video)
    {
        var ytDlpPath = await toolManager.EnsureYtDlpAsync();
        var streamUrl = await ResolveStreamUrlAsync(ytDlpPath, video.Url, "bestaudio/best");
        return !string.IsNullOrWhiteSpace(streamUrl) && TryPlayWithInstalledPlayer(streamUrl, audioOnly: true);
    }

    private static async Task<string?> ResolveStreamUrlAsync(string ytDlpPath, string videoUrl, string formatSelector)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--get-url");
            psi.ArgumentList.Add("--no-warnings");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(formatSelector);
            psi.ArgumentList.Add(videoUrl);

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return null;
            }

            return stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        }
        catch
        {
            return null;
        }
    }

    private async Task<DownloadResult> RunYtDlpAsync(YouTubeVideo video, Action<List<string>> modeArgsBuilder, bool requireFfmpeg = false)
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

        var modeArgs = new List<string>();
        modeArgsBuilder(modeArgs);
        foreach (var arg in modeArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        if (requireFfmpeg)
        {
            if (string.IsNullOrWhiteSpace(tools.FfmpegDirectory))
            {
                return new DownloadResult(false, null, "FFmpeg is not available, so separate video/audio streams cannot be merged. Please retry after tools finish downloading.");
            }

            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("bestvideo*+bestaudio/best");
        }

        if (!string.IsNullOrWhiteSpace(tools.FfmpegDirectory))
        {
            psi.ArgumentList.Add("--ffmpeg-location");
            psi.ArgumentList.Add(tools.FfmpegDirectory);

            if (requireFfmpeg)
            {
                psi.ArgumentList.Add("--merge-output-format");
                psi.ArgumentList.Add("mp4");
            }
        }
        psi.ArgumentList.Add("--no-playlist");
        psi.ArgumentList.Add("--print");
        psi.ArgumentList.Add("after_move:filepath");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(GetOutputPattern(video));
        psi.ArgumentList.Add(video.Url);

        using var process = Process.Start(psi);
        if (process is null)
        {
            return new DownloadResult(false, null, "Unable to start yt-dlp process.");
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var finalPath = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .LastOrDefault();

        var success = process.ExitCode == 0 && !string.IsNullOrWhiteSpace(finalPath) && File.Exists(finalPath);

        var error = success
            ? null
            : string.IsNullOrWhiteSpace(stderr)
                ? $"yt-dlp failed with exit code {process.ExitCode}."
                : stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();

        return new DownloadResult(success, finalPath, error);
    }

    private static bool TryStartProcess(string fileName, IEnumerable<string> arguments, bool useShellExecute)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = useShellExecute,
                CreateNoWindow = !useShellExecute
            };

            foreach (var argument in arguments)
            {
                psi.ArgumentList.Add(argument);
            }

            Process.Start(psi);
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

    private static bool TryPlayWithInstalledPlayer(string streamUrl, bool audioOnly)
    {
        foreach (var player in GetCandidatePlayers())
        {
            var executable = ResolvePlayerExecutable(player.ExecutableNames, player.KnownLocations);
            if (string.IsNullOrWhiteSpace(executable))
            {
                continue;
            }

            var arguments = audioOnly ? player.AudioArguments : player.VideoArguments;
            var resolvedArguments = arguments.Select(arg => arg == "{url}" ? streamUrl : arg);
            if (TryStartProcess(executable, resolvedArguments, useShellExecute: false))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolvePlayerExecutable(IEnumerable<string> executableNames, IEnumerable<string> knownLocations)
    {
        foreach (var knownLocation in knownLocations)
        {
            if (File.Exists(knownLocation))
            {
                return knownLocation;
            }
        }

        foreach (var executableName in executableNames)
        {
            var pathMatch = FindOnPath(executableName);
            if (!string.IsNullOrWhiteSpace(pathMatch))
            {
                return pathMatch;
            }
        }

        return null;
    }

    private static string? FindOnPath(string fileName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var fullPath = Path.Combine(directory, fileName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Ignore invalid PATH entries.
            }
        }

        return null;
    }

    private static IEnumerable<MediaPlayer> GetCandidatePlayers()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return new MediaPlayer(
                ["vlc.exe"],
                [
                    @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                    @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe"
                ],
                ["{url}"],
                ["--no-video", "{url}"]);

            yield return new MediaPlayer(["mpv.exe"], [], ["{url}"], ["--no-video", "{url}"]);
            yield return new MediaPlayer(["mpc-hc64.exe", "mpc-hc.exe"], [], ["{url}"], ["{url}"]);
            yield return new MediaPlayer(["wmplayer.exe"], [], ["{url}"], ["{url}"]);
            yield return new MediaPlayer(["potplayermini64.exe", "potplayermini.exe"], [], ["{url}"], ["{url}"]);
            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return new MediaPlayer(
                ["vlc"],
                ["/Applications/VLC.app/Contents/MacOS/VLC"],
                ["{url}"],
                ["--no-video", "{url}"]);
            yield return new MediaPlayer(["mpv", "iina"], [], ["{url}"], ["--no-video", "{url}"]);
            yield break;
        }

        yield return new MediaPlayer(["vlc"], [], ["{url}"], ["--no-video", "{url}"]);
        yield return new MediaPlayer(["mpv"], [], ["{url}"], ["--no-video", "{url}"]);
        yield return new MediaPlayer(["celluloid", "totem"], [], ["{url}"], ["{url}"]);
    }

    private string GetOutputPattern(YouTubeVideo video)
    {
        var safe = string.Concat(video.title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(DownloadFolder, $"{safe}.%(ext)s");
    }

    private static string BuildDownloadStatus(DownloadResult result, string label)
    {
        return result.Success
            ? $"{label} download complete: {result.OutputPath}"
            : $"{label} download failed: {result.ErrorMessage ?? "Unknown error."}";
    }

    private sealed record DownloadResult(bool Success, string? OutputPath, string? ErrorMessage);
    private sealed record MediaPlayer(
        IReadOnlyList<string> ExecutableNames,
        IReadOnlyList<string> KnownLocations,
        IReadOnlyList<string> VideoArguments,
        IReadOnlyList<string> AudioArguments);
}
