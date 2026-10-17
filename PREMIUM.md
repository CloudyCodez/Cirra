# Cirra Premium Roadmap

**Status: Approved. P01 in progress; interaction, Horizon entry, guide activity, and controller-routing slices delivered.**
**Prepared: September 9, 2026. Updated: September 13, 2026.**

## The Product Standard

Cirra should make a mixed gaming collection feel effortless to own, beautiful to explore, and dependable to play. Desktop Cirra is the precise management workspace. Horizon is the immersive, controller-first dashboard. They share the same games, decisions, and recovery guarantees without forcing the same layout onto both experiences.

Premium is an outcome, not a quantity of glass, animation, or features. A selected game stays selected. A controller reconnects without a mouse. An interrupted download recovers. A case displays its complete cover. A theme remains readable after hours of use.

Surpassing other gaming dashboards is the ambition, not a verified competitive claim. We will judge progress through completed player journeys, measured responsiveness, visual review, and recovery tests.

This document defines the next delivery sequence. `CloudBerry_AuroraV2.md` remains the broader backlog and implementation history; `CIRRA_VISUAL_IDENTITY.md` remains the visual identity reference. Existing capabilities below are foundations to finish, not features to rebuild or relabel as new.

## Baseline And Gaps

The planning baseline was 78 app tests and 134 core tests. The current P01 implementation and beta access checks pass 109 app tests and 144 core tests (253 total). This is not proof that every device, game, theme, or installed build works; see the delivery evidence below.

| Area | Existing foundation | What the next passes must establish |
| --- | --- | --- |
| Startup and Horizon entry | Branded loading, first-frame wait, ownership regression fix | Complete cold/warm/failed/closed handoffs, responsive preparation, multi-monitor behavior |
| Selection and spotlight | Independent spotlight, preserved working selection, saved pause/resume preference | Provider-refresh, full navigation, and installed-product soak coverage |
| Controllers | XInput, directional repeat, shelf selection, Input Lab | Shared four-slot Horizon routing and visible active-player state are implemented; hardware matrix and full workflow navigation remain |
| Cases and artwork | Native 3D models, platform profiles, corrected framing and UV proportions | Accurate packaging families, complete-art rendering, async decode, bounded caches, real-cover gallery validation |
| Themes | Nine identities, shared materials, role-aware effects | Complete-screen contrast, hierarchy, focus, DPI, reduced-motion and frame-time review |
| Guides | Nintendo/Steam/archive discovery, checked attachment, offline cache, spreads, direct page jump | Discovery and attachment now report cancellable Activity Center operations; wider verified coverage, reader state, byte budgets, retry and dependency review remain |
| Activity Center | Operation records, progress, acknowledged cancellation with a stopping state | Remaining operation integration, retry routing, shutdown and restart recovery |
| Launch and saves | Preflight, emulator detection, save manifests, verified backups | BIOS/configuration diagnosis, guided repairs, save discovery, restore preview and session-return context |
| Curation | Command Center, filters, Time Machine, metadata workflows | Protected fields, explainable collections, grouped editions, reversible batch decisions |
| Profiles and social | Device profiles, local profile identity, Cloud Codes | Verified preference isolation, capability-aware co-op, explicit sharing scope |

## Non-Negotiables

- Preserve the approved Cirra icon and the distinctive identities of the nine themes. Do not add more themes before the current ones pass review.
- Artwork leads; controls support it. No foreground effects that obscure covers, titles, or focus rings.
- Use opaque reading surfaces where glass hurts clarity. Keep operational pages compact and predictable; reserve immersive composition for game presentation.
- Theme changes never relocate controls or alter navigation semantics. Density and accessibility choices are independent of theme.
- No unsolicited audio, forced ambience, focus-stealing rotation, or animation that delays an available action.
- No silent destructive repairs, metadata overwrites, save replacements, or background uploads.
- Offline use remains first-class. An unavailable provider is not an empty library or a successful search.
- Every long operation has an owner, an understandable state, a leave/close policy, and a recovery path.
- A pass is not complete because it compiles, has a screenshot, or adds a checkbox to a roadmap.

