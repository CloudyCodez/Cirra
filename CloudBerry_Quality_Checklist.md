# CloudBerry Quality Checklist

Purpose: a living checklist for feature work, polish, fixes, and UIX passes that make CloudBerry easier to understand, easier to trust, and harder to outgrow.

## Pass 1 - Public Clarity And First-Run Trust

- [x] Replace unclear empty-library ornamentation with a stable system icon.
- [x] Give first-time users two obvious starts: scan a folder or add one game manually.
- [x] Tighten Home, Library, Metadata, Achievements, Friends, Settings, and Emulation page subtitles into plain-language outcomes.
- [x] Make Home callouts explain what the user can do next, not just what the app contains.
- [ ] Review all page empty states for the same pattern: short heading, one-sentence reason, primary action, secondary action.
- [ ] Audit button labels for verb-first clarity: Scan, Add, Launch, Sync, Save, Clean, Export.
- [ ] Replace enthusiast-only terms with public-facing labels where possible, keeping advanced detail in tooltips.

## Pass 2 - Home Library Health

- [x] Promote the hidden product-readiness engine into a Home Library Health card.
- [x] Summarize the next best fix in plain language instead of making users inspect a settings page.
- [x] Add one-click health check and safe-test preparation actions from Home.
- [x] Show a compact latest readout with status dots for Ready, Warning, and Action states.
- [x] Reduce the Home card back to a calm summary strip so the page does not become a diagnostics wall.
- [x] Fix the readiness label so a checked 0% score is not confused with an unchecked state.
- [x] Add a direct fix button for the top health issue, such as Clean Missing, Fill Metadata, or Map Emulators.
- [x] Add a last-checked timestamp so users know whether the health readout is fresh.
- [x] Add a "why this matters" tooltip for public users who do not know what launch paths or metadata coverage mean.

## Pass 3 - All-Around De-Clutter And Polish

- [x] Rename maintenance actions so they read as user tasks: Scan Everything, Repair Paths, Polish Titles, Clean Missing Games.
- [x] Replace prototype-style `game(s)` / `title(s)` / `result(s)` copy with natural pluralized text.
- [x] Shorten achievement tracking copy so Settings explains behavior instead of implementation details.
- [x] Remove internal "test pass" wording from health/backup statuses.
- [x] Fix a fire-and-forget dispatcher warning in startup pre-warm code.
- [x] Optimize the library health count pass so it walks the game collection once and tracks duplicates in a dictionary.
- [x] Continue reducing long status strings that can crowd the status bar.
- [ ] Review Settings sections for long explanatory paragraphs that can move into tooltips.
- [ ] Review Home for repeated calls to action and remove anything that competes with Launch, Scan, and Search.

## Feature Candidates

- [ ] Cross-library "what can we play together" matcher using Cloud Codes and local library overlap.
- [ ] Save-state browser with thumbnails for emulated games.
- [ ] Duplicate ownership detector that merges Steam, launcher, and ROM copies into one title surface.
- [ ] "Almost there" achievement widget for games with a small number of likely remaining unlocks.
- [ ] One-click portable library export with JSON plus cached artwork.
- [ ] Diff-based automatic backups with restore previews.

## Polish Passes

- [x] Home: add a guided "Library Health" card that shows next best cleanup action.
- [ ] Library: improve filter summaries so users know exactly why results changed.
- [ ] Metadata: separate beginner "Auto-fill missing art and summaries" from advanced provider comparison.
- [ ] Achievements: make provider setup states easier to diagnose before a sync fails.
- [ ] Emulation: add setup confidence badges for each platform card.
- [ ] Cloudscape: ensure controller-first labels and focus states are consistent across every page.

## Fix And Reliability Passes

- [ ] Run a pass over status messages for recoverable errors with clear next steps.
- [ ] Add regression tests for scan/import edge cases that can silently create duplicate records.
- [ ] Verify all external metadata/image operations fail gracefully offline.
- [ ] Check all maintenance actions have preview, confirmation, or undo when destructive.
- [ ] Ensure launch-session tracking clearly explains when a process cannot be tracked.

## UIX Standards For Every Pass

- [ ] Headings should name the job, not the internal system.
- [ ] Body copy should fit in one breath and avoid jargon unless the user asked for advanced controls.
- [ ] Each empty state should include the next action.
- [ ] Every busy state should say what is happening and whether it is safe to leave it running.
- [ ] Important actions should stay reachable without hunting through settings.
- [ ] Home should favor one-line summaries and progressive disclosure over dense diagnostic lists.
- [ ] Text contrast and wrapping should be checked at the minimum supported window size.
