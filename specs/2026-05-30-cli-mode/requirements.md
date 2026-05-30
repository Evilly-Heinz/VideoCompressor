# Requirements: CLI Mode (Milestone B)

**Source:** [roadmap.md](../roadmap.md) — Milestone B (phases B1–B7)  
**Summary:** Headless command-line compression via the same executable, using `VideoCompressor.Core`.  
**Branch:** `feature/milestone-b-cli-mode`  
**Spec folder:** `specs/2026-05-30-cli-mode/`  
**Target release:** `v1.2.0` (roadmap; tagging out of scope for this spec — see D8)

---

## Context

Milestone A extracted compression logic into `VideoCompressor.Core`. The GUI continues to run as today. Milestone B adds a headless entry path so internal users and scripts can compress video without launching WPF.

The CLI contract is defined in [tech-stack.md](../tech-stack.md). This spec applies stakeholder-confirmed refinements for mode detection, output collision handling, and deliverables.

## Problem statement

| Layer | Finding |
|-------|---------|
| Automation | No scriptable way to compress video from CI, batch scripts, or remote workflows. |
| Product shape | [mission.md](../mission.md) requires one executable serving GUI and CLI; CLI path does not exist yet. |
| Foundation | Core services (`CompressionService`, `FfmpegBootstrap`, `OutputPathResolver`) are ready; only parsing, hosting, progress reporting, and exit codes are missing. |

## Scope

### In scope

- **Minimal CLI v1 flags only:** `-q` (CRF), `-s` (x264 preset), `-o` (output path), plus help flags (`-h`, `--help`, `/?`).
- **Mode detection:** Headless CLI when **any recognized CLI flag** is present (including `--help`); otherwise launch WPF. A bare input path alone still opens the GUI (Explorer double-click / drag-and-drop unchanged).
- **`CliArguments` parser** in Core — validation, defaults, unit-testable design (no test project required this milestone).
- **Exit codes enum** mapped per [tech-stack.md](../tech-stack.md): 0 success, 1 general failure, 2 invalid arguments, 3 cancelled (reserved; CLI v1 has no cancel).
- **`ConsoleProgressReporter`** — writes encode percent to **stderr** (e.g. `42%`).
- **`CliHost.Run()`** — parse args, bootstrap FFmpeg, call `CompressionService`, set process exit code.
- **Bootstrap in `App.xaml.cs`** — branch before WPF starts when CLI mode detected.
- **FFmpeg bootstrap** — same as GUI: auto-download via Xabe if `ffmpeg/` missing.
- **Output collision rules (CLI-specific):**
  - **User-specified `-o`:** if output file already exists → exit code **1**, clear error message on stderr.
  - **Default output** (no `-o`): if `{name}_compressed.mp4` exists, try `{name}_compressed_1.mp4`, `_2`, `_3`, … until a free path is found.
- **README update** with CLI usage examples and exit codes.

### Out of scope

- Resolution scaling, batch flags, custom suffix/folder flags (GUI-only for now).
- Registry cleanup (Milestone C), installer (D), auto-update (E).
- Automated test project — manual validation only.
- Release tag `v1.2.0` and `implementations/` write-up (explicitly excluded per stakeholder).
- Full CLI parity with every GUI option.
- CLI cancellation / Ctrl+C handling beyond default process kill (deferred unless trivial).

## Functional requirements

1. **Invocation contract**

   ```
   VideoCompressorUI.exe "<input>" [-q <crf>] [-s <preset>] [-o "<output>"]
   VideoCompressorUI.exe --help
   ```

   | Flag | Type | Default | Maps to |
   |------|------|---------|---------|
   | `-q` | int (18–40) | 23 | CRF |
   | `-s` | string | `medium` | x264 preset |
   | `-o` | path | auto-resolved default | Output file |

2. **Defaults when flags omitted:** CRF 23, preset `medium`, output via default path rules (D4).

3. **Validation failures** (missing input, bad CRF, unknown preset, input not found) → exit code **2**, message on stderr.

4. **Encode failures** (FFmpeg error, I/O) → exit code **1**.

5. **Success** → exit code **0**; output file exists and is non-empty.

6. **Progress:** during encode, write current percent to stderr; no structured/JSON progress in v1.

7. **Help:** `-h`, `--help`, `/?` print usage and examples to stdout; exit **0**. No input file required.

8. **GUI unchanged:** launching with no recognized CLI flags behaves exactly as before Milestone B.

## Decisions

| # | Decision | Detail |
|---|----------|--------|
| D1 | Minimal flag set | `-q`, `-s`, `-o` only — no resolution, batch, or GUI settings flags in v1. |
| D2 | CLI trigger | Any recognized CLI flag → headless; bare file path → GUI. |
| D3 | Progress channel | Percent only to stderr (roadmap B3). |
| D4 | Default output collision | Increment suffix: `_compressed`, `_compressed_1`, `_compressed_2`, … in input directory. |
| D5 | Explicit `-o` collision | Fail with exit 1 and error message — no overwrite. |
| D6 | FFmpeg bootstrap | Same auto-download behavior as GUI (`FfmpegBootstrap.EnsureAvailableAsync`). |
| D7 | No automated tests | Manual test matrix in validation.md only; parser designed for future unit tests. |
| D8 | Deliverables | Code + README only — no `v1.2.0` tag or `implementations/` note in this milestone scope. |
| D9 | No Jira ticket | Spec references roadmap Milestone B only. |
| D10 | Executable name | CLI runs through `VideoCompressorUI.exe` (same binary per tech-stack). README may alias as `VideoCompressor.exe` if publish renames; document actual exe name. |

## Technical touchpoints

| File / area | Role |
|-------------|------|
| `VideoCompressor.Core/CliArguments.cs` (new) | Parse and validate CLI args |
| `VideoCompressor.Core/CliExitCode.cs` (new) | Exit code enum |
| `VideoCompressor.Core/ConsoleProgressReporter.cs` (new) | stderr percent writer |
| `VideoCompressor.Core/CliHost.cs` (new) | Orchestrate parse → bootstrap → compress → exit code |
| `VideoCompressor.Core/OutputPathResolver.cs` | Extend or add helper for default-path collision (`_1`, `_2`, …) |
| `VideoCompressorUI/App.xaml.cs` | Detect CLI mode; call `CliHost.Run()` and `Environment.Exit` before WPF |
| `README.md` | CLI section: syntax, defaults, exit codes, examples |
| Existing Core | `CompressionService`, `CompressionOptions`, `FfmpegBootstrap`, `FfmpegLocator` |

## Dependencies

| Dependency | Status |
|------------|--------|
| Milestone A — Shared Core | **Complete** (merged to `main`) |
| [tech-stack.md](../tech-stack.md) CLI contract | Base contract; D4/D5 override default overwrite behavior |
| [mission.md](../mission.md) | One exe, two modes |

## Related documents

- [roadmap.md](../roadmap.md) — phases B1–B7
- [tech-stack.md](../tech-stack.md) — exit codes, flag defaults
- [validation.md](./validation.md) — merge-ready checks and manual test matrix
