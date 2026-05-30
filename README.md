# Video Compressor

A lightweight Windows video compressor with a Fluent dark UI.  
Drop a video file, pick quality/speed, click **Compress** — done.

Output: `{original_name}_compressed.mp4` saved next to the source file.

---

## Features

- Drag-and-drop or browse to select video
- H.264 + AAC re-encode via FFmpeg (auto-downloaded on first use)
- Adjustable CRF quality slider (18 – 40) and speed preset
- Live progress bar with percentage
- One-click **Explorer right-click menu** registration ("Compress this video")
- **Command-line mode** for scripts and automation (`-q`, `-s`, `-o`)
- Single-file `.exe`, no installer required

---

## Requirements (end users)

| Requirement | Notes |
|---|---|
| Windows 10 / 11 x64 | |
| [.NET 8 Desktop Runtime](https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe) | ~55 MB, one-time install |
| FFmpeg | Included in `ffmpeg\` inside the release zip — no download needed |

---

## Quick start

1. Download the latest `VideoCompressor-vX.Y.Z.zip` from [Releases](https://github.com/Evilly-Heinz/VideoCompressor/releases/latest).
2. Extract anywhere (e.g. `C:\Tools\VideoCompressor\`).
3. Install [.NET 8 Desktop Runtime](https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe) if not already present.
4. Run `VideoCompressorUI.exe`.

FFmpeg is included in the `ffmpeg\` subfolder — no internet access required.

### Register the right-click menu (optional)

In the app, scroll to the **Explorer Integration** card at the bottom and click **Register**.  
A UAC prompt will appear; click **Yes**.  
After that, right-clicking any `.mp4`, `.mov`, `.avi`, `.mkv`, `.wmv`, `.flv`, `.webm`, or `.m4v`  
file in Explorer shows **"Compress this video"**, which opens the app with that file pre-loaded.

---

## Command-line usage

The same `VideoCompressorUI.exe` runs headless when any CLI flag is present (`-q`, `-s`, `-o`, `-h`, `--help`, `/?`).  
Launching with a file path only (no flags) still opens the GUI.

### Syntax

```
VideoCompressorUI.exe "<input>" [-q <crf>] [-s <preset>] [-o "<output>"]
VideoCompressorUI.exe -h | --help | /?
```

| Flag | Default | Description |
|---|---|---|
| `-q` | 23 | CRF quality (18–40) |
| `-s` | `medium` | x264 preset (`ultrafast` … `veryslow`) |
| `-o` | auto | Output file path (must not already exist) |

### Default output

When `-o` is omitted, output is `{name}_compressed.mp4` in the input file's folder.  
If that file already exists, the app writes `{name}_compressed_1.mp4`, `_2`, and so on.

### Progress and exit codes

- Encode progress (percent) is written to **stderr** during compression.
- Exit codes: **0** success, **1** failure (FFmpeg/I/O or `-o` target exists), **2** invalid arguments, **3** cancelled (reserved).

### Examples

```powershell
# Defaults (CRF 23, preset medium)
VideoCompressorUI.exe "C:\Videos\clip.mp4"

# Custom quality and speed
VideoCompressorUI.exe "C:\Videos\clip.mp4" -q 28 -s fast

# Explicit output (fails if output already exists)
VideoCompressorUI.exe "C:\Videos\clip.mp4" -o "C:\Videos\clip_small.mp4"

# Help
VideoCompressorUI.exe --help
```

**Script tip:** use `Start-Process -Wait -PassThru` to read `$process.ExitCode` — direct `$LASTEXITCODE` in PowerShell may not reflect WinExe exit codes reliably.

---

## Project structure

```
VideoCompressor.sln
│
├── VideoCompressor.Core/          ← Shared compression + CLI parsing
├── VideoCompressorUI/             ← WPF shell; references Core
│   ├── Program.cs                 ← CLI/GUI entry
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   └── Themes/Styles.xaml
│
├── scripts/
│   └── install_context_menu.reg
│
└── .github/workflows/build.yml
```

Output: `bin\Release\VideoCompressorUI.exe`

---

## Build from source

### Requirements

| Component | Notes |
|---|---|
| Visual Studio 2022 (v17+) | Community / Pro / Enterprise |
| Workload: **.NET desktop development** | includes .NET 8 SDK |

NuGet packages are restored automatically:

| Package | Purpose |
|---|---|
| `Xabe.FFmpeg` 6.0.2 | FFmpeg wrapper |
| `Xabe.FFmpeg.Downloader` 6.0.2 | Auto-downloads FFmpeg binaries on first use |
| `System.Text.Json` 9.0.0 | JSON support (transitive dependency) |

### Steps

```bash
# Clone
git clone <repo-url>
cd VideoCompressor2

# Restore & publish (single-file, framework-dependent)
dotnet publish VideoCompressorUI/VideoCompressorUI.csproj -c Release -r win-x64 -o ./publish
```

Or open `VideoCompressor.sln` in Visual Studio 2022, select **Release | x64**, and press `Ctrl+Shift+B`.

---

## CI / CD

GitHub Actions workflow: `.github/workflows/build.yml`

| Trigger | Result |
|---|---|
| Push to `main` | Build + upload artifact |
| Push tag `v1.0.0` | Build + upload artifact + **GitHub Release** with zip attached |
| Pull request to `main` | Build only |
| Manual (`workflow_dispatch`) | Build + upload artifact |

To publish a release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## Technical notes

- **Single project**: C++ wrapper removed; FFmpeg is invoked via `Xabe.FFmpeg` NuGet — no separate `.exe` needed.
- **Framework-dependent, multi-file publish**: avoids extracting anything to `%TEMP%` at runtime, eliminating the main AV false-positive trigger. Single-file WPF bundles extract native DLLs (`PresentationNative`, `vcruntime…`) to `%TEMP%` on every launch.
- **FFmpeg bundled in release**: `ffmpeg.exe` and `ffprobe.exe` are downloaded during CI and included in the release zip — no runtime internet access or executable download needed.
- **Context menu**: written as UTF-16 LE `.reg` and imported via `regedit.exe /s` with UAC elevation — the app remains non-elevated at all other times.
- **UI**: custom title bar with `WindowChrome`, Win11 Fluent dark palette, Segoe UI Variable Text, min/max/close caption buttons.
- **Target**: `net8.0-windows`, `win-x64`, single-file exe.
