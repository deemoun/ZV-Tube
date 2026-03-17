# ZV Tube (Avalonia)

Cross-platform YouTube search/downloader/player desktop app built with **Avalonia UI** and .NET 8.

## Features
- Search YouTube using `yt-dlp` JSON output.
- Play selected video/audio (uses `mpv` if available; falls back to browser).
- Download audio (mp3) and video (mp4) through `yt-dlp`.
- Light/Dark theme toggle.
- Settings persisted in user-local folder:
  - Windows: `%LOCALAPPDATA%/ZV-Tube/settings.json`
  - Linux/macOS: `$HOME/.local/share` equivalent via .NET `LocalApplicationData`.
- `yt-dlp` binary is auto-downloaded per-platform on first use and cached in the app-local `tools` folder.
- `ffmpeg` is also auto-downloaded, cached locally, and passed to `yt-dlp` via `--ffmpeg-location` (no PATH dependency).
- Localization-ready string service abstraction.

## Build

### Linux AppImage script
```bash
bash scripts/build-appimage.sh
```

### Windows publish script
```powershell
pwsh -File scripts/build-windows.ps1
```

## Notes
- `mpv` is optional for playback.
