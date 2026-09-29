# CutFlow Project Reference

## Document purpose and authority

This document is a code-derived technical reference for the current CutFlow workspace. It is intended to support implementation work, code-review question design, UI review, regression analysis, and future maintenance.

The implementation under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` are the source of truth. `README.md` is the user-facing operational guide, while `docs/superpowers/specs/2026-08-04-cutflow-v1-design.md` and the implementation plan record the original design intent. If those documents disagree with the current source, the current source wins.

Snapshot described here:

- Workspace: `C:\Dev\CutFlow`
- Documentation date: 2026-08-18
- Repository: [github.com/Insaner1980/CutFlow](https://github.com/Insaner1980/CutFlow).
- Default publication branch: `main`. The local `C:\Dev\CutFlow` checkout is the active source of truth; GitHub is its publication and backup destination.
- Source inventory, excluding generated `bin` and `obj` trees: 78 files, including 59 C# files (15,051 lines) and 9 XAML files (1,075 lines).
- Test inventory: 46 C# files (13,972 lines).
- Current verification: `.\scripts\Verify-CutFlowRelease.ps1` completed its clean restore, passed 833/833 Release x64 tests, and completed the non-incremental Release x64 build with 0 warnings and 0 errors.
- The verification above proves the automated Release build and test suite. It does not claim that an interactive packaged-app acceptance pass was performed for this documentation update.

## Product summary

CutFlow is a local-only, packaged Windows desktop video editor. It implements a deliberately compact V1 workflow:

1. Create, open, rename, duplicate, and delete local projects.
2. Import local video, image, and audio files without copying the source media.
3. Assemble one magnetic visual track (`V1`), one positioned text track (`T1`), and one positioned background-audio track (`A1`).
4. Reorder, move, trim, split, duplicate, mute, show/hide, lock, and delete timeline items where applicable.
5. Edit text content, typography, color, opacity, alignment, timing, and normalized canvas position.
6. Preview through the native Windows media stack with a live XAML text layer.
7. Autosave the project as schema-versioned JSON.
8. Export H.264/AAC MP4 through `MediaComposition.RenderToFileAsync`.

CutFlow does not contain a web shell, backend, database, account system, cloud sync, telemetry, collaboration, AI functionality, online asset library, FFmpeg, plugin system, or dependency-injection container. Source media remains at its original absolute path. CutFlow owns only project documents, settings, logs, and generated caches.

## Supported media and deliberate V1 limits

### Accepted input extensions

| Kind | Extensions | Metadata/validation path | Timeline destination |
| --- | --- | --- | --- |
| Video | `.mp4` | `MediaClip.CreateFromFileAsync` plus native video properties | Magnetic `V1` |
| Image | `.png`, `.jpg`, `.jpeg` | `BitmapDecoder` dimensions; fixed five-second imported duration | Magnetic `V1` |
| Audio | `.mp3`, `.wav` | `BackgroundAudioTrack.CreateFromFileAsync` | Positioned `A1` |

Extension acceptance is only the first gate. The native Windows APIs must also be able to open and decode the file. Codec support therefore varies with the Windows installation.

### Explicit functional boundaries

- The visual timeline is a single sequential track. There is no stacked video, compositing between multiple visual tracks, transition graph, or arbitrary track creation.
- Text and audio are independently positioned, but there is still only one logical `T1` and one logical `A1` lane.
- Stickers, effects, transitions, captions, filters, and adjustment appear in the editor rail as honest informational empty states. They are not partially implemented tools.
- There are no transitions, filters, color grading, effects, waveform rendering, automatic captions, stickers, keyframes, or adjustment processing.
- Project timeline values are limited to 24 hours, with a minimum item duration of 100 ms.
- Audio fade-in and fade-out values exist in the JSON model for compatibility, but the current UI does not expose them and the native composition path does not apply them.
- Export is MP4 only, at fixed 30 fps, using the four bitrate combinations documented under Export.
- Hardware encoder choice, advanced codec controls, signed installer production, and Store packaging are outside the current workflow.

## Technology and build configuration

### Runtime and language stack

- C# with nullable reference types and implicit global usings enabled.
- XAML and WinUI 3.
- .NET target: `net10.0-windows10.0.26100.0`.
- SDK pinned by `global.json`: .NET SDK `10.0.302`, `latestPatch` roll-forward, prerelease SDKs disabled.
- Windows App SDK NuGet package: `2.5.1`.
- Windows SDK Build Tools NuGet package: `10.0.28000.2705`.
- Minimum declared Windows version: `10.0.17763.0` (Windows 10 version 1809).
- Primary target and only solution platform: x64 / `win-x64`.
- Output type: packaged WinUI `WinExe` with MSIX tooling enabled.
- DPI awareness: `PerMonitorV2` in `app.manifest`.

### Package identity and capabilities

`Package.appxmanifest` defines package identity `CutFlow`, version `1.0.0.0`, publisher `CN=CutFlow`, application ID `App`, and a `Windows.FullTrustApplication` entry point. It declares `runFullTrust`, desktop/universal device families from build 17763 through the tested 26100 SDK level, and the local logo/splash assets.

No media-library capability is declared. User-selected file access is obtained through initialized file pickers, and the app runs as a full-trust packaged desktop application.

### Solution structure

```text
CutFlow.slnx
|-- src/CutFlow/CutFlow.csproj
`-- tests/CutFlow.Tests/CutFlow.Tests.csproj
```

The app project exposes internals to `CutFlow.Tests`; there is no separate core library. The test project uses:

- `Microsoft.NET.Test.Sdk` 18.10.1
- `MSTest.TestAdapter` 4.4.1
- `MSTest.TestFramework` 4.4.1

### Canonical commands

Run from the project root:

```powershell
.\scripts\Verify-CutFlowRelease.ps1
```

The release verifier removes the app and test `bin`/`obj` directories and ignored local `.sonarqube` analysis output, performs a fresh restore, runs the Release x64 tests, and performs a non-incremental Release x64 build. Pass `-RegisterAndLaunch` to continue through development-package registration and package-identity launch; interactive acceptance remains a separately recorded gate in `MANUAL_ACCEPTANCE.md`.

The Release loose-package layout is produced under:

```text
src\CutFlow\bin\x64\Release\net10.0-windows10.0.26100.0
```

The app must be registered from the generated `AppxManifest.xml` and launched through its package identity. `scripts/Register-CutFlowDevelopment.ps1` refuses to register over a `CutFlow` package installed outside this repository's build tree and resolves the requested loose layout before launch. The repository does not generate a signed distribution installer or Microsoft Store package.

## Repository map

