# 🫐 CloudBerry — Public Roadmap

> **The universal game library built for everyone.**
> One app. Every platform. Every era. Every gamer.

CloudBerry is a free, open, privacy-first game library manager for Windows.
This roadmap is our public contract with the community — every item here is
something we are committed to shipping, exploring, or openly reconsidering based
on your feedback.

**Legend**
| Symbol | Meaning |
|--------|---------|
| ✅ | Shipped / Live |
| 🔨 | In active development |
| 🗓️ | Planned — on the schedule |
| 💡 | Proposed — community input welcome |
| 🔬 | Research phase — feasibility unknown |
| ❌ | Declined — won't be built (reason given) |

---

## Table of Contents

1. [Where We Are Today](#1-where-we-are-today)
2. [v0.3 — Polish & Foundations](#2-v03--polish--foundations-next-release)
3. [v0.4 — Discovery & Smart Library](#3-v04--discovery--smart-library)
4. [v0.5 — Playtime & Stats](#4-v05--playtime--stats)
5. [v0.6 — Collections & Curation](#5-v06--collections--curation)
6. [v0.7 — Emulation First-Class](#6-v07--emulation-first-class)
7. [v0.8 — Social & Sharing](#7-v08--social--sharing)
8. [v0.9 — Themes & Personalization](#8-v09--themes--personalization)
9. [v1.0 — The 1.0 Release](#9-v10--the-10-release)
10. [Post-1.0 Horizon](#10-post-10-horizon)
11. [Platform Integrations Master List](#11-platform-integrations-master-list)
12. [Principles We Won't Compromise](#12-principles-we-wont-compromise)
13. [How to Contribute to the Roadmap](#13-how-to-contribute-to-the-roadmap)

---

## 1. Where We Are Today

CloudBerry is functional and in active daily use. Here is what already works:

### ✅ Core Library
- Drag-and-drop folder scanning — detect PC games and ROMs automatically
- Smart confidence scoring for import candidates (checks EXE signals, folder names, known launchers)
- Manual quick-add for any executable
- Per-game metadata editing (title, platform, status, notes, executable path, Steam App ID)
- Game status labels: `Playing`, `Completed`, `Backlog`, `Dropped`, `Wishlist`
- Favorites starring
- Collection filter pills (dynamic, based on platform / status / tags)
- Sort by title, platform, rating, completion, recently added
- Library search with live filtering

### ✅ Metadata Engine
- Unified aggregated provider: **Steam Store + OpenCritic + Wikipedia** in a single no-key search
- Cover art, background art, screenshots, genre, summary, critic rating, release date, developer
- "Fill Library" batch operation — fetches missing art + descriptions for the whole library
- Auto-enrichment: Steam covers → OpenCritic score → Wikipedia fallback summary

### ✅ Achievements
- Local achievement file scanning (JSON/XML sidecar formats)
- Steam Community public API fallback (zero credentials required)
- Per-game achievement cards with icon, description, rarity, unlock date, Gamer Score value
- Completion progress bars in the game list and detail panel
- Library-wide achievement stats: total unlocked, total possible, global Gamer Score
- Recent unlocks feed on the Home dashboard
- Configurable polling interval for live achievement sync

### ✅ UI / UX
- Custom dark theme with berry/mint/gold accent palette
- LaunchBox-inspired two-panel library layout (list + detail hero)
- Hero banner with floating cover art overhang
- Screenshots horizontal scroll strip
- Platform badges, ROM indicators, running-game dot
- Borderless window with custom title bar and resize borders
- Per-game star rating (1–5) + critic score display
- Spotlight card on Home for quick-launch of selected game
- Status bar with live library summary

### ✅ Emulation
- Per-platform emulator path configuration for 15+ systems
- Per-game emulator override
- ROMs flagged with a distinct badge in the library list
- ROM folder scanner (separate from PC game scanner)

---

## 2. v0.3 — Polish & Foundations *(Next Release)*

> **Theme:** Make every existing feature feel production-grade. No new pages — fix the details that matter.

### UI Refinements
- 🔨 **Animated transitions** between pages and tabs (subtle 120ms fade/slide — no jarring cuts)
- 🔨 **Hover tooltips** on all icon-only buttons and truncated text labels
- 🔨 **Empty states** for every list view — illustrated placeholder with a call-to-action instead of a blank box
- 🔨 **Keyboard navigation** — full Tab order, Enter to launch, arrow keys in the game list, `Ctrl+F` to jump to search
- 🔨 **Context menu** on game list items — right-click shows Launch / Edit / Metadata / Achievements / Open Folder / Remove
- 🗓️ **Splash screen polish** — animated berry logo, version number, loading progress message
- 🗓️ **Window state persistence** — remember size, position, and maximized state between sessions
- 🗓️ **Drag-to-resize** the game list column width in Library (persist preference)

### Metadata Quality
- 🔨 **Image caching to disk** — cover/background images downloaded once, served locally forever. No re-fetch on every launch
- 🗓️ **Manual cover art override** — drag a local image onto the cover slot or paste a URL to set a custom cover
- 🗓️ **Background art override** — same for the hero banner
- 🗓️ **Metadata conflict resolution UI** — when applying from search, show a diff of current vs. incoming fields and let the user choose per-field
- 🗓️ **"Refresh Metadata" per game** — one-click re-fetch without opening the Metadata tab

### Quality of Life
- 🗓️ **Undo for remove game** — toast with a 5-second undo window before permanent delete
- 🗓️ **Bulk operations** — multi-select in library, bulk-assign status / platform / collection
- 🗓️ **Import de-duplication** — detect games already in the library during scan and skip them
- 🗓️ **Save confirmation** — auto-save on navigation away from a dirty game instead of silently losing changes
- 🗓️ **Last-launched game** — surface on Home dashboard as "Continue Playing"

### Performance
- 🗓️ **Virtualised game list** — render only visible rows (VirtualizingStackPanel + recycling) for libraries of 1 000+ games
- 🗓️ **Background metadata fetch** — "Fill Library" no longer blocks the UI; shows a progress overlay
- 🗓️ **Startup time target: < 1 second** for libraries under 500 games

---

## 3. v0.4 — Discovery & Smart Library

> **Theme:** Help users find what to play next and discover things they forgot they owned.

### Smart Recommendations
- 🗓️ **"What to play next" card** on Home — ranks backlog games by genre match to recently played titles
- 🗓️ **"Random Pick" button** — weighted random selection from backlog, excludes recently launched games
- 🗓️ **Genre clusters** — automatic grouping of library games by genre tags pulled from metadata
- 💡 **"Hidden gems"** — surface games in the library with critic scores ≥ 80 that the user hasn't started

### Advanced Search & Filtering
- 🗓️ **Filter panel** (collapsible sidebar in Library) — filter by platform, genre, status, year range, has-achievements, is-ROM, user rating range
- 🗓️ **Saved filter presets** — save a filter set as a named shortcut (e.g., "Unplayed RPGs", "Short games to finish")
- 🗓️ **Full-text search** — search across title, developer, genre, notes, and summary simultaneously
- 🗓️ **Sort by completion %** and by "recently added"

### Metadata Depth
- 🗓️ **Genre tags from metadata** — display as pills on game cards and use in filtering
- 🗓️ **Developer / publisher fields** — sourced from Steam and displayed on game detail
- 🗓️ **ESRB / PEGI rating display** — sourced from metadata where available
- 🗓️ **Trailer links** — link out to YouTube/Steam trailer when available from metadata
- 💡 **Franchise / series grouping** — games from the same franchise auto-grouped as a sub-collection

### Grid View Mode
- 🗓️ **Cover grid view** — toggle between the current list view and a cover-art grid (like GOG Galaxy / Playnite)
- 🗓️ **Adjustable grid tile size** — slider for compact / normal / large covers
- 🗓️ **View preference persisted** per collection

---

## 4. v0.5 — Playtime & Stats

> **Theme:** Every gamer wants to know how much of their life they've put into games. Give them the full picture.

### Playtime Tracking
- 🗓️ **Automatic playtime tracking** — monitor process start/stop and accumulate session time
- 🗓️ **Per-session log** — timestamp, duration, date for every play session
- 🗓️ **Total playtime displayed** on game detail panel and library list rows
- 🗓️ **Manual playtime entry** — let users input historical hours for games played before CloudBerry
- 🗓️ **Import playtime from Steam** — read `localconfig.vdf` for Steam App ID-matched games

### Stats Dashboard (Home)
- 🗓️ **"Hours this week" stat card** — how long the user played in the last 7 days
- 🗓️ **All-time hours played** library total
- 🗓️ **Most played game** spotlight
- 🗓️ **Play streak tracker** — days in a row with at least one play session
- 🗓️ **Yearly wrapped-style summary** — top 5 games, total hours, achievements unlocked, completion milestones
- 💡 **Playtime heatmap calendar** — GitHub-style squares showing play days over the past year
- 💡 **Genre breakdown chart** — pie/bar chart of hours by genre

### Milestones & Notifications
- 🗓️ **In-app toast notifications** for achievement unlocks (detected on polling cycle)
- 🗓️ **Milestone toasts** — "100 hours in your library", "10 games completed", "50 achievements unlocked"
- 💡 **Desktop overlay** — small HUD while a game is running showing session time and recent unlock

---

## 5. v0.6 — Collections & Curation

> **Theme:** Give power users fine-grained control over how their library is structured and shown.

### Custom Collections
- 🗓️ **User-created collections** — create, rename, delete named collections
- 🗓️ **Drag games into collections** from the library list
- 🗓️ **Collections sidebar** — dedicated expandable section in the nav sidebar
- 🗓️ **Smart collections** — rule-based: "All games where genre = RPG and status = Backlog"
- 🗓️ **Collection cover art** — auto-mosaic of the top-4 game covers, or manual image
- 🗓️ **Nested collections** — e.g., `Favourites → PS2 Favourites`

### Tags System
- 🗓️ **Custom per-game tags** — free-text tags (e.g., `co-op`, `short`, `replay-worthy`, `gift`)
- 🗓️ **Tag-based filtering and smart collections**
- 🗓️ **Tag autocomplete** with existing tag suggestions
- 🗓️ **Tag manager** in Settings — bulk rename / merge / delete tags

### Notes & Journals
- 🗓️ **Rich per-game notes** — Markdown-rendered journal entries with timestamps
- 🗓️ **Play diary** — optional session notes attached to individual play sessions
- 💡 **Export journal to Markdown / PDF** — share your writeups

---

## 6. v0.7 — Emulation First-Class

> **Theme:** CloudBerry should be the best emulation frontend for Windows, period.

### Emulator Management
- 🗓️ **Emulator profiles** — name, path, default args, supported file extensions
- 🗓️ **Multiple emulators per platform** — e.g., two PS1 emulators with different configs
- 🗓️ **Per-game emulator + launch args** — full control without touching config files
- 🗓️ **RetroArch core detection** — auto-detect installed cores and map to platforms

### ROM Library
- 🗓️ **ROM-specific metadata** — region tags (USA / EUR / JPN), revision, hack/translation labels
- 🗓️ **ROM hash verification** — SHA-1 check against No-Intro database for validation badges
- 🗓️ **ROM filename cleanup** — parse and strip region/revision/flag codes from display titles
- 🗓️ **CHD / compressed ROM support** — detect `.chd`, `.zip`, `.7z` ROMs and pass correctly to emulators
- 💡 **Save state management** — list, preview, and load save states from within CloudBerry

### Additional Platforms
- 🗓️ **Nintendo Switch (Ryujinx / Suyu)** emulator support
- 🗓️ **Sega Saturn / Dreamcast** (SSF, Flycast) — emulator config templates
- 🗓️ **ScummVM** integration for classic adventure games
- 🗓️ **DOSBox** integration for DOS titles
- 💡 **Xbox 360 (Xenia)** support
- 💡 **PlayStation 3 (RPCS3)** support

---

## 7. v0.8 — Social & Sharing

> **Theme:** Games are better together. Let users show off and connect.

### Export & Sharing
- 🗓️ **Library export to JSON / CSV** — full data dump for backup or third-party tools
- 🗓️ **Shareable library card** — generate a static image summary: top games, total hours, completion %, gamer score
- 🗓️ **Achievement showcase image** — shareable PNG of recent unlocks and rarest achievements
- 💡 **Public profile page** — optional hosted page at `cloudberry.app/u/username` showing stats

### Import from Other Launchers
- 🗓️ **Import from Steam** — read installed games via Steam library VDF
- 🗓️ **Import from GOG Galaxy** — scan GOG Galaxy 2 database
- 🗓️ **Import from Epic Games Store** — read EGS manifest folder
- 🗓️ **Import from Xbox App** — scan Xbox/MS Store installed apps
- 🗓️ **Import from Lutris** (Linux-origin, Wine games on Windows)
- 💡 **Import from Playnite** — migrate full library including metadata

### Backup & Sync
- 🗓️ **One-click library backup** — ZIP of all JSON data + cached images
- 🗓️ **Scheduled auto-backup** to a user-specified folder (e.g., Dropbox, OneDrive)
- 💡 **Multi-device sync** via a self-hosted or optional cloud endpoint

---

## 8. v0.9 — Themes & Personalization

> **Theme:** CloudBerry should feel like *your* app, not ours.

### Theme Engine
- 🗓️ **Built-in theme presets**: Dark (current), Light, AMOLED Black, Soft Purple, Forest Green, Ocean Blue
- 🗓️ **Accent colour picker** — choose any hex colour for the berry/mint/gold accents
- 🗓️ **Font selector** — choose from bundled fonts (Bahnschrift, Segoe UI, Inter, JetBrains Mono)
- 🗓️ **Background art intensity** — slider for how much the hero banner bleeds through
- 💡 **Custom theme import** — JSON theme file with full colour token override
- 💡 **Community theme gallery** — in-app browser of community-submitted themes

### Layout Options
- 🗓️ **Compact mode** — smaller row heights, tighter padding for power users
- 🗓️ **Sidebar collapse** — icon-only rail mode for more horizontal content space
- 🗓️ **Home page widgets** — choose which stat cards appear and in what order
- 🗓️ **Detail panel position** — right panel (current) or bottom drawer mode

### Accessibility
- 🗓️ **High contrast mode** compatibility
- 🗓️ **Larger text / UI scale option** — 100% / 125% / 150%
- 🗓️ **Reduce motion option** — disable all transitions and animations
- 🗓️ **Screen reader support** — AutomationProperties on all interactive elements

---

## 9. v1.0 — The 1.0 Release

> **Theme:** Stable, polished, well-documented, and ready for the world.

### Stability & Quality
- 🗓️ **Zero known P0/P1 bugs** at ship
- 🗓️ **Full automated test suite** — unit tests for Core, integration tests for metadata providers
- 🗓️ **Crash reporter** — opt-in, anonymised exception reporting with one-click feedback
- 🗓️ **Library corruption recovery** — detect and repair broken JSON data files automatically

### Onboarding
- 🗓️ **First-run setup wizard** — step-by-step: scan folder, pick emulators, fetch metadata
- 🗓️ **Interactive feature tour** — contextual coach marks on first use of each page
- 🗓️ **In-app help panel** — searchable inline documentation, no browser required
- 🗓️ **Keyboard shortcuts reference** — `?` to open a shortcuts cheat sheet

### Distribution
- 🗓️ **Signed MSIX installer** — one-click install from the CloudBerry website
- 🗓️ **Winget package** — `winget install CloudBerry`
- 🗓️ **Auto-updater** — background check, one-click update from within the app
- 🗓️ **GitHub Releases** — versioned ZIP for portable use (no install required)
- 💡 **Microsoft Store listing**

### Documentation
- 🗓️ **Public docs site** — full user guide, FAQ, emulator setup guides per platform
- 🗓️ **Architecture / contributor guide** — for open-source contributors
- 🗓️ **Changelog** — human-readable, in the app and on the website

---

## 10. Post-1.0 Horizon

These are ideas we are excited about but haven't committed to scheduling yet.
Community interest will heavily influence what gets prioritised here.

### 🔬 Plugin / Extension API
Allow third-party developers to write CloudBerry plugins in C# or via a scripting API.
Use cases: custom metadata scrapers, launcher integrations, overlay extensions, stats exporters.

### 🔬 CloudBerry Companion (Mobile App)
A read-only iOS / Android companion showing your library, stats, achievements, and a play queue.
Would require the optional sync backend (see v0.8).

### 🔬 Controller / Big Picture Mode
A full-screen 10-foot UI optimised for gamepad navigation and couch use. Think Steam Big Picture
but for your whole library, not just Steam games.

### 🔬 AI Game Recommendations
On-device ML model (no cloud, no tracking) trained on the user's own play history, ratings,
and genres to suggest what to play next with explainable reasoning.

### 🔬 Linux / Steam Deck Port
A Gtk4 / Avalonia port of the UI running on Linux, with Proton/Wine-aware launcher detection
and native Steam Deck optimisation.

### 🔬 Multiplayer Game Night Planner
Given two CloudBerry library exports, find the overlap of owned co-op games and suggest
what you and your friend can play together tonight.

### 💡 Price Tracker Integration
Show historical price data (from IsThereAnyDeal / ITAD) for wishlist games. Alert when a
wishlisted game drops below a user-set price target.

### 💡 Speedrun.com Integration
Show world records and personal bests for games where speedrun.com data is available.
Auto-link your submission after a timed session if you're a runner.

### 💡 RetroAchievements Support
Sync achievements from RetroAchievements.org for emulated games — the missing achievement
layer for classic consoles.

---

## 11. Platform Integrations Master List

| Platform | Metadata | Library Import | Launch | Achievements | Status |
|----------|----------|---------------|--------|-------------|--------|
| **PC (any EXE)** | ✅ Steam/OC/Wiki | ✅ folder scan | ✅ | ✅ steam fallback | Live |
| **Steam** | ✅ | 🗓️ v0.8 | ✅ via exe | ✅ | Partial |
| **GOG Galaxy** | 🗓️ | 🗓️ v0.8 | ✅ via exe | 💡 | Planned |
| **Epic Games Store** | 🗓️ | 🗓️ v0.8 | ✅ via exe | ❌ no public API | Planned |
| **Xbox / MS Store** | 🗓️ | 🗓️ v0.8 | ✅ via exe | 💡 | Planned |
| **NES / Famicom** | ✅ | ✅ ROM scan | ✅ emulator | 💡 RetroAch | Live |
| **SNES** | ✅ | ✅ | ✅ | 💡 | Live |
| **Nintendo 64** | ✅ | ✅ | ✅ | 💡 | Live |
| **Game Boy / GBC / GBA** | ✅ | ✅ | ✅ | 💡 | Live |
| **Nintendo DS / 3DS** | ✅ | ✅ | ✅ | 💡 | Live |
| **GameCube / Wii** | ✅ | ✅ | ✅ | 💡 | Live |
| **Nintendo Switch** | 🗓️ | ✅ ROM scan | 🗓️ Ryujinx | 💡 | v0.7 |
| **PlayStation 1** | ✅ | ✅ | ✅ DuckStation | 💡 | Live |
| **PlayStation 2** | ✅ | ✅ | ✅ PCSX2 | 💡 | Live |
| **PSP** | ✅ | ✅ | ✅ PPSSPP | 💡 | Live |
| **PS3** | 🔬 | ✅ ROM scan | 💡 RPCS3 | 💡 | Research |
| **Sega Mega Drive** | ✅ | ✅ | ✅ BlastEm | 💡 | Live |
| **Sega Saturn** | 🗓️ | ✅ | 🗓️ SSF | 💡 | v0.7 |
| **Dreamcast** | 🗓️ | ✅ | 🗓️ Flycast | 💡 | v0.7 |
| **Xbox 360** | 🔬 | ✅ | 💡 Xenia | 💡 | Research |
| **DOS / ScummVM** | 🗓️ | ✅ | 🗓️ | 💡 | v0.7 |

---

## 12. Principles We Won't Compromise

These are the non-negotiables that define what CloudBerry is and isn't.
We list them publicly so you can hold us to them.

### 🔒 Local-first, always
Your library data lives on your machine in plain JSON files.
CloudBerry will never require an account, a login, or a cloud connection to function.
Every feature works fully offline.

### 🆓 Free, forever
CloudBerry will always have a fully functional free tier.
If we ever add a paid tier it will be for optional convenience features (sync, cloud backup)
and will never lock away core library management, metadata, or achievements.

### 🚫 No telemetry without consent
We will never silently collect usage data.
Any opt-in crash/analytics reporting will be clearly labelled, easy to disable,
and the collected data will be publicly documented.

### 🔑 No mandatory API keys
You should never need to register anywhere or obtain credentials to use core CloudBerry features.
Metadata, achievements, and library management all work out of the box.

### 📖 Open source
CloudBerry's source code is and will remain publicly available.
You can audit it, fork it, and contribute to it.

### ⚡ Performance is a feature
We target < 1s cold start, < 100ms UI response to user input, and smooth 60fps animations.
We will never ship a feature that meaningfully degrades these targets for typical library sizes.

### 🎮 Every platform counts
PC, retro, ROM, emulator — no gamer is a second-class citizen in CloudBerry.
We will not optimise only for Steam users.

---

## 13. How to Contribute to the Roadmap

We want this roadmap to reflect the community's priorities, not just ours.

**Vote on features**
Open a GitHub Discussion on any roadmap item tagged `roadmap` and upvote the ones you care about.
The items with the most engagement move up the schedule.

**Propose a new feature**
Open a GitHub Issue with the `feature-request` label.
Include: what problem it solves, who it helps, any design ideas you have.
If it gets traction, it will be evaluated for a future milestone.

**Report a bug**
Open a GitHub Issue with the `bug` label.
P0/P1 bugs (crashes, data loss) are prioritised above all roadmap work.

**Contribute code**
Read `CONTRIBUTING.md` for architecture guidance, coding standards, and the PR process.
All roadmap items tagged `help-wanted` are fair game for community PRs.

**Discuss priorities**
Join the Discord (`discord.gg/cloudberry`) — the `#roadmap` channel is where we discuss
what gets scheduled next.

---

> Last updated: **April 2026**
> Maintained by the CloudBerry core team.
> This document is versioned in the repository root at `ROADMAP.md`.

*Built with care for every gamer — casual, hardcore, retro, completionist.*
*If it's a game, it belongs in CloudBerry.*
