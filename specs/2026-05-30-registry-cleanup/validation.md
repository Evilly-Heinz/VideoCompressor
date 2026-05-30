# Validation: Milestone C — Registry Cleanup

**Branch:** `feature/milestone-c-registry-cleanup`  
**Release:** `v1.2.1` (roadmap reference; tagging not in milestone scope)

---

## Preconditions

| Item | Detail |
|------|--------|
| Environment | Windows 10/11 x64, .NET 8 SDK; admin-capable test account |
| Branch | `feature/milestone-c-registry-cleanup` checked out |
| Build | `dotnet build VideoCompressor.sln -c Release` succeeds with zero errors |
| Binary | `bin\Release\net8.0-windows\VideoCompressorUI.exe` (or publish output path used locally) |
| Tools | PowerShell for registry inspection (`reg query` or Registry Editor) |
| Test media | Any `.mp4` file for Explorer right-click verification |

---

## Success criteria (merge-ready)

1. `RegistryPaths` and `ContextMenuRegistry` exist in `VideoCompressor.Core` with no WPF references.
2. **Register** is hidden only when HKCR command path **matches** current exe (case-insensitive full path).
3. **Register** click writes a combined `.reg` (delete + install) and uses **one** elevated `regedit /s`.
4. On startup with stale registered path, app prompts UAC for cleanup `.reg` and shows **Register** afterward.
5. After successful Register from new path, Explorer right-click launches the **current** exe only (no stale path in command).
6. UAC cancel on startup or Register fails gracefully — no crash; sensible status label.
7. Compression, batch queue, and CLI (if merged) still work — no regression smoke test.
8. Solution builds Release with zero errors.

---

## Architecture checks (code review)

| Check | Expected |
|-------|----------|
| Core types | `RegistryPaths`, `ContextMenuRegistry` in `VideoCompressor.Core` |
| No WPF in Core | Core does not reference `System.Windows.*` |
| No regedit in Core | Process/UAC logic stays in UI layer only |
| `.reg` format | UTF-16 LE; delete lines use `[-key]` syntax; install lines match prior `BuildRegContent` escaping |
| Extension list | All 8 extensions: `.mp4`, `.mov`, `.avi`, `.mkv`, `.wmv`, `.flv`, `.webm`, `.m4v` |
| Shell name | `VideoCompressor` only — no legacy key cleanup |
| Idempotent delete | Cleanup `.reg` safe when keys already absent |
| D7 scope | No README registry section required; no `implementations/` file required |

---

## Manual test cases

### TC-1 — Fresh register (baseline)

| Step | Action | Expected |
|------|--------|----------|
| 1 | Ensure no `VideoCompressor` shell key under `.mp4` (manual delete if needed) | Keys absent |
| 2 | Launch app | **Register** visible |
| 3 | Click **Register** → approve UAC | Status green; **Register** hidden |
| 4 | `reg query "HKCR\SystemFileAssociations\.mp4\shell\VideoCompressor\command"` | Command points to current exe |
| 5 | Right-click test `.mp4` in Explorer | **Compress this video** appears; launches app with file |

### TC-2 — Path match hides Register

| Step | Action | Expected |
|------|--------|----------|
| 1 | With valid registration from TC-1, restart app from **same folder** | **Register** hidden; green registered status |
| 2 | No UAC prompt on startup | Startup cleanup skipped |

### TC-3 — Stale path on startup (roadmap C6 core scenario)

| Step | Action | Expected |
|------|--------|----------|
| 1 | Copy entire app folder to **Path A**; launch; **Register**; verify HKCR command = Path A exe | Registered at A |
| 2 | Copy same build folder to **Path B** (do not unregister) | Two folders exist |
| 3 | Launch exe from **Path B** | UAC prompt for cleanup `.reg` |
| 4 | Approve UAC | **Register** visible; not shown as registered |
| 5 | Inspect HKCR command (may still show A if cleanup failed — retest after step 6) | After cleanup: key deleted or B after re-register |
| 6 | Click **Register** from Path B → approve UAC | **Register** hidden |
| 7 | `reg query` command value | Points to Path B exe only |
| 8 | Right-click `.mp4` | Menu launches Path B exe |

### TC-4 — Startup UAC cancelled

| Step | Action | Expected |
|------|--------|----------|
| 1 | Repeat TC-3 steps 1–2 (stale path at B) | — |
| 2 | Launch from Path B; **deny** UAC | App opens; no crash |
| 3 | Check UI | **Register** visible; not green "registered" |
| 4 | Optional: HKCR may still reference Path A | Acceptable until user Registers or D installer |

### TC-5 — Register UAC cancelled

| Step | Action | Expected |
|------|--------|----------|
| 1 | Unregistered state | **Register** visible |
| 2 | Click **Register** → deny UAC | Message: cancelled / admin required; **Register** still enabled |

### TC-6 — Combined `.reg` single prompt

| Step | Action | Expected |
|------|--------|----------|
| 1 | Set HKCR command to a fake old path manually (or use TC-3 stale state) | Stale registration |
| 2 | Click **Register** → approve UAC **once** | Only one UAC dialog |
| 3 | Verify command path | Current exe; delete+install applied |

### TC-7 — Idempotent re-register (same path)

| Step | Action | Expected |
|------|--------|----------|
| 1 | Registered with correct path | **Register** hidden |
| 2 | Manually show **Register** N/A — instead delete `.mp4` key only via regedit | Partial state |
| 3 | Click **Register** if visible, or force via deleting one key and relaunch | Combined reg restores all extensions |
| 4 | Check all 8 extensions have `VideoCompressor` keys | Complete set |

### TC-8 — GUI / CLI regression smoke

| Step | Action | Expected |
|------|--------|----------|
| 1 | Compress one file via GUI | Works as before |
| 2 | If CLI merged: `VideoCompressorUI.exe --help` | Headless help; exit 0 |
| 3 | Explorer launch after register | File queued in GUI |

---

## Registry inspection commands

```powershell
# Registered command (canonical check)
reg query "HKCR\SystemFileAssociations\.mp4\shell\VideoCompressor\command" /ve

# Shell key exists
reg query "HKCR\SystemFileAssociations\.mp4\shell\VideoCompressor" /ve

# Delete manually for test setup (admin PowerShell)
reg delete "HKCR\SystemFileAssociations\.mp4\shell\VideoCompressor" /f
```

---

## Failure modes

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| **Register** always visible after successful register | Path comparison bug (normalization) | Fix `IsRegisteredForCurrentExe` |
| Two UAC prompts on Register | Separate cleanup + install regedit calls | Merge into one combined `.reg` (D9) |
| Explorer still opens old exe | Cleanup not run or UAC denied | Expected until Register or Milestone D installer |
| App crash on startup stale detect | Unhandled regedit/UAC exception | Match Register error handling |
| `.reg` import fails silently | Wrong encoding (must be UTF-16 LE) | Use `Encoding.Unicode` |

---

## Sign-off checklist

- [ ] All TC-1 through TC-8 pass
- [ ] Architecture checks pass code review
- [ ] `dotnet build VideoCompressor.sln -c Release` clean
- [ ] No README / tag / `implementations/` changes required for merge (D7)
- [ ] Milestone D installer registry requirement noted in [requirements.md](./requirements.md) (D5)
