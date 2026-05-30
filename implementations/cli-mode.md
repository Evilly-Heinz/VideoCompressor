# CLI Mode (Milestone B)

Headless command-line compression via `VideoCompressorUI.exe` when CLI flags are present.

## Implementation steps

1. **CliArguments** (`VideoCompressor.Core/CliArguments.cs`) — parse `-q`, `-s`, `-o`, help flags; validate CRF, preset, input existence, output parent directory; `IsCliMode()` for entry detection.
2. **CliExitCode** (`VideoCompressor.Core/CliExitCode.cs`) — 0 success, 1 failure, 2 invalid args, 3 cancelled (reserved).
3. **OutputPathResolver.ResolveUniqueDefault** — default output `{name}_compressed.mp4`; if taken, use `_1`, `_2`, … suffix.
4. **ConsoleProgressReporter** — writes `{percent}%` to stderr, throttled on duplicate values.
5. **CliHost.Run** — orchestrates parse, help, output collision rules, FFmpeg bootstrap, `CompressionService.CompressAsync`.
6. **Program.cs** — custom entry point with `StartupObject`; `Environment.Exit(CliHost.Run(args))` before WPF when CLI mode.
7. **App.xaml.cs** — GUI path uses `FfmpegBootstrap.ConfigurePaths()` only.
8. **README** — Command-line usage section with syntax, defaults, exit codes, examples.

## How to test

### Build

```powershell
dotnet build VideoCompressor.sln -c Release
```

### Help (exit 0, no window)

```powershell
.\bin\Release\VideoCompressorUI.exe --help
```

### Invalid arguments (exit 2)

```powershell
$p = Start-Process -FilePath ".\bin\Release\VideoCompressorUI.exe" `
  -ArgumentList "-q","99",".\test-cli\sample.mp4" -Wait -PassThru -NoNewWindow
$p.ExitCode   # expect 2
```

### Explicit `-o` collision (exit 1)

```powershell
# Create output file first, then:
$p = Start-Process -FilePath ".\bin\Release\VideoCompressorUI.exe" `
  -ArgumentList ".\video.mp4","-o",".\video_out.mp4" -Wait -PassThru -NoNewWindow
$p.ExitCode   # expect 1 when output exists
```

### Default output collision

1. Place `video.mp4` and `video_compressed.mp4` in the same folder.
2. Run `VideoCompressorUI.exe "video.mp4" -q 23` (any flag triggers CLI).
3. Expect output at `video_compressed_1.mp4` (not overwriting existing).

### Full encode (exit 0)

Use a real video file; FFmpeg must be present or will auto-download on first run:

```powershell
.\bin\Release\VideoCompressorUI.exe "C:\Videos\clip.mp4" -q 28 -s fast
```

Progress appears on stderr; output file is created.

### GUI regression

Launch with no flags — GUI opens unchanged:

```powershell
.\bin\Release\VideoCompressorUI.exe
.\bin\Release\VideoCompressorUI.exe "C:\Videos\clip.mp4"
```

Second form opens GUI with file (Explorer-style); no headless encode.

### Exit codes in scripts

Use `Start-Process -Wait -PassThru` and read `$p.ExitCode`. Direct `$LASTEXITCODE` in PowerShell may not reflect WinExe exit codes reliably.

## Implementation log

See [specs/2026-05-30-cli-mode/implementation-note.html](../specs/2026-05-30-cli-mode/implementation-note.html).
