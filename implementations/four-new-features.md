# Four New Features

**Branch / ticket:** main
**Date:** 2026-04-03

---

## Overview

Four features were added to `VideoCompressorUI`:

1. **Output Settings** — choose output folder and filename suffix before compressing
2. **Resolution Scaling** — dropdown to downscale video to 1080p / 720p / 480p
3. **Estimate File Size** — shows approximate compressed size before encoding starts
4. **Batch Processing** — drop or browse multiple files, compress them sequentially

---

## Implementation Steps

### 1 — Output Settings

**Files changed:** `MainWindow.xaml`, `MainWindow.xaml.cs`, `Themes/Styles.xaml`

- Added a new **Output Settings** card between the settings card and action row in `MainWindow.xaml`.
  - Row 1: displays `_outputFolder` (default: "same as source") + **Browse…** and **Reset** buttons.
  - Row 2: `TextBox x:Name="OutputSuffixBox"` (default `_compressed`) styled with new `FluentTextBox` from `Styles.xaml`.
- `OutputFolderBtn_Click` opens `System.Windows.Forms.FolderBrowserDialog` (requires `<UseWindowsForms>true</UseWindowsForms>` in `.csproj`).
- `ResetOutputFolderBtn_Click` clears `_outputFolder` back to `null`.
- `OutputSuffixBox_TextChanged` writes to `_outputSuffix` (falls back to `_compressed` when empty).
- `GetOutputPath(string inputFile)` now uses `_outputFolder ?? sourceDir` and appends `_outputSuffix` before `.mp4`.

### 2 — Resolution Scaling

**Files changed:** `MainWindow.xaml`, `MainWindow.xaml.cs`

- Settings card grid extended from 3 columns (CRF / Preset) to 5 columns (CRF / Preset / Resolution).
- `ComboBox x:Name="ResolutionCombo"` with items: Keep original (Tag=""), 1080p (Tag="1080"), 720p (Tag="720"), 480p (Tag="480").
- `GetSelectedResolution()` reads the selected `ComboBoxItem.Tag`.
- In `CompressItem()`, if resolution is non-empty and source video exists, appends `-vf scale=-2:{res}` as a post-input FFmpeg parameter. The `-2` ensures the width is auto-calculated and stays divisible by 2 (required by H.264).
- `SelectionChanged` on `ResolutionCombo` calls `UpdateEstimate()`.

### 3 — Estimate File Size

**Files changed:** `MainWindow.xaml`, `MainWindow.xaml.cs`

- `TextBlock x:Name="EstimateLabel"` added below the settings grid, right-aligned.
- `UpdateEstimate()` is an `async Task` with:
  - **400 ms debounce** via `CancellationTokenSource _estimateCts` — re-called on CRF change, resolution change, or queue change.
  - **Guard**: only runs if `ffprobe.exe` is present (available after first compression).
  - Per-file formula:
    ```
    crfFactor  = 0.45 × 2^((23 − crf) / 6)
    resFactor  = (targetH / sourceH)²  if downscaling, else 1.0
    estBps     = sourceVideoBitrate × crfFactor × resFactor  +  128 000 (audio)
    estBytes   = estBps × durationSeconds / 8
    ```
  - Falls back to `fileSize × 8 / duration` when `IVideoStream.Bitrate` is 0.
  - Sums estimated bytes across all Pending queue items.
- Called from `CrfSlider_ValueChanged`, `ResolutionCombo_SelectionChanged`, `AddToQueue`, `RemoveFromQueue_Click`, and after FFmpeg download.

### 4 — Batch Processing

**Files changed:** `MainWindow.xaml`, `MainWindow.xaml.cs`

#### New `QueueItem` class

- `INotifyPropertyChanged` implementation in the `VideoCompressorUI` namespace.
- Properties: `FilePath`, `FileName`, `SizeMb`, `OutputPath?`, `Status`, `Progress`, `IsProcessing`, `StatusColor` (returns a frozen `Brush`).
- Status values and their colors: Pending (tertiary gray), Compressing (accent blue), Done ✓ (success green), Error (danger red), Cancelled (warn amber).

