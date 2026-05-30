# Requirements: Shared Core Library (Milestone A)

**Source:** [roadmap.md](../roadmap.md) — Milestone A (phases A1–A6)  
**Summary:** Extract all compression-related logic from WPF into `VideoCompressor.Core`; GUI behavior unchanged.  
**Branch:** `feature/milestone-a-shared-core`  
**Target release:** `v1.1.0` (internal refactor; no user-visible change)

---

## Context

Video Compressor today is a single WPF project (`VideoCompressorUI`) with FFmpeg encode, batch queue orchestration, resolution scaling, output-path rules, and size estimation implemented directly in `MainWindow.xaml.cs`. The stakeholder extension (CLI, registry cleanup, installer, auto-update) requires a shared, headless-capable compression layer.

Milestone A is the foundation: create `VideoCompressor.Core` (`net8.0`, no WPF) and move compression logic out of the UI shell without changing end-user behavior.

## Problem statement

| Layer | Finding |
|-------|---------|
| Architecture | Compression, FFmpeg bootstrap, output paths, and estimates live in WPF code-behind — not reusable by CLI (Milestone B). |
| Maintainability | `MainWindow.xaml.cs` mixes UI state, batch orchestration, and FFmpeg/Xabe calls in one file (~780 lines). |
| Roadmap blocker | Milestones B–E depend on a platform-neutral Core library per [tech-stack.md](../tech-stack.md). |

## Scope

### In scope

- Create `VideoCompressor.Core` class library (`net8.0`, no WPF/WinForms dependency).
- Extract FFmpeg path resolution into `FfmpegLocator` (exe/ffprobe under `{appBase}/ffmpeg`).
- Extract encode parameters into `CompressionOptions` (CRF 18–40, x264 preset, input, output, optional resolution height).
- Extract encode execution into `CompressionService` — async, `CancellationToken`, progress callback.
- Extract output path logic into Core (`GetOutputPath`) matching current GUI rules: custom suffix (default `_compressed`), optional output folder, always `.mp4`.
- Extract batch compression orchestration into Core (sequential pending items, cancel stops current + batch).
- Extract size estimation logic into Core (CRF factor, resolution factor, 128 kbps audio assumption).
- Extract media probe helpers used by estimate and resolution display (duration, video height, bitrate).
- Wire `MainWindow.xaml.cs` to call Core types; GUI appearance and behavior unchanged.
- Add Core project to `VideoCompressor.sln`; UI references Core.
- Manual GUI regression only (no new automated test project for this milestone).

### Out of scope

- CLI argument parsing, exit codes, headless bootstrap (Milestone B).
- Registry cleanup, context-menu registration changes (Milestone C).
- Installer, auto-update (Milestones D–E).
- Unit/integration test projects.
- User-visible feature changes, UI redesign, new compression options.
- Moving WPF-specific concerns to Core: thumbnails (`GetShellThumbnail`), Explorer registration, window chrome, queue item brushes/bindings.
- Legacy C++ `Compressor/VideoCompressor.cpp` — may remain in repo but is not part of this milestone.

## Functional requirements

1. **FfmpegLocator** resolves `ffmpeg.exe` and `ffprobe.exe` paths under the application base directory `ffmpeg/` subfolder.
2. **FfmpegBootstrap** (or equivalent) ensures binaries exist — download via Xabe.FFmpeg.Downloader when missing (same behavior as current first-run download in GUI).
3. **CompressionOptions** validates CRF (18–40), preset (known x264 presets), non-empty input path, output path; optional target height for scale filter.
4. **OutputPathResolver** produces output paths using: `outputFolder ?? inputDirectory`, `fileNameWithoutExtension + suffix + ".mp4"`, default suffix `_compressed` when blank.
5. **CompressionService.CompressAsync** performs H.264/AAC encode via Xabe.FFmpeg with: `-crf`, `-preset`, `-b:a 128k`, `-movflags +faststart`, optional `-vf scale=-2:{height}`; reports progress 0–100 via callback; honors cancellation (throws `OperationCanceledException` consistent with current GUI).
6. **BatchCompressionService** (or method on CompressionService) compresses a list of items sequentially; stops on cancel; returns per-item status.
7. **SizeEstimateService** computes estimated output bytes for pending items given CRF and optional target height — same formula as current `UpdateEstimate()` in MainWindow.
8. **MediaProbeService** returns source resolution and media info needed by queue extras and estimate.
9. WPF `MainWindow` delegates to Core for all above; UI only handles Dispatcher updates, progress bar, status text, and dialogs.