## Delivery Order

Approval starts P01, now in progress. P02-P10 remain unstarted as dedicated passes; earlier foundations remain in place. Approval does not authorize publishing, uploading private data, deleting library files, or changing unrelated work.

| Pass | Priority | Focus | Depends on |
| --- | --- | --- | --- |
| P01 | P0 | Interaction, loading and lifecycle reliability | Approved; in progress |
| P02 | P0 | Controller-complete Horizon | P01 |
| P03 | P0 | Artwork, physical cases and large-library rendering | P01 |
| P04 | P1 | Complete-screen visual and sensory polish | P02, P03 |
| P05 | P1 | Guide Library as a finished reading product | P01, P02 foundations |
| P06 | P1 | Launch confidence, saves and return-to-game | P01, P02 |
| P07 | P1 | Curation, provenance and reversible changes | P01, P03 |
| P08 | P1 | Personal dashboard and Tonight queue | P04, P06, P07 |
| P09 | P2 | Household and local co-op experiences | P02, P06, P08 |
| P10 | P0 release gate | Installed-product assurance | Continuous; final gate after approved scope |

P0 defects discovered in any later pass interrupt feature expansion. Release assurance runs throughout, not only at the end. Each pass can be divided into small reviewed changes; do not turn this into one application-wide rewrite.

## P01 - Reliable Interaction And Loading

**Outcome:** Cirra always knows which window, operation, and selection the player owns.

- [x] Model desktop-to-Horizon entry as explicit preparing, presenting, ready, failed, and cancelled states. Cover repeat clicks, hotkeys, early close, shutdown, and retry.
- [ ] Measure time to first frame separately from time to usable library and time to artwork readiness. Remove avoidable synchronous work from the UI thread using the existing service boundaries.
- [x] Separate browsing selection, focused control, hero spotlight, and attract-mode selection. Automatic presentation must never write the user's working selection.
- [x] Preserve the current selection-pinning fix until the independent spotlight is complete. Add a clear spotlight pause/resume setting rather than leaving rotation silently disabled forever after selection.
- [x] Establish cancellation semantics: requested, stopping, cancelled after acknowledgement. Do not announce completion while a worker is still writing or cleaning up.
- [x] Connect guide discovery and attachment to Activity Center with progress, acknowledged cancellation, and truthful results.
- [ ] Connect the remaining long operations to Activity Center with retry and shutdown behavior. Resume only operations whose durable state supports it.
- [ ] Audit window ownership, event subscriptions, timers, native PDF work, WebView2 lifetime, and duplicate operation starts. Remove obsolete loading paths once replacements are proven.

**Acceptance:** Twenty repeated enter/exit cycles leave no accumulating windows, timers, listeners, or helper processes. Closing during each startup stage recovers without an orphaned splash. A selected card survives multiple spotlight cycles, provider updates, and overlay round trips. Cancellation cannot mutate the library after the UI reports that cancellation has completed.

**Evidence:** Offscreen lifecycle tests plus real desktop/Horizon smoke runs, injected failures, operation logs, and before/after resource measurements.

### P01 Delivery Log - September 9, 2026

**Slice 1 delivered: selection ownership and cancellation protocol. P01 is not complete.**

- Automatic Horizon spotlight updates no longer assign `SelectedGame`, clear guide results, or recalculate the selected game's launch preflight. Explicit selection restores that game's hero, including reselecting the same card. Opening Horizon or changing its page resets only the spotlight.
- Added a saved pause/resume icon control with an accessible name and state-specific tooltip. Rotation waits during hero interaction, detail overlays, launch handoffs, attract mode, and active navigation. Home controller confirm respects the focused card/button; toggles actually toggle, and Enter no longer launches a game from a focused button.
- Activity Center uses `Cancelling` / `Stopping` until the worker acknowledges completion after cleanup. Repeated cancellation is rejected, active cleanup is retained in history, cancellation callbacks cannot race source disposal, and a worker that already committed can report the truthful successful outcome. Invalid numeric progress becomes indeterminate.
- Corrected the Horizon search placeholder's icon-font fallback and kept the new control within the existing compact hero layout.

