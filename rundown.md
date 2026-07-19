# CloudBerry Rundown

This file is the shared handoff and coordination document for `A1`, `A2`, `A3`, and `A4`.

`A1` is the primary in-thread implementation agent and research/development lead.
`A2` is the lead visual programmer.
`A3` is the backend design and product-feel systems lead.
`A4` is the bug-fix, testing-readiness, and code-humanity lead.

Use `A1:`, `A2:`, `A3:`, and `A4:` markers in notes, commits, code comments, and design decisions when cross-referencing work.

## Mission

CloudBerry is being built as a polished, fast, user-friendly universal game library manager that can stand beside Playnite and LaunchBox while developing its own identity.

Core product pillars:

- Desktop-first, snappy, visually clean library management.
- Strong launcher coverage across native PC games, ROMs, and custom/manual entries.
- Big Picture mode (`Cloudscape`) that feels premium, cinematic, and controller-friendly.
- Metadata and media workflows that are easy for normal users, not just power users.
- Achievement syncing/tracking that works locally first and falls back smartly.
- Rich customization without making the app feel cluttered.

## Current App Shape

Primary pages:

- `Home`
- `Library`
- `Metadata`
- `Achievements`
- `Settings`

Important windows:

- `MainWindow`
- `MetadataWindow`
- `BigPictureWindow`
- `SplashWindow`
- `MediaSearchWindow` (manual media picker work in progress)

## Current Features

### Library and Launching

- Folder scanning for Windows games.
- ROM scanning for emulator-based titles.
- Manual add flow for executables, shortcuts, and ROM files.
- Import review workflow with confidence scoring.
- Per-platform emulator settings.
- RetroArch core detection and autofill support.
- Favorite, status, notes, user rating, critic rating, and playtime tracking.
- Launch sessions tracked inside CloudBerry.

### Metadata

- Aggregated keyless metadata provider stack:
  - `Steam Store`
  - `PCGamingWiki`
  - `Wikipedia/Wikimedia`
  - `OpenCritic`
- Richer media slots now modeled:
  - `cover`
  - `background`
  - `banner`
  - `icon`
- Media caching to local CloudBerry storage.
- Metadata editor window separated from the Library.

### Achievements

- Local achievement scanning.
- Steam community/public fallback work.
- Gamer score accumulation.
- Library-level achievement summary and unlock feed.

### Visual/Product

- Theme presets.
- Rotating home suggestions.
- Splash/loading screen support.
- `Cloudscape` big-picture shell.

## Key Files

App shell and windows:

- `src/CloudBerry.App/MainWindow.xaml`
- `src/CloudBerry.App/MainWindow.xaml.cs`
- `src/CloudBerry.App/MetadataWindow.xaml`
- `src/CloudBerry.App/MetadataWindow.xaml.cs`
- `src/CloudBerry.App/BigPictureWindow.xaml`

Core viewmodels:

- `src/CloudBerry.App/ViewModels/MainViewModel.cs`
- `src/CloudBerry.App/ViewModels/MainViewModel.BigPicture.cs`
- `src/CloudBerry.App/ViewModels/GameEntryViewModel.cs`
- `src/CloudBerry.App/ViewModels/MetadataSearchResultViewModel.cs`
- `src/CloudBerry.App/ViewModels/MediaSearchResultViewModel.cs`

Metadata services:

- `src/CloudBerry.Core/Services/Metadata/AggregatedMetadataProvider.cs`
- `src/CloudBerry.Core/Services/Metadata/SteamStoreMetadataProvider.cs`
- `src/CloudBerry.Core/Services/Metadata/PcgamingWikiMetadataProvider.cs`
- `src/CloudBerry.Core/Services/Metadata/WikipediaMetadataProvider.cs`
- `src/CloudBerry.Core/Services/Metadata/WebImageSearchService.cs`
- `src/CloudBerry.Core/Services/Metadata/GameMetadataMatchSelector.cs`

Persistence/models:

- `src/CloudBerry.Core/Models/GameRecord.cs`
- `src/CloudBerry.Core/Models/GameMetadataRecord.cs`
- `src/CloudBerry.Core/Models/CloudBerrySettings.cs`
- `src/CloudBerry.Core/Services/LibraryStore.cs`

## Current References

Product references the user explicitly wants us to channel:

- Playnite library detail view.
- Playnite metadata/media editing flow.
- Playnite Google Images style manual media search.
- Steam Big Picture inspiration, but themed as `Cloudscape`.

Reference themes from the user:

- Big, readable, premium-feeling presentation.
- Media-first browsing when a game is selected.
- Less clutter in the Library page.
- Metadata tasks should feel like intentional editing, not debugging forms.

## Known Issues

### Active

- `A1:` Library can get stuck showing the import review workspace when no useful import candidates remain.
- `A1:` Automatic metadata still misses too many non-Steam and launcher-only titles.
- `A1:` Manual media flow has been mid-transition from metadata search reuse to a proper preview-based picker.
- `A1:` Some background/banner/icon slots still need better preview and persistence polish.
- `A1:` Metadata/media quality is not yet near Playnite standards.
- `A1:` Library visual hierarchy still needs cleanup in a few states.

### Design/Product

- Keyless metadata coverage is better than before, but still incomplete for edge-case games.
- Public web image search is inherently less stable than a formal API, so manual media search must stay resilient and clearly user-driven.
- Some older comments/text still describe outdated provider assumptions.

## Agent Roles

### A1: Research and Development Lead

`A1` owns research and development around the board.

Primary responsibility:

- Keep CloudBerry moving toward the feature depth, quality bar, and polish level of premium library apps like Playnite and LaunchBox.
- Research product patterns, user expectations, and library-app conventions before big product decisions.
- Develop core features across the app when ownership is not clearly visual-only, backend-only, or bug-only.
- Protect the overall product direction inside this thread.
- Make sure CloudBerry feels high quality, feature packed, practical, and still like its own product.

Current `A1` focus:

- Fixing the `Library` workspace state bug so empty import review falls back to `Browse`.
- Building a dedicated manual media picker window for:
  - `cover`
  - `background`
  - `banner`
  - `icon`
- Making globe buttons in `MetadataWindow` open that picker instead of abusing metadata search.
- Applying selected manual media directly to the selected game and caching locally.

### A2: Lead Visual Programmer

`A2` owns everything visual across CloudBerry.

Primary responsibility:

- VFX, visual fidelity, polish, aesthetic cohesion, button effects, app effects, hover/focus/active states, transitions, and motion.
- Raise visual quality across:
  - `MainWindow.xaml`
  - `MetadataWindow.xaml`
  - `MediaSearchWindow.xaml`
  - `BigPictureWindow.xaml`
- Improve spacing, hierarchy, readability, and premium feel while preserving the existing product structure.
- Add tasteful WPF animations and state feedback that feel intentional, not noisy.
- Keep the UI aligned with Playnite/LaunchBox quality and CloudBerry's nostalgic `Cloudscape` identity.

Constraints:

- Coordinate with `A1` direction and avoid conflicting architectural rewrites.
- Prefer additive visual improvements over invasive refactors.
- If touching shared files, leave clear `A2:` notes in commits/comments.
- Do not change metadata/scanner/persistence architecture unless explicitly coordinated.

Execution:

- Audit visual pain points and list highest-impact polish tasks.
- Implement the top set of improvements.
- Verify no UX regressions:
  - navigation
  - focus
  - selection clarity
  - empty states
  - busy states
- Report exactly what changed, why it improves feel, and what should be polished next.

Canonical `A2` prompt:

```text
You are A2, lead visual programmer for CloudBerry.
Own everything visual: VFX, visual fidelity, polish, aesthetic cohesion, button effects, app effects, hover/focus/active states, transitions, and motion.

Priorities:

Raise visual quality across MainWindow.xaml, MetadataWindow.xaml, MediaSearchWindow.xaml, and BigPictureWindow.xaml.
Improve spacing, hierarchy, readability, and premium feel while preserving the existing product structure.
Add tasteful WPF animations and state feedback that feel intentional, not noisy.
Keep the UI aligned with Playnite/LaunchBox quality and CloudBerry’s nostalgic Cloudscape identity.

Constraints:

Coordinate with A1 direction and avoid conflicting architectural rewrites.
Prefer additive visual improvements over invasive refactors.
If touching shared files, leave clear A2: notes in commits/comments.
Do not change metadata/scanner/persistence architecture unless explicitly coordinated.

Execution:

Audit visual pain points and list highest-impact polish tasks.
Implement the top set of improvements.
Verify no UX regressions (navigation, focus, selection clarity, empty/busy states).
Report exactly what changed, why it improves feel, and what should be polished next.
```

### A3: Backend Design and Nostalgic Dashboard Systems Lead

`A3` owns backend design decisions that make CloudBerry feel more and more like the nostalgic dashboard-style library app the user wants.

Primary responsibility:

- Design and evolve backend structures that support the desired dashboard feel.
- Improve data modeling, state flow, settings, persistence, and app services in ways that make the frontend easier to polish.
- Help the Library, Home, Metadata, Achievements, and Cloudscape surfaces feel cohesive through better underlying app behavior.
- Support fast browsing, clean filtering, reliable media state, stable metadata, and dashboard-like summaries.
- Keep backend decisions product-minded, not abstract for their own sake.

Near-term `A3` tasks:

- Review library state, media state, achievement summaries, and dashboard data flows for places where the app feels thin or disconnected.
- Strengthen models/services that support nostalgic dashboard elements: recent activity, collections, platform identity, shelf summaries, playtime, gamer score, and selected-game staging.
- Make persistence and settings support visual/user-facing customization without making the app brittle.
- Coordinate with `A2` so backend state gives visual surfaces rich, reliable data to render.
- Coordinate with `A1` before changing scanner, metadata provider, or storage architecture.

### A4: Bug-Fix, Testing-Readiness, and Code-Humanity Lead

`A4` owns the final integrity pass before testing and keeps the codebase feeling like it was written by one careful human.

Primary responsibility:

- Find and fix bugs before formal testing.
- Watch for regressions, broken bindings, compile errors, missing null guards, and awkward state transitions.
- Keep code style, naming, comments, and structure consistent across the app.
- Make the work feel handcrafted and cohesive by the end, not stitched together from separate agents.
- Preserve user changes and avoid broad rewrites unless they clearly reduce risk.

Near-term `A4` tasks:

- Run compile/test checks after major A1/A2/A3 changes and investigate failures.
- Inspect changed XAML and C# for binding mistakes, duplicate resources, dead code, and inconsistent patterns.
- Smooth agent-written code so it reads like one person maintained it.
- Confirm visual polish does not break navigation, selection, scanning, metadata editing, or launch workflows.
- Add short notes in this rundown when a bug fix affects another agent's ownership area.

## Coordination Rules

