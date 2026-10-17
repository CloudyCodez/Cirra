# Cirra: Aurora V2

**Working product name:** **Cirra**

Cirra is short, cloud-derived (from cirrus), easy to say, and does not lock the product into one launcher, store, or platform. It keeps the atmospheric feeling of the original product while sounding like a focused library app rather than a utility suite. Cirra is now the working product identity in the application; trademark, domain, and store-name clearance remain release gates.

> **Purpose:** Aurora V2 is the active plan after the 2026-09-03 repository audit. It turns the implemented Aurora foundation into a dependable daily-use product, then adds capabilities that improve discovery, setup, accessibility, and confidence for every kind of player.

## September 8: Reader And Case Reliability

- [x] Serialize native PDF work and disposal; bound the rendered-page cache to six pages.
- [x] Guard overlapping page turns and handle page-render failures inside the reader.
- [x] Correct cover-first spread navigation, including the final facing-page pair.
- [x] Detect wide scanned PDFs and display one original PDF page at a time; offer a facing-pages toggle without reflowing the source.
- [x] Verify a real 11-page Nintendo manual through opening, layout switching, and last-spread navigation in an offscreen WPF reader.
- [x] Preserve Horizon case geometry and cover materials on focus changes instead of reloading artwork; retain platform-specific aspect ratios.
- [ ] Complete visual verification and physical detailing of the new 3D case models across all packaging profiles.
- [ ] Broaden guide discovery coverage and validate additional live sources; this reader pass does not establish that all discovered PDFs are usable.
- [ ] Add reader zoom, saved reading position, and controller-specific navigation verification.

## September 8: Manual Discovery Coverage

