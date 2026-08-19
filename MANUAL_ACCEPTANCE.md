# Manual pointer and focus acceptance

This checklist is an interactive packaged-app release check. It is not automated and is not proven by the test suite or Release build.

Record the app commit/build, Windows version, display scaling, input device, and result for each run. Use a project containing at least one video or image, one audio item, and one text item.

## Fresh-checkout release gate

Start from a fresh checkout with no `bin` or `obj` output. Run `.\scripts\Verify-CutFlowRelease.ps1 -RegisterAndLaunch` from the repository root, then record:

- [ ] Restore, Release x64 tests, and the non-incremental Release x64 build complete successfully without relying on prior generated output or local analysis state.
- [ ] The loose Release package registers from this checkout and opens to Home through its package identity.
- [ ] Registration and launch require no installed development certificate, repository-specific environment variable, or manually generated fixture. Developer Mode and the documented Windows App SDK runtime are machine prerequisites.
- [ ] The generated `AppxManifest.xml`, executable, PRI/resources, and declared package assets all exist under this checkout's Release layout.

Do not treat an incremental developer build, successful registration alone, or process creation without a usable Home window as acceptance.

## Pointer and timeline

- [ ] With mouse and touchpad, select, move, reorder, and trim timeline items from both edges. Targets are practical to acquire, the preview follows the pointer without flicker, and release creates exactly one undoable edit.
- [ ] Drag the playhead and click each track background. Seeking follows the pointer, including after horizontal scrolling and at both timeline ends.
- [ ] Start each timeline drag, keep the pointer button down, switch away from CutFlow and back, then release. Capture loss cancels the preview, does not commit an edit, and leaves the next drag working.
- [ ] Drag the timeline resize handle through its full range. The row previews continuously, commits on release, remains usable at minimum window height, and restores its prior height after the same capture-loss sequence.
- [ ] Drag selected text in the preview, including beyond every edge. The live text follows the pointer; release clamps it to the canvas and creates one undoable edit; capture loss restores the canonical position.

## Focus and keyboard

- [ ] Starting at Home, use only `Tab`, `Shift+Tab`, arrow keys, `Enter`, `Space`, `Esc`, and context-menu keys. Focus is visible, follows visual order, reaches every action, and returns to a sensible control after navigation, menus, and dialogs.
- [ ] Repeat through the editor top bar, all nine tool-rail items, active tool panel, preview transport, timeline resize handle, timeline items, and both wide and overlay inspectors. Focused rail items scroll into view.
- [ ] In real `TextBox`, `NumberBox`, slider, toggle, timeline, and preview controls, verify the documented shortcuts in `README.md`. Typing, `Space`, `Delete`, and `Ctrl` shortcuts edit the focused control when appropriate and do not accidentally trigger editor-wide actions.

## Native surfaces and responsive layout

- [ ] Open import, relink, and export pickers and the rename, delete, and export dialogs using mouse and keyboard. Filters, default/cancel actions, validation, initial focus, focus restoration, and cancellation behave correctly.
- [ ] Resize across 1320 logical pixels while focus is in an inspector text field. Wide and overlay inspectors switch without stale duplicates, losing the active edit, or trapping focus; `Esc` closes the overlay and returns focus to its toggle.

## Real media playback and codec coverage

Run this section on Windows 11 and on the declared minimum supported Windows 10 version, using the codecs installed on each clean test machine. Record the exact Windows build and any additional codec packages. Do not treat a developer machine with extra codecs as representative of the default installation.

- [ ] Import and play representative H.264 MP4, MP3, PCM WAV, PNG, ordinary JPEG, and EXIF-rotated JPEG files. Confirm correct orientation, continuous audio/video, responsive seeking, frame stepping, and smooth play/pause transitions.
- [ ] Build a timeline with overlapping A1 items, muted and non-muted V1/A1 items, audio extending beyond V1, and loop both enabled and disabled. Confirm the audible mix, exact end behavior, playhead continuity, and absence of duplicate audio after edits.
- [ ] While playback is active, make rapid timeline edits that rebuild the preview. Confirm position and play intent survive, obsolete rebuilds never replace the latest preview, and audio does not overlap with the replaced source.
- [ ] Try a valid supported container whose codec is absent on that machine. Confirm import or playback fails without a crash and the message explains that Windows may require the missing codec; repeat after installing the codec when one is available.

## Display and assistive technology

- [ ] At 100%, 150%, and 200% scaling, and while moving between monitors with different scaling, check Home, editor, timeline hit targets, dialogs, and pickers. Nothing clips, overlaps, becomes unreachable, or changes pointer alignment.
- [ ] Enable a Windows High Contrast theme. Text, focus indicators, selection, disabled state, warnings, timeline cards, trim handles, playhead, and resize handle remain distinguishable without relying on color alone.
- [ ] With Narrator, traverse Home and the editor. Control names, selected/muted/locked states, changing timecode, inspector validation, export availability/progress/success, and error messages are announced once, at the right time, without stale or duplicate announcements.

Any failed item blocks acceptance until its exact build, environment, reproduction steps, and observed result are recorded and resolved or explicitly accepted.

## Rendered export visual and audio acceptance

Use real exported MP4 files for this section. The automated suite proves the requested encoding-profile values, native output readability, output dimensions and approximate duration, composition timing values, filler color, cancellation propagation, and destination/staging safety. It does not prove perceived image quality, exact visible trim frames, audible timing or loudness, or rendered typography.

Create a short reference project with frame-identifiable video, a still image, an intentional V1 gap or hidden V1 section, two distinguishable audio cues, and text using non-default font, size, weight, italic, alignment, color, background, opacity, and normalized position. Include source-in/source-out trims and place audio and text at non-zero timeline starts.

- [ ] Export all three aspect ratios (16:9, 9:16, and 1:1) at 720p and 1080p. Inspect the saved file metadata: MP4 container, H.264 video, AAC audio, expected pixel dimensions, and duration matching the project within one frame. Record the requested 30 fps profile and the tool-reported effective frame rate; native readers can derive a different value for very short still-image output. Record the reported average bitrate for each Standard and High export; compare quality tiers without requiring a constant-content file to equal the configured target bitrate exactly.
- [ ] Play every file from start through end. Confirm source-in/source-out trims start and stop on the intended visible frames, adjacent clips have no missing or repeated interval, still-image durations are correct, and filler sections use the configured project background without flashes or stale frames.
- [ ] Confirm each aspect ratio uses the full intended canvas without stretching, unintended cropping, pillarboxing, letterboxing, rotation errors, or text displacement. Check both 720p and 1080p for equivalent layout.
- [ ] Listen with speakers or headphones. Confirm embedded clip audio and A1 audio begin at the intended times, trimmed audio boundaries are correct, overlapping tracks mix once, muted tracks are silent, and relative volume changes are perceptible without clicks, duplicated audio, or tail audio after the project end.
- [ ] Inspect text at its start, middle, and end. Confirm content, font family, size, weight, italic, alignment, foreground/background colors, opacity, line wrapping, normalized position, transparent surroundings, and on/off timing match the editor preview. Check edges for clipping, halos, opaque rectangles, scaling blur, or a one-frame early/late appearance.
- [ ] Start an export over an existing destination, cancel during native rendering, and confirm the existing file is byte-for-byte unchanged and no `.cutflow-*.mp4` staging file remains. Repeat a successful export and confirm only the chosen destination is replaced and the finished file opens normally.

Record the CutFlow build/commit, Windows build, installed codec packages, source fixture hashes, export options, destination state, player/metadata tool, and pass/fail result. A failure in this section blocks rendered-output acceptance even when automated tests and the Release build pass.