**Verification:** `dotnet test CloudBerry.slnx --no-restore -m:1 -nr:false -p:BuildInParallel=false`: 84 app + 144 core tests pass. Six new app cases and ten new core cases cover repeated spotlight cycles, selection/guide/preflight isolation, explicit reselection, removed games, pause preference serialization, delayed cleanup, concurrent completion, callback failures, retention, and finite progress.

**UI evidence:** Offscreen actual Horizon windows with a temporary fixture library at 1280x720 and 1920x1032 (the monitor work-area cap) verify toggle layout, binding, and the controller-confirm handler for pause and resume. Probe screenshots: `tmp_probe/spotlight-1280.png` and `tmp_probe/spotlight-1920.png`. These are targeted layout/input checks, not a real-controller or full-library acceptance run.

**Slice 2 delivered: owned Horizon entry, cancellation, and window cleanup. P01 remains open.**

- One entry operation owns the splash and destination, coalesces repeated requests, and reports preparing/presenting/ready/cancelled/failed. Ownership transfers only after the destination is shown. Closing the splash or destination cancels the handoff; a failure closes partial windows and allows retry. Desktop shutdown cancels entry without resurrecting its window.
- Added Cancel and Escape on the Horizon splash, cancellation-aware first-frame waits and fade-out, and separate log timings for the loading frame, dashboard frame, and completed handoff. Removed the obsolete second loading-overlay implementation. Artwork-ready timing and synchronous preparation optimization remain open.
- Horizon closes its momentum, attract, trailer, gamepad, session, and cursor timers, disposes both WebView2 controls, closes the local trailer listener, releases captured mouse input, and unsubscribes shared state. Late browser/image work checks closure. Delayed trailer blanking cannot blank a newly active preview. Embedded-session subscriptions are detached; native embedded-game detach/return behavior still needs its own audit.
- Added the three owner-supplied beta invitations as hash-only allowlist entries, preserved previous activations, clarified the offline gate's limitations, and prevented Enter from attempting redemption twice. `CLOSED_BETA.md` defines the three-person cohort, safety boundaries, test matrix, feedback template, update isolation, and release gates. A local unsigned Windows x64 candidate is prepared separately from the installed app; nothing is uploaded or published to the shared update feed.

**Verification:** The full solution passes 109 app + 144 core tests (253 total). Fourteen new entry cases cover duplicate/reentrant requests, immediate and stage-specific cancellation, desktop shutdown, failures, retry, ordered milestones, and twenty lightweight handoffs. Eleven access-gate cases cover invalid input, compatibility, encrypted persistence, invalid markers, and write failure. An isolated probe also redeemed all three actual supplied keys and verified each encrypted grant, without writing the user's activation marker or storing plaintext keys in source.

**Window evidence:** Twenty offscreen cycles through actual Horizon and splash windows at 1280x720 with an isolated fixture library completed. Each returned to one desktop window, with every Horizon dispatcher-timer field stopped and the listener released. Nineteen of twenty closed Horizon windows were collected; the final completed entry still retains its result until the next entry. This is bounded retention, not zero retained objects. The Cancel splash was rendered and inspected at 760x460 (`tmp_probe/horizon-entry-cancel.png`). Release publish completed without compiler warnings. Native helper-process counts, installed-build behavior, large-library memory/frame-time measurements, and physical input were not established by this probe.

**Next in P01:** Remaining operation callers, PDF/native embedded-window lifecycle, and shutdown audit; artwork-ready timings and preparation performance; provider-refresh/overlay soak coverage and installed-product resource measurements. `CLOSED_BETA.md` also records future server-backed invitation limits/revocation as a separate decision, not a feature of the current local gate.

### P01/P02 Delivery Log - September 13, 2026

**Slice delivered: visible guide work and stable multi-controller routing. P01 and P02 remain open.**

