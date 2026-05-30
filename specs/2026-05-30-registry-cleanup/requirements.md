# Requirements: Registry Cleanup (Milestone C)

**Source:** [roadmap.md](../roadmap.md) — Milestone C (phases C1–C6)  
**Summary:** Remove stale Explorer context-menu registry entries on Register and startup; treat registration as valid only when the registered exe path matches the running app.  
**Branch:** `feature/milestone-c-registry-cleanup`  
**Spec folder:** `specs/2026-05-30-registry-cleanup/`  
**Target release:** `v1.2.1` (roadmap reference; tagging out of scope for this spec — see D7)

---

## Context

Video Compressor registers an Explorer right-click **"Compress this video"** entry via a UTF-16 LE `.reg` file imported with elevated `regedit.exe /s`. Keys live under `HKEY_CLASSES_ROOT\SystemFileAssociations\{ext}\shell\VideoCompressor` for eight video extensions.

When users move the app folder or install a new build to a different path, old registry entries can remain. Explorer may show duplicate menu items or launch a missing exe. Milestone C fixes this by cleaning known keys before re-registering and detecting path drift on startup.

Today, `CheckRegistrationStatus()` only checks whether the `.mp4` shell key **exists** — it does not verify the registered command path. `BuildRegContent()` in `MainWindow.xaml.cs` generates install `.reg` content but has no delete/cleanup path.

## Problem statement

| Layer | Finding |
|-------|---------|
| User impact | After moving the app, Explorer may still point at the old exe path; **Register** may add a second entry without removing the stale one. |
| Detection | UI shows "Registered" even when the registry command path does not match the current exe. |
| Roadmap | [mission.md](../mission.md) success criterion #3: **Register** and startup both clean context-menu keys before writing the current path. |
| Architecture | Registry helpers belong in `VideoCompressor.Core` per [tech-stack.md](../tech-stack.md); UI keeps UAC/`regedit` orchestration only. |

## Scope

### In scope

- **`RegistryPaths`** constants class — all current `VideoCompressor` shell key paths for extensions: `.mp4`, `.mov`, `.avi`, `.mkv`, `.wmv`, `.flv`, `.webm`, `.m4v`.
- **`ContextMenuRegistry`** in Core — read registered exe path, build delete `.reg` content, build combined delete+install `.reg` content (same rules as existing `BuildRegContent`).
- **Cleanup via `.reg` delete lines** + elevated `regedit /s` — consistent with today's Register flow; no direct `Registry.DeleteSubKey` as primary mechanism.
- **Register click** — one combined `.reg` file (delete all known keys, then write fresh keys); **one** UAC prompt via single `regedit /s`.
- **Startup guard** — compare registered command path (from HKCR) to current exe; if stale, run elevated cleanup `.reg` and show **Register** (same as unregistered).
- **Startup elevation** — auto UAC when stale path detected on startup (user may approve or deny).
- **UAC denied on startup** — show **Register**; stale Explorer entries may remain until user clicks **Register** or Milestone D installer runs.
- **`CheckRegistrationStatus` enhancement** — hide **Register** only when shell key exists **and** registered exe path equals current exe (case-insensitive, normalized paths).
- Manual validation matrix in [validation.md](./validation.md).

### Out of scope

- Legacy shell names or key paths beyond `VideoCompressor` under `SystemFileAssociations` (stakeholder confirmed: **current keys only**).
- Separate persistence for "last registered path" (HKCU, settings file) — path is read from existing registry command value instead (see D2).
- Installer / Inno Setup work (Milestone D) — **document requirement only** for install-time cleanup+register fallback (see D5).
- README updates, release tag `v1.2.1`, `implementations/` write-up (see D7).
- Automated test project.
- Separate **Unregister** button.
- CLI changes (Milestone B).

## Functional requirements

1. **Key inventory (`RegistryPaths`)**  
   Central list of registry subkey paths relative to `HKCR` for each extension's `VideoCompressor` shell and `command` subkeys.

2. **Read registered path**  
   Parse exe path from `HKCR\SystemFileAssociations\.mp4\shell\VideoCompressor\command` default value (pattern: `"<exePath>" "%1"`). If key missing or unparseable → treat as not registered.

3. **Path comparison**  
   Compare registered path to `Process.GetCurrentProcess().MainModule.FileName` (fallback: `{BaseDirectory}\VideoCompressorUI.exe`). Use case-insensitive comparison after `Path.GetFullPath` normalization.

