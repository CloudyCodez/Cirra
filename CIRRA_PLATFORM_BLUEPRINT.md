# Cirra Platform And Experience Blueprint

Date: October 19, 2026
Inputs: Cirra source, the local Playnite 10.63 REA report, and the prior LaunchBox/Big Box REA pass

## Product Direction

Cirra should become a modern gaming platform with two excellent surfaces over one trustworthy engine:

- **Cirra Desktop** is the fast, precise curator and daily launcher.
- **Horizon** is the cinematic, controller-complete couch dashboard.
- **Cirra Engine** owns identity, storage, scanning, metadata, launch orchestration, sessions, input, operations, extensions, and recovery.

The goal is not LaunchBox Jr. or a Playnite skin. Cirra should borrow proven boundaries and operational discipline while keeping its cloud identity, premium material system, physical collection views, integrated guides, local-first privacy, session continuity, and calmer UX.

## Current Cirra Baseline

Cirra already has important advantages:

- modern .NET 10 and WPF rather than the observed Playnite build's .NET Framework 4.6.2 base;
- a core/application project split;
- scanner normalization and duplicate defenses, launch preflight, emulator discovery, metadata aggregation, guide discovery/reading, achievements, save continuity, library history, updates, media caching, Activity Center, controller routing, and device profiles;
- a distinct Horizon window with controller navigation, shelves, physical case rendering, momentum, trailers, audio, splash transitions, and theme effects;
- a growing regression suite around scanning, themes, controllers, Horizon selection, cases, launch paths, and lifecycle behavior.

The limiting architecture is now visible:

- the durable library is still a whole-snapshot `library.json` store, which constrains indexing, transactional imports, provenance, and large-library queries;
- `MainViewModel` and both window code-behind files own too many unrelated workflows;
- Horizon is visually distinct but not yet isolated behind a complete presentation/navigation contract;
- imports create game records too early instead of persisting artifact evidence and staging a reviewable change set;
- metadata fields do not yet have complete source, confidence, lock, and revision provenance;
- extension points are internal conventions rather than a stable SDK;
- themes are rich built-ins, but do not yet have a versioned external contract or structural composition API.

## Reference Interpretation

### Playnite 10.63

The locally installed Playnite build uses a shared managed engine (`Playnite.dll`), distinct desktop and fullscreen executables, and a public SDK. Its complete metadata inventories show explicit database, scanner, exclusion, metadata, controller, theme, update, backup, and extension systems. Fullscreen owns specialized view models, tile virtualization, focus, controller events, smooth scrolling, audio, settings, and resource cleanup. Its versioned desktop/fullscreen theme packages expose controls, views, resources, audio, and prompt families.

The architecture is proven as a shipped organization, but this static pass did not benchmark Playnite or prove that every mechanism is bug-free. Cirra should adopt the responsibility boundaries, not the old .NET Framework runtime, exact dependencies, code, or appearance.

### LaunchBox And Big Box

The prior local REA pass observed a small native launcher handing off to `Core\LaunchBox.exe`, a large managed `LaunchBox.dll` on modern .NET/WPF with Caliburn-style MVVM, structured settings/emulator mappings/input bindings, and relational metadata. The plugin-facing surface included emulator installation/update, BIOS, launch preparation, save management, and achievements.

Big Box was observed as a separate managed couch executable with its own view-model layer, theme loader, input/navigation handling, media lifecycle, filtering, screensaver/attract behavior, and prewarming. That supports treating Horizon as a product surface with independent lifecycle and performance needs while sharing the same engine.

The LaunchBox evidence does not justify copying its schema, UI, naming, themes, or implementation. Its useful contribution is mature orchestration across emulator configuration, media-heavy presentation, plugins, and a dedicated couch shell. The retained evidence for that pass is `C:\Users\conno\AppData\Local\Temp\launchbox-phase1-rea-evidence.json`.

## What To Take From Each Reference