- Guide discovery and guide attachment now enter Activity Center as named, cancellable operations. Discovery reports archive work; attachment forwards connection, download, validation, and first-page verification states. Cancelled, failed, completed, and selection-changed paths now end with a truthful operation outcome instead of an unexplained busy state.
- Horizon now samples all four XInput slots through the same source abstraction used by Input Lab. The first connected controller becomes the active owner, displayed as `P1` through `P4` in Horizon's controller legend. A second controller cannot fight over focus; when the owner disconnects, the next connected controller takes over cleanly. Background-window held input is consumed when Horizon returns to focus.
- Strengthened the shared keyboard/controller focus ring with a theme-reactive outer boundary and a crisp inner confirmation line. It improves couch-distance clarity without covering artwork or changing navigation semantics.

**Verification:** `dotnet test tests/CloudBerry.App.Tests/CloudBerry.App.Tests.csproj --no-restore -m:1 -nr:false -p:BuildInParallel=false` passes 114 app tests; the existing 144 core tests remain in the full solution baseline, for 258 tests. Five focused router tests cover non-primary slots, stable ownership, reassignment after disconnect, held-input suppression, and clean disconnection.

**Validation still required:** Physical wired/wireless pads, mapper duplication, and controller-only page/modal workflows remain release-gate hardware checks. Guide archive health remains a live-network concern; Activity Center makes source and download outcomes visible but does not claim universal manual availability.

## P02 - Controller-Complete Horizon

**Outcome:** A player can browse, inspect, launch, read, recover, and return without reaching for a mouse.

- [x] Share controller discovery and normalized input between Horizon and Input Lab. Support all four XInput slots and explain the active device.
- [ ] Handle disconnect, reconnect, slot changes, and switching between mouse, keyboard, and controller. Avoid duplicate input when a mapper exposes the same physical device twice.
- [ ] Persist tested dead zones and repeat preferences per supported device/profile; retain immediate taps and controlled holds. Include diagonal intent and drift diagnostics.
- [ ] Build a navigation map for every page and modal. Define initial focus, directional neighbors, confirm/back behavior, focus restoration, and scroll-into-view boundaries.
- [ ] Complete combo boxes, sliders, toggles, search entry, errors, guide reading, activity cancellation, and repair dialogs. A must activate the focused action, not an unrelated default launch.
- [ ] Remember the selected game and scroll anchor per page/category. Changing sort, filtering, or removing a game chooses the nearest valid item deliberately.
- [ ] Add a device-capability layer before promising native non-XInput support. Assess a maintained backend and test actual PlayStation/Switch-compatible hardware before claiming compatibility.

**Acceptance:** Controller-only Home -> Library -> Details -> Guides -> Launch -> Return succeeds. Setup and failure recovery have no focus traps. Two connected pads do not fight over focus. Reconnecting does not require restarting Horizon. Text input works without arrow-key hijacking.

**Evidence:** Recorded hardware matrix with wired/wireless and mapper states, normalized-input tests, and screenshot evidence of visible focus on every critical control. Hardware tests are release gates, not inferred from unit tests.

## P03 - Artwork And Physical Collection

**Outcome:** Cases look like coherent physical objects while the collection remains fast and readable.

- [ ] Establish packaging families with reference dimensions: jewel, DVD, Blu-ray, handheld plastic, and cardboard boxes. Prioritize represented consoles before expanding the catalog.
- [ ] Add correct spine depth, hinges/seams, insert recesses, edge materials, and family-specific proportions. Use the library's platform data, allow overrides, and never disguise incorrect metadata as a case-model issue.
- [ ] Keep cover-only artwork intact. Use spine/back assets only when actually available; do not stretch a front cover across invented surfaces or pretend a generic back is authentic.
- [ ] Move file/network artwork decoding off the UI thread; reuse decoded assets, cancel stale requests, and bound memory by bytes as well as item count.
- [ ] Recycle or virtualize offscreen case scenes. Keep item dimensions and focus stable across poster, list, shelf, and case modes.
- [ ] Provide Flat, Gentle Angle, and Physical Case presentation choices with remembered per-device preferences. Reduced-motion mode retains complete artwork without animated tilt.
- [ ] Add an artwork inspection workspace with source, resolution, fit/fill preview, focal point, and a preview of every consuming surface before applying changes.

