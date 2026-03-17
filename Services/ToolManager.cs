using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ZVTube.Services;

public sealed class ToolManager
{
    private readonly SettingsService settingsService;
    private readonly SemaphoreSlim setupLock = new(1, 1);

    public ToolManager(SettingsService settingsService)
    {
        this.settingsService = settingsService;
    }

    public async Task<string> EnsureYtDlpAsync()
    {
        await setupLock.WaitAsync();
        try
        {
            var toolsRoot = Path.Combine(settingsService.AppDirectory, "tools");
            Directory.CreateDirectory(toolsRoot);
            return await DownloadYtDlpIfNeededAsync(toolsRoot);
        }
        finally
        {
            setupLock.Release();
        }
    }

    public async Task<ToolPaths> EnsureToolsAsync()
    {
        await setupLock.WaitAsync();
        try
        {
            var toolsRoot = Path.Combine(settingsService.AppDirectory, "tools");
            Directory.CreateDirectory(toolsRoot);

            var ytDlpPath = await DownloadYtDlpIfNeededAsync(toolsRoot);
            var ffmpegDirectory = await DownloadFfmpegIfNeededAsync(toolsRoot);

            var ffplayPath = ResolveOptionalBinary(ffmpegDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffplay.exe" : "ffplay");

            return new ToolPaths(ytDlpPath, ffmpegDirectory, ffplayPath);
        }
        finally
        {
            setupLock.Release();
        }
    }

    private static async Task<string> DownloadYtDlpIfNeededAsync(string toolsRoot)
    {
        var ytDlpDir = Path.Combine(toolsRoot, "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);

        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
        var ytDlpPath = Path.Combine(ytDlpDir, executableName);

        if (File.Exists(ytDlpPath) && new FileInfo(ytDlpPath).Length > 0)
        {
            EnsureExecutablePermission(ytDlpPath);
            return ytDlpPath;
        }

        var url = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
            : "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";

        await DownloadFileAsync(url, ytDlpPath);
        EnsureExecutablePermission(ytDlpPath);

        return ytDlpPath;
    }

    private static async Task<string> DownloadFfmpegIfNeededAsync(string toolsRoot)
    {
        var ffmpegDir = Path.Combine(toolsRoot, "ffmpeg");
        Directory.CreateDirectory(ffmpegDir);

        var ffmpegBinary = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(ffmpegDir, "ffmpeg.exe")
            : Path.Combine(ffmpegDir, "ffmpeg");

        if (File.Exists(ffmpegBinary))
        {
            return ffmpegDir;
        }

        var url = await ResolveFfmpegArchiveUrlAsync();
        var archivePath = Path.Combine(ffmpegDir, Path.GetFileName(new Uri(url).LocalPath));

        await DownloadFileAsync(url, archivePath);

        var extractDir = Path.Combine(ffmpegDir, "_extract");
        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, true);
        }

        ExtractArchiveToDirectory(archivePath, extractDir);

        var extractedBinary = Directory
            .EnumerateFiles(extractDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg", SearchOption.AllDirectories)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(Path.GetDirectoryName(f)), "bin", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Unable to find ffmpeg in downloaded archive.");

        File.Copy(extractedBinary, ffmpegBinary, overwrite: true);
        EnsureExecutablePermission(ffmpegBinary);

        var ffprobeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffprobe.exe" : "ffprobe";
        var extractedProbe = Directory
            .EnumerateFiles(extractDir, ffprobeName, SearchOption.AllDirectories)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(Path.GetDirectoryName(f)), "bin", StringComparison.OrdinalIgnoreCase));

        if (extractedProbe is not null)
        {
            var ffprobeTarget = Path.Combine(ffmpegDir, ffprobeName);
            File.Copy(extractedProbe, ffprobeTarget, overwrite: true);
            EnsureExecutablePermission(ffprobeTarget);
        }

        var ffplayName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffplay.exe" : "ffplay";
        var extractedPlay = Directory
            .EnumerateFiles(extractDir, ffplayName, SearchOption.AllDirectories)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(Path.GetDirectoryName(f)), "bin", StringComparison.OrdinalIgnoreCase));

        if (extractedPlay is not null)
        {
            var ffplayTarget = Path.Combine(ffmpegDir, ffplayName);
            File.Copy(extractedPlay, ffplayTarget, overwrite: true);
            EnsureExecutablePermission(ffplayTarget);
        }

        File.Delete(archivePath);
        Directory.Delete(extractDir, true);

        return ffmpegDir;
    }

    private static async Task DownloadFileAsync(string url, string targetPath)
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var destination = File.Create(targetPath);
        await source.CopyToAsync(destination);
    }

    private static async Task<string> ResolveFfmpegArchiveUrlAsync()
    {
        const string latestReleaseApi = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest";

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ZV-Tube");

        using var response = await http.GetAsync(latestReleaseApi, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        if (!doc.RootElement.TryGetProperty("assets", out var assets))
        {
            throw new InvalidOperationException("Unable to resolve FFmpeg download URL from release metadata.");
        }

        var platformToken = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "win64"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "macos64"
                : "linux64";

        string? bestCandidate = null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var downloadUrl = asset.GetProperty("browser_download_url").GetString();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            if (!name.Contains(platformToken, StringComparison.OrdinalIgnoreCase) || !IsSupportedArchive(name))
            {
                continue;
            }

            if (!name.Contains("-gpl", StringComparison.OrdinalIgnoreCase) && !name.Contains("-lgpl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (bestCandidate is null || name.Contains("-lgpl", StringComparison.OrdinalIgnoreCase))
            {
                bestCandidate = downloadUrl;
            }
        }

        return bestCandidate
            ?? throw new InvalidOperationException($"Unable to find a compatible FFmpeg build for platform token '{platformToken}'.");
    }

    private static void EnsureExecutablePermission(string filePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using var chmod = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{filePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        chmod?.WaitForExit();
    }

    private static bool IsSupportedArchive(string fileName)
        => fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);

    private static void ExtractArchiveToDirectory(string archivePath, string destinationDirectory)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destinationDirectory);
            return;
        }

        using var tar = Process.Start(new ProcessStartInfo
        {
            FileName = "tar",
            ArgumentList = { "-xf", archivePath, "-C", destinationDirectory },
            UseShellExecute = false,
            CreateNoWindow = true
        });

        tar?.WaitForExit();

        if (tar is null || tar.ExitCode != 0)
        {
            throw new InvalidOperationException($"Unable to extract FFmpeg archive '{Path.GetFileName(archivePath)}'.");
        }
    }

    private static string? ResolveOptionalBinary(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        return File.Exists(path) ? path : null;
    }
}

public sealed record ToolPaths(string YtDlpPath, string FfmpegDirectory, string? FfplayPath);
