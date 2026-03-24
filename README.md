# Video Compressor

A lightweight Windows video compressor with a Fluent dark UI.  
Drop a video file, pick quality/speed, click **Compress** — done.

Output: `{original_name}_compressed.mp4` saved next to the source file.

---

## Features

- Drag-and-drop or browse to select video
- H.264 + AAC re-encode via FFmpeg (auto-downloaded on first use)
- Adjustable CRF quality slider (18 – 40) and speed preset
- Live progress bar with percentage
- One-click **Explorer right-click menu** registration ("Compress this video")
- Single-file `.exe`, no installer required

---

## Requirements (end users)

| Requirement | Notes |
|---|---|
| Windows 10 / 11 x64 | |
| [.NET 8 Desktop Runtime](https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe) | ~55 MB, one-time install |
| Internet connection (first run only) | FFmpeg binaries are downloaded automatically (~70 MB) and cached in `<app folder>\ffmpeg\` |

---

## Quick start

1. Download the latest `VideoCompressor-vX.Y.Z.zip` from [Releases](../../releases).
2. Extract anywhere (e.g. `C:\Tools\VideoCompressor\`).
3. Install [.NET 8 Desktop Runtime](https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe) if not already present.
4. Run `VideoCompressorUI.exe`.
5. On first compression, FFmpeg is downloaded automatically — no manual setup needed.

### Register the right-click menu (optional)

In the app, scroll to the **Explorer Integration** card at the bottom and click **Register**.  
A UAC prompt will appear; click **Yes**.  
After that, right-clicking any `.mp4`, `.mov`, `.avi`, `.mkv`, `.wmv`, `.flv`, `.webm`, or `.m4v`  
file in Explorer shows **"Compress this video"**, which opens the app with that file pre-loaded.

---

## Project structure

```
VideoCompressor.sln
│
├── VideoCompressorUI\              ← C# WPF project (.NET 8, x64)
│   ├── VideoCompressorUI.csproj
│   ├── app.manifest                ← PerMonitorV2 DPI, Win11
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml             ← Fluent dark UI, custom title bar
│   ├── MainWindow.xaml.cs
│   └── Themes\Styles.xaml          ← Win11 Fluent color tokens + styles
│
├── scripts\
│   └── install_context_menu.reg    ← registry template (app auto-generates this)
│
└── .github\workflows\
    └── build.yml                   ← CI: build → zip → GitHub Release
```

Output: `bin\Release\VideoCompressorUI.exe`

---

## Build from source

### Requirements

| Component | Notes |
|---|---|
| Visual Studio 2022 (v17+) | Community / Pro / Enterprise |
| Workload: **.NET desktop development** | includes .NET 8 SDK |

NuGet packages are restored automatically:

| Package | Purpose |
|---|---|
| `Xabe.FFmpeg` 6.0.2 | FFmpeg wrapper |
| `Xabe.FFmpeg.Downloader` 6.0.2 | Auto-downloads FFmpeg binaries on first use |
| `System.Text.Json` 9.0.0 | JSON support (transitive dependency) |

### Steps

```bash
# Clone
git clone <repo-url>
cd VideoCompressor2

# Restore & publish (single-file, framework-dependent)
dotnet publish VideoCompressorUI/VideoCompressorUI.csproj -c Release -r win-x64 -o ./publish
```

Or open `VideoCompressor.sln` in Visual Studio 2022, select **Release | x64**, and press `Ctrl+Shift+B`.

---

## CI / CD

GitHub Actions workflow: `.github/workflows/build.yml`

| Trigger | Result |
|---|---|
| Push to `main` | Build + upload artifact |
| Push tag `v1.0.0` | Build + upload artifact + **GitHub Release** with zip attached |
| Pull request to `main` | Build only |
| Manual (`workflow_dispatch`) | Build + upload artifact |

To publish a release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## Technical notes

- **Single project**: C++ wrapper removed; FFmpeg is invoked via `Xabe.FFmpeg` NuGet — no separate `.exe` needed.
- **Framework-dependent publish**: avoids extracting the .NET runtime to `%TEMP%`, which reduces antivirus false positives.
- **FFmpeg path**: binaries are stored in `<AppDir>\ffmpeg\` and downloaded once via `Xabe.FFmpeg.Downloader`.
- **Context menu**: written as UTF-16 LE `.reg` and imported via `regedit.exe /s` with UAC elevation — the app remains non-elevated at all other times.
- **UI**: custom title bar with `WindowChrome`, Win11 Fluent dark palette, Segoe UI Variable Text, min/max/close caption buttons.
- **Target**: `net8.0-windows`, `win-x64`, single-file exe.