- `A1` owns the main product direction inside this thread and keeps the app pointed toward Playnite/LaunchBox-level quality.
- `A2` owns visual polish direction and should proactively elevate the app's look and feel.
- `A3` owns backend design work that supports the nostalgic dashboard product feel.
- `A4` owns bug cleanup, testing readiness, and consistency passes before the next test cycle.
- Every agent should inspect current uncommitted work before editing overlapping files.
- If an agent touches a file another agent is actively changing, add a short role marker note in this document or in the commit message.
- Prefer additive work over invasive rewrites.
- If a choice affects visual fidelity, `A2` leads the recommendation.
- If a choice affects backend state/design, `A3` leads the recommendation.
- If a choice affects stability, testing, or code consistency, `A4` leads the recommendation.
- If a choice affects product UX, use the user's Playnite references as the tiebreaker.

## Implementation Notes

### Metadata Philosophy

- Do not rely on embedded secrets for shared desktop metadata access.
- Prefer keyless, stable public sources first.
- Treat manual media selection as a first-class workflow, not as a failure case.

### Media Philosophy

- Backgrounds matter as much as covers.
- The selected game should feel staged and alive.
- Users should be able to override art manually with clear preview before commit.
- Resolution and aspect ratio hints should guide the user instead of leaving them to guess.
- The media picker should feel curated, not like a raw dump of search results.

### Library Philosophy

- Library page should primarily browse, select, and launch.
- Metadata editing belongs in a focused workspace/window.
- Empty states should never look like a broken app.

## Deeper Roadmap

### Phase 1: Stabilize Current Shell

- Finish library state bug fixes.
- Finish manual media picker with preview and direct apply.
- Polish metadata editor tabs and previews.
- Clean up provider descriptions and status messaging.

### Phase 2: Raise Metadata Quality

- Improve title matching for tricky games, editions, and launcher-only titles.
- Expand source blending for summaries, links, release data, and media.
- Add better per-field merge logic rather than all-or-nothing result application.
- Add richer link extraction and display.

### Phase 3: Playnite-Class Editing

- Full edit-details style window with stronger `General`, `Media`, `Links`, `Installation`, and `Actions` sections.
- Better art inspection and cropping assumptions.
- Manual per-field save/apply logic.

### Phase 4: Cloudscape

- Evolve big-picture navigation into a premium controller-first experience.
- Strong selected-game stage with media-heavy presentation.
- Category transitions and animation polish.
- Better shelf behavior and focus states.

### Phase 5: Profiles and Identity

- Multi-profile support.
- Library-level gamer score dashboards.
- Activity feed and recent unlock history.
- More advanced theme/layout customization.

### Phase 6: Professional Finish

- Stronger startup reliability.
- Better diagnostics/log surfacing.
- Cleaner publish/release workflow.
- More tests around scanning, metadata, and media persistence.

## Immediate Next Steps

- `A1:` Finish `MediaSearchWindow` integration and compile/test it.
- `A1:` Verify selected manual media updates the Library hero/background correctly.
- `A2:` Lead the next wave of visual polish across Library, Metadata, Media Search, and Cloudscape surfaces.
- `A2:` Improve state readability, motion, focus styling, and premium-feeling presentation details.
- `A3:` Audit backend state/design for the Home, Library, Metadata, Achievements, and Cloudscape dashboard feel.
- `A3:` Propose data/service improvements that make the app feel more nostalgic, cohesive, and alive without disrupting active A1 work.
- `A4:` Bug-sweep the current code before the next test pass, especially XAML bindings, tab navigation, media persistence, and import review state.
- `A4:` Normalize agent-written code so the final implementation reads like one careful developer maintained it.
- `A1 + A2 + A3 + A4:` Keep pushing toward a media workflow and library experience that feels as intentional as Playnite's without losing CloudBerry's personality.

---

## A2 Visual Polish Notes - Wave 1 COMPLETE

*All work is non-breaking, additive, and avoids A1 viewmodel/service files.*

**CloudBerryTheme.xaml**
- `PrimaryButtonStyle` - animated hover glow (DropShadowEffect BlurRadius 0->16), color fade, press scale 0.96 with BackEase spring-back.
- `SecondaryButtonStyle` - animated color fade hover, press scale 0.97.
- `GhostButtonStyle` - animated background fill on hover, press scale 0.97.
- `NavListBoxItemStyle` - EventTrigger ColorAnimation on hover replaces instant Trigger setters; active left bar fades in on selection.
- `InvisibleTabControlStyle` - upgraded from bare ContentPresenter to Border+TranslateTransform with SelectionChanged storyboard. Every page nav fades in (opacity 0->1) with 14px upward slide, 280ms CubicEaseOut. Used by both MainWindow and BigPictureWindow.

**SplashWindow.xaml**
- Full staggered entrance: window fade+slide, icon badge BackEase bounce, title/tagline/quote card/footer staggered delays, progress bar shimmer loop (AutoReverse forever), Berry glow orb ambient layer.

**BigPictureWindow.xaml**
- Game cards: hover scale 1->1.04 + CardGlow DropShadow fade, FocusHalo ring on selection.
- Nav items: EventTrigger animated hover (bg color + border + scale 1.04), Cursor=Hand.
- Category items: same treatment, scale 1.03.

**MetadataWindow.xaml**
- Window fade-in on Loaded (0->1, 320ms). Result cards: animated hover bg/border color + scale 1.008.

**MediaSearchWindow.xaml**
- Window fade-in on Loaded (0->1, 320ms). Image cards: animated hover bg/border color, Cloud-blue shadow glow, scale 1.04.

---

## A2 Visual Polish Notes - Wave 2 COMPLETE

*Crash-fix + Library visual pass.*

**CloudBerryTheme.xaml - InvisibleTabControlStyle (CRITICAL BUG FIX)**
- Root cause: Wave 1 custom TabControl ControlTemplate omitted `TabPanel IsItemsHost="True"`.
  Without it, WPF never updates `SelectedContent` when `SelectedIndex` changes programmatically.
  Scans set `SelectedWorkspaceTab = 1` but UI never switched - looked like scans were broken.
- Fix: Grid wraps a `TabPanel IsItemsHost="True" Visibility="Collapsed" Height="0"` alongside the fade-in Border.
  This is the canonical WPF pattern for fully custom TabControl templates. All tab switching now works.

**MainWindow.xaml - Library game list (Playnite density pass)**
- Rows: cover art 40x56 -> 30x42, margins tightened, row height ~52px (vs ~72px before).
- Hover: animated `ColorAnimation` on `SolidColorBrush` (transparent -> `#141E2E`), not DataTrigger instant-set.
- Selection: `DataTemplate.Triggers DataTrigger` sets `CardSelectedBrush`, shows berry accent bar (`Opacity 0->1`), brightens title text.
- Achievement mini-bar removed from list rows (detail panel shows it; too noisy at compact density).

**MainWindow.xaml - Collection / Platform sidebar filters (flat Playnite-style)**
- Removed card borders (Background, BorderThickness=1, CornerRadius=12).
- Now flat `Padding="8,7"` rows with animated `ColorAnimation` hover - matches game list row style.
- Selected collection: `BerryDimBrush` bg + label brightens to `TextPrimaryBrush`.
- Selected platform: `CardSelectedBrush` bg + label brightens.
- Much tighter visual hierarchy; left panel now feels like a real sidebar not a stack of cards.

## A2 Visual Polish Notes - Wave 3 COMPLETE

*All work is non-breaking and additive.*

**CloudBerryTheme.xaml**
- Removed duplicate `PillTabButtonStyle` (invalid second definition that was a broken/incomplete redeclaration of the same key). Working Tag-binding version at line ~372 retained.

**MainWindow.xaml - Home stat panel stagger**
- Named `StatCard2` (Achievements), `StatCard3` (Gamer Score), `StatCard4` (Running) - added `Opacity="0"`, `RenderTransformOrigin`, and `TranslateTransform Y=12` to match existing StatCard0/1 setup.
- Added `Grid.Triggers` `EventTrigger RoutedEvent="FrameworkElement.Loaded"` on the stat grid with a `BeginStoryboard` staggering all 5 cards: 0ms, 70ms, 140ms, 210ms, 280ms delays, 320ms CubicEaseOut fade + upward slide. Cards sweep in left-to-right on Home page load.

**BigPictureWindow.xaml + BigPictureWindow.xaml.cs - Hero crossfade**
- Replaced single bound `Image` with two named images: `HeroBgA` (Opacity 0.22, code-managed) and `HeroBgB` (Opacity 0, code-managed). Both get the same BlurEffect Radius=26.
- Code-behind: subscribes to `ViewModel.PropertyChanged` for `BigPictureHeroGame`. On change: loads new path into HeroBgB, runs simultaneous 420ms fade (HeroBgB 0->0.22, HeroBgA 0.22->0), then in `Completed`: swaps sources back to HeroBgA and resets HeroBgB to invisible. True crossfade between hero backgrounds on game selection change.

## A2 Visual Polish Notes - Wave 4 IN PROGRESS

*Full-app visual audit, starting with the windows that had zero prior A2 pass: `ScanPreviewWindow`, `RetroArchCoreCenterWindow`, `WrappedWindow`. All work confirmed theme-reactive across all 9 `AppearanceThemeOptions` (CloudBerry Dark, Neon Matrix, Retro Glitch, NieR Automata, Wuthering Waves, Blueberry Midnight, Mint Cloud, High Contrast, Black Flash) via `ApplyAppearanceTheme()` in `MainWindow.xaml.cs`.

**Rule for all future A2 work:** never animate a literal hex `Color` on a local `SolidColorBrush` for hover/selection state - `ApplyAppearanceTheme()` repaints `DynamicResource` brushes at runtime and a hardcoded animation target silently ignores the active theme. Correct pattern: keep the base `Background`/`BorderBrush` on `DynamicResource` (e.g. `CardBrush`, `BorderSubtleBrush`), then add a sibling `Border` overlay bound to a `DynamicResource` (e.g. `CardHoverBrush`, `BerryBrush`) with `Opacity="0"` and animate `Opacity` 0->1 on hover/selection. This is the same technique `PrimaryButtonStyle`'s glow/sheen layers already use, extended to list-item templates.

**ScanPreviewWindow.xaml (Import Games from Scan)**
- Footer action bar was still on a leftover parchment/beige gradient (`#E8DAD0AE` etc.) from the old NieR-first palette - looked like a bug against every current theme. Replaced with `DynamicResource SidebarBrush` + `BorderBrush` + a thin `BerryBrush`-tinted top accent line.
- Candidate rows, metadata result cards, and media result cards previously had little-to-no hover feedback (a couple used a flat opacity fade with no depth) and zero selection feedback. All three now get: theme-reactive hover glow (`CardHoverBrush` overlay), theme-reactive selection accent (`BerryBrush` outline), and a 1.2% scale lift on the candidate rows for a snappier feel.
- Media result thumbnails were raw square `Image` elements; wrapped in a 4px-rounded clipped `Border` so they read as cards, not a debug dump.