| Area | Playnite lesson | LaunchBox / Big Box lesson | Cirra action |
| --- | --- | --- | --- |
| Product topology | Shared engine with distinct desktop/fullscreen applications | Big Box is treated as a dedicated couch product with its own lifecycle | Keep one engine; progressively isolate Desktop and Horizon presentation hosts |
| Library data | Indexed collections, filters, imported-file registry, exclusions, buffered updates | Structured settings/emulator mappings plus relational metadata | Move to a transactional indexed store with explicit migrations and repository boundaries |
| Scanning | Scanner configs, prior-import identity, exclusions, staged database checks | Platform/emulator mapping is preserved as durable configuration | Build an evidence-first import transaction with exact-artifact uniqueness and review |
| Metadata | Field-level on-demand provider contract | Broad media/metadata lifecycle and platform-specific assets | Add field provenance, locks, confidence, source precedence, comparison, and rollback |
| Launching | Explicit play/install/uninstall actions and tracking settings | Rich launch preparation, emulator/BIOS/save orchestration | Model installations and launch profiles separately; make readiness and tracking explicit |
| Fullscreen | Separate executable, virtualizing tile panel, input/audio/settings ownership | Dedicated Big Box navigation, media lifecycle, themes, attract/screensaver, prewarm | Give Horizon a bounded shell, navigation graph, virtualized surfaces, and resource budget |
| Controllers | Central manager, connected-device events, remapping/settings | Controller-first couch navigation across a dedicated shell | One normalized input service with ownership, capabilities, repeat, focus, and prompt families |
| Themes | Versioned desktop/fullscreen theme packages and semantic resources | Deep theme loader and media-aware presentation | Create a Cirra Theme API around semantic tokens, layouts, motion, audio, and atmosphere scenes |
| Extensions | Public SDK across library, metadata, actions, search, menus, panels, input | Plugin APIs cover emulator/save/achievement workflows | Start a small capability-based Cirra SDK with compatibility gates and diagnostics |
| Recovery | Engine-owned backup, restore, update, and migration surfaces | Mature settings/data portability | Make every persistent operation previewable, transactional, backed up, and recoverable |

## Target Architecture

### 1. Cirra Domain

Create stable models that do not know about WPF or provider implementations:

- `GameTitle`: the conceptual game.
- `Release`: platform, region, edition, language, release date, identifiers.
- `Installation`: local/remote availability and owned artifacts.
- `LaunchProfile`: executable, URI, emulator/core, arguments, working directory, prerequisites, scripts, and tracking policy.
- `LibraryArtifact`: normalized path, file identity, size, timestamps, quick/content hashes, container membership, source scanner, and last-seen state.
- `MetadataValue<T>`: value, source, confidence, acquired time, user lock, and revision.
- `PlaySession`: launch intent, tracked processes, confidence, start/end, exit state, and return context.
- `MediaAsset`: role, source, dimensions, integrity, focal point, cache state, and consuming surfaces.

This model solves duplicates more cleanly than forcing one file, one title, and one launch action into a single record.

### 2. Cirra Data

Introduce `CloudBerry.Data` as an indexed transactional store. SQLite with EF Core is the recommended modern-.NET default because it offers migrations, constraints, transactions, indexes, and a well-supported inspection/recovery story. Do not copy Playnite's LiteDB version or LaunchBox's schema.

Required properties:

- schema-versioned migrations with backup-before-migrate;
- unique constraints for canonical artifact identity and provider identifiers;
- transactions for scan commit, metadata batch apply, merge/split, and delete/restore;
- write-ahead logging and short-lived contexts so reads remain responsive;
- repositories/query services that return projections rather than exposing persistence objects to WPF;
- migration from `library.json` with a dry-run report, deterministic IDs, rollback, and round-trip fixture tests;
- JSON export remains a portable user-owned interchange format, not the live query engine.

### 3. Import And Reconciliation Engine

Rebuild scanning as a six-stage operation:

1. **Discover:** enumerate files with source/root ownership and cancellation.
2. **Identify:** canonical path, volume/file identity where available, container/disc grouping, quick hash, and optional full hash.
3. **Classify:** platform, artifact kind, title tokenization, edition/region/disc signals, and launchability.
4. **Reconcile:** exact existing artifact, moved artifact, new installation, probable duplicate, ambiguous sibling, or ignored/excluded item.
5. **Review:** show additions, updates, merges, exclusions, and uncertain candidates with reasons.
6. **Commit:** one transaction, one history entry, one truthful Activity Center outcome.

Hard guarantees:

