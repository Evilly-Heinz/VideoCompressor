# GitHub Actions — Build & Package

## What the workflow does

File: `.github/workflows/build.yml`

| Step | Action |
|------|--------|
| Checkout | Fetches repo source |
| Setup .NET 8 | Installs .NET 8 SDK on the runner |
| Restore | `dotnet restore` — downloads NuGet packages |
| Publish | `dotnet publish -c Release` → produces single-file `VideoCompressorUI.exe` |
| Write README | Adds `README.txt` (runtime + FFmpeg instructions) to the output folder |
| Resolve version | From git tag (`v1.2.3`) or short commit SHA (`dev-abc1234`) |
| Zip | `Compress-Archive` → `VideoCompressor-<version>.zip` |
| Upload artifact | Available in GitHub Actions tab for every run (retained 30 days) |
| Create Release | Only when pushing a `v*.*.*` tag — publishes a GitHub Release with the zip attached |

## Triggers

| Event | What happens |
|-------|-------------|
| Push to `main` | Build + artifact (no release) |
| Push tag `v1.0.0` | Build + artifact + **GitHub Release** |
| Pull Request to `main` | Build only (no artifact upload) |
| Manual (`workflow_dispatch`) | Build + artifact |

## How to create a release

```bash
git tag v1.0.0
git push origin v1.0.0
```

This triggers the workflow, builds the app, and publishes a GitHub Release named `v1.0.0` with `VideoCompressor-v1.0.0.zip` attached.

For a pre-release (tag contains a hyphen, e.g. `v1.0.0-beta`), it is automatically marked as pre-release.

## Zip contents

```
VideoCompressor-v1.0.0.zip
├── VideoCompressorUI.exe   ← single-file WPF app
└── README.txt              ← .NET 8 runtime download link + FFmpeg note
```

## Requirements for the end user

- Windows 10/11 x64
- [.NET 8 Desktop Runtime](https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe) — download link is in `README.txt`
- Internet connection on first run (FFmpeg auto-downloads ~70 MB to `<app folder>\ffmpeg\`)

## Files changed

| File | Change |
|------|--------|
| `.github/workflows/build.yml` | New — CI/CD pipeline |
| `.gitignore` | Added `**/ffmpeg/` to exclude auto-downloaded FFmpeg binaries |
