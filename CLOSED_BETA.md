# Cirra Closed Beta Playbook

Prepared September 9, 2026. First cohort: three invited testers.

## What Is Ready

Five closed-beta invitation keys are registered as Beta 01 through Beta 05, alongside three pre-existing legacy keys. Only normalized SHA-256 hashes are stored in the app. The plaintext keys are deliberately absent from this document and the distribution package. Send each person their own key privately.

The keys require a build produced after this change. They do not become valid in an older installed executable. Previously accepted keys and existing encrypted activation markers remain valid.

Local distribution candidate: `artifacts/Cirra-closed-beta-2026-09-09-r1-win-x64.zip`, with its adjacent `.sha256` checksum. The ZIP includes `START_HERE.md`, build evidence, and the self-contained app. All nine archived files were hash-compared with the published source files. It has not yet passed a fresh-account packaged-executable launch; do that before inviting the cohort.

**Current distribution:** use `artifacts/Cirra-closed-beta-2026-09-09-r4-protected-tester-win-x64.zip` and its adjacent checksum. It supersedes r1 and r3, includes all eight active keys, and excludes readable companion scripts, PDBs, obfuscation mappings, and source directories; private code symbols and strings in Cirra/Core are obfuscated. `START_HERE.md` and `TESTING_GUIDE.md` are tester-facing. `OWNER_BUILD_NOTES.md` is a concise release record, not required reading for testers.

**Important limitation:** this is an offline invitation gate, not an activation server. The app remembers a successful grant using Windows CurrentUser DPAPI. It has no global one-device limit, expiration, activation dashboard, or remote revocation. A shared key can unlock another install. Removing a hash from a future build does not invalidate existing grants or older builds. Do not promise tighter enforcement than this. If a key leaks, restrict access to the download and assess whether server-backed activation is needed before a larger cohort.

Keys are trimmed and case-insensitive for compatibility; punctuation remains significant. Copy the entire string, without surrounding quotation marks.

## Before Inviting Anyone

1. Freeze one build for the cohort. Use a unique identifier, keep its ZIP and SHA-256 checksum, and never replace a file under the same identifier. This package is `2026-09-09-r1`, Windows x64, informational version `0.2.0.1-beta.20260909.1`.
2. Run the packaged executable on a separate Windows test account or clean test machine. Confirm the access gate, first launch, library import, Horizon entry/exit, PDF reading, and ordinary shutdown. An offscreen source-level test is not an installation test.
3. Inspect the package for personal libraries, settings, cached documents, browser profiles, credentials, saves, and plaintext invitation keys. Never share the workspace or your installed app-data folder.
4. Share through a private, access-controlled download with the assigned testers. Send each key separately in a direct message. Keep a private roster of tester name, Beta 01-05 or legacy-key assignment, build ID, device summary, and completion status. Do not store passwords or full keys in bug tickets.
5. Include the tester quick start, known issues, a contact method, the checksum, and an honest signing status. This ZIP is a locally built, unsigned candidate, not a production release or an automatically installed update. Its executable was checked and reports `NotSigned`.

