# Implementation plan: Milestone A — Shared Core Library

**Source:** [roadmap.md](../roadmap.md) Milestone A  
**Branch:** `feature/milestone-a-shared-core`  
**Spec folder:** `specs/2026-05-29-shared-core/`

---

## Task summary

| ID | Role | Description | Estimate |
|----|------|-------------|----------|
| T1 | Dev | A1 — Create `VideoCompressor.Core` project, add to solution, UI project reference | 0.5 h |
| T2 | Dev | A2 — Implement `FfmpegLocator` + FFmpeg bootstrap/download helper | 1 h |
| T3 | Dev | A3 — Implement `CompressionOptions` with validation | 0.5 h |
| T4 | Dev | A4 — Implement `CompressionService` (async encode, progress, cancellation) | 2 h |
| T5 | Dev | A6 — Implement `OutputPathResolver` matching GUI rules | 0.5 h |
| T6 | Dev | Extract `SizeEstimateService` + `MediaProbeService` from MainWindow | 1.5 h |
| T7 | Dev | Extract `BatchCompressionService` sequential orchestration | 1 h |
| T8 | Dev | A5 — Wire `MainWindow.xaml.cs` to Core; remove duplicated logic | 2 h |
| T9 | Dev | Build verification, fix references, move NuGet packages to Core | 0.5 h |
| T10 | QA / Dev | Manual regression per validation.md; write implementations note | 1.5 h |
| T11 | Dev | Tag `v1.1.0` after merge (release step) | 0.25 h |
| | | **Total** | **~11.25 h (~1.5 days)** |

---

## 1. Project scaffold (A1) — T1

1. Add `VideoCompressor.Core/VideoCompressor.Core.csproj` targeting `net8.0`.
2. Add project to `VideoCompressor.sln`.
3. Add `<ProjectReference>` from `VideoCompressorUI` to Core.
4. Add NuGet packages to Core: `Xabe.FFmpeg` 6.0.2, `Xabe.FFmpeg.Downloader` 6.0.2.

**Deliverable:** Empty Core builds; UI builds with reference (no behavior change yet).

---

## 2. FFmpeg location and bootstrap (A2) — T2

1. Create `FfmpegLocator` with `FfmpegDirectory`, `FfmpegExePath`, `FfprobeExePath` (base dir + `ffmpeg/`).
2. Create `FfmpegBootstrap.EnsureAvailableAsync(CancellationToken)` — create directory, download if `ffmpeg.exe` missing, call `FFmpeg.SetExecutablesPath`.
3. Mirror current MainWindow first-run download behavior (~70 MB, Official channel).

**Deliverable:** Core can locate and bootstrap FFmpeg without WPF.

---

## 3. Compression options (A3) — T3

1. Create `CompressionOptions` POCO: `InputPath`, `OutputPath`, `Crf` (default 23), `Preset` (default `medium`), `TargetHeight` (optional, 0 = no scale).
2. Add `Validate()` or factory that throws/returns errors for invalid CRF, missing input, bad preset.
3. Map x264 presets to same set as GUI ComboBox tags.

**Deliverable:** Validated options object usable by service and future CLI.

---

## 4. Output path resolver (A6) — T5

1. Create `OutputPathResolver.Resolve(inputPath, outputFolder?, suffix?)`.
2. Rules: folder = `outputFolder ?? Path.GetDirectoryName(input)`; suffix defaults to `_compressed`; extension always `.mp4`.
3. Unit-testable pure function (even if no test project — design for it).

**Deliverable:** Same paths as current `GetOutputPath()` for all GUI settings combinations.

---

## 5. Single-file compression service (A4) — T4

1. Create `CompressionService.CompressAsync(CompressionOptions, IProgress<int>?, CancellationToken)`.
2. Port logic from `CompressItem`: probe media, H.264 + AAC, CRF, preset, 128k audio, faststart, optional scale filter.
3. Wire Xabe `OnProgress` to progress callback (0–100 clamped).
4. Honor cancellation; propagate `OperationCanceledException`.
5. Return result type (success, output path, or error reason) — avoid WPF types.

**Deliverable:** Headless-capable encode method matching current FFmpeg arguments.

---

## 6. Media probe and size estimate — T6

1. Create `MediaProbeService` — wrap `FFmpeg.GetMediaInfo`, expose duration, video height, bitrate for an input file.
2. Create `SizeEstimateService.EstimateAsync(items, crf, targetHeight, CancellationToken)` — port formula from `UpdateEstimate()`:
   - CRF factor: `0.45 * 2^((23 - crf) / 6)`
   - Resolution factor: `(targetH / sourceH)²` when scaling down
   - Add 128 kbps audio; sum across pending items
3. Support cancellation for estimate pass (replace `_estimateCts` pattern in UI with Core call + UI token).

**Deliverable:** Estimate card and queue resolution labels fed from Core.

---

## 7. Batch orchestration — T7

1. Create `BatchCompressionService` (or static orchestrator) accepting ordered list of `(inputPath, outputPath)` + shared options factory.
2. Sequential compress; check cancellation between items; stop batch on cancel mid-item.
3. Return per-item status enum (Done, Error, Cancelled) — map to current queue status strings in UI.

**Deliverable:** `StartBatchCompression` loop logic lives in Core; UI updates queue bindings.

---

## 8. Wire WPF to Core (A5) — T8, T9

1. Replace inline encode in `CompressItem` with `CompressionService`.
2. Replace `GetOutputPath` calls with `OutputPathResolver`.
3. Replace FFmpeg download block with `FfmpegBootstrap`.
4. Replace `UpdateEstimate` body with `SizeEstimateService`.
5. Replace batch loop with `BatchCompressionService`.
6. Keep in MainWindow: Dispatcher.Invoke for UI updates, queue collection, dialogs, thumbnails, Explorer registration.
7. Remove duplicate Xabe calls from UI if all go through Core.
8. Build Release x64; fix any nullable/reference warnings introduced.

**Deliverable:** GUI behavior unchanged; MainWindow compression methods are thin delegates.

---

## 9. Validation and release — T10, T11

1. Execute all cases in [validation.md](./validation.md).
2. Write `implementations/shared-core-library.md` with steps and test results.
3. After merge to `main`, tag `v1.1.0`.

**Deliverable:** Merge-ready branch; release tag per roadmap.

---

## Phase mapping to roadmap

| Roadmap phase | Task group |
|---------------|------------|
| A1 | §1 Project scaffold |
| A2 | §2 FFmpeg location and bootstrap |
| A3 | §3 Compression options |
| A4 | §5 Single-file compression service |
| A5 | §8 Wire WPF to Core |
| A6 | §4 Output path resolver |
| Release A | §9 Validation and release (`v1.1.0`) |

**Note:** User scope expands A5 to include batch, estimate, and probe (§6–§7) — still within Milestone A, no user-visible change.
