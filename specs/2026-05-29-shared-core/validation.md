# Validation: Milestone A — Shared Core Library

**Branch:** `feature/milestone-a-shared-core`  
**Release:** `v1.1.0` (no user-visible change expected)

---

## Preconditions

| Item | Detail |
|------|--------|
| Environment | Windows 10/11 x64, .NET 8 SDK, Visual Studio 2022 |
| Branch | `feature/milestone-a-shared-core` checked out |
| Build | `dotnet build VideoCompressor.sln -c Release` succeeds with zero errors |
| FFmpeg | Either pre-bundled in `bin/Release/ffmpeg/` or first-run download allowed |
| Test media | At least 2 video files (e.g. `.mp4`, `.mkv`), one ≥ 1 minute for progress/cancel testing |

---

## Success criteria (merge-ready)

1. `VideoCompressor.Core` exists as `net8.0` class library with no WPF/WinForms references.
2. `VideoCompressorUI` references Core; solution builds Release x64.
3. All compression-related logic (encode, batch, output path, estimate, FFmpeg bootstrap) lives in Core — not duplicated in MainWindow.
4. GUI behavior is **unchanged** from pre-refactor baseline for all manual test cases below.
5. Cancel during compress still stops encode and marks item Cancelled; batch does not continue.
6. No new NuGet packages beyond those listed in [tech-stack.md](../tech-stack.md).
7. `implementments/shared-core-library.md` documents what was done and test results.

---

## Architecture checks (code review)

| Check | Expected |
|-------|----------|
| Core TFM | `net8.0` only — no `-windows`, no `UseWPF` |
| UI dependency direction | UI → Core only; Core does not reference UI |
| WPF isolation | No `System.Windows.*` in Core |
| Progress reporting | Core uses callbacks/`IProgress<int>`; Dispatcher only in UI |
| Cancellation | `CancellationToken` threaded through async Core methods |
| FFmpeg packages | On Core project; UI may retain ref only if needed for types in code-behind |
| Public API surface | Small, intentional public types — internal helpers where possible |

---

## Manual test cases

### TC-1 — Clean build and launch

| Step | Action | Expected |
|------|--------|----------|
| 1 | `dotnet build VideoCompressor.sln -c Release` | Build succeeds |
| 2 | Run `bin/Release/VideoCompressorUI.exe` | App opens; no startup errors |

### TC-2 — Single file compress (defaults)

| Step | Action | Expected |
|------|--------|----------|
| 1 | Add one `.mp4` to queue | Item shows Pending, thumbnail/resolution if ffprobe available |
| 2 | CRF 23, preset medium, resolution Original | Defaults match pre-refactor |
| 3 | Click Compress | Progress 0→100; status Done ✓ |
| 4 | Check output file | `{name}_compressed.mp4` in same folder as source; plays; smaller or similar size |

### TC-3 — Output settings

| Step | Action | Expected |
|------|--------|----------|
| 1 | Set custom suffix `_small` | Estimate updates if shown |
| 2 | Choose custom output folder | Output written to chosen folder |
| 3 | Compress | Filename `{original}_small.mp4` in output folder |

### TC-4 — Resolution scaling

| Step | Action | Expected |
|------|--------|----------|
| 1 | Add HD video; select 720p resolution | Estimate shows reduced size vs Original |
| 2 | Compress | Output height ≤ 720; aspect preserved (scale=-2:720) |

### TC-5 — CRF and preset variants

| Step | Action | Expected |
|------|--------|----------|
| 1 | CRF 18, preset slow | Encode completes; larger output than CRF 28 |
| 2 | CRF 35, preset ultrafast | Encode completes; noticeably smaller file |

### TC-6 — Batch queue

| Step | Action | Expected |
|------|--------|----------|
| 1 | Add 3 videos | All Pending |
| 2 | Compress | Items process sequentially; each reaches Done ✓ |
| 3 | Batch summary | Summary window shows after batch (unchanged behavior) |

### TC-7 — Cancel mid-encode

| Step | Action | Expected |
|------|--------|----------|
| 1 | Start compress on long file | Progress advancing |
| 2 | Click Cancel (■) | Encode stops; item Cancelled; button returns to ▶ Compress |
| 3 | Partial output | No corrupt “Done” state; item not marked Done ✓ |

### TC-8 — Cancel mid-batch

| Step | Action | Expected |
|------|--------|----------|
| 1 | Queue 3 files; start batch | First item compressing |
| 2 | Cancel during first item | First Cancelled; remaining stay Pending (not auto-processed) |

### TC-9 — Size estimate card

| Step | Action | Expected |
|------|--------|----------|
| 1 | Add pending items; change CRF slider | Estimate updates (debounced) |
| 2 | Change resolution | Estimate decreases when scaling down |
| 3 | Clear queue | Estimate card hidden |

### TC-10 — FFmpeg first-run download

| Step | Action | Expected |
|------|--------|----------|
| 1 | Delete `bin/Release/ffmpeg/` folder | — |
| 2 | Launch app and compress | Status shows download message; then encode succeeds |
| 3 | Second compress | No re-download |

### TC-11 — Explorer context launch (smoke)

| Step | Action | Expected |
|------|--------|----------|
| 1 | If registered: right-click video → Compress | App opens with file queued (unchanged) |
| 2 | If not registered | Register flow still works (not modified by Core refactor) |

---

## E2e testing

| ID | Scenario | Notes |
|----|----------|-------|
| E2E-1 | Full compress workflow | **Manual only** — no Playwright/Cypress; WPF desktop app |
| E2E-2 | Batch + cancel | Manual — TC-6 + TC-8 |
| E2E-3 | First-run FFmpeg bootstrap | Manual — TC-10 |

**Rationale:** Milestone A is a refactor with no user-visible change. Automated UI tests are out of scope per requirements (manual regression only). Future CLI e2e belongs to Milestone B validation.

---

## Regression areas

- Drag-and-drop and browse add-to-queue
- Queue clear and item removal
- CRF slider description text updates
- Batch summary window contents and dismiss
- Custom title bar / window chrome (unaffected)
- Context menu registration UI (unchanged logic; smoke test only)
- Release CI workflow still builds artifact zip

---

## Failure signals (do not merge)

- Any manual test case fails vs pre-refactor behavior
- Core project references WPF or Windows Forms
- Duplicate encode logic remains in `MainWindow.xaml.cs` (beyond UI wiring)
- Build warnings elevated to errors in CI
- Output path, suffix, or folder rules differ from baseline
- Cancel leaves zombie encode or wrong queue status

---

## Post-merge release check

| Step | Action | Expected |
|------|--------|----------|
| 1 | Merge to `main` | CI green |
| 2 | Tag `v1.1.0` | GitHub Release notes: internal refactor, no user-facing changes |
| 3 | Smoke test release zip | Same as TC-2 on published artifact |
