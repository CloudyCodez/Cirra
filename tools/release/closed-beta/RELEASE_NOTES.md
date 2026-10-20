## Cirra v0.4.1 Closed Beta

This is a prerelease for invited Windows testers. It is not a general public build.

### Included

- A new indexed library engine with schema backups, transactional reconciliation, and fast large-library search.
- Persistent exact-artifact duplicate protection plus moved/copied-file review using bounded content fingerprints.
- Scan operations now report useful intake totals in Activity Center and can be cancelled before import.
- Player-edited metadata fields are protected independently and retain provider, confidence, timestamp, and revision evidence.
- One launch state machine now powers Cirra and Horizon, including readiness, repair, cancellation, tracking, and return states.
- Horizon hero, detail, and attract artwork now use generation-safe loading and a bounded shared media cache, so stale loads cannot replace the current selection.
- Cirra and Horizon premium visual, motion, and loading polish.
- Four-slot Horizon controller routing with stable active-player ownership.
- Activity Center visibility and cancellation for guide discovery and PDF preparation.
- Guide reader with preserved facing-page presentation.
- The Cirra rebrand, icon assets, expanded library tooling, launch readiness, and test coverage.
- Cirra now opens directly without an invitation-key or activation-marker gate.

### Before You Download

- This ZIP is for Windows x64 only.
- Extract it before running `Cirra.exe`.
- The build is unsigned, so Windows may show a SmartScreen warning.
- Read `START_HERE.md` and `TESTING_GUIDE.md` in the ZIP before testing.

### Verification

SHA-256 for the ZIP is included as a separate `.sha256` release asset. All 275 automated tests pass: 108 application tests and 167 Core/Data tests. The eleven retired access-gate tests were removed with the gate. With no activation marker present, the protected packaged executable opened a responsive Cirra window, created no replacement marker, closed normally with exit code 0, and produced no new diagnostic errors.

Please report issues with reproducible steps, your Windows version, and the affected game/platform or controller where relevant.
