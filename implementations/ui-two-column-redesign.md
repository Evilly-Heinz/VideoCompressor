# UI Redesign: Two-Column Layout

## Overview

Complete UI overhaul from a single scrolling column to a two-column side-by-side layout with a fixed bottom bar.

---

## Implementation Steps

### 1. Window layout restructuring (`MainWindow.xaml`)

- **Window size**: 820×720 px, MinWidth=700, MinHeight=580
- **Outer grid** (3 rows): title bar (48 px) · separator (1 px) · main content area (`*`)
- **Main content area** (2 rows): two-column panels (`*`) · bottom bar (56 px action row)
- **Two columns**: `3*` (left, ~60%) · 1 px vertical divider · `2*` (right, ~40%)

### 2. Left panel — drop zone + queue (`Grid.Column="0"`)

Two states managed in `RefreshQueueUI()` via `Visibility`:

| State | `DropZoneLarge` | `QueueArea` |
|---|---|---|
| Queue empty | `Visible` | `Collapsed` |
| Queue has items | `Collapsed` | `Visible` |

**`QueueArea`** has two rows:
- Row 0 (Auto): compact "Add more files" bar + `QueueHeader` label
- Row 1 (`*`): `ScrollViewer` → `ItemsControl` bound to `Queue`

### 3. Queue item DataTemplate

Each item row (CornerRadius=8 card):
- `48×48` **thumbnail**: shell thumbnail via `IShellItemImageFactory` overlaid on video icon placeholder
- **File info column** (`*`): filename, size, per-item `ProgressBar` (visible only when `IsProcessing`)
- **Status badge** (`Auto`, MinWidth=74): semi-transparent background + foreground from `StatusBackground` / `StatusColor` bindings
- **Remove button** (32×28): `✕` with enough padding for comfortable click

**Animated compressing badge**: `DataTemplate.Triggers` with a `DoubleAnimation` on `StatusBadge.Opacity` (`1.0 → 0.35`, 0.75 s, auto-reverse, forever) — starts on `Status == "Compressing"`, stopped on exit.

### 4. Right panel — settings sidebar (`Grid.Column="2"`)

`ScrollViewer` + `StackPanel(Margin="20,16,20,16")` with sections:

| Section | Elements |
|---|---|
| **QUALITY** | CRF label (live value), `CrfSlider` (18–40), quality hint row, `CrfDescLabel` (centered accent text) |
| **SPEED PRESET** | `PresetCombo` |
| **RESOLUTION** | `ResolutionCombo`, `SourceResLabel` (source WxH, loaded async) |
| *(divider)* | 1 px border |
| **OUTPUT FOLDER** | `OutputFolderLabel` + Browse / Reset buttons |
| **OUTPUT SUFFIX** | `OutputSuffixBox` (`FluentTextBox`) |
| *(divider)* | 1 px border |
| **ESTIMATED OUTPUT** | `EstimateCard` mini-card (collapsed when no queue) |
| *(divider)* | 1 px border |
| **EXPLORER INTEGRATION** | `CtxMenuStatusLabel` + `RegisterCtxBtn` |

### 5. Estimated output mini-card

`EstimateCard` (Border, collapsed by default) contains a `WrapPanel` with:
- `EstimateSrcLabel` — total source file size in MB
- `EstimateArrowLabel` — "  →  " (collapsed until ffprobe available)
- `EstimateOutLabel` — estimated output in MB, accent color (collapsed until ffprobe)
- `EstimatePctLabel` — "(↓ XX%)", success-green (collapsed until ffprobe)

Source size = sum of `FileInfo.Length` for pending items (no FFmpeg needed).  
Estimated size = calculated via `ffprobe` bitrate × CRF factor × resolution factor.

### 6. CRF description label

`GetCrfDescription(int crf)` switch:

| CRF range | Label |
|---|---|
| ≤ 22 | Visually lossless |
| 23–28 | Balanced |
| 29–35 | Small file |
| ≥ 36 | Aggressive compression |

Updated in `CrfSlider_ValueChanged` and at startup in the constructor.

### 7. Shell thumbnail loading (`IShellItemImageFactory`)