**Acceptance:** Long titles and every edge of a case remain visible at minimum width and high DPI. A square test image stays square in the mapped surface; real portrait/landscape covers keep their intended ratios. Ten thousand library records do not create ten thousand live 3D scenes. Hover never removes art or starts a fresh file decode.

**Evidence:** Actual-cover galleries for supported packaging families; focused/unfocused and missing-art states; camera/UV tests; cache and scrolling traces. Native WPF rendering remains the current implementation baseline; no rendering-stack migration without measured justification.

## P04 - Complete-Screen Visual And Sensory Pass

**Outcome:** The least-used dialog feels as considered as the main dashboard.

- [ ] Define a small, shared hierarchy for page backgrounds, section boundaries, individual items, inset fields, overlays, and primary actions. Remove repeated borders, nested cards, redundant counters, and competing highlights.
- [ ] Introduce intentional Compact, Comfortable, and Couch density. Keep content priorities consistent while adapting information density, text size, and hit targets.
- [ ] Finish each theme's materials, atmospheric motion, typography, and focus states without adding effects over reading surfaces. Test theme switching in both directions to catch token leakage.
- [ ] Use a shared motion vocabulary for entry, navigation, selection, loading, success, and errors. Interrupt animations cleanly and respect reduced motion everywhere, not only in backgrounds.
- [ ] Add separate interface/launch/ambience audio levels, previews, mute, and device-aware preferences. Avoid duplicate cues, hidden-window sounds, and audible surprises.
- [ ] Add measured effects fallback for low frame rate, remote sessions, and battery constraints, with an understandable user override. Stabilize quality changes so they do not oscillate.

### Screen-By-Screen Contract

| Surface | Composition and interaction pass | Required edge cases |
| --- | --- | --- |
| Startup / entry / launch | Continuous handoff, useful status, no false completion | Offline, retry, early close, second monitor |
| Desktop Home | One primary resume action; reorderable, restrained sections | Fresh library, no history, all games unavailable |
| Desktop Library | Readable title rail, compact filters, inspectable selection | Long names, multi-select, no results, missing art |
| Horizon Home / Library | Full-width stage, consistent shelf rhythm, clear focus | Minimum couch resolution, last row, changing categories |
| Game details | Launch, readiness, personal context and guides in a clear order | Missing metadata, running game, duplicate installations |
| Metadata / artwork | Stable comparison columns and explicit apply scope | Conflicts, protected fields, provider failure |
| Achievements | Trustworthy totals, progress hierarchy, honest sync state | No provider, unavailable source, partial progress |
| Emulation | Scanable setup rows and actionable readiness | Wrong core, missing BIOS, custom emulator |
| Settings / profiles | Searchable settings and immediate reversible previews | Reset, invalid paths, unsaved changes |
| Friends / sharing | Clear privacy scope, useful overlap, compact identity | Empty roster, invalid payload, offline |
| Theatre | Content-first controls and predictable Back behavior | Browser failure, unsupported content, focus leaving web content |
| Guides / activity / recovery | Legible tools, anchored actions, consistent error treatment | Long PDFs, cancelled operations, failed restore |

**Acceptance:** Review all surfaces in all nine themes with real content, high contrast, effects off, long labels, and disabled controls. No text overlap, unreadable glass, moving hit targets, or invisible keyboard/controller focus. Functional parity must survive every visual change.

## P05 - Guide Library, Finished

**Outcome:** Manuals and guidebooks are reliably found, curated, and read as part of the collection.