- the same canonical artifact cannot be committed twice;
- rescanning the same unchanged roots is idempotent;
- overlapping roots do not duplicate candidates;
- cue/bin, m3u/discs, archives, shortcuts, and launcher manifests resolve to one owned artifact group;
- disconnected drives become unavailable, never silently deleted;
- exclusions and manual merge/split decisions survive rescans;
- crashes or cancellation before commit leave the library unchanged.

An absolute zero percent duplicate promise is not honest for ambiguous files. Cirra can guarantee zero duplicate records for identical artifacts and route uncertain title-level matches into review instead of guessing.

### 4. Metadata And Asset Pipeline

Turn the current provider aggregator into a field-oriented pipeline:

- providers declare capabilities, rate limits, regions/languages, and evidence quality;
- every candidate field carries source and match reasoning;
- user locks always win;
- automatic refresh applies only unambiguous, allowed fields;
- conflicts enter a side-by-side Change Review;
- images are acquired, decoded, resized, and cached off the UI thread with byte budgets and cancellation;
- manuals and guides use the same candidate/provenance model, with document type, edition, language, region, integrity, and reader verification.

This upgrades Cirra's existing metadata and guide systems rather than replacing their provider work.

### 5. Launch And Session Orchestrator

Create one launch state machine shared by Desktop and Horizon:

`Idle -> Checking -> RepairRequired | Ready -> Preparing -> Starting -> Tracking -> Returning -> Completed | Failed | Cancelled`

It should own:

- launch-profile selection;
- emulator/core/BIOS/config readiness;
- prerequisite and save checks;
- process handoff and child-process discovery;
- confidence-aware playtime;
- after-exit return context and optional save backup;
- log/event output suitable for Activity Center and support bundles.

The UI asks the orchestrator to launch. It does not reproduce launch logic in buttons, windows, or view models.

### 6. Input And Navigation Platform

Expand the current input source/router/repeater work into `Cirra.Input`:

- device discovery and capabilities;
- normalized intents: move, confirm, back, menu, details, page, search, guide, media;
- active-controller ownership and clean reassignment;
- analog dead zones, hysteresis, acceleration, repeat curves, diagonals, and drift diagnostics;
- duplicate-device suppression for mapper/XInput overlap;
- prompt family selection independent of action binding;
- a navigation scope/graph with initial focus, neighbors, restoration, and scroll-into-view;
- an Input Lab that writes tested per-device profiles.

Mouse, keyboard, controller, and touchpad-like pointer input should coexist without fighting over focus.

### 7. Desktop And Horizon Presentation

Keep WPF and .NET 10. Do not rewrite the rendering stack without measurements.

Extract application services and specialist view models from `MainViewModel`, `MainWindow.xaml.cs`, and `BigPictureWindow.xaml.cs`. The first objective is a clean boundary, not a second process. Once Horizon can consume only shared engine contracts, decide from startup, crash-isolation, and memory evidence whether a separate `Cirra.Horizon.exe` is worthwhile.

Both surfaces share:

- domain projections;
- commands and operation states;
- selection identity and return context;
- theme tokens, media services, launch/session state, and input intents.

They do not share screen composition, density, animation choreography, or navigation assumptions.

### 8. Theme And Sensory Platform

Create a versioned Cirra Theme API with separate Desktop and Horizon compositions.

Stable semantic tokens:

- canvas, atmospheric layer, reading surface, elevated surface, inset surface, selection, focus, danger, success, muted text, and media scrim;
- typography roles and density metrics;
- motion roles for enter, exit, focus, selection, shelf movement, loading, success, and error;
- sound roles for navigation, activation, back, launch, notification, and ambience;
- button-prompt families and reduced-motion/high-contrast variants.

Cirra-specific visual direction:

- each theme has a distinct atmosphere scene, material recipe, motion signature, and audio palette;
- cloud motion supports depth and orientation without moving reading surfaces;
- glass has bounded blur, contrast-aware tint, crisp focus edges, and performance fallbacks;
- Horizon uses large stage media and restrained overlays, with the next shelf always legible;
- cases, posters, lists, and details share selection identity and artwork continuity;
- themes may replace approved composition slots, but core accessibility, input, loading, error, and confirmation behavior remains guaranteed.

