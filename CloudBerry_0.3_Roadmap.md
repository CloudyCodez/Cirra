# CloudBerry 0.3 Roadmap

Baseline: 0.2.1.0

Purpose: move CloudBerry from a strong private build into a calmer, clearer, more trustworthy 0.3 release. Version 0.3 should not be "more panels everywhere." It should make the app feel easier to start, easier to recover from mistakes, and more obviously different from other game libraries.

## Release Theme

0.3 is the public-confidence release.

The goal is not to add the largest feature count. The goal is for a normal person to open CloudBerry, understand what to do next, trust that their library is safe, and feel that the app has a distinct identity: local-first, visually polished, emulation-aware, and built with care.

## 0.3 Release Pillars

### 1. First-Run Confidence

Users should know how to get started within 10 seconds.

- [ ] Finish the empty-state pass for Home, Library, Metadata, Achievements, Friends, Settings, Emulation, and Cloudscape.
- [ ] Make every empty state follow the same structure: short heading, one-sentence reason, primary action, secondary action.
- [ ] Add first-run guidance that points users to Scan Folder, Add One Game, or Emulation setup without blocking the app.
- [ ] Make Scan Preview copy explain what CloudBerry found and why each item is safe or questionable.
- [ ] Add clearer "already in library" handling during scans so duplicate imports feel intentionally prevented, not mysteriously skipped.

Release gate:
- A clean install with no games never shows a blank or confusing page.
- The user can reach their first successful import from Home or Library without opening Settings.

### 2. Library Safety And Recovery

Users should feel safe trying CloudBerry on a real library.

- [ ] Add Undo for Remove from Library, with a short toast-style recovery window.
- [ ] Add preview/confirmation coverage for every destructive maintenance action.
- [x] Add "last checked" text to Library Health so users know whether the result is fresh.
- [x] Add a direct fix button for the top health issue: Clean Missing Games, Fill Metadata, Map Emulators, or Repair Paths.
- [ ] Improve backup/export messaging so it says where the backup went and what it contains.
- [ ] Add regression tests for scan/import duplicate handling and missing-path cleanup.

Release gate:
- No destructive action runs without preview, confirmation, or undo.
- Library Health always tells the user the next best action in plain language.

### 3. Metadata And Artwork Polish

Metadata should feel helpful, not like a provider workbench.

- [ ] Split Metadata into a beginner path and advanced comparison path.
- [ ] Beginner path: "Auto-fill missing art and summaries" as the obvious main action.
- [ ] Advanced path: provider comparison, per-field review, and manual override.
- [ ] Add manual artwork override for cover, background, banner, and icon through URL or local file.
- [ ] Add a per-game Refresh Metadata action from the Library detail view.
- [ ] Make metadata failures explain the next step: try another provider, edit manually, or retry later.

Release gate:
- A user can improve a game's cover/background without understanding metadata providers.
- Metadata search result counts, status text, and error text are natural and public-facing.

### 4. Emulation Setup That Feels First-Class

Emulation is one of CloudBerry's reasons to exist, so 0.3 should make setup feel guided.

- [ ] Add setup confidence badges for emulator platform cards: Ready, Needs Emulator, Needs Core, Custom Path, Not Checked.
- [ ] Add one-click "Use detected emulator/core" actions where detection is confident.
- [ ] Make RetroArch core detection summaries use natural plural text and action-oriented next steps.
- [ ] Add a lightweight ROM launch test path that verifies emulator/core/path readiness without launching into a confusing failure.
- [ ] Improve ROM shelf summaries in Cloudscape so counts and missing-core warnings read cleanly.

Release gate:
- A user can tell which systems are ready from the Emulation page without opening every platform card.
- ROM launch failures point to the missing piece.

### 5. Cloudscape And Visual System Polish

Cloudscape should feel premium without becoming noisy.

- [ ] Finish applying the same visual restraint used in the main shell: softer glows, calmer hover motion, theme-aware grain, readable text.
- [ ] Audit Cloudscape controller/keyboard focus states across Home, Library, Emulation, Achievements, Theatre, and Profile.
- [ ] Add tooltips or visible labels for icon-only actions where the command is not obvious.
- [ ] Verify text does not overlap at the minimum supported window size.
- [ ] Ensure every theme has readable contrast and a distinct but consistent grain/atmosphere treatment.
- [ ] Add a reduced-motion path that trims ambient sweeps, particles, and glow pulses while keeping the UI usable.

Release gate:
- Cloudscape can be used with keyboard/controller without guessing where focus is.
- Visual effects never reduce legibility.

### 6. Performance And Reliability

0.3 should feel fast enough for real libraries.

