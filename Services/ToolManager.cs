using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZVTube.Services;

public class ToolManager
{
    private readonly SettingsService settingsService;

    public ToolManager(SettingsService settingsService)
    {
        this.settingsService = settingsService;
    }

    public async Task<string> EnsureYtDlpAsync()
    {
        var toolsDir = Path.Combine(settingsService.AppDirectory, "tools");
        Directory.CreateDirectory(toolsDir);

        var toolName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
        var targetPath = Path.Combine(toolsDir, toolName);

        if (File.Exists(targetPath))
        {
            return targetPath;
        }

        var url = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
            : "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";

        using var http = new HttpClient();
        var data = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(targetPath, data);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{targetPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            })?.WaitForExit();
        }

        return targetPath;
    }
}