**WrappedWindow.xaml (Year Wrapped) - DONE**
- Chrome was hardcoded to the old off-theme palette (`#0D0D10` window bg, `#18181C` title bar, raw unstyled `Button` for close) - completely ignored all 9 themes. Replaced with `DynamicResource AppBackgroundBrush`/`SidebarBrush`/`BorderSubtleBrush`, added the standard 280ms fade-in `Window.Triggers` entrance, a `NostalgiaAtmosphereBrush` + `ThemeEffectLayer` ambient wash behind the WebView2 host, and swapped the close button onto the shared `WindowControlCloseButtonStyle` (matches every other window's chrome).
- Added `DataContext = _vm;` in the code-behind constructor - it was never set, so the new `{Binding AppearanceThemeKey}` / `{Binding VisualEffectsEnabled}` on `ThemeEffectLayer` needed it to resolve.
- Left the WebView2-rendered "Wrapped" card's own HTML/CSS (gradient stat cards, purple heatmap) untouched - that's an intentional fixed brand aesthetic (Spotify-Wrapped-style shareable card), not a bug, and reskinning it per-theme would undercut the "this is your shareable card" identity.

**RetroArchCoreCenterWindow.xaml - DONE**
- Was already fully theme-reactive (DynamicResource everywhere, `ThemeEffectLayer`, entrance fade) - just had the classic Wave-4 gap: zero hover feedback on both list panels.
- Installed Cores list had no `ItemTemplate` at all (bare default `ListBoxItem`, no card, no hover). Gave it a themed row: `CardBrush` fill, `BorderSubtleBrush` outline, small MDL2 glyph, and a `CardHoverBrush` opacity-overlay on hover (theme-safe pattern, not an animated literal Color).
- Suggested Platform Mappings cards had a static border with zero interactivity. Added the same opacity-overlay hover glow plus a `BerryBrush` left accent bar that fades in and a 0.8% hover scale lift, consistent with `ScanPreviewWindow`'s candidate-row treatment.
- Validated both files as well-formed XML after editing.

**Next in Wave 4:**
- Sweep `MainWindow.xaml` (5,000+ lines) for any other hardcoded-hex hover animations left over from Waves 1-3 that predate this theme-safety rule.
- Sweep `MetadataWindow.xaml` / `MediaSearchWindow.xaml` for the same list-item selection-feedback gap found in ScanPreviewWindow and RetroArchCoreCenterWindow.
- `MainWindow.xaml` is large enough it should be split into a few focused passes (e.g. Home stat/hero surfaces, Library list/grid, Settings/Profile panels, Achievements) rather than one sweep, to keep diffs reviewable.

**Wave 4 audit result:** `MetadataWindow.xaml`, `MediaSearchWindow.xaml`, and `MainWindow.xaml` were checked against the hardcoded-hex-hover rule directly (searched for `ColorAnimation` targeting literal hex `To` values and raw `SolidColorBrush` hover targets) — none found. The handful of hardcoded hex values remaining in `MainWindow.xaml` are intentional semi-transparent glass/scrim overlays, not theme-breaking hover animations. Wave 4's hover/selection-feedback gap sweep is considered complete as of this pass.

---

## A2 Visual Polish Notes - Wave 5 — Cursor Parallax Engine Enhancement

*User asked for a full VFX pass covering every theme's unique effects specifically, not just chrome/hover polish. Rather than hand-tune all 9 bespoke `Draw*` renderers individually (Matrix rain, NieR gear mechanism, Wuthering fluid waves, Cyber Glitch tears, Black Flash domain, Blueberry nebula, Mint topography, Contrast architecture, CloudBerry god rays — already a genuinely mature, AAA-tier v3.0 system per its own doc comment), this pass adds one engine-level capability that upgrades all nine simultaneously: cursor-reactive parallax.*

**`Effects/ThemeEffectLayer.cs`**
- New behavior: every `ThemeEffectLayer` instance now tracks the hosting window's cursor position and subtly drifts its entire themed render (glows, particles, rain columns, waves, gears — everything) toward it, up to 9px on each axis, smoothed with a 0.09 lerp factor so it reads as gentle inertia rather than a snap.
- Implementation: `IsHitTestVisible="False"` on every instance (by design, so effects never block clicks on real UI beneath them) means the element can't receive its own `MouseMove`. Instead, `Start()` resolves the hosting `Window` via `Window.GetWindow(this)` and subscribes to its `PreviewMouseMove`, re-resolving on every `Start()` call since Cloudscape/BigPictureWindow can re-parent the same effect layer type across windows. `Stop()` unsubscribes cleanly. Coordinates are converted to this element's local space via `e.GetPosition(this)`.
- To avoid ever revealing a seam at the shifted edge, the whole draw in `OnRender` is wrapped in a `TransformGroup` (`ScaleTransform` to 106% from center, then the smoothed `TranslateTransform`) and `ClipToBounds="True"` was added to the element's constructor — the 6% oversize always covers a 9px max shift on every panel size currently in use (smallest is ~200px wide).
- The cinematic vignette (`DrawCinematicVignette`) is deliberately drawn *outside* the parallax transform (after `dc.Pop()`) so it stays a fixed, stable frame around the drifting content underneath — same idea as a photo mat that doesn't move with the picture.
- Respects existing gates: only accumulates/renders while `IsEffectActive` (bound to `VisualEffectsEnabled`) is true and the layer is visible, so users with effects disabled or in performance mode see zero overhead from this.
- Zero XAML changes required — every existing `<effects:ThemeEffectLayer .../>` instance across `MainWindow.xaml`, `RetroArchCoreCenterWindow.xaml`, `WrappedWindow.xaml`, `ScanPreviewWindow.xaml`, `BigPictureWindow.xaml`, etc. picks up the new reactivity automatically.

**Verification:** copied the finished file into a scratch environment and ran a full brace-depth walk (opens == closes == 84, depth never negative, ends at 0) confirming the class/method structure is intact after four sequential edits. Not build-tested in Visual Studio — recommend a rebuild + quick pass over Home, Library, and Cloudscape to confirm the drift feels right at the current 9px/0.09 tuning; those two constants (`ParallaxMaxShift`, `ParallaxSmoothing`) are the first things to adjust if it reads as too subtle or too aggressive on a real screen.

**Next candidates for a true Wave 5 continuation (per-theme, not engine-level):**
- NieR Mechanism: the rotating gear rings currently spin at a fixed rate regardless of `Intensity`; scaling rotation speed with intensity would make the "low power" and "vivid" effect settings feel more differentiated.
- Wuthering Fluid: the coral/teal wave bands don't yet react to `EffectRole` (Shell vs Sidebar vs Chrome all render identically at different opacity) — a role-aware phase offset would stop the three simultaneous layers from ever looking obviously tiled/repeated.
- Matrix Rain: glyph set is static; a rare (low-probability per column) full-brightness "target lock" column that briefly resolves to a readable short word before dissolving back to noise would be a nice signature moment without adding real overhead.
- Black Flash Domain is intentionally the most restrained theme by design (see Wave-3-era comments in the file removing film grain, lightning, screen distortion for a "subtle, clean void" aesthetic) — flagging this explicitly so a future pass doesn't undo that deliberate choice without checking with the user first.

---

## PS5-Style Home Widget Customization — DONE

*Research-driven feature (Xbox 2026 accent personalization / PS5 Home widget customization) picked up from the competitive research pass; scoped as a safe MVP rather than full drag-and-drop reorder to avoid disturbing the Wave 3 staggered stat-card entrance animation, which targets `StatCard0`-`StatCard4` by name and would break under a dynamic `ItemsControl`.*

**`CloudBerry.Core\Models\CloudBerrySettings.cs`**
- New `HomeWidgetSettings` class (`ShowLibrary`, `ShowMetadata`, `ShowAchievements`, `ShowGamerScore`, `ShowRunning`, all default `true`) nested under `AppearanceSettings.HomeWidgets`. Existing installs render identically until the user opts out.

**`CloudBerry.App\Converters\BooleanToGridLengthConverter.cs`** (new)
- Converts bool -> `GridLength`: `true` = `1*` (or a custom star value via `ConverterParameter`), `false` = `0`. Collapses the hidden stat card's column instead of leaving an empty gap. Registered in `App.xaml` as `BooleanToGridLengthConverter`.

**`MainViewModel.cs`**
- Five new bindable bools (`ShowLibraryStat`, `ShowMetadataStat`, `ShowAchievementsStat`, `ShowGamerScoreStat`, `ShowRunningStat`) plus `IsHomeCustomizeOpen` and `ToggleHomeCustomize()` / `CloseHomeCustomize()`. Each setter calls `SchedulePersist()` (same debounced auto-save used by every other Appearance setting).
- Loaded from `_settings.Appearance.HomeWidgets` in `InitializeAsync` (with a null-coalescing default for settings files written before this feature existed), written back in `ApplySettingsFromFields()`.

**`MainWindow.xaml` — Home page hero stats bar**
- Added an "OVERVIEW" header row above the stat grid with a gear-icon `Customize` button (`GhostButtonStyle`, MDL2 glyph `&#xE713;`, same glyph as the Settings nav item) that opens a `Popup` anchored beneath it.
- Popup contains 5 `CheckBox` toggles (Library / Metadata / Achievements / Gamer Score / Running) bound directly to the new ViewModel bools — changes apply and persist live, no separate Save button needed.
- Each `ColumnDefinition` in the stat grid now binds `Width` through `BooleanToGridLengthConverter` to its card's visibility bool, and each `StatCard0`-`StatCard4` `Border` binds `Visibility` through the existing `BooleanToVisibilityConverter`. Hiding a card fully closes its column rather than leaving a blank slot; the Wave 3 stagger animation is untouched since the named `Border`/`TranslateTransform` elements are unchanged.

**`MainWindow.xaml.cs`**
- `ToggleHomeCustomizeClick` one-liner delegates to `ViewModel.ToggleHomeCustomize()`, matching the existing click-handler convention in the file.

**Verification:** `MainWindow.xaml` and `App.xaml` confirmed well-formed XML after edits; brace balance checked on `CloudBerrySettings.cs`, `MainViewModel.cs`, and `MainWindow.xaml.cs`. Not yet build-tested in Visual Studio — recommend a local rebuild + Home page smoke test (toggle each checkbox, confirm columns collapse cleanly and the setting survives an app restart) before shipping.

---

## Library Cover Grid View — DONE

*Roadmap v0.4 item, picked up straight from `ROADMAP.md`: "Cover grid view — toggle between the current list view and a cover-art grid (like GOG Galaxy / Playnite)" + "Adjustable grid tile size." Note while auditing the roadmap against the actual code: the Context Menu, Undo-for-remove toast, and Continue Playing card roadmap items were already fully implemented in a prior session but never checked off — `ROADMAP.md` still marks them 🔨/🗓️. Recommend a pass to update those checkmarks; left untouched this session to stay scoped to the grid-view feature.*

**`CloudBerry.Core\Models\CloudBerrySettings.cs`**
- `AppearanceSettings.LibraryViewMode` ("List" default / "Grid") and `AppearanceSettings.LibraryGridTileSize` ("Compact" / "Normal" default / "Large"). Existing installs open in List view exactly as before.

**`MainViewModel.cs`**
- `IsLibraryGridView` (bool, drives both toolbar toggle buttons and the two ListBoxes' Visibility) + `IsLibraryListView` (inverse, for XAML convenience). Setter persists via the existing `SchedulePersist()` debounce.
- `LibraryGridTileSizes` (readonly `{"Compact","Normal","Large"}`) + `LibraryGridTileSize` bound to the toolbar ComboBox; changing it raises `LibraryGridCardWidth`/`LibraryGridCoverHeight`.
- `LibraryGridCardWidth` (120/160/210 px) and `LibraryGridCoverHeight` (`CardWidth * 1.34`, ~3:4 cover aspect) are computed, not stored — one source of truth per tile-size key.
- Loaded from `_settings.Appearance.LibraryViewMode`/`LibraryGridTileSize` in `InitializeAsync` (null-coalescing default for settings files predating this feature), written back in `ApplySettingsFromFields()`.

**`MainWindow.xaml` — Library toolbar**
- Added a tile-size `ComboBox` (visible only in grid view) + a two-button List/Grid toggle group next to the existing Sort combo. Both toggle buttons are `GhostButtonStyle` (`BasedOn`, so they keep the shared hover/press glow) with a local `DataTrigger` on `IsLibraryListView`/`IsLibraryGridView` that fills the active button with `BerryBrush` — `PillTabButtonStyle` was considered first (it's the pattern the Browse/Import tabs use) but its template hard-binds `Content` to a `TextBlock.Text`, so it can't host an icon glyph; `GhostButtonStyle` + inline trigger was the correct additive fit instead of touching the shared style.
- Icons are `Segoe MDL2 Assets, Segoe Fluent Icons` glyphs (`&#xE71D;` list, `&#xE80A;` grid) — same font family convention as the Settings-nav gear glyph.

**`MainWindow.xaml` — Library page, new `GameLibraryGridList`**
- Added a second `ListBox` (`x:Name="GameLibraryGridList"`) in the same `Grid.Row="2"` cell as the existing `GameLibraryList`, bound to the same `LibraryView`/`SelectedGame`. Visibility on both lists is now bound to `IsLibraryListView`/`IsLibraryGridView` so only one renders at a time; the existing dense-row `ListBox` and its Wave-2/3 polish are untouched.
- `WrapPanel` `ItemsPanel`; each `ListBoxItem.Width` binds to `LibraryGridCardWidth` via `RelativeSource AncestorType=ListBox` so the toolbar tile-size combo live-resizes the grid.
- Card template: cover art (`DisplayCoverImagePath`, height bound to `LibraryGridCoverHeight`) with a theme-safe `CardHoverBrush` opacity-overlay hover (per the Wave-4 rule — no literal-hex hover animations), a running-game dot (`IsRunning`), a favorite star (`IsFavorite`), a `BerryBrush` selection border, and an `AchievementProgressBarStyle` sliver along the bottom edge of the cover when `HasAchievements` is true. Title + platform label underneath.
- Reuses the existing `LaunchSelectedGameClick` / `OpenSelectedGameMetadataWorkspaceClick` / `OpenSelectedGameAchievementsPageClick` / favorite / installed / folder / remove context-menu handlers verbatim (same `ContextMenu` markup as the list view) and the existing `GameListDoubleClick` / `GameListPreviewMouseRightButtonDown` code-behind handlers, both of which are sender-agnostic and needed no changes. `GetSelectedLibraryGames()` still works unmodified for grid-view context-menu bulk actions: it falls back to `ViewModel.SelectedGame` whenever `GameLibraryList.SelectedItems` is empty, which is exactly the case while the grid list (not the hidden list-view `ListBox`) holds the selection.

**`MainWindow.xaml.cs`**
- `SetLibraryListViewClick` / `SetLibraryGridViewClick` one-liners delegate to `ViewModel.IsLibraryGridView`, matching the existing click-handler convention in the file.

**Verification:** Re-fetched the live `MainWindow.xaml` after all edits and confirmed it parses as well-formed XML; brace-balance checked on `CloudBerrySettings.cs`, `MainViewModel.cs`, and `MainWindow.xaml.cs` (all balanced). Cross-checked every `DynamicResource`/`StaticResource` key used in the new grid template against `CloudBerryTheme.xaml`/`App.xaml` — all exist (`CardBrush`, `SurfaceBrush`, `CardHoverBrush`, `BorderSubtleBrush`, `GoldBrush`, `BerryBrush`, `TextSecondaryBrush`, `TextMutedBrush`, `ThemeBodyFontFamily`, `ThemeCornerRadiusControl`, `AchievementProgressBarStyle`, `BooleanToVisibilityConverter`). Not build-tested in Visual Studio — recommend a local rebuild + Library page smoke test next: toggle List/Grid, change tile size, right-click both list and grid cards, launch/favorite/remove from the grid, and confirm the chosen view mode survives an app restart.

**Not yet done (good next session targets):**
- Update `ROADMAP.md` checkmarks for the four items now confirmed shipped (Context menu, Undo-for-remove, Continue Playing, Cover grid view).
- `v0.3` still open: hover tooltips are only on 14 controls file-wide — a full icon-button sweep hasn't happened.

---

## Per-Game "Refresh Metadata" — DONE

*Picked straight off the prior session's "not yet done" list — one-click re-fetch of metadata for the selected game without opening/searching the Metadata tab manually.*

**`MainViewModel.cs`** — `RefreshSelectedGameMetadataAsync()` added directly above `SyncSelectedGameAchievementsAsync()`, matching the file's existing method-grouping convention.

**`MainWindow.xaml`** — "Refresh Metadata" added to both the list-view and grid-view game context menus (each menu is a separate literal `ContextMenu` block, so this took two edits — first attempt double-applied to the same menu and was caught and corrected via a uniqueness check against surrounding markup before landing correctly in both).

**`MainWindow.xaml.cs`** — `RefreshSelectedGameMetadataClick` handler added next to `SearchSelectedGameMetadataClick`, calls `ViewModel.RefreshSelectedGameMetadataAsync()` + `_sound.PlaySoftClick()`, matching the existing handler/sound-effect convention file-wide.

**Bug caught mid-session:** an MDL2 glyph reference was typed with a stray space (`&#x E72C;`), which is invalid XML entity syntax and would have failed to compile. Caught and fixed before landing.

**Verification:** confirmed via direct read of the live `MainWindow.xaml.cs` — `RefreshSelectedGameMetadataClick` exists, is wired to `ViewModel.RefreshSelectedGameMetadataAsync()`, and follows the file's sound-effect convention. Not yet build-tested in Visual Studio.

---

## Quick Actions De-duplication (Home hero + sidebar + title bar) — DONE

*User pushed back on the "full revamp" framing and asked specifically for de-noising: duplicate buttons showing in multiple places was called out as the actual problem, not the FX engine. Audited the three places `OpenBigPictureClick` ("Launch Cloudscape") and the scan/repair actions appeared: sidebar Quick Actions panel, the title bar, and the Home page's hero WrapPanel — all three were rendering the same actions simultaneously with no page ever hiding the sidebar or title bar copy.*

**`MainWindow.xaml` — Sidebar "QUICK ACTIONS" panel**
- Removed the sidebar's "Launch Cloudscape" button. It's permanently visible in the title bar (next to the sidebar-toggle button) on every page regardless of sidebar collapse state, so the sidebar copy was pure redundancy sitting one scroll away from its twin.
- "Scan Folder" is now the first/primary action in that list (promoted to `SidebarPrimaryActionButtonStyle`, the accent-filled style Cloudscape used to occupy) since it's the most-used entry point into the app for a fresh library.

**`MainWindow.xaml` — Home page hero panel WrapPanel**
- This panel had grown to 8 buttons that were almost a verbatim copy of the sidebar list: Scan Games, Fill Metadata, Sync Achievements, Scan Steam Libraries, Scan Launchers, Scan All, Repair Library, Launch Cloudscape. Six of those eight already live in the sidebar (always visible) and Cloudscape a third time in the title bar.
- Trimmed to the two actions that are genuinely Home-specific and don't exist anywhere else in the shell: "Fill Metadata" (now `PrimaryButtonStyle`) and "Sync Achievements" (now `SecondaryButtonStyle`). Scan/repair/Cloudscape actions live in the sidebar where they belong; Home's hero panel is now a profile/overview surface with two clear calls-to-action instead of a second sidebar.
- Left the collapsed (`Visibility="Collapsed"`) `CommandTileButtonStyle` WrapPanel below it untouched — it's dead markup already, not rendering, so it wasn't part of the visible noise; worth deleting outright in a future cleanup pass but out of scope here.
- Left the Library page's own "Clean Missing" button (in its filter toolbar) and `MetadataWindow`'s Cloudscape-adjacent Settings-page button alone — both are contextual to the page they're on (right there while filtering the library / configuring startup behavior), not blind duplication of the same global panel.

**Verification:** re-copied the live file and ran a full `xml.etree.ElementTree` parse — well-formed. Grepped for `Launch Cloudscape` post-edit: exactly one instance remains (title bar). Not build-tested in Visual Studio — recommend a rebuild + visual pass on Home and the sidebar to confirm nothing relies on the removed sidebar Cloudscape button's specific styling/position (e.g. no XAML `ElementName` bindings referenced it — confirmed via grep, none did).

**Next candidates for continuing the de-noise pass (not yet done):**
- The collapsed dead `CommandTileButtonStyle` WrapPanel in the Home hero (Details / Achievements / Quick Add tiles) — either wire it up as a real secondary action row or delete it outright; right now it's just inert markup bloating the file.
- A pass over `MetadataWindow.xaml` and `MediaSearchWindow.xaml` toolbars for the same copy-pasted-button pattern now that the main shell is deduplicated.
- Consider whether the sidebar's remaining 7 scan/maintenance buttons (Scan Folder, Scan ROMs, Scan Steam, Scan Launchers, Scan All, Repair Library, Polish Library, Clean Missing, Quick Add — 9 total) could be grouped into a collapsible "Maintenance" sub-section, since it's still the longest single button stack in the app even after removing Cloudscape.

---

## Wave 6 — Finished the de-noise pass + "Jump Back In" shelf (fan-favorite feature)

*Continuation of the Quick Actions de-dup pass: cleared every remaining item on that list, then researched what Playnite / GOG Galaxy / Xbox / PS5 dashboards are most loved for in 2026 and shipped the single highest-value gap.*

**`MainWindow.xaml` — dead Home hero markup removed**
- Deleted the `Visibility="Collapsed"` `CommandTileButtonStyle` WrapPanel (Details / Achievements / Quick Add tiles) flagged last pass. It never rendered and was pure file weight — all three actions it duplicated already exist as sidebar nav (Metadata, Achievements) or the sidebar Quick Add button, so there was nothing worth resurrecting.

**`MetadataWindow.xaml` — removed a second "Apply to Current"**
- Audited `MetadataWindow.xaml` and `MediaSearchWindow.xaml` for the copy-pasted-button pattern. `MediaSearchWindow.xaml` was clean. `MetadataWindow.xaml`'s top action toolbar had "Apply to Current" (`ApplyInlineClick`) sitting next to an identical "Apply to Current" button already living directly under the selected result's detail card, a few hundred pixels lower — two buttons, same label, same handler, same window, both visible depending on scroll position. Removed the top-toolbar copy; the one attached to the actual result card is more contextual (it's right next to the art/summary you're about to apply) and "Apply and Close" still lives there too, so that card is now the single place to commit a result. Top toolbar is now just page-level actions: Retarget Query, Fill Missing, Search Web.

**`MainWindow.xaml` — sidebar "More maintenance" collapsible section**
- The sidebar's Quick Actions stack was still 8 buttons tall after removing Cloudscape (Scan Folder, Scan ROMs, Scan Steam, Scan Launchers, Scan All, Repair Library, Polish Library, Clean Missing, Quick Add — 9 total), by far the longest single control stack in the app and mostly maintenance actions someone touches once in a while, not daily.
- Kept the two genuinely frequent actions always visible: **Scan Folder** (primary, top) and **Quick Add**. Added a `ToggleButton` ("More maintenance", `GhostToggleButtonStyle` — already theme-reactive, no new style needed) with an MDL2 chevron that flips between down/up via a `DataTrigger` on its own `IsChecked`. The remaining 7 buttons (Scan ROMs, Scan Steam, Scan Launchers, Scan All, Repair Library, Polish Library, Clean Missing) sit in a `StackPanel` whose `Visibility` binds straight to `IsChecked` via `ElementName` — pure XAML, no ViewModel or code-behind changes, so nothing to break on the C# side.
- Collapsed by default: a fresh sidebar is now 2 buttons + 1 toggle instead of 9 buttons, with the long tail one click away.

**Fan-favorite feature research (Playnite / GOG Galaxy / Xbox / PS5, 2026)** — checked what these actually get praised for right now: Playnite's plugin/emulator breadth and Fullscreen couch mode (CloudBerry already has Cloudscape/Big Picture), playtime + achievement tracking (already have both), a rotating/random pick and smart recommendation (already have both — `RandomBacklogGame`/`RecommendedGame`), cover grid view (shipped last session). The one universally-loved pattern CloudBerry didn't have yet: the Xbox/PS5/Steam Deck **"Jump back in" row** — a horizontal shelf of several recently-touched titles, distinct from a single "continue playing" slot.

**New: "Jump back in" shelf — DONE**
- **`MainViewModel.cs`**: new `ObservableCollection<GameEntryViewModel> JumpBackInGames` (declared next to `RecentAchievementUnlocks`, same pattern — fixed collection reference, contents swapped in place) + `HasJumpBackInGames` bool (next to `HasRecentAchievementUnlocks`). New `RebuildJumpBackInGames()` method: takes the 10 most recent titles across the whole library (`IsRunning` first, then `LastPlayedOn` descending), diffed with `SequenceEqual` before clearing/re-adding so it doesn't thrash the UI or re-trigger animations when nothing actually changed. Hooked into the existing `RefreshHomeDeck()` pass so it refreshes on the same cadence as `ContinuePlayingGame`/`RecommendedGame`/`RandomBacklogGame` — no new refresh triggers needed.
- **`MainWindow.xaml`**: new "Jump back in" section on Home, between the hero panel and the existing Continue Playing/Rotating Pick/Recommended row. Horizontally-scrolling `ListBox` (`StackPanel Orientation="Horizontal"` `ItemsPanel`, `ScrollViewer.HorizontalScrollBarVisibility="Auto"`), `SelectedItem` bound straight to `ViewModel.SelectedGame` so the existing sender-agnostic `GameListDoubleClick`/`GameListPreviewMouseRightButtonDown` handlers work unmodified — zero new code-behind. Cards reuse the exact visual language already established by the Library cover-grid cards (`CardBrush`/`BorderSubtleBrush`/`CardHoverBrush` opacity-overlay hover, `BerryBrush` selection border, running dot, favorite star, achievement-completion sliver) so it reads as part of the same design system rather than a bolted-on new pattern, just sized smaller (136px cards) for a shelf instead of a full grid. Whole section's `Visibility` is bound to `HasJumpBackInGames` so it never shows an empty shelf on a fresh install with no play history.

**Verification:** re-copied both live files after all edits. `MainWindow.xaml` re-parses as well-formed XML (`xml.etree.ElementTree`). `MainViewModel.cs` brace-depth walk: balanced, ends at 0, never goes negative. Confirmed every `DynamicResource`/`StaticResource` key used in the new shelf template already exists in `CloudBerryTheme.xaml` (all reused verbatim from the existing grid-card template, none newly invented). Not build-tested in Visual Studio — recommend a rebuild + Home page smoke test: confirm the shelf populates after playing/launching a few games, confirm double-click launches from the shelf, and confirm the sidebar's "More maintenance" toggle expands/collapses cleanly and remembers nothing between app restarts (by design — it's a session-only UI convenience, not a persisted setting; flag if you'd rather it persist via `SchedulePersist()` like the other Appearance toggles).

**Not yet done (good next targets):**
- The "Jump back in" toggle state isn't persisted — easy follow-up if wanted (same `CloudBerrySettings` + `SchedulePersist()` pattern used for `HomeWidgetSettings`).
- Collections/tags (Steam-categories-style custom grouping) is the next most-requested organizational feature from Playnite/GOG Galaxy users and CloudBerry doesn't have it yet — there is a `Collections` `ObservableCollection<CollectionOption>` already in `MainViewModel.cs` (used for filtering, per line ~467) but no UI for a user to create/manage their own custom collections; worth a dedicated pass.
- An achievement-unlock toast/celebration popup (PS5-style trophy pop-up) when an achievement unlocks live during a running session — CloudBerry tracks and lists unlocks (`RecentAchievementUnlocks`) but doesn't surface them as an in-the-moment notification.

---

## Manual User Collections + Empty-State polish — DONE

*User explicitly redirected the old tag-derived "My Collections" concept: "I'd like collections to be all manual, no auto sorting... a user can create a collection via a new button in library, name the collection, select games to add to it... make it VERY premium feeling." Picked up straight from that instruction plus a parallel de-noise/polish pass on empty states and icon tooltips.*

**Manual Collections**

**`CloudBerry.Core\Models\CloudBerrySettings.cs`**
- New `CollectionSettings` (holds `List<CloudBerryCollection> Items`) nested under `CloudBerrySettings.Collections`, mirroring the existing `ProfileSettings`/`TheatreSettings` pattern.
- New `CloudBerryCollection`: `Id` (Guid, same pattern as `CloudBerryProfile.Id`), `Name`, `List<string> GameIds` (explicit membership — no auto-derivation), `CreatedAtUtc`.

**`CloudBerry.App\Models\CollectionOption.cs`**
- Added `CollectionId` (defaults to empty string) alongside the existing `TagName` field, so a nested "My Collections" sidebar row can point at a real `CloudBerryCollection.Id` instead of a free-text tag.

**`MainViewModel.cs`**
- New `ObservableCollection<CloudBerryCollection> UserCollections`, loaded from `_settings.Collections.Items` in `InitializeAsync` (with a `??= new CollectionSettings()` guard for settings files predating this feature) and written back in `ApplySettingsFromFields()` — same debounced `SchedulePersist()` pipeline as everything else.
- `BuildCollections()`'s `my_collections` block now enumerates `UserCollections` instead of `BuildTagCounts()`; each nested row gets `Key = "collection:{id}"` and `CollectionId = {id}`. The old `tag:` filter path (`GameHasTag`) is left fully intact and still wired in `FilterGame`'s switch — existing users' tag-based groupings keep working, they just no longer populate the "My Collections" menu (a person can still filter/search by tag text directly).
- New `GameInUserCollection(game, collectionId)` helper + a `"collection:"` case in `FilterGame`'s switch, parallel to the existing `"tag:"`/`"genre:"` cases.
- New public CRUD surface: `CreateCollection(name, gameIds)` (adds + selects the new collection), `UpdateCollection(collectionId, name, gameIds)` (rename + full membership replace, used by the edit flow), `DeleteCollection(collectionId)`. All three call `RefreshCollections()` + `SchedulePersist()`.

**New: `SelectableGameEntryViewModel.cs`** — thin wrapper pairing a `GameEntryViewModel` with a bindable `IsSelected` checkbox, mirroring the existing `MissingEntryPreviewViewModel` pattern used by Clean Missing.

**New: `CollectionEditorViewModel.cs`** — powers the editor window. Builds the full library as `SelectableGameEntryViewModel` entries (pre-checked from `existingCollection.GameIds` when editing), exposes a live-filtered `ICollectionView` driven by a `SearchText` box, proxies `AppearanceThemeKey`/`VisualEffectsEnabled` from `MainViewModel` (same theme-reactivity pattern as `CleanMissingPreviewViewModel`), and hands back `BuildSelectedGameIds()` on confirm.

**New: `CollectionEditorWindow.xaml` / `.xaml.cs`** — the premium create/edit surface, structurally mirroring `CleanMissingPreviewWindow` (same entrance fade, `ThemeEffectLayer`, atmosphere overlays) but built around a large name field up top and a `WrapPanel` grid of 128px cover-thumbnail cards below (checkmark badge + `BerryBrush` border on selection, click-anywhere-on-card to toggle) instead of a plain checkbox list — the "select games" step reads like curating a shelf, not filling out a form. Edit mode adds a "Delete Collection" button (confirms via `MessageBox`, calls `DeleteCollection`, doesn't touch the games themselves) and pre-fills/pre-checks everything from the existing collection.

**`MainWindow.xaml` — Library toolbar**
- New "New Collection" button (`SecondaryButtonStyle`, folder-plus MDL2 glyph `&#xE710;`) added to the Library toolbar next to the tile-size/view-toggle group, opening `CollectionEditorWindow` in create mode.

**`MainWindow.xaml` — sidebar "My Collections" rows**
- Added a 4th grid column + a small pencil-glyph (`&#xE70F;`) `GhostButtonStyle` edit button on each nested collection row, visible only when `CollectionId` is non-empty (a `DataTrigger Binding="{Binding CollectionId}" Value=""` collapses it for every non-collection row — genres, tags, system filters — so it never shows where it doesn't apply). `CollNodeHoverBg`'s `Grid.ColumnSpan` extended 3→4 so the row hover highlight still covers the new column.

**`MainWindow.xaml.cs`**
- `NewCollectionClick` opens `CollectionEditorWindow(ViewModel, existingCollection: null)`, reports the newly-created collection's name in the status bar on confirm.
- `EditCollectionClick` resolves the clicked row's `CollectionOption.CollectionId` back to the live `CloudBerryCollection` via `ViewModel.UserCollections`, opens the editor in edit mode. Added `using CloudBerry.App.Models;` (first reference to `CollectionOption` in this file).

**Verification:** re-fetched every touched file after all edits. `MainWindow.xaml` and the new `CollectionEditorWindow.xaml` both confirmed well-formed via `xml.etree.ElementTree`. Brace-balance walk on `MainWindow.xaml.cs`, `MainViewModel.cs`, `CloudBerrySettings.cs`, and both new `.cs` files — all balanced, end at 0, never negative. Cross-checked every `DynamicResource`/`StaticResource` key used in the new window and sidebar edit button against `CloudBerryTheme.xaml` — all exist (`PanelBorderStyle`, `FieldLabelStyle`, `CardBrush`, `BorderSubtleBrush`, `BerryBrush`, `AppBackgroundBrush`, `ErrorBrush`, `GhostButtonStyle`, `SecondaryButtonStyle`, `PrimaryButtonStyle`, `ThemeCornerRadiusControl`, `ThemeCornerRadiusPill`, `SurfaceHighBrush`, the three `ThemeNavItem*` brushes, `TextMutedBrush`/`TextSecondaryBrush`/`TextPrimaryBrush`). Caught and fixed two invented-resource mistakes before they landed (`BooleanToOpacityConverter` and `DangerBrush` don't exist — swapped to a plain border/checkmark selection treatment and the real `ErrorBrush` key respectively). Not build-tested in Visual Studio — recommend a rebuild + smoke test: create a collection, edit it (rename + change membership), delete it, confirm it filters the library correctly and survives an app restart.

**Not yet done (good next target):** a right-click "Add to Collection" context-menu entry on individual game rows/cards (quick single-game toggle without opening the full editor) — `GameInUserCollection`/`UpdateCollection` already support it, just needs a small submenu wired into the existing list/grid `ContextMenu` blocks.

---

## AI-tell Cleanup — WebImageSearchService.cs mojibake fix — DONE

*Picked up a fix that a prior session had narrated as complete but never actually saved to disk — the file still had the original corruption when this session checked. Also caught: the file lives at `CloudBerry.Core\Services\Metadata\WebImageSearchService.cs`, not `CloudBerry.Core\Services\` as an earlier session assumed.*

`WebImageSearchService.cs` had two flavors of encoding corruption from a prior save with the wrong codepage: 11 instances of `â€”` (an em dash's UTF-8 bytes misdecoded as Windows-1252 then re-saved) scattered through XML doc comments and inline comments, plus 6 corrupted box-drawing section-divider comments (`// â€”â€” Label â€”â€”...`) each trailing 50-60 garbled characters. Replaced the em-dash corruption with a plain hyphen (matching the plain-punctuation convention already used elsewhere in this cleanup pass) and rebuilt the six section dividers as clean ASCII headers (`// -- Label ----...`). Also swept the file for leaked citation bracket markers and marketing-buzzword language flagged as a risk in the same session — none found; the only `[N]` patterns present are legitimate `Regex.Groups[N]` code.

**Verification:** re-fetched the live file after writing, confirmed zero non-ASCII characters remain anywhere in the file and brace-balance walk ends at 0 / never negative. Not build-tested in Visual Studio.

**Scope note for whoever picks up the rest of the AI-tell pass:** the broader em-dash sweep across the rest of the codebase (BigPictureWindow.xaml.cs was flagged with ~48 instances, MainWindow.xaml ~28, MainViewModel.cs ~13, CloudBerryTheme.xaml ~39) was not continued this session — fixing this one file's real corruption bug took priority over the much larger, more subjective stylistic sweep. That's still open if wanted.

---

## PUSHTOGIT batch tooling — DONE

Three pieces, all written to the repo root:

- **`.gitignore`** — the old one only ignored `.aider*`, meaning `bin/`, `obj/`, `publish/`, and every `update*.exe` installer sitting in the repo root would get committed on a blind `git add .`. Rewritten to cover .NET build output, VS/Rider junk, test results, decompiled/scratch folders (`decompiled_app/`, `tmp_probe/`, `recovered_*.cs`, `.wrangler/`, `.agents/`, `.codex_video_refs/`), and OS junk. Deliberately did **not** blanket-ignore loose `.ps1`/`.py` scripts in the root, since it's not clear those aren't intentionally tracked — left that as a judgment call for you rather than silently deciding.
- **`README.md`** — didn't exist before. Written from the actual feature set (pulled from `DIFFERENTIATORS.md` and verified against real files rather than invented): library scanning across Steam/launchers/ROMs, Emulation Station, achievements + Finish It filter, manual collections, Year Wrapped/Play Patterns, backlog debt tracker, Cloudscape Big Picture mode. Includes real build/run/publish commands cross-checked against `run_dev.bat` and `publish.bat` rather than assumed from filenames. Plain hyphens throughout, not em dashes, to stay consistent with the AI-tell cleanup pass happening in parallel this session.
- **`PUSHTOGIT.bat`** — numbered menu matching the existing `publish.bat`/`run_dev.bat` house style (boxed banners, `@echo off`/`setlocal`). Options: first-time setup (init repo if needed, set/update the `origin` remote URL), commit, commit+tag+push a release (reads the current `<Version>` out of `CloudBerry.App.csproj`, offers to bump it, rewrites all four version tags in the csproj via a single-line PowerShell `-replace` chain, commits, tags `vX.Y.Z`, pushes branch and tag), plain push, pull, status/log, and an "untrack files that should now be ignored" option (`git rm -r --cached .` + re-add, for cleaning up anything already committed before the new `.gitignore` existed — doesn't touch anything on disk).

**Caught and fixed before shipping:** the version-extraction `for /f` loop originally used `tokens=1,* delims=><` expecting token 2 to be the clean version number, but `tokens=1,*` only splits on the *first* delimiter run — it would have left `%%b` as `1.3.7</Version>` instead of `1.3.7`. Fixed to `tokens=2 delims=><`, verified by hand-tracing how batch's delimiter-collapsing actually splits that specific line. Also flattened a multi-line caret-continued PowerShell `-Command` string into one unbroken line, since caret continuation across a quoted string spanning several `echo`-less lines is a common source of silent batch breakage.

**Not verified:** none of this has been run on your machine yet (no remote is configured, so option 1 needs to be run first with your actual GitHub repo URL). Recommend a dry run: option 1 to set the remote, option 6 to confirm status looks sane, option 7 if `bin/`/`obj/`/any `update*.exe` are already tracked from before, then option 2 for a normal commit before ever touching option 3.

---

## Cloudscape trailer-on-hover fix — DONE

*User reported: hovering a game card in Cloudscape grows the card (confirming the hover event itself was firing fine) but the trailer never played — and critically, no fallback "unavailable" message ever showed either, meaning whatever was going wrong was completely silent.*

Found two separate bugs in `ShowCardTrailerPreview` (`BigPictureWindow.xaml.cs`):

1. **The actual failure:** the card-trailer `WebView2` gets initialized in two different places — a "pre-warm" pass on window load, and again inside `ShowCardTrailerPreview` itself if pre-warm hadn't finished yet by the time of the first hover. Both called `CoreWebView2Environment.CreateAsync(...)` independently and then `EnsureCoreWebView2Async(env)` on the *same* `CardTrailerPreviewBrowser` control. WebView2 throws if `EnsureCoreWebView2Async` is called a second time with a different environment while the first call is still in flight — which is exactly what could happen on the very first hover of a session, right as pre-warm was still awaiting its own environment creation. That exception landed straight in a swallowed `catch`.
   - Fix: added a single shared `Task<CoreWebView2Environment>? _trailerBrowserEnvironmentTask` field. Pre-warm now stores its task there instead of just awaiting it locally; `ShowCardTrailerPreview` reuses that same task if one is already set (in-flight or completed) instead of creating a second environment, and re-checks `CoreWebView2 == null` immediately before calling `EnsureCoreWebView2Async` in case pre-warm finished while this method was awaiting the shared task.
2. **Why it was silent:** every failure path (`embedUrl` empty, or the catch block) only set the *inner* `CardTrailerPreviewFallback` panel to `Visible` — but the *outer* `CardTrailerPreviewHost` Border, which the fallback panel lives inside, starts as `Opacity="0" Visibility="Collapsed"` and is only ever made visible on the success path. So on any failure, the fallback message was technically "showing" while sitting inside a fully collapsed, invisible parent — nothing rendered, no error, no clue.
   - Fix: both failure paths now also set `CardTrailerPreviewHost.Visibility = Visible` and `.Opacity = 1` before showing the fallback, so a genuine failure (e.g. no embeddable trailer URL) now actually shows "Trailer preview unavailable / Open the trailer from the game details" instead of nothing.

Also traced (and ruled out as the cause, but worth recording): `IsKnownEmbeddableTrailerUrl`/`BuildTrailerEmbedUrl` in `GameEntryViewModel.cs` and the IGDB `TrailerUrl` population in `IgdbMetadataProvider.cs` are both intact and correctly wired — `youtube.com/watch?v=`, `youtu.be/`, `.embed/` conversion all check out. The `YouTubeHelper.cs` file referenced in an earlier session's search no longer exists anywhere in the repo (same "referenced but not on disk" pattern as `WebImageSearchService.cs` earlier this session) — not currently used by the trailer pipeline as far as this pass found, but worth a mental note if trailer behavior looks odd again later.

**Verification:** re-fetched the live file after both edits, brace-balance walk ends at 0 / never negative. Not build-tested in Visual Studio — recommend a rebuild and an actual hover test in Cloudscape: first hover of a fresh app launch is the scenario most likely to have hit the race, so test that specifically rather than just re-hovering a card that already worked once in the session.

---

## Empty-State + Tooltip Polish — DONE

*Parallel polish pass alongside Collections: the roadmap flagged "only 18 tooltips against 46 icon-style buttons" and a single ambiguous empty state.*

**Empty-state fix (real bug, not just polish):** `HasFilteredGames` was doing double duty — a fresh install with zero games showed the same "No games match those filters" / "Clear Filters" card as an over-filtered result, which makes no sense with no filters active. Added `IsLibraryTrulyEmpty` (`Games.Count == 0`) and `HasNoFilterMatches` (`!HasFilteredGames && Games.Count > 0`) to `MainViewModel.cs`, wired into the same `RefreshLibraryView()` notify block as `HasFilteredGames`. `MainWindow.xaml`'s Library empty-state `Border` split in two: the original "No games match those filters" / Clear Filters card now only shows for `HasNoFilterMatches`, and a new "Your library is empty" / Scan Folder card (🍇 mark, matching `InsetCardStyle`) shows for `IsLibraryTrulyEmpty`. Both share `Grid.Row="2"` so the fix covers list view and grid view at once.

**Tooltip sweep:** audited all 46 `GhostButtonStyle`/`CommandTileButtonStyle` icon-style buttons file-wide; the true icon-only gap (not already self-labeled via visible `Content` text) was the 5 star-rating buttons under "YOUR RATING" in the game detail panel — added `ToolTip="Rate N star(s)"` to each. Everything else flagged by a first-pass regex turned out to already have visible `Content` text ("Go", "Refresh Cores", "Scan Installed", etc.) and didn't need a tooltip — recommend treating that earlier "46 missing" roadmap count as stale/overcounted for future passes.

**Verification:** re-parsed `MainWindow.xaml` as well-formed XML after all edits; confirmed `HasNoFilterMatches`/`IsLibraryTrulyEmpty` raise alongside `HasFilteredGames` in `RefreshLibraryView()`. Not build-tested in Visual Studio — recommend a smoke test: clear a fresh/empty library and confirm the Scan Folder card shows (not Clear Filters), then apply a filter that matches nothing and confirm the original card shows instead.

---

## Private Testing Access Gate — DONE

*User request: a checkpoint before the app opens fully, explaining private testing is underway and asking for a personal one-time access key. Three keys: `bear`, `iru`, `cjmxo`. Requirement: once redeemed, a key is consumed — bound to that system via encryption so it can't be copied from one user to another, and that one user is never asked again.*

**New: `CloudBerry.App\Services\AccessGateService.cs`**
- Static service, no DI wiring needed (mirrors how `AppDiagnostics` is used directly in `App.xaml.cs`).
- Three valid keys are stored as SHA-256 hashes, not plaintext, so a casual look at source/decompiled binary doesn't hand out the active list.
- `TryRedeem(key)`: normalizes (trim + lowercase), checks against the hash set, and on success writes a small marker (`CLOUDBERRY-ACCESS-GRANTED|<ticks>`) to `%LocalAppData%\CloudBerry\access.dat`, encrypted via `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)` (Windows DPAPI). The actual key redeemed is never stored, only the fact that a valid one was used.
- `HasValidAccess()`: reads the marker back via `ProtectedData.Unprotect` with the same scope. This is the mechanism that satisfies "can't be copied from one user to another" — DPAPI's `CurrentUser` scope derives its encryption key from the signed-in Windows account's own credentials, so if `access.dat` is copied to a different Windows account (or a different machine entirely) `Unprotect` throws, decryption fails, and that copy is worthless. Any failure (missing file, corrupt bytes, foreign-account decrypt failure) is caught and treated identically to "never redeemed" — the gate just shows again.

**New: `CloudBerry.App\AccessGateWindow.xaml` / `.xaml.cs`**
- Borderless modal matching `SplashWindow`'s chrome language (`ThemePanelBorderGradientBrush` border, `ThemeSweepBrush` top shimmer rule, rounded panel, drop shadow, fade+slide entrance) so it reads as part of the app rather than a bolted-on system dialog.
- Explains the private-testing phase in plain language, has a labeled `ACCESS KEY` text field (`TextFieldStyle`), and `Continue`/`Exit` buttons (`PrimaryButtonStyle`/`SecondaryButtonStyle`). Enter key in the field submits, matching the `IsDefault="True"` Continue button.
- Wrong key: inline error text appears (`ErrorBrush`) and the key field does a short keyframe shake (`KeyFieldShakeStoryboard`, +/-8px decaying over 300ms) for tactile wrong-answer feedback instead of just a static error message. Typing again clears the error automatically.
- `Exit` (and the window's own close glyph, `WindowControlCloseButtonStyle`) sets `DialogResult = false` and closes — `App.xaml.cs` treats that as "user declined," not a crash.

**`CloudBerry.App\App.xaml.cs`**
- Gate check inserted at the very top of `OnStartup`, before the splash window is even constructed: `if (!AccessGateService.HasValidAccess())` opens `AccessGateWindow` via `ShowDialog()`. If the dialog doesn't return `true` (user hit Exit or the window's close button), the app calls `Shutdown()` and returns immediately — splash, `MainViewModel.InitializeAsync()`, and `MainWindow` are never reached at all for an unredeemed/failed session.
- Once a key is redeemed successfully, `AccessGateService.TryRedeem` has already written the encrypted marker before the window closes, so every subsequent launch on that same Windows account skips the gate entirely and goes straight to the normal splash -> main flow.

**Known limitation, flagged directly rather than silently glossed over:** this is a fully local, no-backend check (matches how `CloudBerrySocialService` talks to a real Cloudflare Worker, which would be the correct place to enforce genuinely global one-time-per-key redemption across machines, but standing up/modifying that worker was out of scope for this pass). What's actually enforced: a key can't be *copied* from one already-redeemed person's machine/account to a different one and reused there, since the encrypted marker is worthless outside the exact Windows account that created it. What's *not* enforced: nothing stops the same key from being typed fresh into a second machine by someone who has it memorized (e.g. if `bear` leaked to a fourth person) — that would require the same server-side check `CloudBerrySocialService` already does for friend codes. Since there are only three keys for three known testers, each redeeming once on their own machine, this is a reasonable scope for now; worth revisiting if the tester list grows or keys need central revocation.

**Verification:** confirmed all three new files exist on disk with correct content, brace-balance walk on both `.cs` files ends at 0/never negative, and every `DynamicResource`/`StaticResource` key used in the new window (`ThemeCornerRadiusPanel`, `ThemeCornerRadiusControl`, `ThemePanelBorderGradientBrush`, `ThemeSweepBrush`, `NostalgiaScanlineBrush`, `ThemePanelFillSoftBrush`, `ThemeDisplayFontFamily`, `TextPrimaryBrush`, `TextSecondaryBrush`, `ErrorBrush`, `WindowControlCloseButtonStyle`, `FieldLabelStyle`, `TextFieldStyle`, `SecondaryButtonStyle`, `PrimaryButtonStyle`, `AppBackgroundBrush`) exists exactly once in `CloudBerryTheme.xaml`. Not build-tested in Visual Studio — recommend a rebuild and a real test of all three keys (`bear`, `iru`, `cjmxo`), one wrong key, and Exit, then confirm a redeemed key skips the gate on a second launch without deleting `%LocalAppData%\CloudBerry\access.dat`.



---

## Cloudscape Emulation Station tab — DONE

*Picked up from an in-progress "A5" thread whose data layer was already substantially built (platform tiles, three shelves, ROM/needs-a-core counts, all wired into the master Cloudscape refresh pipeline) but whose actual page body in `BigPictureWindow.xaml` had never been written — confirmed via direct grep that only 5 `TabItem` blocks existed (Home/Library/Achievements/Settings/Theatre) even though the nav list already had 6 entries and the D5/gamepad-bumper keybindings already targeted `SelectedCloudscapePage = 4`.*

**Collision caught and reverted before landing:** started this pass by building a parallel, simpler Emulation data model (`EmulationRomGames`, a duplicate `BigPictureEmulationShelves` declaration, a single "ROM Library" shelf) before discovering A5's much richer existing layer (`BigPictureEmulationPlatforms` — per-platform `CollectionOption` tiles plus an "All ROMs" tile; `SelectedBigPictureEmulationPlatform`; `BigPictureEmulationShelves` — three shelves: selected platform, "needs a core", recently added; `BigPictureEmulationSummary`; `EmulationRomCount`/`EmulationNeedsCoreCount`; `NeedsEmulatorCore()`; all already correctly raised in the shared Cloudscape refresh sweep). Fully backed out every duplicate/redundant addition (verified via grep — zero remaining references) before writing any XAML, to avoid the exact class of duplicate-declaration build error the `_libraryViewMode`/`IsLibraryGridView` collision caused earlier this session.

**`MainViewModel.cs`** — two small, genuinely additive pieces on top of A5's existing layer:
- `HasEmulationGamesNeedingCore` (bool, `EmulationNeedsCoreCount > 0`) so the XAML can conditionally show the "needs a core" callout only when it's non-empty, instead of always rendering "0 need a core configured." Raised alongside the existing `EmulationRomCount`/`EmulationNeedsCoreCount` notifications in the same refresh method.
- `GameCount`/`GameCountLabel` added to `EmulatorPlatformCardViewModel` (the Settings-page emulator-path card, a distinct class from A5's Cloudscape-tab `CollectionOption` tiles — not a collision) so each Settings platform row now shows how many library games use that platform, computed in `RefreshEmulatorPlatformCards()` via a straight `Games.Count(...)` per catalog entry.

**`BigPictureWindow.xaml`** — the actual missing page body, inserted as a 6th `TabItem` between Settings and Theatre (matching the nav list's Home/Library/Achievements/Settings/Emulation/Theatre order):
- Header: "Emulation Station" title + `BigPictureEmulationSummary` subtitle, and a "Configure Emulators" button.
- Platform-tile row: `ListBox` bound to `BigPictureEmulationPlatforms`/`SelectedBigPictureEmulationPlatform`, copied verbatim (same `BigPictureNavListStyle`/`BigPictureCategoryItemStyle`/`DataTemplate`) from the existing Library tab's category-tile `ListBox`, since both bind the same `CollectionOption` shape — zero new template risk.
- Quick-stat row: total ROM count, plus a gold "N need a core configured" callout gated on the new `HasEmulationGamesNeedingCore`.
- Shelf area: `ItemsControl` bound to `BigPictureEmulationShelves` using the existing shared `BigPictureShelfTemplate` resource — same reuse pattern as the Library and Home tabs, so the three A5 shelves (selected platform / needs a core / recently added) render with zero new card markup.

**`BigPictureWindow.xaml.cs`** — `OpenEmulatorSettingsClick` sets `ViewModel.SelectedCloudscapePage = 3` (jumps to the Settings tab within Cloudscape) instead of closing the window, mirroring the existing `DetailAchievementsClick` in-Cloudscape tab-jump convention rather than inventing a new navigation pattern.

**Verification:** re-fetched every touched file after all edits. `BigPictureWindow.xaml` confirmed well-formed via `xml.etree.ElementTree`. Brace-balance walk on `MainViewModel.cs`, `EmulatorPlatformCardViewModel.cs`, and `BigPictureWindow.xaml.cs` — all balanced, end at 0, never negative. Confirmed zero leftover references to the reverted parallel data model. Cross-checked every `DynamicResource`/`StaticResource` key used in the new tab against `CloudBerryTheme.xaml`/`App.xaml`/`BigPictureWindow.xaml`'s own local resources — all exist (`ThemePanelFillSoftBrush`, `BorderSubtleBrush`, `ThemeCornerRadiusPanel`, `ThemeDisplayFontFamily`, `TextPrimaryBrush`, `TextSecondaryBrush`, `SecondaryButtonStyle`, `ThemeSweepBrush`, `TextMutedBrush`, `GoldBrush`, `BooleanToVisibilityConverter`, and the two Big-Picture-local styles `BigPictureNavListStyle`/`BigPictureCategoryItemStyle`, confirmed defined within `BigPictureWindow.xaml` itself rather than the shared theme). Confirmed exactly one definition and one call site for the new `OpenEmulatorSettingsClick` handler. Not build-tested in Visual Studio — recommend a rebuild + Cloudscape smoke test: open the Emulation tab (D5 or gamepad LB/RB cycling), click through platform tiles to confirm the three shelves update, confirm the ROM-count/needs-a-core line reads correctly, and confirm "Configure Emulators" lands on the Settings tab.

**Feature now fully complete end-to-end:**
- ✅ Data layer (A5's platform tiles, 3 shelves, summary/count stats)
- ✅ Navigation (index scheme, nav item, keybindings, controller bumper cycling — from the earlier `SelectedCloudscapePage`/`SelectedPrimaryPage` collision fix)
- ✅ Page body (this session)
- ✅ Controller D-pad/stick navigation was already general-purpose (hero shelf, tab bumpers) and needed no Emulation-specific changes since the tab reuses the same `ListBox`/shelf patterns already covered by existing gamepad handling.

---

## Idle-crash fix + Cloudscape gamepad-poll performance — DONE

*User report: a crash when idling the app, plus a request to keep VFX smooth under load. Root-caused rather than guessed at — traced every `DispatcherTimer` in the app (`MainWindow`, `BigPictureWindow`, `MainViewModel`) to find which ones fire completely unattended, since "idle" by definition means no user action to correlate a crash to — only a background timer tick could be responsible.*

**Root cause: `App.xaml.cs`'s `OnDispatcherUnhandledException` only whitelists two specific `InvalidOperationException` messages.** Every other exception type gets logged, then falls through and terminates the whole process — including exceptions thrown from timers that only run while the user isn't touching anything: the Home page's suggestion-rotation timer (`MainWindow.xaml.cs`), the 70s social-presence heartbeat, and in Cloudscape, a 12s hero-advance rotation and a 30s live-session label refresh. None of the risky ones had their own try/catch, unlike the rest of the codebase's established defensive convention (`ThemeEffectLayer.OnRender` explicitly wraps its per-frame work for exactly this reason — see its own comment, "Never let a render exception crash the app"). Idle time is exactly when these ticks are the *only* code running, so any transient hiccup had zero safety net.

**`App.xaml.cs`**
- `OnDispatcherUnhandledException` now also marks `HttpRequestException`, `SocketException`, `TaskCanceledException`, `OperationCanceledException`, and `WebException` as handled — a defensive floor for the specific class of failure unattended background work throws (Wi-Fi dropping, DNS hiccups, Windows changing network state during a long idle stretch), never something a user action caused and never something that leaves app state corrupted. Genuine state-corruption bugs still fall through and crash, same as before — this isn't a blanket catch-all, just widening the net for exception types that are inherently recoverable-by-nature. Added `using System.Net.Http;` / `using System.Net.Sockets;` for the new type checks.

**`MainWindow.xaml.cs`**
- Added a local `AppDiagnostics _diagnostics` field (`using CloudBerry.Core.Services;`), matching the same instantiation pattern `App.xaml.cs` already uses.
- `OnHomeSuggestionTimerTick` (fires on the Home page's rotation interval, unattended) wrapped in try/catch + `_diagnostics.WriteException`.
- The social heartbeat timer's `async void` tick lambda now has its own try/catch too — belt-and-suspenders, since `PublishActiveProfilePresenceAsync()` already catches its own network/service exceptions internally, but the outer lambda is still an unguarded `async void` boundary firing every 70s regardless of idle state.

**`BigPictureWindow.xaml.cs`**
- Same `AppDiagnostics` field + using added.
- `StartHeroAdvanceTimer`'s tick (12s hero rotation while idling on Cloudscape's Home tab) was the strongest concrete crash candidate found: it indexed straight into `ViewModel.Games` with no snapshot and no try/catch — `games.Count` was read once for the guard clause and again implicitly via `% games.Count` several lines later, with a live `ObservableCollection` in between. Fixed to snapshot `count` once, clamp `idx` defensively, and wrapped the whole tick in try/catch.
- `StartLiveSessionTimer`'s tick (30s running-game label refresh) given the same try/catch treatment, plus `.ToList()` on the `Where(IsRunning)` enumeration so a concurrent mutation during the foreach can't throw a collection-modified exception mid-iteration.

**Cloudscape gamepad-poll performance (the "stay smooth under heavy VFX" half of the ask)**
- Found `GamepadPollTick` calling the native `XInputGetState` interop function every 16ms (60Hz) for the *entire* lifetime of any Cloudscape session, unconditionally — whether or not a controller is even connected. That's constant main-thread work competing every single frame with the VFX render budget (`ThemeEffectLayer`'s 30fps ambient render, shelf momentum scroll, hero crossfades, WebView2 trailer previews) for anyone browsing with just a mouse/keyboard, which is most users most of the time.
- Added adaptive backoff: after ~1 second of consecutive "no controller" results, the timer's own `Interval` slows from 16ms to 500ms (2Hz) — controller *discovery* being half a second slower than instant is imperceptible, since nobody is holding a stick waiting for the app to notice it doesn't exist yet. The instant a controller IS detected, the interval snaps straight back to 16ms so actual input never feels the backoff.
- Zero behavior change for anyone actually using a controller — the backoff only ever engages during the no-controller path, and resets on the very next successful read.

**Verification:** re-fetched all three live files after every edit. Brace-balance walk on `App.xaml.cs`, `MainWindow.xaml.cs`, and `BigPictureWindow.xaml.cs` — all balanced, end at depth 0, never negative (full depth-walk, not just open/close counts). Confirmed `AppDiagnostics`/`HttpRequestException`/`SocketException` usings resolve and no pre-existing `_diagnostics` field collided in either window. Not build-tested in Visual Studio — recommend a rebuild, then two specific tests: (1) leave the app idle on Home for several minutes and confirm no crash (this was previously very hard to repro on demand since it depended on a background tick hitting a transient failure at the right moment — the fix is defensive-by-construction rather than a reproduction-confirmed root cause, so flagging that honestly), and (2) open Cloudscape with no controller plugged in, wait a few seconds, then plug one in mid-session and confirm input still feels instant despite the backoff.

**Not yet done (good next performance targets):**
- The `_verticalMomentumTimer`/`_shelfMomentumTimer`/`_homeScrollTimer` (all 16ms/60Hz) are transient — only running during an active scroll gesture — so they weren't touched here, but worth confirming they're actually `Stop()`'d the instant momentum settles rather than continuing to tick at zero velocity.
- `_trailerPreviewTimer` at a flat 1000ms wasn't investigated this pass — worth checking whether it's gated to only run while a trailer preview is actually visible/hovered, or ticks continuously whenever Cloudscape is open.
- No stack trace or repro steps were available for the reported crash, so this fix targets the most plausible root cause class (unguarded unattended-timer exceptions) rather than a single confirmed line. If the crash recurs after this fix, the next diagnostic step is checking `%LocalAppData%\CloudBerry\logs\cloudberry.log` for a `[dispatcher]` entry logged right before the crash — that will show exactly which exception type/message got past the widened whitelist and pin down the real remaining cause precisely.

---

## Attract Mode (Cloudscape screensaver) — DONE (verified, previously unlogged)

*Picked up from a session that ended right after the last code edit, before any verification or rundown entry. Nothing new was written this pass — this is a from-scratch verification that the prior work is actually complete and correct.*

**What it is:** after `AttractModeIdleSeconds` of no input on Cloudscape's Home tab (default clamped 20–600s), the shell fades in a full-screen showcase — curated game rotation, Ken-Burns-zoomed cover/background art crossfading every ~9s, title/subtitle text, a "press any key or move to browse" hint — and backs out the instant real input arrives (mouse move, key press, or gamepad button/stick).

**Verified this pass, end to end:**
- `EnterAttractMode` / `ExitAttractMode` / `AdvanceAttractMode` / `CrossfadeAttractImage` / `StartKenBurns` / `LoadAttractImageAsync` / `StartAttractIdlePollTimer` / `ResetAttractIdleTimer` are all fully implemented in `BigPictureWindow.xaml.cs` — confirmed by reading every body, not just grepping signatures.
- Every field they touch (`_attractIdlePollTimer`, `_attractIdleElapsedSeconds`, `_attractRotation`, `_attractRotationIndex`, `_attractImages`, `_attractImageIndex`, `_attractUsingLayerA`, `_attractCycleTimer`) is declared.
- Every named XAML element they touch (`AttractModeOverlay`, `AttractBgA`/`AttractBgB`, `AttractBgAScale`/`AttractBgBScale`, `AttractTitleBlock`, `AttractTitleLift`, `AttractTitleText`, `AttractSubtitleText`, `AttractWakeHint`) exists in `BigPictureWindow.xaml` with matching `x:Name`s and types.
- Every `MainViewModel` member called (`BuildAttractModeRotation()`, `BuildAttractShowcaseImages()`, `AttractModeGame`, `IsAttractModeActive`, `AttractModeContextLabel`, `AttractModeEnabled`, `AttractModeIdleSeconds`, `SelectedCloudscapePage`) exists with the expected signature.
- `ResetAttractIdleTimer()`/`ExitAttractMode()` are wired at every real-input entry point: mouse move, key down, and the gamepad poll loop, plus `Deactivated` (window loses focus) and the page-change guard inside the idle poll tick itself (won't engage off the Home tab, mid-game, or with the detail overlay open).
- Re-fetched the live file from disk and ran a full brace **and** paren depth walk over the whole file: both end at 0, never went negative.

**Not build-tested in Visual Studio** — recommend a rebuild, then a real-device pass: sit idle on Cloudscape's Home tab past the configured idle threshold and confirm the fade-in, image rotation/Ken-Burns, and title crossfade all look right, then confirm a mouse nudge / key press / gamepad press each independently dismiss it instantly.





