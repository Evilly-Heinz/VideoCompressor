# Validation: Milestone B — CLI Mode

**Branch:** `feature/milestone-b-cli-mode`  
**Release:** `v1.2.0` (roadmap reference; tagging not in milestone scope)

---

## Preconditions

| Item | Detail |
|------|--------|
| Environment | Windows 10/11 x64, .NET 8 SDK |
| Branch | `feature/milestone-b-cli-mode` checked out |
| Build | `dotnet build VideoCompressor.sln -c Release` succeeds with zero errors |
| Binary | `bin\Release\VideoCompressorUI.exe` |
| Test media | At least 2 video files (`.mp4` recommended), one ≥ 30 s for progress observation |
| Shell | PowerShell or cmd for CLI invocation |

---

## Success criteria (merge-ready)

1. Recognized CLI flag in args → headless run; **no WPF window** appears.
2. No recognized CLI flag (including bare file path only) → GUI launches unchanged from Milestone A baseline.
3. `VideoCompressorUI.exe --help` prints usage to stdout and exits **0**.
4. Valid encode with defaults produces output file; exit **0**.
5. Progress percent appears on **stderr** during encode.
6. Invalid args (bad CRF, missing file, unknown preset) exit **2** with message on stderr.
7. FFmpeg failure / I/O error exits **1**.
8. Explicit `-o` to existing file exits **1** with clear error — no overwrite.
9. Default output when `{name}_compressed.mp4` exists writes to `{name}_compressed_1.mp4` (or next free index).
10. FFmpeg auto-download works in CLI when `ffmpeg/` folder missing (same as GUI).
11. README documents CLI syntax, defaults, exit codes, and examples.
12. GUI manual smoke test (TC-G1) still passes — no regression from App.xaml.cs changes.

---

## Architecture checks (code review)

| Check | Expected |
|-------|----------|
| CLI types in Core | `CliArguments`, `CliHost`, `ConsoleProgressReporter`, `CliExitCode` in `VideoCompressor.Core` |
| No WPF in Core | Core remains `net8.0` without `System.Windows.*` |
| Single entry exe | CLI via `App.xaml.cs` branch only — no separate console project |
| GUI isolation | WPF startup path untouched when no CLI flags |
| Exit codes | Match [tech-stack.md](../tech-stack.md) table |
| Progress | stderr only; no structured JSON in v1 |

---

## Manual test cases — CLI

### TC-1 — Help

| Step | Action | Expected |
|------|--------|----------|
| 1 | `VideoCompressorUI.exe --help` | Usage on stdout; exit 0; no window |
| 2 | `VideoCompressorUI.exe -h` | Same as above |
| 3 | `VideoCompressorUI.exe /?` | Same as above |

### TC-2 — Valid encode (defaults)

| Step | Action | Expected |
|------|--------|----------|
| 1 | `VideoCompressorUI.exe "C:\test\video.mp4" -q 23 -s medium` | stderr shows progress; exit 0 |
| 2 | Check output | `C:\test\video_compressed.mp4` exists; plays |

### TC-3 — Custom output path

| Step | Action | Expected |
|------|--------|----------|
| 1 | `VideoCompressorUI.exe "C:\test\video.mp4" -o "C:\test\out\custom.mp4"` | exit 0; file at custom path |
| 2 | Repeat same command | exit **1**; stderr error that output exists; file unchanged |

### TC-4 — Default path collision

| Step | Action | Expected |
|------|--------|----------|
| 1 | Ensure `video_compressed.mp4` exists | — |
| 2 | `VideoCompressorUI.exe "C:\test\video.mp4"` (no `-o`) | exit 0; creates `video_compressed_1.mp4` |
| 3 | Run again | creates `video_compressed_2.mp4` |

### TC-5 — Invalid arguments (exit 2)

| Step | Action | Expected |
|------|--------|----------|
| 1 | `VideoCompressorUI.exe -q 99 "file.mp4"` | exit 2; stderr explains invalid CRF |
| 2 | `VideoCompressorUI.exe -s invalid "file.mp4"` | exit 2; invalid preset |
| 3 | `VideoCompressorUI.exe "missing.mp4"` | exit 2; file not found |
| 4 | `VideoCompressorUI.exe` (no args, no flags) | **GUI opens** (not exit 2) |

### TC-6 — CRF and preset variants

| Step | Action | Expected |
|------|--------|----------|
| 1 | `-q 18 -s slow` | exit 0; larger output than `-q 35 -s ultrafast` on same input |

### TC-7 — FFmpeg bootstrap (CLI)

| Step | Action | Expected |
|------|--------|----------|
| 1 | Delete `bin\Release\ffmpeg\` | — |
| 2 | Run CLI encode on small file | Download occurs; encode succeeds; exit 0 |
| 3 | Second CLI encode | No re-download |

### TC-8 — Flag triggers CLI without other flags

| Step | Action | Expected |
|------|--------|----------|
| 1 | `VideoCompressorUI.exe -q 28 "file.mp4"` | Headless; no GUI |
| 2 | `VideoCompressorUI.exe "file.mp4"` only | GUI opens with file (existing behavior) |

---

## Manual test cases — GUI regression

### TC-G1 — GUI smoke (no CLI flags)

| Step | Action | Expected |
|------|--------|----------|
| 1 | Double-click exe (no args) | GUI opens |
| 2 | Add file; compress with defaults | Same behavior as Milestone A TC-2 |
| 3 | Explorer context menu launch (if registered) | File queued in GUI |

---

## E2e testing

| ID | Scenario | Notes |
|----|----------|-------|
| E2E-1 | CLI encode end-to-end | **Manual** — run exe from shell, verify output file |
| E2E-2 | GUI + CLI coexistence | Manual — TC-G1 + TC-2 on same build |
| E2E-3 | Script integration | Manual — optional PowerShell script calling exe and checking `$LASTEXITCODE` |

**Rationale:** No automated test project this milestone (per requirements D7). Parser is structured for future unit tests.

---

## Regression areas

- WPF startup, main window, batch queue, cancel, estimate card
- FFmpeg bootstrap in GUI after CLI changes
- Explorer registration and drag-and-drop
- Release CI workflow still builds zip artifact
- Core library public API — no breaking changes for GUI callers

---

## Failure signals (do not merge)

- WPF window appears during any TC-1–TC-8 CLI case
- GUI fails to launch when no CLI flags present
- Exit codes disagree with tech-stack / requirements
- Explicit `-o` overwrites existing file silently
- Default output overwrites without incrementing suffix
- Progress on stdout instead of stderr (or missing entirely)
- README CLI section missing or incorrect
- Build errors or Core references WPF

---

## Post-merge (out of scope for this spec)

| Step | Action | Notes |
|------|--------|-------|
| Tag `v1.2.0` | Optional follow-up | Excluded per requirements D8 |
| `implementations/` note | Optional follow-up | Excluded per requirements D8 |