4. **Register flow (`RegisterCtxBtn_Click`)**  
   - Build combined `.reg`: `[-delete]` lines for all `RegistryPaths` entries, then install blocks (current `BuildRegContent` logic).  
   - Write UTF-16 LE next to exe (reuse or rename `install_context_menu.reg`).  
   - Single elevated `regedit.exe /s`.  
   - On success → `CheckRegistrationStatus()` shows registered state; **Register** hidden.

5. **Startup flow**  
   - After main window load (or during existing registration check), if registered path ≠ current exe:  
     - Write cleanup-only `.reg` with delete lines for all known keys.  
     - Launch elevated `regedit /s` (UAC prompt).  
     - Update Explorer Integration UI to unregistered state (**Register** visible).  
   - If registered path matches → registered UI unchanged.

6. **Startup UAC cancelled**  
   - Do not crash; show **Register** and unregistered status message.  
   - Stale keys may remain until user re-registers.

7. **Idempotent cleanup**  
   Delete operations must succeed when keys are already absent (`.reg` delete of missing keys is harmless).

8. **GUI unchanged elsewhere**  
   Compression, batch queue, CLI bootstrap, and Explorer card layout unchanged except registration status logic.

## Decisions

| # | Decision | Detail |
|---|----------|--------|
| D1 | Legacy keys | **VideoCompressor only** — no alternate shell names or legacy hives. |
| D2 | Last registered path | **Read from HKCR** command value (same data `BuildRegContent` writes); no separate settings/registry store (roadmap C5 satisfied by inference, not new persistence). |
| D3 | Stale path UI | Cleanup + show **Register** — treat as unregistered after startup cleanup attempt. |
| D4 | Startup elevation | **UAC on startup** when stale path detected — auto elevated cleanup `.reg`. |
| D5 | Installer fallback | **Document only** — Milestone D installer must delete all `VideoCompressor` shell keys and register the new install path if in-app startup cleanup was impossible. No installer code in C. |
| D6 | Register visibility | **Register hidden** only when registered path **matches** current exe; wrong path → **Register** visible. |
| D7 | Deliverables | **Code only** — no README, no `v1.2.1` tag, no `implementations/` note in this milestone. |
| D8 | Cleanup mechanism | **`.reg` delete lines + `regedit /s`** — same pattern as existing Register flow. |
| D9 | Register UAC count | **One combined `.reg`** — delete + install in single elevated import. |
| D10 | Core location | `RegistryPaths`, `ContextMenuRegistry` in `VideoCompressor.Core`; UI calls Core for content generation and path reads. |
| D11 | Elevated import | UI layer retains `ProcessStartInfo` + `Verb = "runas"` + `regedit.exe /s` — Core stays free of process/UAC code. |

## Milestone D forward requirement (document only)

When the Inno Setup installer (Milestone D) is implemented, it **must**:

1. Delete all `HKCR\SystemFileAssociations\{ext}\shell\VideoCompressor` keys for the eight extensions (same list as `RegistryPaths`).
2. Register context menu pointing at the installed `VideoCompressorUI.exe` under Program Files.
3. Run with installer elevation so cleanup succeeds even if the user previously denied in-app startup UAC.

This covers the stakeholder fallback: *"If impossible [startup cleanup], remove and register on installation."*

## Technical touchpoints

| File / area | Role |
|-------------|------|
| `VideoCompressor.Core/RegistryPaths.cs` (new) | Constants for shell/command key paths and extension list |
| `VideoCompressor.Core/ContextMenuRegistry.cs` (new) | Read registered path; build cleanup, install, and combined `.reg` content |
| `VideoCompressorUI/MainWindow.xaml.cs` | Use Core for reg content; enhanced `CheckRegistrationStatus`; startup stale-path cleanup; slim or remove `BuildRegContent` |
| `scripts/install_context_menu.reg` | Optional header comment update only if needed; manual script unchanged in behavior |
| `VideoCompressor.Core` | No WPF, no `Process`/`regedit` — string generation and registry **read** only |

## Dependencies

| Dependency | Status |
|------------|--------|
| Milestone A — Shared Core | **Complete** |
| Milestone B — CLI Mode | Spec exists; registry work independent of CLI |
| [tech-stack.md](../tech-stack.md) Windows Integration | Base mechanism; this spec adds cleanup + path validation |
| [mission.md](../mission.md) success criterion #3 | Primary driver |

## Related documents

- [roadmap.md](../roadmap.md) — phases C1–C6
- [tech-stack.md](../tech-stack.md) — context menu and registry cleanup summary
- [validation.md](./validation.md) — merge-ready checks and manual test matrix
- [implementations/context-menu-registration.md](../../implementations/context-menu-registration.md) — original Register feature
