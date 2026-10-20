# Playnite 10.63 Reverse-Engineering Report

Date: October 19, 2026
Target: the locally installed Playnite build under `C:\Users\conno\AppData\Local\Playnite`

## Purpose And Boundaries

This report maps Playnite's shipped architecture so Cirra can learn from proven product boundaries and workflows. It does not copy Playnite code, assets, layouts, branding, or proprietary implementation details. The useful output is a set of independently implementable architectural and product lessons.

The primary managed assemblies were inventoried completely at the metadata/member level. Selected systems were then inspected more deeply through their types, methods, call edges, fields, and string use. Static evidence establishes that a responsibility exists and how it is divided; it does not prove runtime quality, performance, or every branch of behavior.

## Executive Summary

Playnite's strongest lesson is not any single feature. It is the way the product separates concerns:

- a shared library/data engine owns games, taxonomy, import exclusions, scanner settings, metadata, launch actions, backups, updates, controllers, and extensions;
- desktop and fullscreen are distinct presentation applications with their own view models, controls, settings, and navigation behavior;
- a public SDK makes databases, metadata providers, launch/install controllers, search, menus, panels, controller events, and fullscreen settings explicit extension contracts;
- the theme system separates desktop and fullscreen packages and exposes versioned manifests, resource dictionaries, views, control styles, audio, and button prompts;
- import is modeled as a configurable pipeline with exclusions and a record of imported ROM/executable artifacts, rather than as a blind folder-to-list operation;
- fullscreen owns controller discovery, repeat/navigation behavior, focus, scrolling, audio, and resource release as first-class lifecycle concerns.

Cirra should adopt those boundaries while keeping its own identity, modern .NET 10 base, stronger visual system, local-first privacy, guide library, physical collection presentation, and Horizon experience.

## Artifact Identity

| Artifact | SHA-256 | Observed runtime | Assembly version | Metadata inventory |
| --- | --- | --- | --- | --- |
| `Playnite.dll` | `4acd8e311d4dddf9c457550fbbf796b1ea1c5b37d307dbfaf85a1815b55094c2` | .NET Framework 4.6.2, x86 CIL | 10.63.0.0 | 1,201 types, 8,634 methods |
| `Playnite.DesktopApp.exe` | `4fa131e555660bdb8a4c3f5d32e605d7fb3ef884e93ba6582a569bcb1a08e8a9` | .NET Framework 4.6.2, x86 CIL | file version 1.0.0.0 | 409 types, 3,848 methods |
| `Playnite.FullscreenApp.exe` | `6e94f4e026284bfc6e4ee510676ba240d42f27e9224bcd3cdd016ebf48a869cd` | .NET Framework 4.6.2, x86 CIL | file version 1.0.0.0 | 159 types, 1,179 methods |
| `Playnite.SDK.dll` | `73e07635701fc0e944d6b10a69727fb050e0105b18da7522cf0f8cc9e366aed2` | .NET Framework 4.6.2, AnyCPU CIL | 6.18.0.0 | 254 types, 1,856 methods |

The observed `Playnite.dll` references LiteDB 4.1.4, Playnite.SDK 6.18, WPF, image scaling/processing libraries, archive handling, YAML, logging, Chromium embedding, and controller/native support. This is dependency evidence, not a recommendation to duplicate the same package choices.

## Application Topology

### Shared engine

`Playnite.dll` is the common engine. Its type inventory contains the database, import/scanner, metadata, controller, theme, extension, backup, update, image/cache, emulation, and filtering systems consumed by both user interfaces.

The engine is not a single generic service. It exposes bounded areas such as `GameDatabase`, `DatabaseFilter`, `GameScanner`, `MetadataDownloader`, `ExtensionFactory`, `GameControllerManager`, `ThemeManager`, `Backup`, and `Updater`. This makes the main workflows independently testable and reusable by more than one shell.

### Desktop application

The desktop executable has a large operational surface with dedicated view models and controls for:

- library grid, list, and detail views;
- filtering, grouping, sorting, search, sidebar, and top panel;
- game editing, metadata comparison, image selection, and metadata downloads;
- emulator and emulated-game import;
- integrations, add-ons, settings, statistics, tools, and notifications;
- separate appearance settings for grid, list, details, layout, and top panel.

`DesktopAppViewModel` is broad, but specialist view models exist for high-complexity workflows such as `GameEditViewModel`, `MetadataComparisonViewModel`, `EmulatedGamesImportViewModel`, and `DatabaseFieldsManagerViewModel`.

### Fullscreen application

Fullscreen is a separate executable and presentation model, not a desktop view stretched to a television. Its inventory includes:

- a `FullscreenApplication` that initializes native/controller/audio resources, opens the main view, switches app mode, and releases resources;
- a `FullscreenAppViewModel` that owns selection, filters, menus, notifications, focus, controller connection changes, view changes, and window state;
- specialized game details, game menu, main menu, filters, settings, text input, notification, and dialog views;
- a virtualizing tile panel with explicit visible-range, cleanup, extent, item-size, centering, line/page/wheel scrolling, and make-visible behavior;
- fullscreen-specific input, visual, audio, layout, menu, controller, image-loading, smooth-scroll, and navigation-speed settings.

This is the most relevant Playnite lesson for Horizon: couch mode needs its own interaction model, lifecycle, density, navigation map, and performance behavior even when it shares the same domain data.

## Library And Data Engine

The database surface separates games from reusable taxonomy and configuration collections. Observed collections include games, platforms, emulators, genres, companies, tags, categories, series, age ratings, regions, sources, features, game scanners, completion statuses, import exclusions, and filter presets.

Useful mechanics visible in the API and engine inventory include:

- filtered queries and per-game filter matching;
- buffered updates for batch mutations;
- database-open state and lifecycle events;
- managed file storage with add, save, remove, and full-path resolution;
- explicit imported ROM and executable tracking;
- migration paths and separate filter/scanner settings;
- backup and restore selection rather than an undifferentiated file copy.

The local installation references LiteDB, but the durable lesson is the indexed, collection-oriented data boundary. Cirra should not switch from modern .NET to Playnite's older runtime or copy its exact database technology.

## Scanning And Import

The `GameScanner` surface contains directory scanning, recursive traversal, archive support, exclusion parsing/matching, database lookup, file-data checks, and script-result handling. The surrounding model includes scanner configurations, an import exclusion collection, and imported-file registries.

The practical design pattern is:

1. Preserve scanner configuration and source ownership.
2. Normalize discovered artifacts before constructing games.
3. Check explicit exclusions and prior imported artifacts.
4. Compare candidates with the database.
5. Stage results before a committed batch update.
6. Keep the scanner's evidence so later rescans can explain added, updated, skipped, moved, and ambiguous items.

Static inspection does not establish Playnite's real-world duplicate rate. It does establish that exclusions, prior-import identity, and database matching are first-class concepts instead of cleanup added after import.

## Metadata And Media

Playnite separates metadata acquisition from application. A metadata plugin declares supported fields and returns an on-demand provider. The provider exposes individual calls for name, genres, release date, developers, publishers, tags, description, ratings, images, links, features, age ratings, series, regions, platforms, and install size.

That field-level contract enables:

- selective refresh;
- provider precedence per field;
- progress and cancellation at meaningful boundaries;
- preservation of manual fields;
- a comparison/review UI before applying changes.

The desktop application includes dedicated metadata comparison, download, and image-selection workflows. The engine also contains image source/cache services and references dedicated image scaling libraries. Runtime cache behavior and quality were not benchmarked in this pass.

## Launch, Installation, And Emulation

The SDK's `GameAction` model carries action type, path, arguments, additional arguments, working directory, emulator/profile identity, tracking mode/path, scripts, initial tracking delay, and tracking frequency. Plugins can supply play, install, and uninstall actions, while lifecycle hooks cover starting, started, stopped, cancelled, installed, installation cancelled, and uninstalled states.

The important concept is an explicit action/session contract. A title may have several valid actions, and process tracking is configurable rather than inferred from only one executable field.

For Cirra this supports a clearer split among title identity, installation, launch profile, emulator profile, prerequisite/readiness evidence, and active session tracking.

## Controllers And Fullscreen Input

The engine includes controller factories/managers and generic play-controller handling. The SDK exposes connected controller information plus connected, disconnected, and button-state lifecycle events.

Fullscreen contains an SDL initialization/event loop, input setup, confirm/cancel remapping, controller connection handling, focus handlers, list-navigation speed, and explicit tile-panel directional/page/wheel methods. Its settings include controller support, disabled controllers, guide-button focus, action swapping, prompt style, smooth scrolling, and list navigation speed.

The lesson for Horizon is to centralize input ownership and route normalized intents to the active navigation scope. Individual controls should not poll devices independently.

## Themes And Visual Composition

The install has separate `Themes\Desktop\Default` and `Themes\Fullscreen\Default` packages. Each has a `theme.yaml` manifest with a versioned `ThemeApiVersion: 2.0.0`.

Observed desktop theme structure includes:

- shared constants and media resources;
- default control styles;
- derived styles;
- custom controls;
- replaceable views for library, grid, list, details, search, filters, sidebar, top panel, and notifications;
- images and an HTML description view.

Observed fullscreen theme structure includes:

- its own constants, media, controls, views, and manifest;
- game details, game menu, main menu, filters, settings, notifications, message box, text input, and custom menus;
- activation and navigation sounds plus background music;
- separate PlayStation and Xbox button-prompt resources.

The valuable pattern is a versioned theme contract with stable semantic resources and replaceable composition points. Cirra should use this to strengthen its own cloud/material/motion language, not imitate Playnite's visual appearance.

## Extension Platform

The SDK is a real product boundary. The inspected contract exposes:

- database, dialogs, paths, notifications, resources, web views, settings, add-ons, emulation, and main-view services;
- game start/install/uninstall commands and game-variable expansion;
- library and metadata plugins;
- play/install/uninstall controllers and lifecycle events;
- game/main menus, sidebar items, top-panel items, custom game controls, settings views, converters, and global search commands;
- search contexts and actions;
- controller events and connected-controller queries;
- plugin configuration, user-data paths, and settings persistence.

This breadth explains why Playnite can grow without every feature being merged into its primary application. It also creates trust and compatibility responsibilities. Cirra should start with a smaller, versioned, capability-based contract and avoid unrestricted in-process scripting until signing, compatibility, diagnostics, and recovery are designed.

## Backup, Update, And Recovery

The engine inventory includes backup/restore, automatic backup options, theme inclusion, updater manifest download, release notes, package download, update-file verification, installation, and latest-version selection.

Method names alone do not establish cryptographic authenticity, rollback quality, or failure safety. The transferable lesson is that backup, migration, update verification, and restore selection are engine responsibilities with explicit states and UI, not release-script afterthoughts.

## What This Pass Did Not Establish

- No Playnite process was executed or instrumented.
- No live UI responsiveness, memory, startup, controller hardware, or large-library performance was measured.
- The local `User Data` directory contained only `Crashpad`, so no user database was inspected and no claim is made about the physical schema of a populated library.
- Installed extensions were absent, so third-party plugin compatibility and isolation were not tested.
- Managed inspection did not reconstruct every method into source-equivalent C#.
- Auxiliary third-party DLLs were inventoried as dependencies but were not reverse engineered individually.
- Static names and call graphs establish design surface and relationships, not bug-free behavior.

## Evidence Index

| Evidence | Meaning |
| --- | --- |
| `ev_d0076142b8f21ada6f95257cef62bd2b36c622f9e30d66f6c33a7e1aeab93ea4` | `Playnite.dll` artifact summary |
| `ev_6573f05fc481aca36f0103077e5e63d5beac0fbd973a47442418004d8a6b0d31` | Complete core member-inventory summary |
| `ev_776cfedacff336c1e9490bb3c4be91b9bf757887dc34789942ac3dd0a37cc5d2` | Focused database/scanner/metadata/controller/theme/update/backup summary |
| `ev_ce6d7a1ce1b9b727d3e7a3b14e24c67658351c9d799365f2836745314e896764` | Core assembly-reference page, including LiteDB and SDK |
| `ev_25766785cd72113adb5b54c78d9ae12b21078d35afe6d2492e153615a250ec23` | Desktop artifact summary |
| `ev_d2b2ccd3514860c80290fbab3d6abc0e304c9daf1af4f8c1a1a7b69afa02dda2` | Complete desktop member-inventory summary |
| `ev_ada70dc0fc1e77cac29dfd9b815d616a40d3e734a5cb4aab755ff84405ba1189` | Fullscreen artifact summary |
| `ev_6bdc2a8090fc2df24ef97335f6a72aa6bdbb97c785b9b8413888a40a8e65939b` | Complete fullscreen member-inventory summary |
| `ev_e58cd71eaed550f260efd6d1e6e4c4bf26becf3d05aa7bad2ca3600a4725ddd7` | Focused fullscreen lifecycle/navigation/settings summary |
| `ev_0e9c3a40e2fde2a69ca1776d21c9a03e9da80cdeb9d306ca295f6b5ba812dacf` | SDK artifact summary |
| `ev_7afffb3ebe331a48dc45dc71cf4a0bf05f7c098848a9005ed6446a6cc8fdf2ea` | Complete SDK member-inventory summary |
| `ev_5ee63fa4e9fdf38c6e3c120d7aa97154696917a8ef8569b38d255f9f16b74852` | Focused SDK contract summary |

The complete retained bundle is at `C:\Users\conno\AppData\Local\Temp\cirra-competitor-rea-evidence-2026-10-19.json` (49 evidence records, 75,716,753 bytes). The earlier LaunchBox phase bundle remains at `C:\Users\conno\AppData\Local\Temp\launchbox-phase1-rea-evidence.json`.