### 9. Cirra Extension SDK

Start narrower than Playnite and grow deliberately:

Phase A contracts:

- metadata and guide providers;
- library source/import providers;
- emulator discovery/readiness providers;
- launch actions;
- dashboard/search contributions;
- read-only game/library queries;
- settings and diagnostics.

Every extension declares an ID, semantic version, minimum/maximum API version, capabilities, network/file access needs, and migration behavior. Cirra records health, duration, exceptions, and last successful run. A failing extension can be disabled without blocking startup.

In-process extensions are trusted code, not a sandbox. Untrusted or broadly distributed integrations should move to a brokered out-of-process model before Cirra markets them as safe.

## Feature Upgrades

### Library

- Replace cleanup-after-scan with Import Review and a permanent Artifact Registry.
- Add duplicate explanations, merge/split, moved-file detection, and disconnected-drive states.
- Make filters composable, saveable, count-aware, and explainable.
- Preserve selection and scroll anchors across refresh, sort, filter, metadata updates, and spotlight rotation.

### Horizon

- Virtualize shelves and case grids with deterministic item sizes and resource recycling.
- Add controller-complete overlays for filters, sorting, search, launch repair, guides, Activity Center, and settings.
- Add a lightweight prewarm snapshot so entry can present useful cached content immediately.
- Give every media/trailer/background task cancellation, generation identity, and a hard memory budget.
- Treat attract mode, auto-rotation, screensaver, and ambient motion as non-owning presentation states that never steal selection.

### Metadata And Guides

- Add provider health, field provenance, match confidence, edition/region/language matching, locks, and conflict review.
- Add manual/guide covers, verified document types, offline shelf state, bookmarks, search/outlines where available, facing-page defaults, and preserved 1:1 pages.
- Never present a weak match, HTML error page, restricted loan, or unrelated sequel as a verified guide.

### Emulation

- Add an emulator/core catalog with detected, configured, compatible, tested, and broken states.
- Resolve platforms to ordered launch profiles, not one global executable path.
- Add BIOS/firmware evidence, core extension checks, per-game overrides, and a test-launch workflow.
- Preserve exact launch command evidence in support bundles with sensitive paths redacted.

### Operations And Recovery

- Route scan, metadata, guide, update, backup, migration, asset, and launch-preparation work through one operation contract.
- Add cancellation, progress phases, logs, retry, ownership, and truthful terminal state.
- Provide automatic pre-migration backups, selective restore, and a startup recovery path that does not require the main database to open.

## New Cirra Features Enabled By The Architecture

1. **Change Review:** one inbox for scan, metadata, moved-drive, artwork, and provider changes before they alter the library.
2. **Artifact Passport:** inspect why an item exists, where it came from, what launch profiles use it, and whether it has moved or changed.
3. **Edition Stack:** one title can hold several releases/installations without appearing as accidental duplicates.
4. **Launch Doctor:** runs the same readiness graph as real launch and offers the smallest repair action.
5. **Session Thread:** notes, guide bookmarks, achievements, backups, and return context around a game without becoming a social feed.
6. **Theme Studio:** live token/material/motion/audio previews across representative Desktop and Horizon fixtures.
7. **Couch Command Palette:** controller-first global search for games, actions, settings, collections, guides, and repair tools.
8. **Local Co-op Ready:** combines sourced player support, installed readiness, connected controllers, profile preferences, and time budget.
9. **Portable Library Report:** a human-readable, redacted health/support bundle with no game files or secrets.
10. **Provider Trust Dashboard:** source health, last success, latency, rate-limit state, and exact fields each provider may modify.

## Delivery Sequence

### Foundation 0: Measure And Freeze Contracts

- Capture current startup, scan, search, memory, Horizon entry, input, and launch baselines.
- Add 1k and 10k deterministic fixture libraries.
- Freeze JSON import/export fixtures and current launch behavior.
- Establish crash-safe operation and migration acceptance tests.

### Foundation 1: Indexed Library

- Add domain identities and the new data project.
- Build migration/dry-run/rollback.
- Move reads behind query services while preserving the existing UI.
- Exit only when old libraries migrate without loss and JSON export remains usable.

### Foundation 2: Trusted Import