- [x] Add official Nintendo DS and 3DS manual indexes alongside N64, accepting Nintendo-owned CDN links and accent-insensitive exact titles.
- [x] Retrieve Steam publisher manuals for PC games with an existing Steam ID; require the matching store title and an actual manual link.
- [x] Search Archive queries with bounded concurrency and include software/data-hosted manual collections.
- [x] Return multiple matching documents from an archive item, including manuals and strategy guides.
- [x] Reject wrong multi-digit editions, ROM/APK download advertisements, restricted files, and PDFs whose declared size exceeds the download limit.
- [x] Download, cache, open, and render the first page before attaching a guide. Failed downloads remain unattached with an actionable error.
- [x] Add direct page navigation to the reader, preserving facing-page parity and text-field caret navigation.
- Verification: live downloads succeeded for Alpha Sapphire's official manual and 122 MB strategy guide, Tomodachi Life's official manual, and Civilization V's Steam manual. The Pokemon strategy guide opened as a 484-page document in Cirra's renderer. Rendering the first page is not a guarantee that every page in a third-party PDF is intact.
- Sources: [Nintendo 3DS index](https://www.nintendo.com/en-gb/Support/Legacy-system/Nintendo-3DS-games-electronic-manuals-850636.html), [Nintendo DS index](https://www.nintendo.com/en-gb/Support/Legacy-system/Nintendo-DS-games-manuals-1006833.html), [Civilization V Steam manual](https://store.steampowered.com/manual/8930).
- [ ] Expand verified publisher indexes and regional title aliases; retain explicit no-match results where no usable document exists.
- [ ] Continue the broader Horizon/dashboard polish with visual regression checks, readable dense layouts, and controller ergonomics.

## September 8: Controller And Case Follow-up

- [x] Center the Horizon case camera on the model, integrate platform labels into the physical header, and preserve source artwork proportions in UV mapping.
- [x] Verify centered Switch, DS, and PlayStation 2 cases in an offscreen WPF rendering harness.
- [x] Route Home/Library shelf controller directions to visible card selection, including movement between shelves and bringing selected cards into view.
- [x] Give stick and D-pad input a shared time-based hold-repeat model, a lower stick threshold, and dominant-axis diagonal handling.
- [x] Allow directional focus movement in the detail overlay and activate the focused control with A.
- [ ] Verify on physical controllers; XInput directional mapping is covered by automated tests but hardware behavior is not yet confirmed.

## September 9: Startup And Horizon Entry

- [x] Unify startup and Horizon entry around a restrained branded splash with stable status layout, original icon, theme atmosphere, and reduced-motion support.
- [x] Coalesce startup progress updates, reject non-finite values, and prevent progress from moving backward. Remove text reanimation on every status update.
- [x] Reduce the artificial startup minimum from 1.35 seconds to 300 ms and wait for the main window's first rendered frame before revealing it.
- [x] Add a dedicated Horizon splash with real preparation stages and indeterminate activity rather than invented percentages.
- [x] Keep Cirra visible until Horizon renders; guard repeated entry requests, handle early closure and timeout, and recover to Cirra on failure.
- [x] Remove hidden Horizon construction immediately after desktop startup and after returning from Horizon.
- [x] Visually inspect the Horizon splash in an offscreen WPF harness; add progress and first-frame lifecycle regression tests.
- [ ] Profile cold startup on large real libraries and low-end GPUs; a first rendered frame does not mean every remote artwork download has completed.

## September 9: Handoff And Selection Regression Fixes

- [x] Fix Horizon entry ownership: show the destination before transferring the loading window's Owner, then await its first rendered frame.
- [x] Add an offscreen three-window regression covering desktop, loading screen, and previously unshown destination ownership.
- [x] Preserve existing and deliberate game selections against hero auto-rotation. Selection remains pinned until cleared, including across idle intervals.
- [x] Suspend hero rotation while inactive, outside Home, in a detail overlay, during gameplay/attract mode, over a game card, or within 12 seconds of input.
- Verification: 78 app tests and 134 core tests passed. Full-library and physical-controller checks remain separate from these focused regression tests.

## Reality Check

Aurora already has a serious base: library scanning, backups and restore, metadata providers, achievements, duplicate ownership handling, filters, Big Picture, emulation discovery, and an embedded RetroArch path. The important unfinished work is not a lack of screens. It is confidence: proving that a game will launch, explaining failures plainly, making couch use genuinely reliable, and shipping a build people can trust.

## V2 Principles

- **Make the next action obvious.** A new user should never need to learn the app before it is useful.
- **Show confidence, not mystery.** Imports, metadata, emulators, saves, and launches must say what is known, what is missing, and what will happen next.
- **Keep ownership local.** The library remains usable offline and exportable; optional sharing never becomes a requirement.
- **Respect different play styles.** Mouse, keyboard, controller, large libraries, small libraries, desktop games, and ROM collections all deserve first-class paths.
- **Earn release confidence.** A feature is not done merely because code exists; it needs tests, observable behavior, and a recovery path.

## Premium Experience Foundation

This first Aurora V2 implementation pass is complete at the code level:

- [x] Adopt **Cirra** across the executable, window titles, shell copy, update UI, publishing output, and fresh-install metadata.
- [x] Rename the couch interface to **Horizon** while preserving internal compatibility identifiers.
- [x] Replace the inherited default aesthetic with **Cirra Core**: obsidian surfaces, ice-light accents, restrained champagne highlights, modern typography, and tighter geometry.
- [x] Give all nine shipped themes independent palettes, material language, atmospheric rendering, motion timing, and procedural sound signatures.
- [x] Replace stock WPF fallbacks with a shared premium control layer for buttons, fields, selectors, context menus, sliders, expanders, checkboxes, progress, focus, scrolling, and tooltips.
- [x] Remove theme-fixed color islands from the main shell and Horizon, and add Cirra atmosphere to pre-launch surfaces.
- [x] Migrate legacy saved theme names to their Cirra equivalents without resetting user preferences.
- [x] Preserve the existing local data directory and internal namespaces so upgrades retain libraries, artwork caches, access state, and backups.
- [x] Replace the legacy icon with the approved Cirra crescent, including a full-bleed 1024 px source, 256 px WPF resource, verified 16-256 px Windows ICO, executable metadata, installer, splash, access gate, and window chrome.
- [x] Complete the first hierarchy and de-cluttering pass: compact sidebar identity, remove duplicate shell labels and status summaries, reserve top navigation for Focus Mode, and stop static panels from behaving like buttons.
- [x] Rebalance Verdant Glass around neutral work surfaces, readable text, restrained mint atmosphere, and warm highlight detail; fix theme-switch token leakage that could leave labels with the previous theme's contrast.
- [x] Modernize the highest-density pages: wider Settings and Friends workspaces, simplified social identity, full-width achievement rows, calmer three/four-column emulator setup, and a reduced Home overview.
- [x] Complete the Horizon interaction-modernization pass: theme-aware overlay scrollbars with generous hit areas and expanding hover/drag states, touch panning, retained inertial wheel and shelf motion, a restrained directional scroll pulse, glass quiet-state buttons with low-opacity light sweeps, and distinct command/glass/inset surface tiers across Home, Library, Achievements, Settings, Emulation, Theatre, and game details.
- [x] Refine Horizon's vertical physics after real-library testing: faster but controlled wheel impulses, custom inertial decay, empty-surface click-and-drag panning, drag-release momentum, interactive-control protection, and independent horizontal shelf gestures without jumpy axis competition.
- [x] Complete the Horizon library visual sweep: add a metadata-backed cover grid with angled case faces, shaded spines, reflective edges, focus lift/straightening, wrapped keyboard and controller navigation, shelf/grid switching, compact category controls, and a unified liquid-glass treatment for navigation, commands, hero surfaces, filters, and sort controls.
- [x] Add the global Cirra Command Center: a themed glass palette with live game-cover search, exact library reveal, page navigation, maintenance actions, Horizon launch, theme switching, keyboard traversal, focus restoration, title-bar access, and theme-specific motion and sound feedback.
- [x] Add private per-game **Next Time** markers with timestamped persistence, Time Machine history, desktop editing, Home resume context, Horizon presentation, and Command Center indexing.
- [x] Add the first **Cirra Guide Library** workflow: guide/manual metadata contracts, persisted per-game provenance, local PDF attachment, multi-query Internet Archive discovery, exact physical-case manual discovery through Games Database, title/platform relevance scoring, document-type labels, restricted/derived/wrong-platform rejection, duplicate protection, desktop management, immersive Horizon access, and a Cirra-owned WebView2 reader that preserves original PDF pages without conversion or reflow.
- [x] Add a cohesive motion and loading pass: animated startup progress copy, a simultaneous splash-to-shell crossfade/reveal, directional theme-tuned desktop and Horizon page transitions, reduced-effects safeguards, and one readable glass activity capsule in place of competing anonymous loading bars.
- [x] Unify the launch handoff across desktop and Horizon: every Play route now enters the same artwork-backed preparing state, reports launch-readiness failures without a false success cue, respects reduced-effects and high-contrast settings, and releases input cleanly after launch.
- [x] Add **Play Window** to desktop Home and Horizon: choose 15 minutes, 30 minutes, an hour, or open-ended; Cirra ranks installed games using median recent-session length, resume state, Next Time context, favorites, and achievement progress, then explains the strongest match in plain language.
- [x] Add controller-aware Horizon guidance and broader couch parity: the compact action legend appears only while a controller is connected, updates by page, exposes bumper navigation and contextual actions, and supports directional focus plus confirm across Achievements, Settings, Emulation, and Theatre.
- [x] Rebuild the shared desktop/Horizon theme renderer around cached, frozen compositions: cirrus ribbons, observatory orbits, mint contours, signal tracks, arcade light rails, archive etching, tidal caustics, and noir facets. High Contrast has no ambient artwork. Remove competing legacy rain, sweep, scanline, and decorative blob layers.
- [x] Rework all nine shared material palettes with readable glass controls, restrained borders, neutral shadows, and distinct secondary accents. Profile cards adapt to the active shell's luminance while retaining their saved accent; primary buttons have a clearer accent edge.
- [x] Bound ambient animation to 30 fps Full / 15 fps Efficient, reduce composition layers in Efficient, keep navigation chrome static, pause scene time while hidden/minimized, and respect disabled effects and system animation preferences. Hardware frame-time and battery measurements remain release QA.
- [x] Increase visible ambient drift with theme-specific travel paths and wider observatory/tidal rotation, retaining static navigation chrome and existing reduced-motion safeguards.
- [x] Fix desktop Library title starvation: default rail width 340 px (minimum 280), full-width wrapping titles, platform/source metadata underneath, compact status indicators, and stable selected-row typography with theme-safe contrast.
- [x] Harden guide opening: bounded/cancellable PDF downloads, header/end-marker checks, atomic local cache, offline reuse, writable per-user WebView2 storage, explicit errors, retry, and reader disposal on close. Local PDFs are checked without network access; original pages are not converted or reflowed.
- [x] Expand accurate guide discovery with Nintendo's official N64 manual index, console-wide Archive manual-pack filename matching, punctuation-tolerant queries, sequel checks, boolean private/nodownload rejection, isolated document failures, and honest partial-source status. Discovery matches are no longer called verified before their downloads are checked.
- [ ] Add per-theme audio controls for interface sounds, launch sounds, ambience, output device, and a global mute hotkey.
- [ ] Add an effects-quality selector (`Off`, `Efficient`, `Full`) with automatic fallback for battery saver, remote desktop, and low frame rate. **Selector and runtime quality scaling are implemented; OS power/remote-session detection and frame-time fallback remain.**
- [ ] Run contrast, focus, reduced-motion, text-scale, and high-DPI visual QA across every theme and every primary window.
- [ ] Add adaptive density (`Comfortable`, `Compact`, `Couch`) so 13-inch laptops, ultrawides, touch users, and large libraries get intentional spacing rather than one universal layout.

## Active Implementation Status

Started on 2026-09-03. A roadmap item stays open until its complete workflow, recovery behavior, UI, and tests are present.

- [ ] **Library Time Machine - in progress:** bounded local before/after journal, meaningful game-level diffs, serialized writes, selected-entry restore, restore confirmation, broad mutation coverage, and timeline UI are implemented. Undo creates its own reversible entry. Remaining: field-level previews, retention controls, disk-budget reporting, and restore performance telemetry for very large libraries.
- [ ] **Launch confidence - in progress:** native files, launcher URIs, ROM paths, emulator assignments, emulator executables, RetroArch core DLLs, core-declared content extensions, and install-state warnings now produce reusable preflight results. Library Play shows the result and blocks known-bad launches. Remaining: BIOS validation, repair routing, full Horizon presentation, caching, and performance telemetry.
- [ ] **Activity Center - in progress:** a global operation window, retained status log, progress reporting, cancellation tokens, and success/failure/cancel states are implemented. Metadata batches and Save Continuity discovery/backups/restores are connected. Remaining: scans, achievement sync, updates, history restores, retry dispatch, and persistence across restarts.
- [ ] **Device Profiles and Input Lab - in progress:** additive desktop/handheld/living-room profiles now keep theme, density, effects quality/intensity, audio, and startup view per device; Settings can create, capture, and activate them. Emulator override storage is reserved. Input Lab now polls all four XInput slots in a read-only live window, reports buttons/triggers/sticks/disconnects, and exposes an adjustable quiet-zone threshold for drift checks. Remaining: mapping capture, per-controller identity, haptic tests, calibration persistence, output devices, device auto-detection, and Horizon-specific preferences.
- [ ] **Save Continuity - in progress:** games can store explicit save roots; the editor discovers and SHA-256 fingerprints files, creates portable verified manifests, lists backups, rejects path traversal/tampering, restores the newest healthy backup, and preserves replaced live files in a rollback copy. Remaining: provider-based automatic discovery, a visual per-game timeline, conflict comparison, scheduled/exit backups, emulator state thumbnails, encryption, and remote destinations.
- [ ] **Return-to-game context - in progress:** private Next Time markers now persist per game, participate in Time Machine, appear on Home and in Horizon details, and are searchable by their text in the Command Center. Remaining: session-attached notes, newest-save context, pause/finish decisions, screenshot capture, and an in-game quick-entry overlay.
- [ ] **Guide Library - in progress:** original case manuals are resolved through Games Database while manuals, strategy guides, guidebooks, quick-reference cards, and digital manuals are independently searched across targeted Internet Archive collections and broader metadata queries. Results are scored against title and platform, labeled by document kind and source, and screened for restricted files, derivative OCR PDFs, unrelated one-word matches, and wrong-console editions. Attached documents keep provenance and open in desktop or immersive Horizon readers with native page and zoom controls. Remaining: bookmarks and reading position, optional offline pinning with integrity checks, two-page spread preference, OCR-assisted in-guide search, richer region/language labels, cover thumbnails, and controller-specific reader controls.
- [x] **Play Window:** desktop Home and Horizon now share a four-choice time budget, history-derived session estimates, ranked ready-to-play results, and an explicit reason for the lead recommendation. The future Tonight queue can build on this ranking without duplicating the decision engine.

### Latest Verification

- Full solution rebuild on 2026-09-05: **0 warnings, 0 errors**. The nullable-warning cleanup from the previous pass is verified.
- Core and App test projects on 2026-09-05: **187 passed, 0 failed** (127 Core + 60 App). Coverage includes all nine themes and 81 profile/shell combinations; guide download/cache/error handling, restricted files, wrong sequels, real-world non-game false positives, manual packs, and the shipped Library row template at 280/340/540 px.
- Guide live checks: Super Mario 64 PDFs downloaded successfully from Nintendo Support, Games Database, and Internet Archive; Tag Force 2 PDFs downloaded from Games Database and Archive's PSP manual pack. The actual Cirra reader rendered the Nintendo manual (page 1 of 11), with a captured preview at `tmp_probe/reader-proof.png`. Tag Force 3 and Portal 2 currently return no matching public PDFs in the tested sources; broader coverage remains open, and unrelated documents are not substituted.
- Theme rendering follow-through: corrected Archive Ivory's dark sidebar/title-bar background and marginal hover-text contrast. The nine-theme material/effect contact sheet was generated and visually inspected (`tmp_probe/theme-gallery.png`; regenerate by setting `CLOUDBERRY_THEME_GALLERY` to an output PNG path before running App tests). This is a renderer/material preview, not a screenshot of live application pages. Full-window focus, high-DPI, text scaling, and interactive motion QA remain open.
- Desktop smoke launch: the Debug `Cirra.exe` process opened and remained responsive. Pixel-level screenshot QA for the new panels is still required because native app capture was unavailable in this coding session.

## V2.0: Trust The Library

**Goal:** a first-time user can import games, understand the result, fix a problem, and launch something without needing a manual.

- [x] Restore both test projects, fix the RetroAchievements score regression and renamed backup assertion, then record a clean `dotnet test CloudBerry.slnx --no-restore --no-build` run: **91 passed, 0 failed** on 2026-09-03.
- [ ] Add a deterministic first-run walkthrough that can be skipped and resumed: choose sources, scan, review, add metadata later, then launch.
- [ ] Build a Library Health Center that unifies missing files, duplicate candidates, metadata gaps, disconnected sources, and backup status into prioritized, reversible actions.
- [ ] Add per-game source provenance: display where the game record, artwork, and metadata came from, with a link to refresh or change each source.
- [ ] Add a launch preflight panel: game file exists, launcher is available, emulator/core is ready when relevant, and the exact recovery action for each failed check.
- [ ] Add session notes and a lightweight "next time" marker per game, useful for long RPGs, co-op groups, and interrupted sessions. **The timestamped per-game Next Time workflow is complete across desktop, Home, Horizon, Command Center search, persistence, and Time Machine; session-attached notes remain.**
- [x] Add an optional time-budget chooser on Home: "15 min", "30 min", "an hour", and "open-ended" ranks suitable games using play history, remaining achievements, and session length. Implemented as Play Window on desktop and in Horizon, with explainable ranking and direct launch.
- [x] Add a global command palette for finding games, changing pages, running maintenance actions, switching themes, and opening settings without hunting through menus. The Cirra Command Center is available from the title bar or `Ctrl+K`, supports cover-backed live search, and restores the previous focus when dismissed.
- [ ] Add a unified activity center for scans, metadata, achievements, backups, and updates with progress, cancellation, results, and a readable recent-history log.

**Done when:** a fresh library can be imported, checked, repaired, and launched using only in-app guidance.

## V2.1: Emulation That Explains Itself

**Goal:** setting up a ROM feels like pairing a controller, not assembling a command line.

- [ ] Add setup confidence badges on every platform card: `Ready`, `Needs Emulator`, `Needs Core`, `Custom Path`, `Not Checked`, and `Broken Path`.
- [ ] Add a non-launching ROM readiness test that validates the ROM, emulator executable, RetroArch core, and launch template; show the result in plain language.
- [ ] Add a per-platform setup checklist with one-click fixes where Cirra has a detected emulator or core.
- [ ] Turn RetroArch detection into a readable summary: found executable, core directory, usable cores, missing expected cores, and the selected default.
- [ ] Add actionable launch-failure explanations that distinguish missing ROMs, missing executables, unavailable cores, invalid templates, access errors, and process failures.
- [ ] Add platform recipes with recommendations and import rules, while keeping custom paths and command templates for advanced users.
- [ ] Detect ROM duplicates, revisions, and region variants; let the player group them as one title without deleting anything.
- [ ] Build the save-state browser: detect states, group by game, show thumbnails/date, resume safely, and expose the latest save in game details and Horizon.

**Done when:** a casual user can set up a detected system and get a trustworthy readiness result before they press Play.

## V2.2: A Library That Knows Your Context

**Goal:** help people decide what to play without turning the library into a noisy recommendation feed.

- [ ] Add a "Tonight" queue that combines explicit picks, unfinished games, friends' local overlap, and a chosen time budget. It must always explain why each game appears.
- [ ] Add a quiet return-to-game flow: resume last session, show the last save or note, and offer a one-click "mark finished / pause for later" decision.
- [ ] Add collection rules that are inspectable and composable: platform, play state, launch readiness, achievement progress, installed size, session length, and custom tags.
- [ ] Add a bulk metadata conflict review that compares source values field by field, keeps user edits protected, and supports per-field choices.
- [ ] Finish manual artwork control with image URL paste, local file, web result, preview, crop/focal-point selection, and clear attribution/provenance.
- [x] Add original-format game guides and manuals: explicit archival discovery, local PDF attachment, source provenance, a no-reflow Cirra reader, and Horizon access. Advanced reading-state and offline features continue under the Guide Library milestone.
- [ ] Add optional storage insight: installed size, duplicate installs, unused launchers, and a reversible "free space" action list. Never delete files automatically.
- [ ] Add a personal privacy dashboard describing local data, network-enabled features, cached artwork, export locations, and a one-click data export.

**Done when:** users can choose a game, understand its state, and curate a growing library without losing control of their data.

## V2.3: Horizon For The Couch

**Goal:** Horizon is fully usable with a controller and remains comfortable in a living room.

- [ ] Audit every screen for keyboard and controller focus order; preserve focus when overlays open and close.
- [x] Add a controller-ready Library cover grid with wrapped spatial navigation, selected-game background/hero updates, metadata-backed angled cases, direct detail opening, and instant shelf/grid switching.
- [x] Rework Horizon's primary visual hierarchy around compact filters, consistent focus treatment, quieter navigation, layered glass surfaces, and theme-reactive highlights without changing the placement of critical controls between themes.
- [ ] Add visible, consistent focus rings and a controller action legend for unfamiliar actions. **The connection-aware contextual action legend and generic directional focus/confirm path are implemented; a full per-control focus-ring audit remains.**
- [ ] Add reduced-motion, high-contrast, large-text, and subtitle-friendly presentation settings.
- [ ] Remove or soften effects that reduce readability; verify contrast in every shipped theme.
- [ ] Make the library, achievements, emulator setup, save-state picker, and launch handoff controller-complete. **All current Play routes share the controller-safe launch handoff; setup workflows, save-state picking, and failure-repair focus still need full controller completion.**
- [ ] Validate the embedded RetroArch path with real games: attach, resize, exit, crash recovery, and normal external fallback.
- [ ] Add an optional idle showcase mode using the user's own library art, disabled by default and respectful of reduced-motion settings.

**Done when:** a player can operate Horizon from the couch without reaching for a mouse.

## V2.4: Share Without Surveillance

**Goal:** useful social features that remain local-first and understandable.

- [ ] Validate, version, and explain Cloud Code payloads before import.
- [ ] Add a local library-overlap view that can answer "what can we both play?" without uploading a full library.
- [ ] Add friend cards with visible privacy scope and a clear import/export path.
- [ ] Add shareable, opt-in stat cards for Wrapped, current library milestones, and achievement moments.
- [ ] Add copy-ready summaries for Discord and social posts, with a preview and no forced account connection.

**Done when:** sharing is useful, deliberate, and never required for the core product.

## V2.5: Release Candidate

**Goal:** Cirra can be installed and trusted outside its development environment.

- [ ] Measure startup, search, scrolling, import, metadata fill, and shutdown with 500+ games and large ROM folders.
- [ ] Keep avoidable network work off the startup path; prove offline startup and flaky-provider recovery.
- [ ] Add progress, cancellation, and resumable results to every long-running operation.
- [x] Eliminate solution build warnings without suppression: verified full rebuild with zero warnings and zero errors on 2026-09-05.
- [ ] Complete regression coverage for launch preflight, emulator confidence, save-state discovery, and metadata provenance.
- [ ] Verify app version, installer metadata, update feed, previous-build upgrades, portable build, self-contained build, and clean uninstall behavior.
- [ ] Smoke-test a fresh installed app and an existing-library upgrade on a non-development machine.
- [ ] Publish a concise changelog, recovery guide, privacy explanation, and known-limitations page.

**Done when:** the release candidate passes automated checks and a human can install, import, launch, update, recover, and uninstall it cleanly.

## Product Innovation Backlog

LaunchBox already covers broad importing, media, emulator management, playlists, editing, themes, and a ten-foot interface. Playnite likewise covers desktop/fullscreen libraries, emulation, themes, extensions, scripting, statistics, and dense filtering. Cirra should not win by reproducing those feature lists with different chrome. It should make a large mixed library feel safer, calmer, more personal, and easier to resume across real devices.

### New Cirra Capabilities

| Priority | Capability | Meaningful user outcome | Target |
| --- | --- | --- | --- |
| P0 | **Library Time Machine** | Every import, merge, removal, metadata edit, artwork change, and repair produces a readable before/after entry that can be undone without restoring the entire database. | V2.0 |
| P0 | **Save Continuity** | Discover supported PC saves and emulator states, verify backups, show a per-game timeline, warn about conflicts, and resume the newest valid state from desktop or Horizon. | V2.1 |
| P0 | **Device Profiles** | A desktop, handheld, and living-room PC can keep different density, effects quality, audio, controller mappings, launch arguments, and emulator choices while sharing one library model. | V2.1 |
| P0 | **Input Lab** | Test buttons, sticks, triggers, dead zones, disconnect recovery, and haptics before launch; store per-game remaps and show exactly which controller caused a failed readiness check. | V2.1 |
| P1 | **Local Co-op Finder** | Ask what can be played with the controllers and people present, then filter by local players, shared/split screen, estimated session length, and input readiness. | V2.2 |
| P1 | **Installation Profiles** | Keep vanilla, modded, beta, VR, and alternate-executable setups under one title with distinct launch rules, saves, notes, artwork badges, and a remembered default. | V2.2 |
| P1 | **Session Canvas** | A lightweight in-game hotkey records a screenshot, quick note, objective, or "next time" marker and attaches it to the current session without forcing a separate journal workflow. | V2.2 |
| P1 | **Household Profiles** | Local users get independent play history, ratings, accessibility, themes, hidden titles, queues, and achievement presentation without requiring online accounts. | V2.2 |
| P1 | **Play Capsule** | Export a portable, inspectable bundle of Cirra settings, metadata choices, notes, and permitted saves for another PC; game files are never silently included. | V2.4 |
| P2 | **Accessibility Studio** | Preview text scale, contrast, reduced motion, color-vision filters, focus treatment, narration cues, and couch distance against real screens before applying them globally or per device. | V2.3 |
| P2 | **Automation Recipes** | Build permissioned, human-readable triggers such as "before launch, switch audio device" or "after exit, back up saves" with dry runs, timeouts, and failure history. | Post-V2 |
| P2 | **Change Watch** | Detect store installs, moved folders, new ROMs, changed executables, and disconnected drives in the background, then present a diff instead of silently rewriting the library. | Post-V2 |

### Existing Feature Reinventions

| Existing area | Enhancement or innovation | Success measure |
| --- | --- | --- |
| **Home** | Replace the static dashboard with an intent surface: resume, play for a chosen amount of time, continue a queue, host local co-op, or repair something. Every suggestion explains itself and can be dismissed. | A useful launch is reachable in two actions without turning Home into a feed. |
| **Search and filters** | Add fuzzy title/alias matching, typo tolerance, deterministic natural-language filters, reusable query chips, keyboard/controller parity, and inline explanations of active constraints. | A 1,000-title library filters without UI stalls or mystery exclusions. |
| **Imports** | Make every scan a staged dry run with additions, removals, duplicates, confidence, source changes, and exclusions shown before commit; retain a Time Machine entry afterward. | No scan can silently destroy curation. |
| **Metadata** | Track provenance and confidence per field, lock user edits, compare conflicting providers side by side, and auto-apply only high-confidence empty fields. | Refreshing metadata never overwrites a protected value. |
| **Artwork** | Add focal points, crop previews for every surface, minimum-quality checks, duplicate detection, source attribution, and per-theme contrast previews. | One approved asset set remains legible in Library, Home, Horizon, and share cards. |
| **Launch flow** | Turn Play into a confidence-aware handoff: validate dependencies, explain the exact failure, offer the smallest repair, remember successful fallbacks, and return with a session summary. | Common failures are understandable and recoverable without leaving Cirra. |
| **Emulation** | Evolve platform cards into guided recipes with BIOS/core/path tests, controller checks, game-specific overrides, region/revision grouping, and safe launch templates. | A supported platform can reach `Ready` without command-line knowledge. |
| **Achievements** | Normalize provider terminology, surface near-completion and rarity, explain sync state, show progress over time, and create optional goal queues without pressuring completionists. | Users can trust totals and understand why an unlock is missing. |
| **Collections** | Add nested, composable rules with live previews, exclusions, manual pins, custom sort layers, and a plain-language explanation of why each title belongs. | Dynamic collections stay predictable as the library changes. |
| **Library Health** | Rank issues by impact, group repeated causes, preview bulk repairs, preserve undo, and distinguish cosmetic gaps from launch blockers. | The user always knows which problem matters first. |
| **Horizon** | Preserve spatial focus, remember the last item per screen, expose controller legends only when useful, support every critical setup/repair path, and tune density for viewing distance. | Core library use never requires a mouse or hidden shortcut. |
| **Themes and sound** | Treat each theme as a complete sensory system with unique materials, motion grammar, particles, semantic audio, launch transitions, accessibility variants, and strict frame-time/contrast budgets. | Themes feel distinct without changing control placement or sacrificing readability. |
| **Friends and sharing** | Upgrade Cloud Codes with versioning, expiration, selective fields, preview-before-import, local overlap calculations, and revocable privacy scopes. | Social value works without uploading a complete library or creating an account. |
| **Backups** | Add scheduled incremental snapshots, integrity verification, restore previews, retention controls, encrypted destinations, and periodic test-restore reminders. | A backup is not marked healthy until Cirra has verified it can be read. |
| **Updates** | Use staged channels, signed packages, atomic apply, automatic rollback, a human-readable migration preview, and post-update health checks. | A failed update returns to the previous working build and data state. |
| **Playtime and sessions** | Add idle detection, editable session history, external-session reconciliation, privacy controls, and clear separation between active, paused, and estimated time. | Correcting one session never corrupts lifetime totals. |

### Competitive Research Basis

- [LaunchBox's official feature overview](https://www.launchbox-app.com/) lists unified imports, metadata/media, modern and retro libraries, and emulator support.
- [LaunchBox Premium](https://www.launchbox-app.com/premium) adds themes, controller automation, startup/shutdown screens, customization, and Big Box for televisions and cabinets.
- [Playnite's official feature documentation](https://api.playnite.link/docs/manual/features/featuresOverview.html) covers statistics, emulation, themes, extensions, and scripting.
- [Playnite Fullscreen Mode](https://api.playnite.link/docs/manual/gettingStarted/playniteFullscreenMode.html) establishes controller-oriented couch use as part of the current baseline.

The differentiators to protect first are **Library Time Machine**, **Save Continuity**, **Device Profiles**, **Input Lab**, and the confidence-aware launch flow. Together they make Cirra feel dependable before it tries to feel limitless.

## User Coverage

| Player | V2 improvements that matter |
| --- | --- |
| New to game libraries | Resumable first-run flow, health center, plain launch preflight |
| Casual player | Tonight queue, time budgets, return-to-game notes |
| Emulator user | Confidence badges, readiness test, platform recipes, save states |
| Couch/controller player | Focus audit, action legends, large text, controller-complete setup |
| Large-library curator | Provenance, composable rules, duplicate/revision grouping, storage insight |
| Privacy-minded player | Offline-first core, clear network controls, local overlap, full export |

## Deferred On Purpose

- Animated social video cards and broad account-based social systems.
- A full cloud-sync service before local backup, recovery, and privacy controls are release-grade.
- AI-generated metadata or recommendations that cannot explain their source or give the user a clear override.
- Additional visual themes until the nine shipped themes pass readability, performance, and controller-focus checks.

## Suggested Delivery Order

1. V2.0 trust and verification work.
2. V2.1 emulation readiness and save states.
3. V2.3 Horizon accessibility and controller completion.
4. V2.2 library intelligence and curation.
5. V2.4 sharing, then V2.5 release hardening in parallel with every milestone.
