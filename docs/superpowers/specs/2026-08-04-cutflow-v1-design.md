# CutFlow V1 Design

## Approval and scope

The user supplied an implementation-ready product specification and explicitly directed the implementation to proceed without follow-up questions. That specification is the approved design baseline. This document records the concrete engineering choices used to implement it without expanding the requested scope.

CutFlow V1 is a local-only, packaged WinUI 3 desktop application for Windows. It provides the essential workflow of a compact modern video editor: local projects, media import, a magnetic primary track, text and audio tracks, preview, editing, persistence, undo/redo, and MP4 export. Unsupported creative categories remain visible only as honest empty states.

## Considered approaches

1. **One packaged WinUI 3 app plus one unit-test project — selected.** This matches the specification, supplies package identity for pickers and Windows media APIs, and keeps the codebase small. Testable model and editing code remains in the app project and is referenced by the test project.
2. **A separate core class library.** This makes test references simpler but adds a third project and an artificial boundary to a small product. It is unnecessary for V1.
3. **An unpackaged self-contained app.** This simplifies direct executable launch in some environments, but loses the requested packaged single-project model and makes package-identity-dependent Windows behavior less predictable.

## Technology and deployment

- C#, XAML, WinUI 3, .NET 10.0.302 LTS, Windows SDK 10.0.26100.0, and Windows App SDK 2.3.1, all verified stable and compatible with the installed toolchain.
- Packaged single-project MSIX application with x64 as the primary development architecture.
- Windows 11 is the primary visual target; the app targets `net10.0-windows10.0.26100.0` and declares Windows 10 build 17763 as its supported minimum without adding down-level special cases.
- No web shell, backend, database, DI container, MVVM framework, FFmpeg, telemetry, or cloud dependency.
- `System.Text.Json` stores lightweight project documents. Imported media remains at its original absolute path.

## Application shape

`MainWindow` owns the custom title bar, minimum/remembered window geometry, global keyboard routing, and switches a single content host between `HomeView` and `EditorView`.

The implementation uses three focused state holders:

- `MainViewModel` coordinates Home/Editor navigation and the current project.
- `HomeViewModel` loads recent projects and performs create, rename, duplicate, and delete operations.
- `EditorViewModel` owns the editable `ProjectDocument`, selection, playhead, save state, undo history, and commands that affect more than one control.

View-specific pointer behavior stays in code-behind. In particular, `TimelineControl` performs hit testing, capture, drag previews, trim handles, scroll synchronization, and pixel/time conversion. It commits one model operation on pointer release through `TimelineEditingService`; it never performs file or media-composition work.

Services are concrete classes constructed directly in `MainWindow`:

- `ProjectService`: local paths, listing, atomic JSON persistence, rename, duplicate, and project-only deletion.
- `SettingsService`: bounded non-sensitive window and editor preferences.
- `MediaImportService`: file validation, metadata, deduplication, and missing-media/relink updates.
- `ThumbnailService`: lazy, keyed thumbnail generation into project cache.
- `CompositionService`: fresh `MediaComposition` construction from the serializable model.
- `TextOverlayRenderer`: transparent cached PNG overlays keyed by text style and output resolution.
- `ExportService`: validates, builds a full-resolution composition, and renders MP4 asynchronously.
- `SimpleLogService`: bounded local exception log with readable UI errors kept separate.

## Data model and editing semantics

`ProjectDocument` schema version 1 contains identity, timestamps, `ProjectSettings`, media assets, ordered V1 items, positioned A1 items, and positioned T1 items. Identifiers are GUIDs and every serialized time is an integer millisecond value.

V1 is a magnetic sequential list. Its timeline starts are derived from ordered durations, so reorder, trim, split, and delete cannot leave gaps. T1 and A1 items have explicit non-negative starts and can extend past V1. All edits enforce a 100 ms minimum item duration and clamp source ranges to known asset duration.

`TimelineEditingService` implements add, reorder, split, trim, move, duplicate, delete, duration, and clamping as deterministic functions. `UndoHistory` stores at most 50 lightweight project snapshots. Continuous UI changes preview immediately but commit as one history entry.

## Persistence flow

Projects live under the app local data folder in `CutFlow/Projects/{id}` with `project.json`, `cache/thumbnails`, and `cache/text-overlays`. Source media is never copied or deleted.

Committed edits mark the project dirty. A 1-second debounce triggers autosave; Ctrl+S, editor exit, export, and window closure save immediately. Saving serializes to a sibling temporary file, flushes it, then atomically replaces or moves it into `project.json`. A failed serialization leaves the previous file intact and updates the visible save state.

When opening a project, each source path is revalidated. Missing assets stay in the library and timeline with explicit missing state, are excluded from preview/export, and can be relinked to a compatible file.

## Media, preview, text, and export flow

Native multiple-file pickers and Explorer drag/drop accept decodable MP4, PNG/JPEG, MP3/WAV, plus additional formats only when Windows metadata/media APIs validate them. Metadata and thumbnails are asynchronous and cancellation-aware; media bytes are never loaded wholesale.

Structural edits debounce composition rebuilding. `CompositionService` creates a new `MediaComposition` from V1 clips, A1 background tracks, and cached text overlays. `EditorView` preserves the global playhead and prior play state while replacing the preview `MediaSource`. A timer driven by the playback session updates timecode and the visual playhead without rebuilding the timeline tree.

Active text is also rendered as XAML over the preview for responsive edits and normalized drag positioning. Export uses the same model but creates a fresh full-resolution composition and transparent overlay PNGs, then calls `RenderToFileAsync` with H.264/AAC MP4 settings for 720p or 1080p at 30 fps. Progress and cancellation remain asynchronous; incomplete output is removed only when safe.

## Visual system

Reusable XAML resources hold the specified opaque palette, compact typography, spacing, radii, focus visuals, and common button/field styles. The editor uses a 48 px top bar, 70 px rail, approximately 292 px content panel, flexible preview, approximately 324 px inspector, and approximately 300 px lower timeline with one-pixel dividers. Layout uses grids and width thresholds; the inspector can collapse before the preview becomes unusable.

All icon-only controls have tooltips and automation names. Selected, missing, muted, disabled, and error states use text or icon cues in addition to color. Unsupported rail categories contain only a category icon, name, and scope statement.

## Error handling and lifecycle

Routine failures use a non-modal `InfoBar` naming the operation and affected file/item. Destructive project deletion uses a confirmation dialog. Technical exceptions are appended to the bounded local log. Background work uses cancellation tokens and checks view lifetime before updating UI. Media sources and streams are released when replaced and at editor/window shutdown.

## Verification strategy

Unit tests cover timeline scale, timecode, aspect dimensions, duration, reorder, split, trim, positioned-item clamping, 100 ms minimum duration, undo/redo, JSON round trips, schema persistence, and absent optional JSON values. Each behavioral implementation follows red-green-refactor.

Verification proceeds incrementally: restore/build after scaffold, tests after each model/editing slice, packaged launch after each major UI slice, then a final clean build and full test run. The final manual acceptance pass uses generated local MP4, PNG/JPEG, and WAV assets and records exactly which UI actions, playback behavior, persistence, resize behavior, and export output were actually observed.

## Explicit non-goals

CutFlow V1 has no accounts, online libraries, AI features, automatic captions, collaboration, advanced effects, arbitrary extra tracks, complex grading, plugin architecture, watermark, social publishing, or fake waveform. No architecture is reserved for those features.
