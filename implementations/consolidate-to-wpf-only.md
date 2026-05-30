# Consolidate to Single WPF Project with FFmpeg via NuGet

## What Changed

The solution was restructured from two projects (C++ CLI backend + C# WPF frontend) to a **single WPF project** that calls FFmpeg directly via NuGet packages.

### Before
```
VideoCompressor.sln
├── Compressor\VideoCompressor.vcxproj   (C++ — wraps FFmpeg, spawns process)
└── VideoCompressorUI\VideoCompressorUI.csproj  (WPF — spawns C++ exe, parses stdout)
```

### After
```
VideoCompressor.sln
└── VideoCompressorUI\VideoCompressorUI.csproj  (WPF — calls FFmpeg directly via Xabe.FFmpeg)
```

## NuGet Packages Added

| Package | Version | Purpose |
|---------|---------|---------|
| `Xabe.FFmpeg` | 6.0.2 | High-level C# API for FFmpeg — build conversions, get media info, track progress |
| `Xabe.FFmpeg.Downloader` | 6.0.2 | Downloads FFmpeg binaries from GitHub on first run (to `bin\ffmpeg\`) |

## FFmpeg Binary Management

FFmpeg binaries (`ffmpeg.exe`, `ffprobe.exe`) are **not bundled** in the repo.  
On first compression, `FFmpegDownloader.GetLatestVersion()` downloads them to `<app dir>\ffmpeg\` (~70 MB, one-time download).  
The UI shows `"Downloading FFmpeg…"` status during this step.

## Key Code Changes

### `App.xaml.cs`
Sets the FFmpeg executable path on startup:
```csharp
FFmpeg.SetExecutablesPath(Path.Combine(BaseDirectory, "ffmpeg"));
```

### `MainWindow.xaml.cs`
Replaced `Process`-based logic with direct `Xabe.FFmpeg` calls:
```csharp
IMediaInfo info = await FFmpeg.GetMediaInfo(inputFile, cts.Token);

var conversion = FFmpeg.Conversions.New()
    .SetOutput(outputPath)
    .SetOverwriteOutput(true)
    .AddStream(video.SetCodec(VideoCodec.h264))
    .AddStream(audio.SetCodec(AudioCodec.aac))
    .AddParameter($"-crf {crf}",          ParameterPosition.PostInput)
    .AddParameter($"-preset {preset}",    ParameterPosition.PostInput)
    .AddParameter("-b:a 128k",            ParameterPosition.PostInput)
    .AddParameter("-movflags +faststart", ParameterPosition.PostInput);

conversion.OnProgress += (_, args) => UpdateProgressBar(args.Percent);
await conversion.Start(cts.Token);
```

Cancel via `CancellationTokenSource` instead of `Process.Kill()`.

## Files Changed

| File | Change |
|------|--------|
| `VideoCompressor.sln` | Removed C++ project reference |
| `VideoCompressorUI\VideoCompressorUI.csproj` | Added `Xabe.FFmpeg` and `Xabe.FFmpeg.Downloader` packages |
| `VideoCompressorUI\MainWindow.xaml.cs` | Full rewrite — no more `Process` spawning |
| `VideoCompressorUI\App.xaml.cs` | Added FFmpeg path initialization |

> The `Compressor\` folder (C++ source) is kept on disk but is no longer part of any project build.

## How to Test

1. Build: open VS 2022 → `VideoCompressor.sln` → Build → Build Solution (or `msbuild VideoCompressor.sln /p:Configuration=Debug /p:Platform=x64`)
2. Run `bin\Debug\VideoCompressorUI.exe`
3. On first use: the app will download FFmpeg to `bin\Debug\ffmpeg\` automatically — watch the "Downloading FFmpeg…" status in the progress panel
4. Drop or browse a video file
5. Click Compress — progress bar should count up from 0 → 100%
6. After completion, a dialog shows input/output sizes and offers to open the output folder
7. Click "■ Cancel" during compression — operation should stop cleanly without crashing

## Requirements

- .NET 8 Desktop Runtime installed on target machine ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Internet connection on first run (for FFmpeg download) — subsequent runs work offline