```text
C:\Dev\CutFlow
|-- CutFlow.slnx                         Solution definition
|-- global.json                          Pinned .NET SDK
|-- README.md                            User/build/operator guide
|-- PROJECT.md                           This code-derived reference
|-- docs/superpowers/
|   |-- specs/                           Original V1 design intent
|   `-- plans/                           Historical implementation plan
|-- src/CutFlow/
|   |-- App.xaml[.cs]                    Application resources and launch
|   |-- MainWindow.xaml[.cs]             Composition root, window lifecycle, navigation
|   |-- Package.appxmanifest             Package identity and assets
|   |-- app.manifest                     Win32 compatibility and DPI awareness
|   |-- Models/                          Serializable project model and selections
|   |-- ViewModels/                      Home/editor state holders
|   |-- Views/                           Home and editor shells
|   |-- Controls/                        Media, preview, inspector, and timeline controls
|   |-- Services/                        Persistence, import, composition, export, caches, log
|   |-- Utilities/                       Math, gates, normalization, save coordination
|   |-- Styles/ThemeResources.xaml       Global visual tokens and common control styles
|   `-- Assets/                          Package logos and splash images
`-- tests/CutFlow.Tests/                 MSTest behavioral and acceptance-style tests
```

Generated `bin` and `obj` directories are build artifacts and are not architectural source.

## High-level architecture

### Composition root

`App.OnLaunched` creates and activates exactly one `MainWindow`. `MainWindow` directly constructs the concrete services and `MainViewModel`; there is no service locator or dependency-injection framework.

`MainWindow` owns:

- the custom title-bar integration;
- `AppWindow` activation and closing events;
- per-monitor DPI-aware geometry restore and save;
- application-wide Home `Ctrl+N` routing;
- top-level file pickers and relink picker integration;
- Home/Editor view switching;
- workspace-setting persistence;
- safe close coordination.

### State holders

| Type | Responsibility |
| --- | --- |
| `MainViewModel` | Holds Home, current project, and active editor state; coordinates Home/Editor navigation. |
| `HomeViewModel` | Lists and mutates projects; exposes busy/error state and project cards. |
| `EditorViewModel` | Owns the editable `ProjectDocument`, selected rail tool, current selection, playhead, playback state, save status, undo/redo history, and model-level commands. |

The view models implement `INotifyPropertyChanged` through a small in-project `ViewModelBase`. They do not use a command framework; views invoke methods and subscribe to explicit events.

### UI and control boundary

The project follows a pragmatic MVVM/code-behind split:

- Serializable state and deterministic operations live in models, services, and utilities.
- `EditorViewModel` exposes application editing actions and history.
- Native media, file pickers, Win32 window APIs, pointer capture, visual-tree creation, dialogs, and focus handling live in views/controls.
- `TimelineControl` owns hit testing, pointer drag previews, ruler rendering, zoom, snapping, track locks, and conversion between time and pixels. It emits one edit request after a completed drag.
- `PreviewPane` owns the native `MediaPlayer`, source replacement, playback events, live text visuals, and direct text dragging.
- `InspectorPanel` is an event-producing form. It does not mutate the model itself.

### Primary dependency flow

```text
App
`-- MainWindow
    |-- ProjectService
    |-- MediaImportService
    |-- SettingsService
    |-- SimpleLogService
    |-- MainViewModel
    |   |-- HomeViewModel
    |   `-- EditorViewModel (while a project is open)
    |-- HomeView
    `-- EditorView (while a project is open)
        |-- MediaPanel
        |-- PreviewPane
        |-- TimelineControl
        |-- InspectorPanel (desktop and narrow-overlay instances)
        |-- ThumbnailService (project-scoped)
        |-- TextOverlayRenderer (project-scoped)
        |-- CompositionService
        |-- PreviewRebuildGate
        `-- DebouncedSaveCoordinator
```

## Application lifecycle and navigation

### Startup

1. `App` creates `MainWindow` and calls `Activate`.
2. The window uses a dark requested theme and extends content into a custom title bar.
3. On the first window activation, `MainWindow` resolves the HWND, `WindowId`, and `AppWindow`.
4. Async initialization waits for that native window context before loading settings.
5. Stored physical bounds are preferred. Legacy logical bounds are migrated using the target display DPI.
6. Bounds and minimum dimensions are clamped to the selected display work area.
7. Home projects are loaded, then `HomeView` is shown.

`MainWindowLifecycle` guarantees a single initialization task. Closing waits for that same task and prevents stale post-await UI continuation after a close has begun.

### Navigation to Editor

Opening a project passes through `HomeProjectOpenGate`, which rejects a concurrent second open/create action. `MainViewModel.OpenEditorAsync` refreshes every asset's missing-file flag before it creates the `EditorViewModel` and raises `CurrentViewChanged`.

`MainWindow.ShowCurrentView` disposes any old `EditorView`, creates a new project-scoped editor, wires top-level import/relink/export/workspace events, places it in the content host, and registers the view's title-bar drag region.

### Navigation back to Home

Returning Home is blocked while native export rendering is active. Otherwise:

1. The editor flushes pending project autosave.
2. `MainViewModel.ShowHomeAsync` performs an additional immediate save if the editor status is not `Saved`.
3. A save failure keeps the editor open and surfaces the error.
4. Successful navigation clears current project/editor state, raises the view-change event, and reloads the project list.

### Closing

The `AppWindow.Closing` handler cancels the native close on the first attempt and starts an asynchronous safe-close sequence:

1. Wait for initialization.
2. Invalidate export setup, cancel an active render, and await it.
3. Flush the project save.
4. Capture physical and logical window bounds.
5. Flush workspace settings.
6. Approve and invoke the real close.

Any failure cancels the close attempt and keeps the app open with an actionable error. Disposal cancels editor lifetime work, preview rebuilds, thumbnails, autosave debounce, export work, and native preview resources.

## Persistent data and ownership

### Package-local layout

The default persistence root is `ApplicationData.Current.LocalFolder.Path`, which is the package's `LocalState` folder:

```text
LocalState\
|-- Projects\<project-guid>\
|   |-- project.json
|   `-- cache\
|       |-- thumbnails\<sha256>.jpg
|       `-- text-overlays\<text-guid>-<style-hash>.png
|-- settings.json
`-- cutflow.log
```

Only `Projects/<guid>`, its base `cache` directory, and `project.json` are created eagerly. Thumbnail and text-overlay subdirectories are created lazily.

### Ownership rules

- Imported source files are referenced by normalized absolute path and are never copied into the project.
- Removing an asset deletes only its model reference and an owned thumbnail cache file when possible.
- Deleting a project recursively deletes only its GUID-named directory under the configured `Projects` root.
- Export writes only to the user-selected destination and a uniquely named staging file in the same destination directory.
- Project-relative cache paths are resolved through path-containment checks before access or deletion.

### Project JSON persistence

`ProjectService` uses indented camel-case `System.Text.Json` with explicit enum-as-string converters declared on the model enums. Save behavior is designed to preserve the last valid document:

1. Normalize the in-memory document.
2. Deep-copy it and assign the candidate UTC modification timestamp only to that snapshot.
3. Serialize the snapshot to a unique `project.json.<guid>.tmp` sibling.
4. Write asynchronously with `FileOptions.WriteThrough`.
5. Flush the managed stream and then flush to disk.
6. Replace an existing `project.json` atomically with `File.Replace`, or move the first file into place.
7. Publish the new `ModifiedAt` to the live document only after the file commit succeeds; on failure, leave it unchanged and remove the operation's temp file.

Load rejects malformed JSON, a missing required schema version, unsupported schema versions, and an ID that does not match the GUID directory name. `ListAsync` skips unreadable, invalid, unauthorized, or concurrently removed project directories without deleting them.

### Settings persistence

`settings.json` contains only non-sensitive workspace state:

- logical and versioned physical window bounds;
- timeline zoom;
- timeline height;
- preview loop preference;
- last export folder.

Settings normalization supplies safe defaults, clamps ranges, removes invalid folder paths, and distinguishes valid versioned physical bounds from legacy logical bounds. Unlike project saving, settings saving is a direct `WriteAllTextAsync` operation rather than a temp-and-replace transaction.

### Bounded technical log

`SimpleLogService` writes UTC timestamped single-line entries to `cutflow.log`. Newlines are replaced with spaces, individual caller-provided messages are truncated to 1,024 characters by default, and the file retains at most 200 entries. An in-process semaphore and a path-derived named OS semaphore serialize writes within and across app processes. Each update is written to a unique sibling temp file and atomically replaces the log. `TryWriteAsync` intentionally suppresses logging failures so telemetry-like infrastructure cannot break a user workflow.

## Project data model

### Root document

`ProjectDocument` is schema version 1.

| JSON property | CLR property | Meaning/default |
| --- | --- | --- |
| `schemaVersion` | `SchemaVersion` | Required; current value `1`. |
| `id` | `Id` | Project GUID; must match directory GUID when loaded. |
| `name` | `Name` | User-visible name. Creation/rename trims and rejects blank names. |
| `createdAt` | `CreatedAt` | UTC creation timestamp. |
| `modifiedAt` | `ModifiedAt` | Updated only by successful save. |
| `settings` | `Settings` | Canvas and track state. Null JSON is normalized to defaults. |
| `assets` | `Assets` | Imported source references. Null JSON is normalized to an empty list. |
| `videoItems` | `VideoItems` | Ordered magnetic `V1` items. |
| `audioItems` | `AudioItems` | Positioned `A1` items. |
| `textItems` | `TextItems` | Positioned `T1` items. |

Global model limits:

- `MinimumItemDurationMilliseconds = 100`
- `MaximumTimelineDurationMilliseconds = 86,400,000` (24 hours)

### Project settings

| Property | Default | Behavior |
| --- | --- | --- |
| `Width` / `Height` | 1920 × 1080 | Updated as a pair by aspect preset. |
| `FrameRate` | 30 | Used for display timecode; export also forces 30 fps. |
| `AspectRatio` | `Landscape16By9` | Other values: `Portrait9By16`, `Square1By1`. |
| `BackgroundColor` | `#FF000000` | Must be opaque ARGB (`#FFRRGGBB`). |
| `VideoTrackVisible` | `true` | Persisted and undoable; hidden V1 renders black filler for its duration. |
| `TextTrackVisible` | `true` | Persisted and undoable; hidden T1 is omitted. |
| `AudioTrackMuted` | `false` | Persisted and undoable; forces all A1 composition volumes to zero. |

Aspect dimensions are exactly:

- 16:9: 1920 × 1080
- 9:16: 1080 × 1920
- 1:1: 1080 × 1080

### Asset model

Each `ProjectAsset` stores:

- GUID and kind (`Video`, `Image`, or `Audio`);
- normalized absolute source path and display filename;
- source/import duration in integer milliseconds;
- width and height when meaningful;
- file size and UTC last-write timestamp;
- project-relative thumbnail cache path;
- current missing flag.

Image assets always receive a five-second asset duration at import/relink time. Timeline image items may later use a different duration.

### V1 visual items

`VideoTimelineItem` stores ID, asset ID, source-in/out, effective duration, volume, and mute state. The class name covers both video and still-image items.

- Timeline start is not serialized. It is derived by summing all preceding V1 item durations.
- `DurationMilliseconds` returns its explicitly stored positive value; otherwise it falls back to `max(0, sourceOut - sourceIn)` for older JSON.
- Video trim changes the source range and effective duration together.
- Image duration is independent of the asset's fixed five-second metadata duration.
- Splitting video creates two adjacent source ranges. Splitting an image creates two adjacent image items with the same still source range and divided timeline durations.
- Reorder, delete, and duration changes ripple every later V1 start automatically.

### A1 audio items

`AudioTimelineItem` stores ID, asset ID, explicit start, source-in/out, volume, fade values, and mute state. Duration is computed as `max(0, sourceOut - sourceIn)` and is not serialized separately.

- Moving changes explicit timeline start.
- Trimming the left edge changes both source-in and timeline start so the right edge remains stable.
- Trimming the right edge changes source-out.
- Duplicating places the copy immediately after the source item's timeline end.
- A1 items may overlap one another in the model and native background-audio collection.

### T1 text items

`TextTimelineItem` stores:

| Property | Default/range |
| --- | --- |
| Start | Explicit integer milliseconds, clamped within project bounds. |
| Duration | 3,000 ms by default; 100 ms minimum. |
| Text | Empty string in the base model. |
| Font family | `Segoe UI`; supported UI choices are Segoe UI, Arial, Georgia, Consolas, Impact. |
| Font size | 64; normalized to 8–400. |
| Font weight | 600; bold command uses 700; normalized to 1–999. |
| Italic | `false`. |
| Text color | `#FFFFFFFF`. |
| Background color | `#00000000`. |
| Background enabled | `false`. |
| Opacity | 1.0; clamped to 0–1. |
| Alignment | Center (`Left`, `Center`, `Right`). |
| Normalized X/Y | 0.5 / 0.5; clamped to 0–1. |

Presets create a selected three-second item at the clamped playhead:

| Preset | Content | Differences from defaults |
| --- | --- | --- |
| Default | `Text` | No style changes. |
| Title | `Title` | 96 px, weight 700, Y = 0.22. |
| Subtitle | `Subtitle` | 52 px, Y = 0.78. |
| Minimal label | `Label` | 38 px, enabled `#CC000000` background, Y = 0.88. |

T1 items may overlap. An item is active on the half-open interval `[start, start + duration)`.

### Selection model

`EditorSelection` is a value record containing a kind and optional item GUID. Kinds are `None`, `Project`, `Asset`, `VideoItem`, `AudioItem`, and `TextItem`. Current UI editing primarily uses `None` plus the three timeline-item kinds; asset selection support exists in the model for library/relink scenarios.

## Normalization and edit invariants

### Load/save normalization

Before save and after load, `ProjectService.Normalize` repairs safe model boundaries:

- null settings/lists become defaults/empty lists;
- invalid project background becomes opaque black;
- V1 values are clamped while at least the 100 ms minimum remains; a document with additional V1 entries after that capacity is exhausted is rejected as invalid rather than silently dropping items;
- V1, A1, and T1 start/duration/source values are clamped to project limits;
- volume becomes finite and 0–1;
- fades become 0 through item duration;
- null text becomes empty;
- text family, size, weight, colors, opacity, alignment, and normalized coordinates are sanitized.

Normalization is not a schema migration system. Any schema version other than 1 is rejected.

### Edit transaction paths

`EditorViewModel` has two edit mechanisms:

- `TryCommitEdit` deep-clones the current document with JSON, applies a candidate edit, rejects no-ops or any candidate outside the timeline duration limit, records the original in history, then swaps in the candidate.
- `CommitEdit` records the current document, mutates it directly, and marks it unsaved. It is used for controlled insertions such as imported assets and newly created text.

After a successful edit the view model clamps the playhead, normalizes selection when necessary, updates undo/redo and duration properties, marks the document `Unsaved`, and raises `EditCommitted`.

### Undo and redo

`UndoHistory` stores whole-document JSON snapshots, not command objects. It has a default capacity of 50 undo snapshots. Recording a new edit clears redo; the oldest undo snapshot is discarded at capacity.

Undo/redo restores independent deserialized documents. The editor rebinds the controls to the restored instance, clamps the playhead to the restored duration, removes orphan selection, and triggers autosave/preview rebuild through the same `EditCommitted` event.

### Timecode

Display timecode is `HH:MM:SS:FF`. Negative milliseconds clamp to zero. Frames are `floor(remainderMilliseconds × frameRate / 1000)`. The formatter requires a positive finite frame rate and is non-drop-frame.

## Project and Home workflows

### Create

Creation uses the name `New project`, a new GUID, current UTC creation/modification timestamps, the default 16:9 project settings, and an immediate first save. If creation fails after making a new project directory, only that newly created directory is cleaned up.

### List and cards

Projects are discovered only from GUID-named direct children of `Projects`. Valid documents are sorted by descending `ModifiedAt`. Each card shows:

- first usable cached visual thumbnail, otherwise a restrained placeholder;
- project name;
- localized modified date/time;
- aspect ratio label;
- formatted project duration.

The first usable cached visual is chosen in asset order. Invalid or outside-project cache references are ignored.

### Rename

Home rename uses a dialog with a 120-character text box and requires non-whitespace input. Service normalization trims the final name. Editor title-bar rename uses Enter to commit, Escape to revert, and lost focus to commit; it participates in undo/redo.

### Duplicate

Duplicate deep-clones the JSON document, assigns a new project GUID, adds ` copy` unless a name is provided, replaces creation/modification timestamps, and saves to a new directory. Source media remains shared by absolute path. Generated cache files are not copied; any serialized cache reference resolves inside the new project and will be regenerated lazily when needed.

### Delete

Home deletion requires a confirmation dialog that explicitly states imported source media will not be deleted. Only the CutFlow project directory and owned cache are removed.

## Media import, relink, and thumbnails

### Picker and drag/drop scopes

- Media panel picker/drop: MP4, PNG, JPG, JPEG.
- Audio panel picker/drop: MP3, WAV.
- Preview empty-state import: all supported types.
- Timeline V1 drop: visual media only.
- Timeline A1 drop: audio only.
- Timeline T1 drop: rejected with guidance to use the Text panel.

File pickers are initialized with the `MainWindow` HWND. The export picker uses the modern `Microsoft.Windows.Storage.Pickers.FileSavePicker` with the window ID.

### Import algorithm

`EditorImportGate` holds a semaphore from preparation through UI commit so two concurrent import operations cannot both commit the same duplicate asset.

For each file, `MediaImportService`:

1. Requires a local path.
2. Normalizes the full path and deduplicates case-insensitively against the project and the current batch.
3. Rejects unsupported extensions.
4. Reads native metadata and validates duration.
5. Creates an asset with a new GUID and no thumbnail reference.
6. Returns one `ImportResult` per file rather than failing the entire batch.

Expected file, codec, access, argument, overflow, and COM failures become readable per-file errors. Cancellation still propagates.

### Missing media

Opening a project runs a file-existence refresh. Missing assets remain in the library and timeline, receive a visible `Missing` status/badge, cannot be added again, and retain all timeline references.

Preview behavior is tolerant:

- a missing V1 item becomes same-duration black filler;
- a missing A1 item is omitted;
- errors are shown in a warning `InfoBar`.

Export behavior is strict for every positive-duration A1 item and every positive-duration V1 item while V1 is visible: each referenced source must exist and still match its import snapshot, or preflight blocks export and lists missing names. Hidden V1 is rendered as project-background filler and therefore does not require its source files.

### Relink

Relink requires the replacement to have the same asset kind, not duplicate another project source path, and be long enough for the maximum existing source-out reference. A successful relink preserves asset identity, refreshes metadata, clears the thumbnail reference, and keeps timeline references intact. The old project-owned thumbnail is deleted when possible.

### Removing an asset

An asset cannot be removed while any V1 or A1 item references it. T1 has no asset reference. Removal changes only the project and owned thumbnail cache; the source file is never touched.

### Thumbnail cache

Visual thumbnails are loaded lazily as asset cards enter the GridView realization queue.

- Default requested size: 256.
- Maximum accepted requested size: 1,024.
- Native thumbnail modes: `PicturesView` for images, `VideosView` for video.
- Cache encoding: JPEG.
- Maximum encoded or decoded thumbnail budget: 8 MiB.
- Key: SHA-256 of uppercased normalized path, file size, UTC last-write ticks, and requested size.
- Relative path: `cache/thumbnails/<64-lowercase-hex>.jpg`.

Temporary generation files live inside the thumbnail cache. Before publication, the service verifies that the asset still matches the captured request. A relink or metadata change therefore cannot allow a stale asynchronous thumbnail to overwrite the new asset state.

## Timeline behavior

### Track semantics

| Track | Placement | Supported item operations | Persisted track state | Session-only state |
| --- | --- | --- | --- | --- |
| V1 | Magnetic, sequential, starts derived | Add, reorder, split, trim, image duration, duplicate, delete, item volume/mute | Visibility | Lock |
| T1 | Explicit start; overlaps allowed | Add preset, move, edge trim, duplicate, delete, style/content/position | Visibility | Lock |
| A1 | Explicit start; overlaps allowed | Add at playhead/drop, move, source trim, duplicate, delete, volume/mute | Global mute | Lock |

Track locks are held only in `TimelineControl`; they are not serialized, undoable, or shared with another editor instance. Snapping is also session-only. Visibility and global audio mute are model edits, so they are serialized and undoable.

### Geometry

The timeline shell has:

- 42 px toolbar;
- 1 px divider;
- 108 px fixed track-header column;
- 1 px column divider;
- scrollable content with a total logical height of 186 px:
  - 30 px ruler;
  - 64 px V1;
  - 44 px T1;
  - 48 px A1.

