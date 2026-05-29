# Tech Stack

Technology choices for this project are **locked**. New tools are permitted only where this document explicitly allows (installer, auto-update scripting).

## Platform

| Layer | Choice | Notes |
|---|---|---|
| OS | Windows 10 / 11 x64 | Primary target; design for future portability at the library layer only |
| Runtime | .NET 8 (`net8.0-windows`) | Self-contained in release installer |
| Architecture | x64 only | Matches FFmpeg bundle and CI |

## Application

| Layer | Choice | Version | Notes |
|---|---|---|---|
| UI framework | WPF | — | Custom title bar, Fluent dark theme |
| UI helpers | WinForms | — | `FolderBrowserDialog` only |
| Entry point | Single exe (`VideoCompressorUI.exe`) | — | GUI when no CLI flags; headless CLI when `-q`/`-s`/`-o` present |
| DPI | PerMonitorV2 | — | Via `app.manifest` |

## Video Processing

| Layer | Choice | Version | Notes |
|---|---|---|---|
| FFmpeg wrapper | Xabe.FFmpeg | 6.0.2 | Encode, probe, progress events |
| FFmpeg downloader | Xabe.FFmpeg.Downloader | 6.0.2 | First-run / CI bootstrap only |
| Binaries | `ffmpeg.exe`, `ffprobe.exe` | CI-pinned | Bundled in release zip and installer |
| Codec | H.264 + AAC | — | CRF quality, x264 preset for speed |

## Project Structure (Target)

```
VideoCompressor.sln
├── VideoCompressor.Core/          ← NEW: shared compression, CLI parsing, registry helpers
├── VideoCompressorUI/             ← WPF shell; references Core
├── installer/
│   └── VideoCompressor.iss        ← NEW: Inno Setup script
├── scripts/
│   └── install_context_menu.reg
└── .github/workflows/build.yml
```

`VideoCompressor.Core` is a class library (`net8.0`, no WPF dependency) consumed by the UI project. CLI mode runs inside the same exe by branching in `App.xaml.cs` / a small host bootstrap before WPF starts.

## CLI Contract

```
VideoCompressor.exe "<input>" [-q <crf>] [-s <preset>] [-o "<output>"]
```

| Flag | Type | Default | Maps to |
|---|---|---|---|
| `-q` | int (18–40) | 23 | CRF slider |
| `-s` | string | `medium` | x264 preset dropdown |
| `-o` | path | `{input_dir}/{name}_compressed.mp4` | Output path |

**Output:** progress lines to stdout/stderr during encode.

**Exit codes:**

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | General failure (FFmpeg error, I/O) |
| 2 | Invalid arguments |
| 3 | Cancelled (GUI only; CLI has no cancel in v1) |

## Windows Integration

| Feature | Mechanism |
|---|---|
| Context menu | UTF-16 LE `.reg` file → `regedit.exe /s` with UAC elevation |
| Registry cleanup | Delete known current + legacy keys before import; also on startup if exe path changed |
| Install location | `%ProgramFiles%\Groove Technology\Video Compressor\` (configurable in Inno Setup) |
| Shortcuts | Start Menu + optional Desktop (Inno Setup) |

## Installer

| Layer | Choice | Notes |
|---|---|---|
| Tool | **Inno Setup 6** | `.iss` script in repo; built in CI or locally |
| Deployment | Self-contained publish | Bundles .NET 8 runtime; no separate runtime install |
| Upgrade | In-place | Download → extract → close app → replace files in install dir → remove old binaries |

Portable zip releases may continue alongside the installer for ad-hoc use.

## Auto-Update

| Layer | Choice | Notes |
|---|---|---|
| Version source | GitHub Releases HTML/API | URL hardcoded in app constant (placeholder until final repo URL) |
| Check timing | Startup (background) + manual menu action | Non-blocking on startup |
| Update flow | Download release asset → extract to temp → spawn updater cmd → exit → replace install dir |

Constant placeholder:

```csharp
// VideoCompressor.Core/UpdateConstants.cs
public const string GitHubReleasesUrl = "https://github.com/ORG/REPO/releases/latest";
```

Replace `ORG/REPO` before first release with auto-update enabled.

## CI/CD

| Layer | Choice | Notes |
|---|---|---|
| CI | GitHub Actions | `.github/workflows/build.yml` |
| Triggers | push `main`, tag `v*`, PR, manual | Existing behavior |
| Artifacts | zip (portable) + installer exe | Extend workflow in installer phase |
| Versioning | Git tags `vX.Y.Z` | Semver; tag push creates GitHub Release |

## Dependencies (NuGet)

| Package | Purpose |
|---|---|
| Xabe.FFmpeg 6.0.2 | FFmpeg interop |
| Xabe.FFmpeg.Downloader 6.0.2 | Bootstrap FFmpeg in dev/CI |
| System.Text.Json 9.0.0 | JSON (version parsing if API used) |

No additional NuGet packages without updating this document.

## Explicitly Not Changing

- WPF → WinUI / MAUI / Avalonia
- Xabe.FFmpeg → raw Process + ffmpeg CLI strings (unless Xabe blocker found)
- Framework-dependent publish → already moving to self-contained **for installer only**; portable zip policy TBD per release
- GitHub Actions → other CI

## Build Requirements

| Component | Version |
|---|---|
| Visual Studio 2022 (v17+) | .NET desktop workload |
| .NET 8 SDK | Required |
| Inno Setup 6 | Required for installer build |

## Related Documents

- [mission.md](./mission.md) — product purpose and scope
- [roadmap.md](./roadmap.md) — build order for new components
- [README.md](../README.md) — current build and publish commands
