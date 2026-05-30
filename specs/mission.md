# Mission

## Purpose

Video Compressor is an internal Groove Technology tool for re-encoding video files to H.264/AAC with a simple GUI and a scriptable CLI. The product reduces file size while keeping quality tunable, and integrates with Windows Explorer for one-click access.

## Audience

**Primary:** Groove Technology internal users on Windows 10/11 x64.

This is not a consumer SaaS product. Decisions favor reliability, low friction for internal workflows, and maintainability by a small team over broad market features.

## Product Shape

One executable serves both modes:

| Mode | Trigger | Use case |
|---|---|---|
| **GUI** | Launch with no CLI flags (or with a file path only) | Interactive compression, batch queue, settings |
| **CLI** | Launch with compression flags (`-q`, `-s`, `-o`) | Automation, scripts, CI pipelines |

Same binary, same compression engine. See [tech-stack.md](./tech-stack.md) for architecture.

## Core Values

1. **Simple by default** — Drop a file, compress, done. Advanced options stay available but not required.
2. **Windows-native** — Explorer integration, UAC elevation only when needed, familiar install/update flow.
3. **No surprises** — Progress is visible (GUI bar or CLI stdout/stderr). Failures return clear exit codes.
4. **Safe updates** — In-place upgrade replaces the current install without orphaning registry entries or stale paths.
5. **Honest scope** — Ship what works well on Windows; do not block future cross-platform work, but do not promise it now.

## Stakeholder Requirements

Source: [Requirements.md](../Requirements.md)

| # | Requirement | Status |
|---|---|---|
| 1 | CLI: `VideoCompressor filePath [-q quality] [-s speed] [-o output]` | Planned |
| 2 | Remove stale registry entries when clicking **Register** (legacy paths included) | Planned |
| 3 | Inno Setup installer + GitHub release version check with in-place auto-update | Planned |

Existing GUI capabilities (batch queue, resolution scaling, output settings, size estimate) remain in scope as shipped features. The CLI v1 covers only `-q`, `-s`, `-o` — not full GUI parity.

## Out of Scope (Current Extension)

- macOS / Linux builds
- Cloud or server-side encoding
- Licensing, activation, or telemetry
- Full CLI parity with every GUI option (resolution, suffix, batch flags)
- Separate Unregister button (re-register handles path changes)

## Out of Scope (Deferred, Not Forbidden)

- Cross-platform ports (architecture should not prevent future extraction of a platform-neutral core)
- Additional codecs beyond H.264/AAC
- Silent background service / watch-folder automation

## Success Criteria

The extension is complete when:

1. A shared compression library powers both GUI and CLI without duplicated FFmpeg logic.
2. `VideoCompressor.exe "input.mp4" -q 23 -s medium -o "out.mp4"` runs headless, prints progress, and exits with a defined code.
3. **Register** and startup both clean legacy context-menu registry keys before writing the current path.
4. Inno Setup installs to Program Files (self-contained .NET 8), and the app checks GitHub releases on startup and on demand, then auto-updates in place.

## Related Documents

- [Requirements.md](../Requirements.md) — stakeholder input
- [README.md](../README.md) — user-facing setup and build instructions
- [tech-stack.md](./tech-stack.md) — locked technology decisions
- [roadmap.md](./roadmap.md) — phased implementation order
- [implementations/](../implementations/) — completed feature write-ups
