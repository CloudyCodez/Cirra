# 🫐 CloudBerry — Differentiator Ideas

> Features that would set CloudBerry apart from Playnite, LaunchBox, and GOG Galaxy —
> not just parity features, but things that give someone an actual reason to switch.

This list is deliberately separate from `ROADMAP.md`. Anything here that gets scheduled
should graduate into the real roadmap with a version tag; until then this is a scratchpad
of "what would make CloudBerry undeniably better," ranked by how unique the idea is rather
than by ship order.

**Legend** *(same convention as `ROADMAP.md`)*
| Symbol | Meaning |
|--------|---------|
| ✅ | Shipped / Live |
| 🔨 | In active development |
| 🗓️ | Planned — on the schedule |
| 💡 | Proposed — community input welcome |
| 🔬 | Research phase — feasibility unknown |

---

## 1. True Differentiators *(nobody else does these well)*

- ✅ **Backlog debt tracker** — instead of a flat "Backlog" status pill, surface a running
  "$X / Y hours sitting unplayed" number, cost-per-hour for owned-but-untouched games, and a
  gentle "oldest untouched purchase" callout. Turns guilt into a fun stat instead of a nag.
  Shipped: `GameRecord.PurchasePriceUsd` (manual, optional, USD) flows through
  `GameEntryViewModel` (`PurchasePriceInput` text-proxy for the detail-panel editor,
  `PurchasePriceLabel`, `CostPerHourLabel`, `IsBacklogDebtEligible` = unplayed + priced + zero
  playtime) into `MainViewModel` (`BacklogDebtTotalUsd`, `BacklogDebtGameCount`,
  `BacklogDebtLabel`, `OldestUntouchedGame`/`OldestUntouchedLabel`, all recomputed in the same
  per-game pass as the existing `BacklogCount`). UI: an editable "Price Paid (USD)" field next
  to Status in the Library detail editor, a read-only Price Paid / Cost-per-Hour stat-card pair
  under Playtime/Last Played (only shown once a price is set), and a gold-accented Home page
  callout between the hero stats and Quick Actions (only shown once at least one game qualifies,
  so a fresh install stays quiet).
- 💡 **Cross-library "what can we play together" matcher** — two CloudBerry libraries (via the
  existing Friend Code / Cloud Code system) find co-op/multiplayer overlap instantly. Already
  hinted at in `ROADMAP.md` Post-1.0 Horizon — worth pulling forward since the Friends system
  already exists.