Content width is at least the viewport width. Otherwise it represents at least one second or the current project duration at the selected pixels-per-second scale, plus 48 px trailing space.

### Zoom and ruler

- Default zoom: 80 px/s.
- Range: 20–400 px/s.
- Zoom buttons change 20 px/s.
- `Ctrl` + mouse wheel multiplies/divides zoom by 1.12 around the pointer and preserves the time under that pointer.
- Fit computes a bounded scale from duration and viewport, then scrolls to zero.
- Ruler candidate intervals are 100, 250, 500, 1,000, 2,000, 5,000, 10,000, 30,000, and 60,000 ms.
- The first interval producing at least 80 px spacing is selected.
- Only visible ticks plus one interval of buffer are materialized.

### Snapping

Snapping rounds to 100 ms and also considers all item starts/ends, the playhead, and total duration. A candidate edge must be within 60 ms. When multiple candidates qualify, the nearest is used; an equally near later edge may replace the earlier candidate. Disabling snapping returns the non-negative raw value.

### Pointer editing

Cards are dynamically created from model state. Their widths are duration-derived with an 18 px visual minimum. Selected cards use accent borders and reveal 3 px trim indicators; locked cards use reduced opacity and do not begin edit drags.

- Press within 8 px of the left/right card edge: begin the corresponding trim.
- Press card body: reorder V1 or move T1/A1.
- Press ruler/track background or the 12 px playhead target: seek and capture the pointer.
- Drag movement updates only a visual preview.
- Pointer release converts the preview to one `TimelineEditRequestedEventArgs` and therefore one undoable model edit.
- Cancellation or lost capture discards the preview and rerenders canonical model state.

V1 reorder target is selected by comparing pointer time with each clip midpoint. A1/T1 move and trim use explicit time. The service layer performs the final source-duration and 24-hour validation even if a visual drag preview exceeds a valid source range.

### Context and toolbar commands

The toolbar provides selection mode, split, delete, undo, redo, snapping, zoom out/in, zoom slider, and fit. Card context menus provide applicable combinations of split, duplicate, delete, and show source file.

Split is enabled only when the current playhead leaves at least 100 ms on both sides of the selected V1 item.

### Keyboard behavior

Editor-level shortcuts are suppressed when a `TextBox`, `PasswordBox`, or `RichEditBox` has focus.

| Shortcut | Behavior |
| --- | --- |
| `Ctrl+N` | Save and create a new project; blocked while export renders. |
| `Ctrl+S` | Flush project save. |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo. |
| `Ctrl+B` | Split selected V1 item; rejected when its track is locked. |
| `Ctrl+D` | Duplicate selected V1/T1/A1 item; rejected when locked. |
| `Delete` | Delete selected timeline item; rejected when locked. |
| `Space` | Play/pause preview. |
| `Escape` | Close narrow inspector overlay, otherwise clear selection. |
| `Home` / `End` | When timeline has focus, seek to start/end. |
| `Left` / `Right` | When timeline has focus, seek by 100 ms. |
| `Ctrl` + mouse wheel | Zoom timeline around pointer. |

Media cards can be added with Enter or Space when addable. Text content uses `Ctrl+Enter` to commit because plain Enter inserts a newline. Other inspector text boxes commit with Enter, revert with Escape, and also commit on lost focus.

## Preview pipeline

### Composition rebuild

Every model edit schedules a preview rebuild after a 140 ms debounce. `PreviewRebuildGate` cancels the previous build, assigns a monotonically increasing version, and permits only the newest lease to replace the active preview.

`CompositionService.BuildPreviewAsync` builds a fresh `MediaComposition` without rasterized text overlays. Text is intentionally drawn once as live XAML above the media element.

During successful source replacement, `PreviewPane`:

1. Generates a preview media stream capped within 1280 × 720 while preserving aspect ratio and never upscaling the requested project dimensions.
2. Invalidates the old native-event generation.
3. Detaches event handlers and pauses playback.
4. Clears and disposes the old `MediaSource`.
5. Installs the new composition/source.
6. Restores the clamped position and play intent.
7. Reattaches current-generation events.

The failure cleanup order is also explicit: invalidate events, detach, pause, clear state, dispose prepared source, then reattach.

### Native composition plan

`CompositionPlan` is the deterministic bridge between JSON state and Windows media objects.

- V1 order is preserved.
- Hidden V1 and missing/unknown visuals become same-duration black filler clips.
- Video source trim and per-item volume/mute are applied.
- Images become fixed-duration media clips using the timeline item duration.
- A1 uses source trim, explicit delay, and effective volume.
- Missing/failed A1 tracks are omitted and named in errors.
- Visible T1 items become delay/duration overlay plans only when a renderer is supplied.
- If audio or text extends past V1, one trailing black filler clip extends the visual composition to the exact project duration.

Native item creation failures are isolated when possible: a failed visual becomes black filler, while a failed audio item is omitted. Preview surfaces up to the first three build errors in a warning.

### Playback state

`PreviewPane` owns one `MediaPlayer` and a 33 ms dispatcher timer.

- Play intent is tracked separately from actual native playback state.
- UI play/pause state changes are reported only when the actual playback state changes.
- Rebuild preserves both current position and intended playback.
- Starting from the end seeks to zero.
- Looping is implemented in the `MediaEnded` handler; native `IsLoopingEnabled` is deliberately held false.
- With loop off, media end clears play intent, pauses, and stops the timer.
- Previous/next frame uses a fixed 30 fps frame grid. Thirty forward steps equal one second.
- Preview mute is transient player state and is not a project edit.

Timer updates flow from `PreviewPane` to `EditorViewModel.Seek`, then back to the timeline and live text layer. The timeline autoscrolls only when the playhead exits the visible horizontal viewport.

### Preview geometry

The central preview area has 20 px padding. The visible frame is centered, has a 1 px border, and is capped at 820 × 480. A `Viewbox` maintains the project aspect ratio. The lower 48 px transport row contains timecode, frame-step, play/pause, mute, loop, and fit controls.

The preview background is the project's opaque ARGB color. Media is rendered by `MediaPlayerElement`; active text is a separate XAML `Canvas` above it.

### Live text

The live-text layer renders only active T1 items when the text track is visible. Each item uses the same `TextStyle` normalization as export:

- normalized supported font family;
- 8–400 size;
- 1–999 weight;
- italic mapping;
- ARGB foreground/background;
- opacity and alignment;
- wrapping within 90% of canvas width;
- optional 18/8 px background padding.

Normalized X/Y denotes the center of the text container. Selected text receives a 2 px accent border and pointer handlers. Dragging updates the visual directly; pointer release clamps coordinates to 0–1 and commits one model edit. The `LiveTextRenderGate` prevents the normal render loop from replacing a captured drag visual and forces a canonical rerender after release/cancel.

## Text overlay export cache