- [ ] Maintain a verified provider catalog with source health, expected document types, rate limits, and recorded live fixtures. Broaden official publisher/platform coverage first, then trustworthy archives.
- [ ] Add explicit regional aliases, edition/platform/language matching, match reasons, and a source/edition review when evidence is ambiguous. Never substitute a sequel or market a weak match as verified.
- [ ] Separate candidate, downloadable, cached, and reader-checked states. Expose size and language when known. Preserve source links and make replacement of broken attachments straightforward.
- [ ] Add cover thumbnails, document-type filters, bookmarks, reading position, fit-page/fit-width/zoom, and remembered single-page/facing-page preferences per document.
- [ ] Add text search and a contents list when the PDF provides text/outlines. Offer local OCR separately, only if justified; do not pretend image-only scans are searchable or reflow original pages.
- [ ] Add an offline shelf with pin/unpin, integrity checks, cache budget and cleanup previews. Preserve notes/bookmarks if a cached file is removed.
- [ ] Audit PDF renderer maintenance, native packaging, licensing, malformed-document handling, cancellation, and memory limits. Keep third-party PDFs untrusted; rendering one page is not whole-document integrity proof.

**Acceptance:** Discover -> review -> download -> read -> bookmark -> close -> reopen offline returns to the correct page in desktop and Horizon. Test representative manuals and multi-hundred-page guides, already-spread scans, corrupt/encrypted files, restricted sources, missing PDFs, and partial provider outages. No automatic loan/access-control circumvention.

## P06 - Launch Confidence And Session Continuity

**Outcome:** Cirra explains what will launch, protects the player's progress, and brings them back to useful context.

- [ ] Complete readiness checks and guided repair routing for executable/URI/ROM/core/BIOS/configuration problems. Distinguish detected from configured, compatible, and successfully tested.
- [ ] Introduce per-game installation profiles for vanilla, modded, VR, alternate executable, and emulator overrides. Preview the chosen target and keep save scope explicit.
- [ ] Build a reusable launch/return state model for immediate exit, launcher handoff, long sessions, crashes, and uncertain process tracking. Do not fabricate playtime when tracking is unavailable.
- [ ] Finish supported save discovery, per-game backup timelines, integrity validation, conflict/restore previews, retention, and rollback. Never infer that the newest timestamp is the right save without showing the choice.
- [ ] Extend Next Time into a session return sheet: optional note, objective, verified save reference, duration correction, and pause/finished decision. Quiet by default; no mandatory post-game form.
- [ ] Add opt-in after-exit save backups only once operation cancellation, retention, and failure reporting are reliable.

**Acceptance:** A broken launch produces the smallest actionable repair and keeps focus on the affected game. Save restoration is previewable and reversible. Returning from a game restores the exact browsing context. At least one real title per supported emulator family validates each claimed launch path.

## P07 - A Curator's Workspace

**Outcome:** Large collections become easier to maintain without risking years of personal edits.

- [ ] Add field-level metadata provenance, confidence, user locks, and side-by-side conflict review. Refresh only the approved fields.
- [ ] Make import/merge/repair changes previewable with additions, exclusions, duplicate sources, revision/region groupings, and an explicit commit step.
- [ ] Expand Time Machine with field-level differences, selective restore, retention controls, storage usage, and linked operation context.
- [ ] Add composable smart collections with live counts, manual pins/exclusions, custom ordering, and an explanation of why a game belongs.
- [ ] Extend Command Center/search with aliases, typo tolerance, scoped filters and saved searches. Structured filters stay deterministic and inspectable; no opaque recommendation dependency.
- [ ] Add storage insight and Change Review: detect moved/offline drives and new installs, then propose changes without automatically deleting records or files.

**Acceptance:** An import followed by metadata refresh cannot overwrite a locked field. A curator can inspect and undo a batch without restoring the entire library. Search preserves focus and selection as results update. Disconnecting a drive never looks like permission to delete its collection.

## P08 - Personal Dashboard And Tonight

**Outcome:** The dashboard helps a player choose and resume, rather than becoming a wall of widgets.