No extra NuGet packages. P/Invoke into `shell32.dll`:
```csharp
SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out object ppv)
// cast ppv to IShellItemImageFactory, call GetImage(Size(56,56), 0, out hBitmap)
// convert hBitmap → BitmapSource via Imaging.CreateBitmapSourceFromHBitmap
// DeleteObject(hBitmap) in finally
```
Called in `LoadItemExtrasAsync` (background thread via `Task.Run`) immediately after a file is added to the queue. Source resolution is also loaded here via FFprobe (if available).

### 8. Explorer Integration in settings panel

- `CheckRegistrationStatus()` called in the constructor to show current state on launch.
- Checks `HKEY_CLASSES_ROOT\SystemFileAssociations\.mp4\shell\VideoCompressor`.
- If registered: shows green "✓ Registered …" text, hides the Register button.
- If not registered: shows secondary-color hint text, shows the Register button.
- `RegisterCtxBtn_Click` re-calls `CheckRegistrationStatus()` on success (button auto-hides).

### 9. Fixed bottom bar

```
[Border, BgLayer1Brush, top border 1px]
  Grid:
    Row 0 (Auto): ProgressBar x:Name="BottomProgress" Height=3 (collapsed when idle)
    Row 1 (56px):
      Grid (3 cols):
        Col 0 Auto: [Browse file…] [Clear queue]
        Col 1 *:    StatusLabel (centered)
        Col 2 Auto: [▶ Compress]  ← AccentButton
```

`BottomProgress` is an indeterminate bar during FFmpeg download and a determinate bar (0–100%) during per-file compression. It's `Visibility.Collapsed` at all other times.

### 10. `QueueItem` new properties

| Property | Type | Notes |
|---|---|---|
| `Thumbnail` | `BitmapSource?` | Notifies; loaded async from shell |
| `SourceResolution` | `string?` | e.g. `"3840×2160"`; loaded async via ffprobe |
| `StatusBackground` | `Brush` | Frozen semi-transparent brush per status |

`StatusBackground` brushes (all frozen):
- Pending: `#18FFFFFF`
- Compressing: `#2860CDFF`
- Done ✓: `#286CCB5F`
- Error: `#28FF453A`
- Cancelled: `#28FFD60A`

---

## How to Test

### Layout
1. Launch app — should show the 820×720 two-column window.
2. Left panel: large drop zone fills the left column.
3. Right panel: all settings visible without scrolling (CRF, Preset, Resolution, Output, Estimate, Explorer).
4. Bottom bar: "Browse file…" on the left, "Ready…" centered, "▶ Compress" (disabled) on the right.
5. Resize the window — queue list grows/shrinks, right panel scrolls if needed at minimum height.

### Queue items
1. Drag a video onto the window — file appears in queue with a shell thumbnail (may take a moment to load).
2. Check the status badge shows "Pending" with a semi-transparent white background.
3. Drag multiple files — queue shows all of them with correct thumbnails, sizes, and count in the header.
4. Click ✕ to remove an item — it disappears, queue count updates.
5. Click "Clear queue" — all items removed, drop zone reappears.

### CRF description
- Move the slider from 18 → 40 — label below slider should cycle through: **Visually lossless** → **Balanced** → **Small file** → **Aggressive compression**.

### Source resolution
- Add a video file. After ffprobe loads (first compression required), "Source: 1920×1080" appears below the Resolution dropdown.

### Estimate mini-card
- Add files → card appears with source size. After first compression (FFmpeg downloaded), the card shows `"7.8 MB  →  ~3 MB  (↓ 62%)"`.

### Animated badge
- Start a compression — the "Compressing" badge on the active queue item pulses (opacity cycles 1.0 ↔ 0.35).

### Explorer Integration
- On a fresh install, "Register" button is visible.
- Click "Register" → UAC prompt → after accepting, button disappears and status shows "✓ Registered …".

### Bottom bar progress
- During FFmpeg download: `BottomProgress` shows as indeterminate (animated stripe).
- During compression: `BottomProgress` fills 0 → 100% for the current file, then hides.