Export text is rasterized as transparent full-canvas PNG files in the project cache. `TextOverlayRenderer` requires a loaded, visible-to-layout `Panel` render host; `PreviewPane` supplies a 1 × 1, zero-opacity, non-hit-test render host that remains in the tree.

The renderer creates a full project-dimension transparent `Canvas`, lays out the same styled text container used by preview, positions its center from normalized coordinates, and renders through `RenderTargetBitmap`. Rasterization-scale compensation requests logical dimensions that produce exact project pixel dimensions.

Cache identity includes:

- cache format version 2;
- every pixel-affecting text/style/position field;
- project width and height;
- text item GUID in the filename.

Timing is not included because the same pixels can be reused at a different delay/duration.

Before reuse or publication, a cached PNG must:

- be between the structural minimum and 64 MiB;
- have a valid PNG signature and ordered chunk structure;
- contain one valid IHDR and non-empty IDAT data;
- have valid CRCs and a terminal IEND with no trailing bytes;
- decode successfully as premultiplied BGRA;
- match the exact expected pixel dimensions.

Rendering is serialized per renderer instance. Publication uses a unique temp file and overwrite move; a concurrently published valid winner may be reused. The cache keeps at most 256 PNG files, retaining the current result and most recent others.

## Export pipeline

### UI flow

Export is enabled only when at least one positive-duration V1 item exists and no export setup/render operation is active.

1. Begin an exclusive export operation.
2. Deep-copy the project and run fresh file-existence preflight off the UI thread.
3. Flush project save.
4. Show a dark `ContentDialog` for sanitized filename, resolution, and quality.
5. Show the modern Windows Save picker, suggesting the last export folder when valid.
6. Persist the chosen folder as a workspace preference.
7. Mark the operation as rendering and display the progress panel.
8. Run export from another deep project snapshot so subsequent editor changes do not affect the active render.

Closing invalidates setup, cancels rendering, waits for it, and then saves. Returning Home or creating another project is blocked while the render phase is active.

### Filename handling

Suggested names:

- trim whitespace and an existing `.mp4` suffix;
- replace control/invalid filename characters with spaces;
- collapse whitespace;
- remove leading/trailing spaces and periods;
- limit the base to 96 characters;
- reject reserved device names such as `CON`, `NUL`, `COM1`, and `LPT1`;
- fall back to `CutFlow export.mp4`.

### Preflight

Export requires:

- at least one positive-duration V1 item;
- every distinct positive-duration A1 asset, and every distinct positive-duration V1 asset while V1 is visible, to resolve to a current source path;
- a text renderer when visible positive-duration text items exist;
- a destination path different from every imported source path.

The service refreshes missing flags on its export snapshot immediately before composition. Missing names are de-duplicated case-insensitively.

### Encoding profiles

All profiles use MP4, H.264 video, AAC stereo audio, 30/1 fps, 48 kHz, two channels, and 192 kbit/s audio.

| Tier | 16:9 | 9:16 | 1:1 | Standard video | High video |
| --- | --- | --- | --- | --- | --- |
| 720p | 1280 × 720 | 720 × 1280 | 720 × 720 | 5 Mbit/s | 8 Mbit/s |
| 1080p | 1920 × 1080 | 1080 × 1920 | 1080 × 1080 | 8 Mbit/s | 12 Mbit/s |

### Safe destination commit

The service creates a unique sibling file named approximately `<base>.cutflow-<guid>.mp4`, renders into it with `MediaTrimmingPreference.Precise`, and only then replaces/moves it to the chosen path. The final blocking `File.Move(..., overwrite: true)` runs off the calling thread.

Failure or cancellation permanently deletes only the operation's staging file. Cleanup errors are logged without replacing the primary export outcome. Existing destination content is unchanged until successful staging commit.

Native transcoder failures map to readable messages for unknown error, invalid profile, or missing codec. The UI separately maps path, access, I/O, and native failures to actionable `InfoBar` text.

### Export progress UI

The 380 px status card appears at the editor's upper-right and is announced with polite live-region behavior. During render it shows filename, progress bar/percentage, and Cancel. Success shows Open file, Open folder, and Dismiss. Cancellation hides the panel and explicitly states that no output file changed.

## Autosave and save state

Project and workspace settings each use a `DebouncedSaveCoordinator` with a default one-second delay.

The coordinator tracks monotonically increasing edited and saved revisions:

- every edit cancels the previous delay and marks `Unsaved`;
- one semaphore prevents concurrent saves;
- if another edit commits during a save, the loop immediately saves the newest revision before declaring `Saved`;
- `FlushAsync` cancels the delay and synchronously catches up to the latest revision;
- failed saves set `SaveFailed` and remain retryable;
- disposal cancels only pending debounce work and does not pretend it was saved.

Editor save labels are exactly `Saved`, `Saving…`, `Unsaved`, and `Save failed`. Autosave failures produce an `InfoBar` and technical log entry. Workspace-settings failures are surfaced once per failure period and become eligible for another notification after a successful save.

## UI system

### Theme and tokens

The application forces the dark theme and merges WinUI control resources with `Styles/ThemeResources.xaml`.

Core palette:

| Token | Value | Use |
| --- | --- | --- |
| Window background | `#FF0D0E10` | App/home/editor base |
| Title bar | `#FF141518` | Top chrome |
| Surface | `#FF1A1C20` | Panels/cards |
| Elevated surface | `#FF22252A` | Timeline cards/status panels |
| Hover surface | `#FF272A30` | Selected/hovered cards |
| Control background | `#FF2A2D33` | Inputs, buttons, contained states |
| Border | `#FF343840` | One-pixel structure |
| Primary text | `#FFF2F4F7` | Headings and high-emphasis content |
| Secondary text | `#FFA8AEB8` | Body/captions |
| Tertiary text | `#FF737A86` | Placeholder/icon restraint |
| Accent | `#FF21CFA6` | Selection, playhead, primary actions |
| Accent hover | `#FF3CDDB9` | Hover/focus |
| Accent pressed/muted | `#FF18B991` | Pressed/brand secondary |
| Error | `#FFFF656D` | Validation/errors |
| Warning | `#FFF3B95F` | Missing media/warnings |
| Selection | `#3321CFA6` | Selected rail background |
| Preview background | `#FF050506` | Media viewport |
| Chrome background | `#FF111215` | Rail/timeline chrome |

Controls use a 6 px common corner radius. The common button styles define explicit borders, padding, primary/secondary focus brushes, and icon-button dimensions of 34 × 32. Rail buttons are 62 px wide with at least 54 px height.

Typography styles:

- title: 14 px semibold;
- page heading: 28 px semibold;
- section title: 16 px semibold;
- body: 14 px;
- caption: 12 px;
- timeline card title/detail: 11 px / 9 px.

### Home screen

The Home layout has a 48 px custom title bar and a left `NavigationView` pane 208 px wide.

Navigation items:

- Home: project list headed `Recent projects`;
- Projects: the same complete local list headed `All projects`;
- Settings: a local-preferences information panel.

