using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace ZVTube.Services;

public sealed class ToolManager
{
    private readonly SettingsService settingsService;
    private readonly SemaphoreSlim setupLock = new(1, 1);

    public ToolManager(SettingsService settingsService)
    {
        this.settingsService = settingsService;
    }

    public async Task<ToolPaths> EnsureToolsAsync()
    {
        await setupLock.WaitAsync();
        try
        {
            var toolsRoot = Path.Combine(settingsService.AppDirectory, "tools");
            Directory.CreateDirectory(toolsRoot);

            var ytDlpPath = await EnsureYtDlpAsync(toolsRoot);
            var ffmpegDirectory = await EnsureFfmpegAsync(toolsRoot);

            return new ToolPaths(ytDlpPath, ffmpegDirectory);
        }
        finally
        {
            setupLock.Release();
        }
    }

    private static async Task<string> EnsureYtDlpAsync(string toolsRoot)
    {
        var ytDlpDir = Path.Combine(toolsRoot, "yt-dlp");
        Directory.CreateDirectory(ytDlpDir);

        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
        var ytDlpPath = Path.Combine(ytDlpDir, executableName);

        if (File.Exists(ytDlpPath))
        {
            return ytDlpPath;
        }

        var url = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
            : "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";

        await DownloadFileAsync(url, ytDlpPath);
        EnsureExecutablePermission(ytDlpPath);

        return ytDlpPath;
    }

    private static async Task<string> EnsureFfmpegAsync(string toolsRoot)
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

        var archivePath = Path.Combine(ffmpegDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.zip" : "ffmpeg-linux64.zip");
        var url = "https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-linux64-lgpl.zip";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            url = "https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-lgpl.zip";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            url = "https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-macos64-lgpl.zip";
            archivePath = Path.Combine(ffmpegDir, "ffmpeg-macos64.zip");
        }

        await DownloadFileAsync(url, archivePath);

        var extractDir = Path.Combine(ffmpegDir, "_extract");
        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, true);
        }

        ZipFile.ExtractToDirectory(archivePath, extractDir);

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
}

public sealed record ToolPaths(string YtDlpPath, string FfmpegDirectory);