#### `MainWindow` changes

- `string? _inputFile` removed; replaced by `ObservableCollection<QueueItem> Queue` (public, bound via `DataContext = this`).
- `AddToQueue(string[] paths)` deduplicates, creates `QueueItem` per file, and calls `RefreshQueueUI()`.
- `RefreshQueueUI()` manages visibility of `QueueCard`, `ClearQueueBtn`, updates `QueueHeader` count, and resets drop-zone label.
- `OpenFileDialog.Multiselect = true` on Browse.
- `Window_Drop` now passes all video files to `AddToQueue`.
- Queue card (`Border x:Name="QueueCard"`) contains an `ItemsControl` bound to `Queue` with a `DataTemplate` showing filename, size, per-item `ProgressBar` (visible only when `IsProcessing`), status text with dynamic color, and a remove (✕) button.
- `RemoveFromQueue_Click` guards against removing an in-progress item.
- `ClearQueue_Click` clears the queue (blocked during compression).
- `StartBatchCompression()` loops through all Pending items sequentially; breaks on Cancelled; restores UI afterward; calls `ShowBatchSummary()`.
- `CompressItem(QueueItem, CancellationToken)` contains the per-file FFmpeg logic extracted from the old `StartCompression()`.
- `ShowBatchSummary()` shows a single `MessageBox` with total input/output size and aggregate savings; offers to open the output folder of the last completed file.
- Cancel button cancels `_cts`, which causes the current `conversion.Start()` to throw `OperationCanceledException`; status is set to Cancelled and the loop breaks.

---

## How to Test

### Feature 1 — Output Settings

1. Drop a video file.
2. In the **Output Settings** card, click **Browse…** and select a different folder.
3. Change the suffix field to e.g. `_720p`.
4. Click **▶ Compress**.
5. After completion, verify the output file appears in the chosen folder with the custom suffix.
6. Click **Reset** → label reverts to "(same as source)".
7. Clear the suffix field entirely → suffix defaults back to `_compressed` (no empty filename).

### Feature 2 — Resolution Scaling

1. Use a 4K or 1440p source video.
2. Select **720p** from the Resolution dropdown.
3. Compress. Verify with Windows File Explorer properties or VLC that the output resolution is 1280×720 (or similar -2 width variant).
4. Test **Keep original** to confirm no scaling is applied.
5. If source is already ≤ target resolution, `-vf scale=-2:{res}` is still passed but FFmpeg will not upscale (it will keep original dimensions).

### Feature 3 — Estimate File Size

> Requires FFmpeg to have been downloaded at least once (ffprobe.exe must exist).

1. Drop one or more videos.
2. The estimate label ("Estimated output: ~XX MB") appears below the settings card within ~1 second.
3. Move the CRF slider → estimate updates.
4. Change Resolution dropdown → estimate updates.
5. Add more files → estimate increases.
6. Remove a file from queue → estimate decreases.
7. Before FFmpeg is downloaded, the label remains empty.

### Feature 4 — Batch Processing

1. Drag 3–5 video files into the window at once.
2. Verify the Queue card appears showing all files with status "Pending".
3. Click **▶ Compress**. Observe each file progressing sequentially: per-item progress bar + status updates.
4. After all complete, a summary dialog shows total input/output size and average savings.
5. Test **Cancel** mid-batch: current item becomes "Cancelled", remaining items stay "Pending".
6. Test **Clear queue** (only enabled when not compressing).
7. Test remove (✕) button on individual items.
8. Test adding more files while some are already Done ✓ (only Pending items are processed on next Compress).
9. Test context-menu launch: right-click a video → "Compress this video" → app opens with 1 item pre-loaded in queue.
10. Test duplicate detection: drag the same file twice → second drop is silently ignored.
