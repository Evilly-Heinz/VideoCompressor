# Context-Menu (Explorer Right-Click) Integration

## What was done

Adds a one-click "Register" button inside the app that writes a `.reg` file with the
correct exe path baked in, then silently imports it into the registry via a UAC-elevated
`regedit /s` call.

---

## Files changed

| File | Change |
|------|--------|
| `VideoCompressorUI/MainWindow.xaml` | Added **Explorer Integration** card at the bottom of the content panel |
| `VideoCompressorUI/MainWindow.xaml.cs` | Added `RegisterCtxBtn_Click` handler + `BuildRegContent` helper |
| `scripts/install_context_menu.reg` | Added `.m4v` entry, updated header with both usage options |

---

## Implementation steps

### 1. `MainWindow.xaml` — Explorer Integration card

Added a `Card`-styled `Border` below the status bar with:
- Label row describing the feature
- `x:Name="CtxMenuStatusLabel"` — shows live status feedback
- `x:Name="RegisterCtxBtn"` — triggers registration

### 2. `MainWindow.xaml.cs` — `RegisterCtxBtn_Click`

Flow:
1. Read live exe path via `Process.GetCurrentProcess().MainModule.FileName`
   (always correct regardless of where the user installed the app)
2. Call `BuildRegContent(exePath)` to generate valid `.reg` text
3. Write file as **UTF-16 LE** (`Encoding.Unicode`) next to the exe:
   `<AppDir>\install_context_menu.reg`
4. Start `regedit.exe /s "<path>"` with `Verb = "runas"` → triggers UAC prompt
5. `WaitForExit()` on a background thread (`Task.Run`) so the UI stays responsive
6. Update `CtxMenuStatusLabel` with success (green) or failure (red) message
7. Handle `Win32Exception` error code 1223 (user cancelled UAC) gracefully

### 3. `BuildRegContent` — .reg escaping rules

`.reg` string values require:
- Every `\` doubled → `\\`
- Every `"` escaped → `\"`

Example for `C:\Program Files\VideoCompressorUI.exe`:

```
[HKEY_CLASSES_ROOT\SystemFileAssociations\.mp4\shell\VideoCompressor]
@="Compress this video"
"Icon"="C:\\Program Files\\VideoCompressorUI.exe,0"
[HKEY_CLASSES_ROOT\SystemFileAssociations\.mp4\shell\VideoCompressor\command]
@="\"C:\\Program Files\\VideoCompressorUI.exe\" \"%1\""
```

Registered extensions: `.mp4` `.mov` `.avi` `.mkv` `.wmv` `.flv` `.webm` `.m4v`

---

## How to test

1. **Build** the project in Release mode.
2. Run `VideoCompressorUI.exe`.
3. Scroll to the bottom — you will see the **Explorer Integration** card.
4. Click **Register** → UAC prompt appears.
5. Click **Yes** → status turns green: *"✓ Registered — right-click any video file…"*
6. Open File Explorer, right-click any `.mp4` (or other supported) file.
7. You should see **"Compress this video"** in the context menu with the app icon.
8. Click it — the app opens with that file pre-loaded.

### Verify the registry entry manually

```powershell
Get-Item "HKLM:\SOFTWARE\Classes\SystemFileAssociations\.mp4\shell\VideoCompressor"
```
*(Requires admin PowerShell)*

### UAC cancel test

Click **Register**, then click **No** on the UAC prompt.  
The status label should show: *"Cancelled — administrator permission is required."*

### Manual import (no app)

Edit `scripts/install_context_menu.reg`: replace every `%INSTALL_DIR%` with
the actual folder path (double backslashes), then double-click the file.
