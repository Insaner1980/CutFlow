# CutFlow

CutFlow is a local-only Windows video editor built with C#, WinUI 3, and the native Windows media stack. It provides a compact project home, a three-track editing workspace (V1 video/images, A1 background audio, and T1 text), native preview, JSON autosave, and MP4 export. Imported media stays at its original location; CutFlow stores only project data and generated cache files.

## Requirements

- An x64 Windows PC. Windows 11 is the primary target. The package declares Windows 10 version 1809 (build 17763) as its minimum, but media support still depends on the codecs installed in Windows.
- [.NET SDK 10.0.302](https://dotnet.microsoft.com/download/dotnet/10.0), pinned by `global.json` (`latestPatch` roll-forward, prerelease SDKs disabled).
- Windows SDK 10.0.26100.0. The project restores `Microsoft.Windows.SDK.BuildTools` 10.0.26100.7705.
- [Windows App SDK 2.3.1 x64 runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads). The matching 2.3.1 NuGet package is restored with the project.
- PowerShell and [Developer Mode](https://learn.microsoft.com/windows/apps/get-started/enable-your-device-for-development) for loose-package registration.

The verified development configuration is x64; x86 and Arm64 are not configured by this solution. Visual Studio is optional for the command-line workflow below.

## Restore, test, and build

Run from the repository root:

```powershell
dotnet restore .\CutFlow.slnx
dotnet test .\CutFlow.slnx -c Release --no-restore
dotnet build .\CutFlow.slnx -c Release -p:Platform=x64 --no-restore
```

The Release loose-package layout is written to:

```text
src\CutFlow\bin\x64\Release\net10.0-windows10.0.26100.0
```

These commands are the reproducible verification sequence; successful execution on the target machine is required before treating a build as release-ready.

## Register and run

The application is a framework-dependent, packaged WinUI 3 app. Register the built loose layout before launching it. This is a development deployment, not a signed installer:

```powershell
$manifest = Resolve-Path .\src\CutFlow\bin\x64\Release\net10.0-windows10.0.26100.0\AppxManifest.xml
Add-AppxPackage -Register $manifest.Path

$package = Get-AppxPackage -Name CutFlow
Start-Process "shell:AppsFolder\$($package.PackageFamilyName)!App"
```

For iterative Debug work, build and register the Debug layout, then launch it through its package identity:

```powershell
dotnet build .\src\CutFlow\CutFlow.csproj -c Debug -p:Platform=x64
$manifest = Resolve-Path .\src\CutFlow\bin\x64\Debug\net10.0-windows10.0.26100.0\AppxManifest.xml
Add-AppxPackage -Register $manifest.Path

$package = Get-AppxPackage -Name CutFlow
Start-Process "shell:AppsFolder\$($package.PackageFamilyName)!App"
```

If activation reports a missing framework package, install the Windows App SDK 2.3.1 x64 runtime linked under Requirements and register the layout again.

## Editing workflow

1. Select **New project**. CutFlow creates and saves a local 16:9, 1920x1080, 30 fps project.
2. In **Media** or **Audio**, use **Import** or drop files from File Explorer onto the library. Files are validated by Windows before being added.
3. Double-click an asset, or use **Add to track**. Video and images append to magnetic V1; audio is added to A1 at the playhead.
4. Select timeline items to use the contextual inspector. Drag items to reorder V1 or reposition A1/T1, and drag clip edges to trim. The toolbar and context menu provide split, duplicate, delete, undo, and redo.
5. Use **Text** to add a Default, Title, Subtitle, or Minimal label preset, then edit and position it in the preview.
6. Preview and seek with the transport controls or timeline. Changes autosave after a short debounce; returning Home, exporting, and closing perform an immediate save boundary.
7. Select **Export**, choose a profile and `.mp4` destination, and keep the editor open while native rendering completes.

Missing source files remain visible in the project. Relink them from the asset menu before previewing or exporting. Removing an asset or deleting a CutFlow project never deletes the original imported media.

## Supported input

| Type | Accepted extensions | Notes |
| --- | --- | --- |
| Video | `.mp4` | Must be decodable by the installed Windows media stack. |
| Images | `.png`, `.jpg`, `.jpeg` | Images are added with a default five-second duration. |
| Audio | `.mp3`, `.wav` | Must be readable as a native `BackgroundAudioTrack`. |

An accepted extension is not a guarantee that the contained codec or file is valid. Unsupported, damaged, inaccessible, duplicate, or too-short files are rejected with a readable error. Other container extensions are not accepted in V1.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+N` | Create a new project |
| `Ctrl+S` | Save immediately |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
| `Ctrl+B` | Split the selected V1 clip at the playhead |
| `Ctrl+D` | Duplicate the selected supported item |
| `Delete` | Delete the selected item |
| `Space` | Play / pause |
| `Home` / `End` | Seek to the start / end while the timeline has focus |
| `Left` / `Right` | Seek backward / forward 100 ms while the timeline has focus |
| `Escape` | Close the inspector overlay or clear selection |
| `Ctrl` + mouse wheel | Zoom the timeline around the pointer |

Editor shortcuts do not override normal text editing while a text field has focus.

## Local data

As a packaged app, CutFlow uses its package-local `LocalState` directory:

```text
%LOCALAPPDATA%\Packages\<CutFlow package family>\LocalState\
|-- Projects\<project-id>\
|   |-- project.json
|   `-- cache\
|       |-- thumbnails\
|       `-- text-overlays\
|-- settings.json
`-- cutflow.log
```

Resolve the exact directory for the registered development package with:

```powershell
$package = Get-AppxPackage -Name CutFlow
Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalState"
```

`project.json` is the schema-versioned source of truth. `settings.json` stores non-sensitive window and editor preferences. `cutflow.log` is a bounded local technical log. Thumbnail and text-overlay files are disposable generated caches. Imported media is referenced by absolute path and is neither copied into LocalState nor deleted by CutFlow.

## Export profiles

Exports are H.264 video with AAC stereo audio in an MP4 container, always at 30 fps. Audio uses 192 kbit/s, 48 kHz, two channels.

| Tier | 16:9 | 9:16 | 1:1 | Standard video | High video |
| --- | --- | --- | --- | --- | --- |
| 720p | 1280x720 | 720x1280 | 720x720 | 5 Mbit/s | 8 Mbit/s |
| 1080p | 1920x1080 | 1080x1920 | 1080x1080 | 8 Mbit/s | 12 Mbit/s |

Export requires at least one visual V1 item and all referenced V1/A1 source files. Rendering uses `MediaComposition.RenderToFileAsync` with precise trimming and a temporary sibling file; an incomplete temporary export is removed after failure or cancellation. CutFlow does not add a watermark.

## V1 limitations

- Playback, import, and export use native Windows media APIs. Codec availability and behavior therefore vary with the Windows installation; FFmpeg is not bundled.
- Project timeline positions and durations are limited to 24 hours. Inspector input beyond that limit is rejected, and out-of-range saved values are normalized before editing or composition.
- The Windows SDK 10.0.26100 projection for `BackgroundAudioTrack` exposes delay, trim, and volume, but no fade-in/fade-out API. Fade values are serialized in the project schema, but V1 does not expose or apply them in preview or export.
- V1 has one magnetic visual track plus positioned text and background-audio tracks. It does not provide stacked video, arbitrary tracks, transitions, effects, stickers, captions, filters, or adjustment processing; those rail categories are deliberate informational empty states.
- There are no accounts, cloud sync, online assets, AI features, telemetry, social publishing, or collaboration.
- Export is limited to the profiles above. Hardware encoder selection and advanced codec controls are not exposed.
- The repository produces a development loose-package layout. Creating and signing a distributable MSIX installer is outside the current workflow.