- [ ] Add a small, editable Tonight queue shared between desktop and Horizon. Explicit picks take priority over suggestions and work offline.
- [ ] Extend Play Window with queue-aware suggestions, supported session-length evidence, a clear reason, and dismiss/snooze controls.
- [ ] Allow users to reorder, hide, and pin dashboard sections. Persist layouts per device; cap above-the-fold density and provide Reset Preview before applying defaults.
- [ ] Add a compact session thread combining Next Time, meaningful local milestones, selected guide bookmarks, and verified backup events. Do not create an engagement feed.
- [ ] Add optional personal goals such as finishing a campaign or revisiting a series. Distinguish local goals from provider achievements and allow all prompts to be disabled.

**Acceptance:** A returning player can resume a ready game within two primary actions. Suggestions never displace a selection or queue item. A completely offline Home remains useful. Large and small libraries get intentional empty/populated layouts.

## P09 - Household And Local Co-op

**Outcome:** Cirra understands the people and supported controllers present without requiring online accounts.

- [ ] Define local household-profile boundaries for preferences, queues, notes, ratings, visibility and history. Preserve shared library identity and provide explicit migration choices.
- [ ] Add a local co-op finder using sourced/manual player-count and shared/split-screen metadata, installed readiness, connected controllers, and chosen time budget. Treat unknown support as unknown.
- [ ] Add a pre-session checklist showing participating devices, input conflicts, and the selected launch profile. Never promise compatible multiplayer solely from a tag.
- [ ] Extend existing Cloud Codes with versioned selective payloads, validation, preview-before-import, and local overlap calculations. Keep publication explicit.
- [ ] Add inspectable Play Capsule export for configuration, metadata decisions, notes, and explicitly permitted saves. Exclude game binaries, credentials, and unrelated personal files by default.

**Acceptance:** Household switching does not leak private notes or overwrite another user's queue. Co-op results explain their evidence. Malformed shared data cannot alter arbitrary paths or trigger commands. Capsule import previews every change and supports cancellation before commit.

## P10 - Release Assurance

- [ ] Use representative fixture libraries: empty, 100, 1,000, and 10,000 titles; long names; missing art; mixed ROM/store entries; offline drives; large PDFs.
- [ ] Run the full application on clean installs and upgrades, not only standalone control harnesses. Verify executable, installer, update-feed and displayed versions agree.
- [ ] Review native/browser/PDF dependencies, licenses and advisories. Record unavailable audit services as missing evidence, not a clean security result.
- [ ] Verify backup-before-migration, atomic data writes, failed-update recovery, and rollback compatibility. Confirm portable/self-contained packaging includes required native assets.
- [ ] Make diagnostics export opt-in and previewable; redact secrets, private notes, usernames and local paths where feasible.
- [ ] Test accessibility navigation, screen-reader names, large text, high DPI, multi-monitor transitions, sleep/resume, remote sessions, and clean shutdown.
- [ ] Publish truthful known limitations and a concise recovery guide. Unsupported controllers, unavailable manuals, and uncertain launch tracking must be named.

## Measurement And Acceptance Matrix

These are proposed engineering targets, not measured claims. Establish and record reference hardware in P01; change a target only with documented evidence and user-visible tradeoffs.

| Dimension | Initial target / required evidence |
| --- | --- |
| Local input | p95 input-to-visible-feedback <= 100 ms on reference hardware |
| Interactive motion | Target 60 Hz presentation on reference hardware; p95 interactive frame time <= 20 ms; distinguish ambient redraw cadence from UI responsiveness |
| Local search | p95 result update <= 150 ms for a 10,000-title fixture after index readiness |
| Cached startup | Target usable desktop <= 3 seconds for 1,000 cached titles; report cold and warm separately |
| Horizon entry | Target usable dashboard <= 2 seconds from an initialized desktop; report first frame and interactive readiness separately |
| Memory | Define byte budgets after baseline; no unbounded artwork/PDF caches, nor monotonically growing retained resources across 20 round trips |
| Layout | Minimum supported window sizes, 1280x720 couch view, 1080p, ultrawide and 4K; test Windows scaling at 100/125/150/200 percent |
| Theme/readability | All nine themes; aim for >= 4.5:1 normal text and >= 3:1 large text and meaningful focus/control boundaries; inspect composites, not only raw color tokens |
| Network | Offline, slow, timeout, rate-limit, malformed response, source removed, and access-restricted fixtures |
| Controllers | Actual supported hardware, multiple slots, reconnect, drift, held input, mapper duplication and mixed-input sessions |
| Recovery | User data unchanged after failed/cancelled preparation; committed operations have truthful outcomes and defined rollback |