- [ ] Audit startup work and keep avoidable refreshes off the hot path.
- [ ] Keep library health/readiness calculations single-pass where possible.
- [ ] Verify large libraries remain responsive in Library and Cloudscape shelves.
- [ ] Make Fill Library and metadata batch operations show clear progress and stay cancellable.
- [ ] Verify offline behavior for metadata, media search, social presence, and update checks.
- [ ] Reduce or triage the most meaningful nullable warnings in touched code paths.

Release gate:
- Build and tests pass.
- Offline startup does not look broken just because web services are unavailable.
- The app remains interactive during long metadata/artwork work.

## Differentiators To Pull Into 0.3

These are the "above other libraries" items worth considering for 0.3 because they build on what already exists.

### Must Consider

- [ ] Cross-library "what can we play together" prototype using existing Cloud Codes and friend cards.
- [ ] Duplicate ownership detector that groups Steam, launcher, and ROM copies into one title surface.
- [ ] "Almost there" achievement surfacing for games near completion.
- [ ] One-click portable library export with JSON plus cached artwork.

### Recommended 0.3 Pick

Ship one flagship differentiator in 0.3, not all four.

Recommended: Duplicate ownership detector.

Why:
- It improves an existing pain point instead of adding a whole new surface.
- It supports PC games and ROM collections.
- It makes CloudBerry feel smarter immediately.
- It pairs naturally with scan/import safety.

0.3 scope:
- Detect likely duplicate titles by normalized title, platform/source, Steam App ID, install path, and metadata IDs.
- Show a "possible duplicate" badge in Scan Preview.
- Let the user skip, import anyway, or link as another source.
- Do not attempt full merged-title UI until later unless the basic flow is solid.

## Milestone Plan

### 0.2.1.1 - Stabilize The Current Build

Focus: no regressions after the visual/copy polish.

- [ ] Run full build and core tests.
- [ ] Quick manual smoke: start app, scan folder, add one game, launch test, remove/undo if implemented, metadata search, Cloudscape open.
- [ ] Fix any obvious broken bindings or clipped text from recent UI changes.
- [ ] Update README/current feature list if needed.

### 0.2.2.0 - First-Run And Safety

Focus: users can start and recover.

- [ ] Finish all empty states.
- [ ] Add remove undo.
- [x] Add health direct-fix buttons.
- [ ] Add scan/import duplicate tests.
- [ ] Tighten backup/export status messages.

### 0.2.3.0 - Metadata And Emulation Polish

Focus: setup and enrichment feel guided.

- [ ] Beginner metadata flow.
- [ ] Manual artwork override.
- [ ] Per-game refresh metadata.
- [ ] Emulation confidence badges.
- [ ] ROM readiness test copy and statuses.

### 0.2.4.0 - Cloudscape And Performance

Focus: couch mode feels release-grade.

- [ ] Focus-state audit.
- [ ] Reduced-motion polish.
- [ ] Theme contrast/grain pass.
- [ ] Large-library responsiveness pass.
- [ ] Offline behavior pass.

### 0.3.0.0 - Release Candidate

Focus: freeze scope, verify, ship.

- [ ] No new features after RC begins unless they fix a release-blocking issue.
- [ ] Build passes.
- [ ] Core tests pass.
- [ ] Manual smoke checklist passes.
- [ ] Update ROADMAP/README/rundown.
- [ ] Package installer.
- [ ] Verify update manifest/version flow from 0.2.1.0 to 0.3.0.0.

## 0.3 Non-Goals

These are good ideas, but they should not distract 0.3 unless they become tiny opportunistic wins.

- Full plugin API.
- Full merged-title source system.
- Full social matchmaking network.
- Save-state browser with thumbnails.
- Animated shareable Wrapped video export.
- Major new visual theme pack.
- Large settings redesign.
- New landing-page style Home dashboard.

## Release Checklist

- [ ] Version displays as 0.3.0.0 in app and installer metadata.
- [ ] Update feed recognizes 0.3.0.0.
- [ ] Fresh install path tested.
- [ ] Existing 0.2.1.0 library upgrade path tested.
- [ ] Offline startup tested.
- [ ] Empty library tested.
- [ ] 500+ game library smoke tested.
- [ ] ROM folder scan tested.
- [ ] Metadata search tested.
- [ ] Media/artwork search tested.
- [ ] Remove/undo or destructive-action safety tested.
- [ ] Cloudscape keyboard/controller navigation tested.
- [ ] Reduced visual effects tested.
- [ ] Build passes.
- [ ] Core tests pass.

## Success Definition

CloudBerry 0.3 is successful if a new user can install it, import a real library, improve a few games, set up at least one emulator platform, open Cloudscape, and trust that mistakes are recoverable.

It should feel less like "an app with a lot of interesting systems" and more like "my game library finally has a home."