Prefer a signed distribution. Signing identifies the publisher but does not guarantee that a new build will avoid reputation warnings. Do not ask testers to disable Defender, SmartScreen, Smart App Control, or add broad antivirus exclusions. Ask them to report a block and pause installation instead. See [Microsoft's app reputation guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

Cirra uses WebView2 for embedded web content. A self-contained .NET build does not imply that the WebView2 Runtime is included. If needed, use Microsoft's official Evergreen Runtime installer; record a missing-runtime failure as onboarding feedback. See [Microsoft's WebView2 distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

## Protect Tester Data

- A ZIP build is not a sandbox or a separate data profile. Cirra still uses `%LOCALAPPDATA%\CloudBerry\library.json` and related legacy app-data paths. Two Cirra builds under the same Windows account can share that data. Keep only one instance open.
- Prefer a separate Windows test account with a small set of legitimately owned test games. Do not request anyone's store password, account token, entire game collection, or private documents.
- Before upgrading an existing install, fully close Cirra and make an external backup of its app-data folder and the relevant game saves. A library export is not a backup of game-save files. Test restore only against disposable copies, never the sole copy of a real save.
- If corruption, unintended deletion, unexpected metadata replacement, or a save problem appears, stop that workflow immediately. Preserve the affected files and report privately; do not repeatedly run repair actions.
- Keep bug attachments private and redact Windows usernames, paths, account identifiers, friend codes, keys, and browser content. Obtain permission before retaining diagnostics. Agree on a retention period, such as deleting raw attachments 30 days after the beta ends unless the tester agrees otherwise.

## Keep The Cohort On One Build

In **Settings > Updates**, turn off all three options for this cohort:

- Check for updates automatically.
- Download new builds automatically.
- Install staged updates when Cirra closes.

The existing app defaults to checking the shared update feed. Download and install-on-exit default to off, but an existing profile can have different settings. Ignore unrelated update offers; do not press Install or Check for Updates unless that is the assigned test. If an update is already staged, resolve it with the owner before testing. Do not post this private candidate to the shared update feed.

Ship fixes as a new build ID with a short changelog and a fresh checksum. Back up before switching. Do not assume an older executable can safely read data written by a newer build; rollback may require the matching pre-update data backup, with the tester's consent.

## Divide The Coverage

Everyone does the common smoke test. Then give each person a primary specialty, chosen to match their hardware rather than their invitation number.

| Assignment | Primary coverage | Evidence to collect |
| --- | --- | --- |
| Tester A: desktop and library | Import 10-30 mixed games, metadata matching, search, filtering, collections, themes, long titles, normal/high DPI | Wrong matches, clipped controls, cover hover behavior, saved preferences after restart |
| Tester B: Horizon and controllers | First entry, repeated entry/exit, cancellation, analog stick/D-pad, bumpers, overlays, reconnect, couch-distance readability | Controller model, wired/Bluetooth/mapper state, stuck focus, repeat rate, entry time, duplicate windows |
| Tester C: emulation and guides | A few known-working emulator launches, especially 3DS; manual discovery, wrong-platform matches, local PDFs, facing pages, cached reading offline | Exact title/platform, emulator/core version, error text, source URL and document/page details, return-to-Cirra behavior |

For emulator failures, compare the same game launched directly through its configured emulator with launching through Cirra. Change one setting at a time. Share diagnostic details, not game ROMs, BIOS files, or restricted scans.

## Common Smoke Test

1. Record build ID, Windows version, CPU/GPU/RAM, resolution and scale, number of monitors, library count, and controller model/connection. No serial numbers or account credentials needed.
2. Enter the assigned key. A deliberately incorrect entry should show a recoverable error. Restart normally and confirm that a successful activation is remembered.
3. Add a small library. Select a game, hover several other covers, sort/filter, and return. Covers should stay visible and the intended selection should remain understandable.
4. Enter Horizon, return to desktop, and enter again. Try a repeated entry click, Cancel/Escape on the loading screen, and closing during entry. There should be one dashboard or a clean return, never a stranded splash. Record a failure once rather than forcing shutdown repeatedly.
5. Leave a selected title in place through two spotlight changes. Confirm its working selection is preserved; test spotlight pause and resume. Open and close a detail overlay.
6. Open one known local PDF and discover one guide online. Confirm title/platform/source, original page layout, next/previous spread, last page, direct page jump, and reopen. After caching, try the same document offline. A wrong game or unavailable document is a bug, not a successful result.
7. Launch one known-working game, play briefly, exit the game normally, and return to Cirra. Check that play state and navigation recover. Use a disposable save for any save-related tests.
8. Change theme and motion settings, resize the desktop, and restart. Watch for unreadable text, art distortion, focus loss, forgotten preferences, or audio continuing from a closed window.

Tester B additionally performs 20 enter/exit cycles and records Task Manager observations before/after. An initial cache increase alone does not establish a leak; look for growth across repeated batches, surviving windows/audio, and helper processes that never settle. Physical controller tests remain necessary even when input-handler tests pass.

## One Week, Small Feedback Loops

- Day 1: 20-30 minutes of onboarding and common smoke testing. Fix installation/access/data-safety blockers before asking for deeper feedback.
- Days 2-4: two normal play sessions each, plus the assigned specialty. Ask what felt confusing or slow, not just whether they liked the visuals.
- Day 5: triage the reports, reproduce them, and issue one focused candidate if needed. Keep new feature requests separate from defects.
- Days 6-7: retest fixed bugs with the original reporter and run the common smoke test again. End with: best part, biggest frustration, and what stops them using Cirra daily.

Use one private place for reports, with one issue per report. A private chat plus a pinned checklist is enough for three people; screenshots alone are not reproducible bug reports.

## Bug Report Template

```text
Title:
Build ID:
Area: Desktop / Horizon / Guides / Emulation / Other
Device: Windows, GPU, resolution/scale, controller and connection
Game/platform or document/source URL (if relevant):
Steps:
1.
2.
3.
Expected:
Actual (exact error text):
Frequency: e.g. 3/3 attempts
Recovery/workaround:
Approximate local time:
Redacted screenshot/video and relevant log excerpt:
Data loss or changed files: yes / no / unknown
```

Logs currently live at `%LOCALAPPDATA%\CloudBerry\logs\cloudberry.log`. Ask for only the relevant redacted time range, not the whole app-data folder. Horizon entry records include loading-first-frame, dashboard-first-frame, and handoff-ready timings; these do not yet measure artwork completion.

## Triage And Exit Criteria

| Severity | Examples | Response |
| --- | --- | --- |
| Blocker | Data loss, save overwrite, cannot start, repeatable crash/freeze, security concern | Stop affected testing and distribution; preserve evidence; investigate before expansion |
| Major | Controller trap, game fails only through Cirra, wrong guide attachment, unreadable essential controls | Reproduce next, provide a safe workaround only when verified, retest before promotion |
| Minor | Cosmetic clipping, inconsistent animation, awkward wording with a usable workaround | Track with screenshot and conditions; batch for a polish pass |

Do not expand the cohort until all three people complete the core journey, no unresolved blocker remains, major defects are fixed or explicitly accepted with a tested workaround, and fixes are retested in the exact candidate being distributed. State the remaining limitations honestly. Three testers provide valuable early feedback, not proof of universal hardware compatibility.

## Invitation Message

> Hey! I'd love your help testing Cirra privately for about a week. It's an early build, so start with a few games and back up anything important. I'll send your download and personal key separately. Please keep the build and key private, and tell me what breaks, feels confusing, or slows you down. A short description with steps and a screenshot is perfect. No need to share passwords or your game files. If Windows blocks the build or anything touches saves unexpectedly, stop and message me.

## Next Access-System Work

For a larger beta, decide whether to add server-backed invite redemption, explicit device limits, expiry/revocation, an offline grace policy, and privacy-conscious activation records. Those features are not implemented by adding these three hashes. Preserve legitimate testers' offline access when designing that policy.