- Add Artifact Registry, scan stages, exclusions, reconciliation, review, and transactional commit.
- Prove idempotence, overlapping roots, moved paths, disc groups, archives, and cancellation.
- Make duplicate regression fixtures release gates.

### Foundation 3: Metadata Provenance

- Add field wrappers, locks, provider capabilities, comparison, and batch rollback.
- Integrate guide/manual candidates into the same evidence model.

### Foundation 4: Launch And Input

- Introduce launch/session state machine and installation/launch-profile models.
- Complete normalized multi-controller input and navigation scopes.
- Convert Desktop and Horizon launch routes to the same service.

### Experience 5: Horizon Shell

- Extract Horizon view models and navigation graph.
- Virtualize shelves/cases, budget media, stabilize focus, and finish all controller-only workflows.
- Measure whether a separate executable improves reliability enough to justify it.

### Experience 6: Theme Platform

- Version semantic tokens and composition slots.
- Move each built-in theme to the contract.
- Add diagnostics, reduced motion, quality tiers, and Theme Studio.

### Ecosystem 7: Extension SDK

- Ship read-only/query, provider, import, and launch contracts first.
- Add compatibility validation, health reporting, safe mode, and extension disable/recovery.

### Assurance 8: Backup, Update, And Release

- Complete migration/update verification, restore selection, diagnostics export, and installed-build soak tests.
- Gate releases on large-library, controller, offline, recovery, and portable-package matrices.

## Performance And Quality Gates

| Concern | Gate |
| --- | --- |
| Rescan | Unchanged second scan commits zero additions and zero mutations |
| Duplicate identity | The same canonical artifact cannot exist twice; ambiguous title matches require review |
| Search/filter | 10k-title fixture updates within the `PREMIUM.md` p95 target after index readiness |
| Import safety | Cancel/crash before commit leaves the durable library unchanged |
| Horizon input | Controller-only browse, filter, details, guide, launch, error recovery, and return |
| Horizon rendering | Bounded live item/media counts while scrolling 10k-title fixtures |
| Selection | Background rotation and async completions never steal working selection |
| Metadata | Locked fields survive every automated refresh and migration |
| Recovery | Failed migration/update restores the last valid database and explains what happened |
| Themes | All built-in themes pass contrast, reduced-motion, DPI, minimum-size, and missing-media fixtures |

## Adopt, Adapt, Reject

**Adopt:** explicit engine boundaries, indexed data, imported-artifact history, field-level metadata, separate couch presentation, centralized input, versioned themes, extension contracts, backup/migration ownership, and virtualized fullscreen surfaces.

**Adapt:** implement these ideas with .NET 10, Cirra's existing services, Cirra's visual identity, modern async/cancellation, stronger provenance, and a staged migration that preserves current users.

**Reject:** copying layouts/assets/code, moving backward to .NET Framework, a big-bang rewrite, unrestricted scripts by default, theme freedom that can break accessibility or core behavior, silent metadata overwrite, and claims that all duplicates/controllers/manuals can be solved without ambiguity or hardware/source evidence.

## Immediate Implementation Slice

The highest-leverage next slice is Foundation 0 plus the first vertical part of Foundation 1:

1. Define `GameTitle`, `Release`, `Installation`, `LaunchProfile`, and `LibraryArtifact` identities beside the current models.
2. Add a new data project with schema versioning and a read-only migration dry run from `library.json`.
3. Create 1k/10k fixture generators and baseline startup/query/scan tests.
4. Persist an Artifact Registry for exact-path/file identity without changing the visible library yet.
5. Route one existing library query through the new projection service and measure it.

This creates the engine Cirra needs without destabilizing the current app or hiding a rewrite behind visual work.

## Implementation Log - October 19, 2026

**Foundation 0 and the first Foundation 1 vertical slice are delivered. The indexed store is deliberately a synchronized sidecar, not yet Cirra's sole source of truth.**