Fast machines must not hide UI-thread stalls. Average FPS, a passing unit suite, and one attractive screenshot are insufficient acceptance evidence.

## Engineering Boundaries

Keep the WPF/.NET application and existing service conventions. Introduce abstractions only to separate real responsibilities or reduce repeated lifecycle/input/operation logic. Avoid a wholesale `MainViewModel` or window rewrite while repairing a narrow workflow.

| Workstream | Initial code ownership |
| --- | --- |
| Entry and lifecycle | `App.xaml.cs`, `MainWindow.xaml.cs`, `SplashWindow.*`, `Services/WindowPresentation.cs` |
| Input and selection | `BigPictureWindow.xaml.cs`, `ControllerDirectionRepeater.cs`, `HeroRotationPolicy.cs`, `Core/Services/InputLabService.cs` |
| Artwork and cases | `Controls/HorizonGameCase3D.cs`, `ViewModels/GameCaseProfile.cs`, image converters/cache services |
| Materials and motion | `Resources/CloudBerryTheme.xaml`, `Effects/ThemeVisualProfile.cs`, `Effects/ThemeEffectLayer.cs`, `SoundDesignService` |
| Guide workflow | `Core/Services/Metadata/GuidebookDiscoveryService.cs`, `GuideDocumentService.cs`, `Services/PdfSpreadRenderer.cs`, `GuideReaderWindow.*` |
| Operations and recovery | `ActivityCenterService.cs`, `LibraryHistoryService.cs`, `SaveContinuityService.cs`, mutation/persistence integration |
| Launch and context | Launch preflight/launcher services, `MainViewModel`, `GameEntryViewModel`, session/profile models |

Paths above are ownership pointers under `src/CloudBerry.App` or `src/CloudBerry.Core`, not a mandate to edit every listed file. Persisted changes require migration tests and rollback consideration. Provider fixtures belong in deterministic tests; network smoke checks are separate and rate-limited.

## Pass Completion Contract

Every approved pass must produce:

1. A concise observed-problem baseline and a bounded implementation slice.
2. Working UI and mechanics, including empty/loading/error/offline/cancel states.
3. Focused automated regression tests, with broader coverage for shared contracts.
4. Complete-screen visual evidence on desktop and Horizon where affected.
5. Before/after performance evidence for any rendering, loading, or indexing claim.
6. Data-preservation and recovery checks for persistent or external operations.
7. An updated roadmap entry with evidence, limitations, and remaining work.

If a hardware or installed-build check cannot be performed, label the pass **implemented, awaiting validation**, not complete. Pause feature expansion when a new regression threatens launching, selection, saves, or library integrity.

## Deliberately Deferred

- Additional themes, decorative particle layers, and animated social video cards before current surfaces pass their quality gates.
- Cloud accounts/sync, broad plugin execution, unrestricted scripting, and automatic downloads of game/BIOS content.
- AI-generated metadata or recommendations without sources, review, and manual overrides.
- Full emulator replacement, universal controller compatibility claims, and cosmetic effects that require a rendering-stack rewrite without evidence.

## Approval And Next Action

**No implementation begins from this document until the user gives the go.**

The proposed first delivery is P01: record real startup/selection/operation baselines, separate spotlight from working selection, harden loading and cancellation lifecycles, and prove those workflows in the full application. P02 follows immediately to make controller behavior dependable before broader visual composition changes.

Approval can accept this sequence or reprioritize a pass. It does not require approving every later speculative feature in advance.