- ✅ **Local, on-device "why this recommendation" engine** — not just "what to play next," but a
  transparent one-liner, e.g. *"Because you 92%-completed Hades and haven't touched a
  roguelike in 40 days."* Zero cloud, fully explainable.
  Shipped: the existing `RecommendedGame`/`RecommendedGameReason` scoring engine
  (`ScoreRecommendation`) was already solid and untouched; `BuildRecommendationReason` was
  rewritten from a comma-joined bag of generic clauses into a priority ladder that leads with
  the single most specific, numbers-driven signal — exactly the flagship example above.
  Priority order: (1) a *different* highly-completed (65%+) game in the same genre that's gone
  cold 14+ days (the cross-reference pattern, e.g. "Because you're 92% through Hollow Knight and
  haven't touched a Metroidvania in 41 days"), (2) a genre-level cold streak even without a
  completion anchor, (3) never-played + strong critic score, (4) the original recent-genre-streak
  match, (5) condensed structural fallbacks (favorite/installed/first-session). New helpers
  `DaysSinceGenrePlayed` (per-genre last-played lookup across the whole library) and
  `WithArticle` ("an RPG" vs "a Platformer", including acronym pronunciation) support it. No UI
  changes needed — `RecommendedGameReason` already renders wherever the pick shows.
- ✅ **Session replay timeline** — a visual timeline of *when* you played (time of day, day of
  week, session length distribution), not just a raw hours total. Makes Year Wrapped feel
  alive year-round instead of a once-a-year novelty.
  Shipped: `WrappedInsights` (`MainViewModel.cs`) gained `TimeOfDayMinutes` (24 hourly buckets),
  `DayOfWeekMinutes` (7 buckets), `SessionLengthCounts` (<15m/15-30m/30-60m/1-2h/2h+ buckets),
  and `PeakHourLabel`, all computed in the same per-session pass `GetWrappedInsights()` already
  ran. `GetWrappedInsights(year)` now treats `year <= 0` as an **all-time** request (no year
  filter) via a new `IsAllTime` flag. `WrappedWindow` gained an `allTime` constructor parameter;
  in that mode the 53-week heatmap (which only makes sense for a single year) is skipped in
  favor of three new bar charts — Time of day, Day of week, Session length — rendered with the
  same vanilla-JS/WebView2 technique already used for the heatmap. New **Play Patterns** button
  added next to the existing Home page Customize button, opening `WrappedWindow` in all-time
  mode — this is what actually delivers the "alive year-round" part of the idea, since it was
  previously only reachable, if at all, from inside the once-a-year Wrapped recap. While in
  there: also fixed **Year Wrapped itself having no UI entry point at all** — `OpenYearWrappedClick`
  existed in `MainWindow.xaml.cs` but nothing in `MainWindow.xaml` called it; added a matching
  "Year Wrapped" button alongside Play Patterns.
- 💡 **"Second life" detection** — flag 100%-completed games that received a major
  update/DLC/remaster since completion, and resurface them on Home.

## 2. Emulation — Lean Into Being the Best

- 🗓️ **Save state browser with thumbnails** — already flagged 💡 in `ROADMAP.md` v0.7; promote
  to a headline feature, not an afterthought. Visual save-state picking is the #1 thing
  RetroArch's own UI does badly.
- 💡 **Region/version-aware "best version" auto-pick** — when multiple ROM copies of the same
  game exist (USA/EUR/JPN, revisions), auto-suggest the best one and collapse duplicates on
  shelves by default.
- 💡 **Controller-first setup wizard** — guided flow that maps a controller to both CloudBerry
  *and* RetroArch cores in one pass, since that's the biggest emulation-frontend support
  headache.

## 3. Achievements — Go Deeper Than Unlocked/Locked

- 💡 **Rarity-weighted "true completion %"** — a global score adjusted for achievement
  difficulty/rarity, so 100%-ing an easy game doesn't outweigh 60%-ing a brutal one.
- 💡 **Cross-platform achievement unification** — Steam + RetroAchievements + local sidecar
  files shown as one unified profile timeline instead of siloed per source. Pairs naturally
  with the planned RetroAchievements sync in `ROADMAP.md`.
- 💡 **"Almost there" surfacing** — a Home widget for achievements 1–2 actions away
  (percentage/rarity heuristic), giving completionists a natural next-session hook.

## 4. Identity & Presentation — Build on Cloudscape and Wrapped

- 💡 **Ambient "attract mode"** for Cloudscape — after N minutes idle in Big Picture, cycle a
  Ken-Burns showcase of the library like a screensaver. Cheap to build, very premium-feeling.
- 💡 **Shareable animated Wrapped card** — short MP4/GIF export instead of a static image, built
  for Discord/Twitter sharing. Organic-growth lever the competition ignores entirely.
- 💡 **Live "Now Playing" rich presence** — optional always-on-top overlay or Discord Rich
  Presence integration showing current game + achievement progress, sourced from data already
  tracked.

## 5. Data Ownership — Sharpen the Strongest Philosophical Edge

- 💡 **One-click "leave Steam behind" export** — portable, human-readable archive (JSON +
  cached art) explicitly positioned as "yours forever, no launcher required to read it."
- 💡 **Diff-based automatic backups** — versioned snapshots of the library JSON (mini git
  history) so a bad metadata fetch or accidental bulk-edit is one click to revert.
- 🔬 **Sandboxed local plugin API** — even a minimal read-only "stats exporter" hook early would
  seed a community ecosystem before Playnite's much larger one becomes an unbeatable moat.

## 6. Smaller but Sharp

- ✅ **"Finish it" mode** — a filtered Library view: games with high completion (achievement % or
  status) that haven't been played in 30+ days, sorted by how close they are to done. Shipped
  as a new option in the existing Achievement filter dropdown (`SelectedAchievementFilterKey =
  "finishit"`): 70%+ achievement completion, not 100%, no play session in the last 30 days.
- 💡 **Duplicate-ownership detector** — same game across Steam + GOG + a ROM copy merged into
  one library entry with source badges, instead of three separate rows.
- ✅ **Price-per-hour stat** — folded into the Backlog Debt Tracker above (`CostPerHourLabel`)
  rather than shipped separately, since it's the same $/hour math on the same data.

---

> This is a working scratchpad, not a commitment. Items get promoted into `ROADMAP.md` with a
> real version tag once they're actually scheduled.
