# Tasks: Registry Cleanup (Milestone C)

**Spec:** [specs/2026-05-30-registry-cleanup/](../specs/2026-05-30-registry-cleanup/)  
**Branch:** `feature/milestone-c-registry-cleanup`

- [x] T1 — `RegistryPaths` constants
- [x] T2 — `ContextMenuRegistry` read path + cleanup `.reg` content
- [x] T3 — Combined delete+install `.reg` content (install logic moved to Core)
- [x] T4 — `RegisterCtxBtn_Click` uses combined `.reg`, single UAC
- [x] T5 — Startup stale-path detection + elevated cleanup
- [x] T6 — `CheckRegistrationStatus` path match required
- [x] T7 — Shared elevated regedit + UTF-16 LE write helpers
- [x] T8 — Milestone D installer requirement (documented in requirements.md)
- [ ] T9 — Manual test matrix (requires interactive Windows + UAC)