There is no filtering difference between Home and Projects in the current implementation; only the heading changes.

Project content uses 32/28/32/36 padding and a responsive `UniformGridLayout` with 260 px-wide, 224 px-high cards. Each card reserves 140 px for a thumbnail. The card is broadly clickable, while a separate actions button opens Open/Rename/Duplicate/Delete commands.

Empty state, busy indicator, create action, errors, and destructive confirmation are explicit. The Settings page states that there is no account, cloud sync, telemetry, or online library.

### Editor shell geometry

The editor root rows are:

- 48 px top bar;
- flexible workspace;
- 8 px timeline resize handle;
- timeline row, default 300 px and clamped to 180–600 px.

The main workspace columns are:

- 70 px tool rail;
- 1 px divider;
- 292 px tool panel;
- 1 px divider;
- flexible preview with a 280 px minimum;
- optional 1 px inspector divider;
- optional 324 px inspector.

At an editor width of 1320 px or less, the desktop inspector collapses and the same 324 px inspector becomes a right overlay. Above that breakpoint, the inspector can be independently shown or hidden as a fixed column. Toggling layout always closes the previous overlay state.

The top bar contains Back, product mark/name, editable project name, save status, Undo, Redo, inspector toggle, and accent Export. The center remainder is the native title-bar drag region; the project name remains outside that drag region so it is interactive.

### Tool rail and panel

The 70 px rail scrolls vertically and contains Media, Audio, Text, Stickers, Effects, Transitions, Captions, Filters, and Adjustment. The selected button uses the translucent selection brush, accent foreground, and automation help text `Selected`.

The 292 px panel changes by tool:

- Media: visual import, filename search, visual asset grid.
- Audio: audio import, filename search, audio asset grid.
- Text: primary Add text action plus Default, Title, Subtitle, and Minimal label presets.
- Other categories: icon, category name, and `This category is not included in this version.`

Asset cards are 124 × 154 layout cells with 78 px thumbnails, type badges, missing badge, filename, duration, dimensions, tooltip full path, drag support, keyboard/double-click activation, and context actions.

### Inspector

Two `InspectorPanel` instances exist so wide and narrow layouts can switch without dynamically reparenting the same control. Both receive the same project and selection and emit the same strongly typed edit events.

Project inspector:

- project name display;
- aspect ratio selector;
- resolution;
- fixed 30 fps label;
- opaque ARGB background input.

Video inspector:

- filename, source resolution, source duration;
- source-in and source-out seconds;
- effective timeline duration;
- volume slider, mute, reset volume.

Image inspector:

- filename and resolution;
- timeline duration seconds;
- reset to five seconds.

Audio inspector:

- filename and source duration;
- explicit timeline start;
- source-in/source-out;
- volume, mute, reset volume.

Text inspector:

- multiline content;
- supported font family;
- font size;
- bold and italic;
- left/center/right alignment;
- text and background ARGB colors;
- background enable switch;
- opacity slider with percentage label;
- normalized horizontal and vertical `NumberBox` controls;
- timeline start and duration;
- reset style.

Inline validation is a polite live region. Invalid input remains visible with a concise correction message. Model values are refreshed after rejected/no-op edits so controls do not drift from canonical state.

### Visual and state language

- Accent color marks primary action, playhead, selected cards, selected rail category, and selected live text.
- Missing media uses a text badge and warning foreground, not color alone.
- Muted timeline items include a `MUTED` text badge.
- Locked tracks change glyph/name/tooltip, dim cards, suppress trim handles, and block mutation.
- Disabled export includes automation help text explaining whether media is absent or export is already active.
- Empty states contain both icon and explanatory text.
- Routine failures use non-modal `InfoBar`; destructive project deletion uses a modal confirmation dialog.

### Accessibility implemented in code/XAML

- Icon-only buttons generally include both `AutomationProperties.Name` and a tooltip.
- Toggle automation names update with the next available action (`Hide`/`Show`, `Mute`/`Unmute`, `Lock`/`Unlock`).
- Current/total timecode updates its automation name with both values.
- Export status uses `AutomationProperties.LiveSetting="Polite"`.
- Inspector validation uses a polite live setting.
- Missing, muted, locked, selected, disabled, and error states have non-color cues.
- Common button styles specify visible focus brushes.
- The editor top bar, tool rail, and preview transport carry explicit tab indices; remaining controls use visual-tree focus order.
- Keyboard shortcuts avoid overriding editable text controls.

This is an implementation inventory, not a claim of full WCAG conformance. Pointer-only operations such as free dragging and timeline resizing should remain explicit review targets.

## Error handling and concurrency

### User-visible error strategy

- Home project errors: closable error `InfoBar`.
- Editor import, missing media, locks, preview, save, and export: central `InfoBar` with severity/title/message.
- Field validation: inline inspector validation text.
- Project deletion: confirmation `ContentDialog`.
- Export progress/success: dedicated status card.
- Native technical detail: bounded local log rather than raw stack traces in UI.

Expected exception filters are intentionally narrow around I/O, access, invalid data/arguments, unsupported formats, invalid operations, COM/native media errors, and cancellation. `OutOfMemoryException` is not swallowed by broad export handling.

### Concurrency/lifetime mechanisms

| Mechanism | Prevented failure mode |
| --- | --- |
| `HomeProjectOpenGate` | Duplicate/concurrent Home open or create. |
| `EditorImportGate` | Two import preparations committing duplicate results. |
| `DebouncedSaveCoordinator` | Overlapping saves and stale revision marked saved. |
| `PreviewRebuildGate` | Older async composition replacing newer state. |
| `EventGenerationGate` | Queued native playback callback from a disposed/replaced source. |
| `LiveTextRenderGate` | Model rerender destroying a captured text-drag visual. |
| `ExportOperationState` | Overlapping export setup/render and continuation during close. |
| Thumbnail request snapshot + asset lock | Stale thumbnail publishing after relink. |
| Text renderer semaphore | Concurrent use of one hidden XAML render host. |
| Simple log semaphore | Concurrent read-trim-write corruption. |
| Editor lifetime token | Post-disposal import/thumbnail/preview UI commits. |

## Test architecture and coverage

The suite is a single x64 MSTest assembly referencing the app project. Internal visibility allows deterministic utilities and state gates to be tested without introducing a separate production abstraction layer.

The current 833 passing cases cover these major areas:

| Area | Representative coverage |
| --- | --- |
| Model/schema | Defaults, enum JSON, absent/null optional fields, schema requirement, identity matching, normalization. |
| Project service | Create/list/load/save/rename/duplicate/delete, timestamps, deep independence, directory confinement, atomic-save failure/cancellation. |
| Settings/log | Workspace normalization, DPI bounds, physical-bound versions, bounded/concurrent log writes. |
| Timeline math/editing | Magnetic bounds, 24-hour limits, reorder, split, trims, move, duration, delete, duplicate, snapping, zoom, ruler virtualization. |
| Undo/autosave | 50-entry bound, redo invalidation, rapid debounce, edit-during-save, retry after failure, save-state baseline. |
| Media import | Path normalization/dedupe, metadata mapping, minimum duration, relink kind/length, missing refresh, source-safe removal. |
| Thumbnails | Key/path confinement, bounded copy, stale-request rejection after relink. |
| Composition/preview | Filler rules, trims/delay/volume, missing media, overlay timing, rebuild cancellation, resource cleanup, playback generation/intent, live-text render gating. |
| Text | Presets, style setters/reset/duplicate, coordinate clamp, style hash, cache reuse/regeneration/race/cancellation/bounds, transparent PNG. |
| Export | Preflight, snapshot isolation, source overwrite refusal, staging commit/cleanup, cancellation, profile dimensions/bitrates, close/setup state, picker-path contracts. |
| UI contracts | Project-name drag-region separation, Text panel actions, rail/track states, import scopes, Home gates, shortcut routing, duration ingress. |

Some tests instantiate WinUI/native media types and use generated temporary media fixtures. The suite proves a large amount of behavior below the interactive surface, but it does not replace manual checks of focus order, real pointer ergonomics, native picker appearance, playback smoothness, installed-codec behavior, multi-monitor movement, or final rendered visual quality.

## Code-review map and critical invariants

This section identifies the most useful seams for review-question generation.

### Persistence review

Primary files:

- `Services/ProjectService.cs`
- `Services/SettingsService.cs`
- `Utilities/DebouncedSaveCoordinator.cs`
- `Models/ProjectDocument.cs`

Critical invariants:

- A failed project save must preserve prior JSON and prior `ModifiedAt`.
- Project directory resolution must remain a direct GUID child of `Projects`.
- External media paths must never become deletion targets.
- A new schema version must introduce an explicit migration/compatibility decision; silently accepting it is unsafe.
- Any new model field must have a safe missing-JSON default and normalization path.
- `CommitEdit` call sites must remain constrained to operations that cannot violate timeline limits.

### Timeline review

Primary files:

- `Services/TimelineEditingService.cs`
- `Utilities/TimelineMath.cs`
- `Utilities/TimelineLayoutProjection.cs`
- `Controls/TimelineControl.xaml.cs`

Critical invariants:

- V1 starts are derived; no code should persist or independently mutate a V1 start.
- Every successful user gesture should create one history entry, not one per pointer move.
- Source ranges must remain ordered, duration-compatible, and within known asset duration.
- All tracks must stay within the 24-hour project limit and 100 ms minimum.
- Lock enforcement must cover toolbar, context menu, keyboard, inspector, preview text drag, and drag/drop ingress.
- Visual previews may be permissive, but final service edits must clamp/reject invalid source ranges.

### Async media/cache review

Primary files:

- `Services/MediaImportService.cs`
- `Services/ThumbnailService.cs`
- `Services/TextOverlayRenderer.cs`
- `Services/PreviewRebuildGate.cs`

Critical invariants:

- A stale asynchronous result must never overwrite a relinked asset or newer composition.
- Cache resolution and deletion must remain project-confined.
- Cancellation must prevent commit after preparation.
- Cache reuse must validate content, not merely file existence.
- Hidden XAML render-host access must remain serialized on the UI-capable context.

### Preview/composition review

Primary files:

- `Services/CompositionPlan.cs`
- `Services/CompositionService.cs`
- `Controls/PreviewPane.xaml.cs`
- `Utilities/PreviewPlaybackStateCoordinator.cs`

Critical invariants:

- Preview and export must derive from the same serializable timeline semantics.
- Preview must not rasterize a second copy of text below the live XAML layer.
- Missing visuals must retain duration through filler; missing audio must not shift other content.
- Native media events from a replaced source must be generation-invalidated.
- Position and play intent must survive a composition rebuild without reporting false playback state.
- Native source/player disposal order must detach the element before disposing resources.

### Export review

Primary files:

- `Services/ExportService.cs`
- `Services/ExportPresentation.cs`
- `Views/EditorView.Export.cs`

Critical invariants:

- Never render directly over the destination.
- Never allow destination to equal any imported source path.
- Cancellation/failure may delete only the unique staging file.
- Export must use a deep snapshot so active editing cannot mutate the render.
- Close must invalidate setup, cancel render, await completion, and then save.
- UI success must be shown only after staging commit succeeds.

### UI review

Primary files:

- `Styles/ThemeResources.xaml`
- `Views/HomeView.xaml[.cs]`
- `Views/EditorView.xaml[.cs]`
- `Controls/*.xaml[.cs]`

Critical contracts:

- Preserve the dense three-zone editor geometry and the 1320 px inspector transition unless redesign is intentional.
- Keep unsupported tools honest; do not imply an action exists when it does not.
- Every icon-only control needs an accessible action name and tooltip.
- State must not rely on color alone.
- Editable controls must continue to suppress destructive/global shortcuts.
- Rejected edits must refresh controls from canonical state.
- Wide and overlay inspectors must receive identical project/selection updates.
- Pointer-only actions should have discoverable or keyboard-accessible alternatives where practical.

## Extension guide

When adding a model-backed editing feature, the smallest coherent change usually crosses these layers:

1. Add a JSON field with a safe default in `Models`.
2. Normalize it in `ProjectService`.
3. Add a deterministic operation in `TimelineEditingService` or `EditorViewModel`.
4. Route one typed edit event from `InspectorPanel`, `TimelineControl`, or `PreviewPane`.
5. Apply it in both `CompositionPlan`/`CompositionService` and the live preview layer if it affects pixels/audio.
6. Include every pixel-affecting text field in `TextOverlayRenderer.CalculateStyleHash`.
7. Add focused serialization, edit/history, composition, and acceptance-contract tests.
8. Update this document and `README.md` only where user-visible behavior changes.

Avoid adding a new abstraction layer unless the feature actually requires an independent boundary. Current architecture intentionally keeps one app project, concrete services, typed events, and deterministic static helpers.

## Known implementation facts worth preserving or reconsidering explicitly

- Home and Projects currently show the same project collection; only the heading differs.
- Track locks and snapping are intentionally session-only and reset when the editor is recreated.
- Preview mute is session-only and does not change item or track model volume.
- Duplicating a project does not copy generated caches.
- Project save is atomic; settings save is not.
- Timeline cards have a visual minimum width, so very short clips are not visually proportional at extreme scales.
- A1 items can overlap and are all added as native background tracks.
- The preview frame cap is 820 × 480, while the generated native preview stream is independently capped at 1280 × 720.
- Preview uses live XAML text; export uses validated cached PNG overlays.
- Export preflight is stricter than preview: preview degrades gracefully, while export rejects missing referenced media.
- Windows codec availability remains an external runtime dependency even when extension validation succeeds.

These are not automatically defects. They are current product/engineering behaviors that should be changed only with an explicit design and test update.
