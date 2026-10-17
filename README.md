# Cirra

A premium universal game library for Windows. One place for your Steam games, ROM collections,
and whatever else is scattered across launchers, with real emulation support and
achievement tracking baked in instead of bolted on.

Built with WPF on .NET 10.

## What it does

- **Library scanning** across Steam, standalone launchers, and ROM folders, with metadata
  pulled from IGDB, RAWG, OpenCritic, PCGamingWiki, Steam Store, and Wikipedia.
- **Emulation Station** page - per-platform emulator cards (RetroArch core detection,
  manual browse/clear, "use detected" one-click setup) instead of digging through a
  settings menu every time you add a system.
- **Achievements**, synced and shown as an in-app unlock feed, with a "Finish It" filter
  that surfaces games you're 70%+ through but haven't touched in a month.
- **Manual collections** - name a shelf, pick the games, done. No auto-sorting by genre
  guessing what you meant.
- **Year Wrapped / Play Patterns** - a Spotify-Wrapped-style yearly recap, plus an
  all-time view of when you actually play (time of day, day of week, session length).
- **Backlog debt tracker** - turns "$40 game, 0 hours played" into an actual number
  instead of a guilty feeling you ignore.
- **Horizon mode** for couch use, plus a set of distinct visual, motion, and audio themes.

Longer-term plans and half-baked ideas live in `ROADMAP.md` and `DIFFERENTIATORS.md`.

## Project layout

```
src/
  CloudBerry.App/    WPF front end (windows, view models, themes, effects)
  CloudBerry.Core/   Everything else - scanning, metadata providers, achievements,
                      library storage, emulator detection
tests/
  CloudBerry.Core.Tests/
```

## Building it

Requires the .NET 10 SDK and Windows (WPF doesn't run anywhere else).

```
dotnet build CloudBerry.slnx
```

To run it locally:

```
run_dev.bat
```

To produce a self-contained single-file release build:

```
publish.bat
```

This drops `Cirra.exe` in `publish\win-x64` - no .NET runtime install required on
the machine running it.

## Contributing

This is currently a solo/small-scale project, not a big open contributor base - if
you're poking around the code, `rundown.md` has a running log of what's been worked on
recently and why, which is probably more useful than the commit history for figuring
out the reasoning behind anything that looks odd.

## License

Not yet decided / not currently licensed for redistribution. Ask before you fork this
for anything beyond personal tinkering.