## Decisions

| # | Decision | Detail |
|---|----------|--------|
| D1 | Full compression extraction | User confirmed: move all compression-related logic (encode, batch, resolution, estimate, output path) — not minimal roadmap subset. |
| D2 | No unit tests this milestone | Validation is manual GUI regression per user confirmation. |
| D3 | Preserve cancel behavior | `CompressionService` must accept `CancellationToken` and match existing GUI cancel-button semantics. |
| D4 | Output path parity | Core replicates exact GUI suffix/folder rules; overwrite prompt stays in WPF if present today (`SetOverwriteOutput(true)` in encode). |
| D5 | Core target framework | `net8.0` (not `-windows`) — no WPF dependency; Xabe.FFmpeg packages referenced from Core. |
| D6 | Progress callback | Core uses `Action<int>` or `IProgress<int>` — WPF maps to `BottomProgress` and `QueueItem.Progress` on Dispatcher. |
| D7 | No Jira ticket | Spec references roadmap Milestone A only. |
| D8 | Release tag | Ship as `v1.1.0` after A6 validation passes. |

## Technical touchpoints

| File / area | Role |
|-------------|------|
| `VideoCompressor.Core/` (new) | Class library: locator, options, services |
| `VideoCompressor.Core/FfmpegLocator.cs` | Static paths for ffmpeg/ffprobe |
| `VideoCompressor.Core/CompressionOptions.cs` | Encode parameters + validation |
| `VideoCompressor.Core/CompressionService.cs` | Single-file async encode |
| `VideoCompressor.Core/OutputPathResolver.cs` | Output path from input + folder + suffix |
| `VideoCompressor.Core/SizeEstimateService.cs` | Pre-compress size estimate |
| `VideoCompressor.Core/BatchCompressionService.cs` | Sequential batch orchestration |
| `VideoCompressorUI/MainWindow.xaml.cs` | Thin UI shell calling Core |
| `VideoCompressor.sln` | Add Core project + reference |
| `VideoCompressorUI/VideoCompressorUI.csproj` | ProjectReference to Core; may remove direct Xabe refs if only Core uses them |

### Current logic to migrate (reference)

- `FfmpegDir`, FFmpeg download block — lines ~130–131, ~406–421
- `CompressItem` — lines ~452–520 (encode + progress + cancel)
- `StartBatchCompression` — lines ~392–450 (batch loop)
- `UpdateEstimate` — lines ~556–607 (size formula)
- `GetOutputPath` — lines ~674–680
- `GetSelectedPreset` / resolution — mapped to options from UI controls

## Dependencies

| Dependency | Notes |
|------------|-------|
| [mission.md](../mission.md) | Shared library powers GUI + future CLI |
| [tech-stack.md](../tech-stack.md) | Core structure, Xabe.FFmpeg 6.0.2, H.264/AAC |
| [roadmap.md](../roadmap.md) | Phases A1–A6 ordering |
| Milestone B | Blocked until A complete |
| Xabe.FFmpeg 6.0.2 | Encode and probe — move package ref to Core |

## Related documents

- [Requirements.md](../../Requirements.md) — stakeholder source (CLI/registry/installer)
- [implementations/](../../implementations/) — implementation notes written when work completes
