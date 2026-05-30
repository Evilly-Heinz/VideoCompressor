# Shared Core Library (Milestone A)

**Branch:** `feature/milestone-a-shared-core`  
**Spec:** `specs/2026-05-29-shared-core/`  
**Release:** `v1.1.0` (tag after merge)

## Implementation Steps

1. **Created `VideoCompressor.Core`** — `net8.0` class library with Xabe.FFmpeg 6.0.2 and Xabe.FFmpeg.Downloader 6.0.2.
2. **Added to solution** — `VideoCompressor.sln` includes Core; `VideoCompressorUI` references Core via `<ProjectReference>`.
3. **FfmpegLocator** — resolves `{appBase}/ffmpeg/ffmpeg.exe` and `ffprobe.exe`.
4. **FfmpegBootstrap** — `ConfigurePaths()` and `EnsureAvailableAsync()` (download on first run).
5. **CompressionOptions** — input, output, CRF (18–40), preset, target height; validates before encode.
6. **OutputPathResolver** — suffix/folder rules matching previous `GetOutputPath()`.
7. **CompressionService** — H.264/AAC encode with progress callback and cancellation.
8. **MediaProbeService** — source resolution and media metadata for queue extras.
9. **SizeEstimateService** — pre-compress size estimate (same formula as before).
10. **BatchCompressionService** — sequential batch with index-based callbacks; stops on cancel.
11. **MainWindow wired to Core** — removed direct Xabe usage; UI handles Dispatcher, queue bindings, dialogs only.

## New files

```
VideoCompressor.Core/
├── VideoCompressor.Core.csproj
├── FfmpegLocator.cs
├── FfmpegBootstrap.cs
├── CompressionOptions.cs
├── CompressionItemStatus.cs
├── CompressionItemResult.cs
├── OutputPathResolver.cs
├── CompressionService.cs
├── MediaProbeService.cs
├── SizeEstimateService.cs
└── BatchCompressionService.cs
```

## How to Test

Build:

```powershell
dotnet build VideoCompressor.sln -c Release
```

Run:

```powershell
.\bin\Release\VideoCompressorUI.exe
```

Manual regression (see `specs/2026-05-29-shared-core/validation.md`):

| # | Test | Expected |
|---|------|----------|
| 1 | Single file compress (defaults) | `{name}_compressed.mp4` beside source; Done ✓ |
| 2 | Custom suffix + output folder | Output in chosen folder with custom suffix |
| 3 | Resolution 720p | Scaled output; estimate updates |
| 4 | Batch (3 files) | Sequential Done ✓; summary window |
| 5 | Cancel mid-encode | Item Cancelled; batch stops |
| 6 | Size estimate card | Updates on CRF/resolution change |
| 7 | First-run FFmpeg download | Delete `bin/Release/ffmpeg/`; compress triggers download |

**Build result:** Release build succeeded (2026-05-29). Manual GUI tests not run in CI — execute locally before merge.

## Notes

- Living implementation log: `specs/2026-05-29-shared-core/implementation-note.html`
- T11 (`v1.1.0` tag) deferred until merge to `main`