- Added explicit `GameTitle`, `GameRelease`, `GameInstallation`, `LaunchProfile`, and `LibraryArtifact` domain models.
- Added `CloudBerry.Data`, backed by SQLite with a versioned schema, WAL, foreign keys, indexes, and automatic backup before schema upgrades.
- Added deterministic JSON-to-index migration with dry-run reporting. Repeating an unchanged migration inserts nothing; changed records synchronize; deleted titles and unreferenced artifacts are removed in one transaction.
- Added an Artifact Registry with one canonical artifact row per normalized path and many installation links. Shared/duplicate launch paths remain explainable without duplicating the physical artifact identity.
- Kept `library.json` authoritative and portable during the transition. Startup and every successful save/history mutation reconcile the sidecar; an unavailable JSON source cannot wipe a populated index.
- Routed desktop text search through a cancellable, debounced indexed ID prefilter. The existing in-memory predicate still performs the final comparison and remains the fallback for non-ASCII queries, stale state, cancellation, or index failure.
- Expanded scan summaries with inspected file/folder counts, accepted candidates, collapsed duplicates/companions, and unreadable entries.
- Added deterministic migration coverage for shared artifacts, idempotence, update/delete synchronization, rollback, schema upgrade/backup, literal wildcard search, more than 1,000 matches, and a 10,000-title fixture.
- Explicitly upgraded the SQLite native bundle after the first restore exposed a high-severity advisory. The complete solution now reports no vulnerable direct or transitive packages from the configured sources.
- Verified a Release, self-contained, ReadyToRun, compressed single-file `win-x64` publish containing the new data engine.

**Verification at the end of the first slice:** 114 application tests and 160 core/data tests passed (274 total), with zero build warnings or errors. The 10,000-title fixture migrated and queried inside the one-minute safety ceiling; this is a regression guard, not the final reference-hardware performance claim.

## Implementation Log - v0.4.0 Platform Update

The second vertical slice is implemented in source and automated tests:

- All scan entry points batch candidates through the persistent Artifact Registry without SQLite parameter-limit failures. Exact artifacts are blocked; probable moved/copied files are left unchecked with an explicit reason for review.
- Launch artifacts now carry a bounded first/last-content fingerprint. Schema v3 added the fingerprint index and updates availability, size, timestamp, and hash evidence on reconciliation.
- Scan and preview work is represented in Activity Center with cancellation and truthful inspected/kept/merged outcomes.
- Import commit rechecks persistent identity immediately before mutation. Failed durable writes restore both the in-memory library and removed review rows.
- Metadata fields now retain source, confidence, acquisition time, user-lock state, and revision. Manual edits automatically lock only the affected field; provider refreshes continue around it. Schema v4 mirrors this evidence into the indexed store.
- Desktop and Horizon launch through one ordered orchestrator covering checking, repair-required, preparation, start, tracking, return, completion, cancellation, and failure.
- Horizon hero, detail, and attract artwork share a 192 MB decoded-image cache. Source reads are cancellable and capped at 64 MB, stale generations cannot replace current art, and failed loads preserve the visible hero.
- The background JSON-to-index queue coalesces rapid saves, never blocks startup, preserves a populated index when the JSON source is unavailable, and drains during clean shutdown.

**Current verification:** 108 application tests and 167 core/data tests pass (275 total), including 1,501-artifact reconciliation, moved-content detection, schema backup/upgrade, metadata-lock round trips, launch state ordering, bounded image-cache reuse, cancellation rollback, and a deterministic 10,000-title library. Eleven obsolete access-gate tests were removed with the gate in v0.4.1. The solution builds with zero warnings and zero errors.

**Release boundary:** `library.json` remains the authoritative portable store in v0.4.0; SQLite is the synchronized index and evidence registry. Database-first writes, field-by-field conflict comparison, and an external extension SDK remain roadmap work and are not represented as complete in this build.

## v0.4.1 Access Update

- Removed the startup invitation gate, local hash allowlist, DPAPI activation marker handling, access window, and gate-only tests.
- Cirra now proceeds directly through its normal splash and startup pipeline for every user.
- Updated the closed-beta playbook and tester documents to use private distribution rather than client-side activation.

## Research Evidence

Playnite findings and exact evidence IDs are in [PLAYNITE_REA_REPORT.md](PLAYNITE_REA_REPORT.md). The retained combined evidence bundle is `C:\Users\conno\AppData\Local\Temp\cirra-competitor-rea-evidence-2026-10-19.json`. The prior LaunchBox bundle is `C:\Users\conno\AppData\Local\Temp\launchbox-phase1-rea-evidence.json`.
