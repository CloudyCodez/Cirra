# CloudBerry Installer Roadmap

This file is the handoff point for the next agent continuing CloudBerry's installable-product push.

## Current state

- CloudBerry already stores its mutable data in `%LocalAppData%\CloudBerry`:
  - library: `%LocalAppData%\CloudBerry\library.json`
  - media cache: `%LocalAppData%\CloudBerry\media`
  - logs: `%LocalAppData%\CloudBerry\logs\cloudberry.log`
- That means the app is already in decent shape for a per-user installer. We do **not** need a large data-path migration just to stop installing from the repo.
- App identity metadata has been added to [CloudBerry.App.csproj](C:\Users\conno\Documents\CloudBerry\src\CloudBerry.App\CloudBerry.App.csproj):
  - `Product`
  - `Company`
  - `Description`
  - `Version`
  - `AssemblyVersion`
  - `FileVersion`
  - `InformationalVersion`
- Update package pipeline is now live:
  - installer build wrapper supports `build_installer.bat update1-1`
  - staged update packages are copied to `%LocalAppData%\CloudBerry\updates`
  - a local update handoff exists in Settings so the app can surface and launch staged installers

## Completed in the 1.1 sweep

- Installable app flow is in place with Inno Setup
- A packaged update artifact can be generated as `update1-1.exe`
- Settings now includes an `Installation & Updates` section with:
  - current version
  - install folder
  - staged package status
  - install/update folder actions
- Settings now includes an `Audio & Presence` section with:
  - audio enable toggle
  - audio profile selector
  - cache/log shortcuts
- Theme-aware procedural sound cues now support:
  - launch handoff
  - metadata apply confirmation
  - achievement unlock feedback
  - focus movement in Cloudscape
- Cloudscape now has:
  - theme-specific hero motion timing
  - subtle hero parallax
  - stronger focus movement feedback
- Shared premium card treatments now cover:
  - feature/detail cards
  - media asset cards
  - command tiles
  - installation/update panels

## Installer scaffolding added

- Inno Setup script:
  - [tools/installer/CloudBerry.iss](C:\Users\conno\Documents\CloudBerry\tools\installer\CloudBerry.iss)
- Installer build wrapper:
  - [tools/installer/build_installer.bat](C:\Users\conno\Documents\CloudBerry\tools\installer\build_installer.bat)
  - supports custom output names, for example `build_installer.bat update1-1`
- Publish script update:
  - [publish.bat](C:\Users\conno\Documents\CloudBerry\publish.bat) now supports `--no-explorer`

## Intended install behavior

- Per-user install path:
  - `%LocalAppData%\Programs\CloudBerry`
- No admin required by default
- Start menu shortcut
- Optional desktop shortcut
- Self-contained published app copied from:
  - `publish\win-x64`
- Installer output target:
  - `publish\installer`

## What still needs to be done

### 1. Compile and validate the installer

The current workspace did not have `ISCC.exe` available on `PATH` or in the default Inno Setup install locations during this pass.

Once Inno Setup 6 is installed, run:

```bat
tools\installer\build_installer.bat
```

Then validate:
- install over a clean machine or user profile
- launch from Start menu
- desktop shortcut behavior
- uninstall behavior
- upgrade install over an existing version

### 2. Add first-run install polish

Recommended next steps:
- first-run welcome/setup flow
- optional launcher/Steam/Epic scan prompt
- optional emulator detection prompt
- optional theme selection prompt

### 3. Add upgrade and support infrastructure

Recommended:
- persistent app version surface in Settings/About
- export diagnostics button
- open log folder button
- backup-before-upgrade messaging
- local staged update handoff from Settings when a package is present

### 4. Consider code signing

For a genuinely shippable Windows feel:
- sign `CloudBerry.exe`
- sign the installer

That will reduce SmartScreen friction and make the install flow feel much more legitimate.

### 5. Decide whether to stay on Inno Setup or move to MSIX later

Current recommendation:
- stay with Inno Setup first because it is flexible and fast
- revisit `MSIX` only after CloudBerry's install/update story is stable

## Follow-up engineering ideas

- Add an `About / Installation` section in Settings showing:
  - installed path
  - data path
  - cache path
  - log path
  - current version
- Add a `Check for updates` stub so updater work has a natural home later
- Add installer tasks for:
  - launch after install
  - desktop icon
  - optional Start menu folder behavior

## Premium flare roadmap

Once the installer path is stable, the next agent should continue on the authored-premium polish track without slipping into noisy or procedural-looking effects.

### Add more flare without making it tacky

- theme-specific transition packs
  - each theme should have its own timing, easing, and selection cadence
  - `NieR` stays restrained and precise
  - `Wuthering Waves` can feel softer and more cinematic
  - `Matrix` gets sharper digital motion
  - `Glitch` gets controlled interference, not random chaos

- subtle parallax on hero/media surfaces
  - only on large feature art
  - keep movement low-amplitude and smooth
  - avoid making text or controls swim

- cleaner focus states and controller-first motion in Cloudscape
  - stronger selected-card framing
  - directional movement that feels intentional with keyboard/controller input
  - gentler shelf momentum and focus carry

- richer hover states on cards and action rows
  - not bigger for the sake of bigger
  - prefer cleaner lift, border response, and light treatment

- animated art crossfades when changing selection
  - especially on Home hero, Library detail hero, and Metadata previews
  - keep them fast and elegant

- sound design toggles per theme/profile
  - subtle UI profile
  - quiet cinematic profile
  - retro/digital profile for themes like Matrix or Glitch

- better “alive” moments
  - launch handoff feedback
  - metadata apply confirmation
  - achievement pop styling
  - these should feel authored and satisfying, not flashy for its own sake

### Performance guardrails for the flare pass

- every new effect should be tested against scrolling smoothness
- avoid always-running storyboards unless they add real value
- prefer transition-on-state-change over constant ambient animation
- image-heavy transitions should stay async and avoid blocking the UI thread

## Notes for the next agent

- The user wants CloudBerry to feel like a real, polished shipped product, not just a repo-run utility.
- Keep visuals tasteful. They strongly dislike anything that reads as fake, random, or procedurally generated.
- Prefer practical productization wins over speculative platform rewrites.
- If you add installer UI polish, keep it clean and authored.
