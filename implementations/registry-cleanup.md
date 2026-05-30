# Registry Cleanup (Milestone C)

## What was done

Removes stale Explorer context-menu registry entries when the app moves to a new path. Registration is valid only when the HKCR command path matches the running exe.

### New Core types

| File | Purpose |
|------|---------|
| `VideoCompressor.Core/RegistryPaths.cs` | Shell name, extension list, HKCR key path helpers |
| `VideoCompressor.Core/ContextMenuRegistry.cs` | Read registered exe path; build cleanup, install, and combined `.reg` content |

### UI changes

| File | Change |
|------|--------|
| `VideoCompressorUI/MainWindow.xaml.cs` | Startup stale-path guard; path-aware `CheckRegistrationStatus`; Register uses combined `.reg`; shared `ImportRegFileElevatedAsync` / `WriteRegFileAsync` |

### Dependency

- `Microsoft.Win32.Registry` 5.0.0 added to Core for HKCR reads (Windows-only; `[SupportedOSPlatform("windows")]` on `ContextMenuRegistry`).

---

## Implementation steps

1. **`RegistryPaths`** — constants for `VideoCompressor` shell under `SystemFileAssociations` for 8 extensions.

2. **`ContextMenuRegistry`** in Core:
   - `TryGetRegisteredExePath()` — parse exe from `.mp4\shell\VideoCompressor\command`
   - `IsRegisteredForCurrentExe()` — normalized case-insensitive path compare
   - `BuildCleanupRegContent()` — `[-HKEY_CLASSES_ROOT\...\shell\VideoCompressor]` delete lines
   - `BuildInstallRegContent()` / `BuildCombinedRegContent()` — port of former `BuildRegContent` with delete-first combined variant

3. **MainWindow** — moved registration check to `Loaded` so startup cleanup can run async:
   - `RunStartupRegistryGuardAsync()` — if registered path ≠ current exe, write `cleanup_context_menu.reg` and elevated `regedit /s` (UAC on startup)
   - UAC cancel on startup: no dialog; **Register** shown via `CheckRegistrationStatus`
   - `RegisterCtxBtn_Click` — single combined `.reg` + one UAC prompt
   - Removed inline `BuildRegContent`

4. **Register visibility** — **Register** hidden only when registry command path matches current exe (not merely when key exists).

---

## How to test

Build:

```powershell
dotnet build VideoCompressor.sln -c Release
```

### TC-1 — Fresh register

1. Delete `HKCR\SystemFileAssociations\.mp4\shell\VideoCompressor` if present (admin).
2. Launch app → **Register** visible.
3. Click **Register** → approve UAC → green status; **Register** hidden.
4. `reg query "HKCR\SystemFileAssociations\.mp4\shell\VideoCompressor\command" /ve` → current exe path.

### TC-2 — Same path restart

1. Restart from same folder → **Register** hidden; no startup UAC.

### TC-3 — Stale path (path A → B)

1. Copy build to **Path A**; register.
2. Copy folder to **Path B**; launch from B.
3. Approve startup UAC → **Register** visible.
4. Click **Register** from B → command points to B exe only.
5. Right-click `.mp4` → launches B exe.

### TC-4 — Startup UAC denied

1. Stale path scenario; deny UAC on startup.
2. App opens without crash; **Register** visible.

### TC-5 — Register UAC denied

1. Click **Register** → deny UAC → cancelled message; **Register** still enabled.

### TC-6 — Single UAC on Register

1. With stale registration, click **Register** → only **one** UAC dialog.

### Regression

- GUI compress one file still works.
- CLI `--help` still headless if CLI milestone merged.

See [specs/2026-05-30-registry-cleanup/validation.md](../specs/2026-05-30-registry-cleanup/validation.md) for full matrix.

---

## Notes

- Milestone D installer must delete all `VideoCompressor` shell keys and register Program Files path (documented in requirements.md only).
- Manual UAC tests (T9) require an interactive Windows session — not automated in CI.
