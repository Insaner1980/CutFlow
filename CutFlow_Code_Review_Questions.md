# CutFlow Code Review Questions

## Repository authority, product scope, and architecture

### 001. Source-of-truth consistency

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review whether the implementation, tests, README, historical design documents, and comments consistently follow the authority rule stated in PROJECT.md: current source and executable tests win when documents disagree. Look for stale comments, assertions, test names, or helper APIs that encode obsolete design intent and could mislead future changes. Do not rewrite documentation merely because wording differs; identify only contradictions that could cause incorrect behavior, maintenance mistakes, or false assumptions.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 002. Local-only product boundary

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the entire production dependency graph for accidental violations of CutFlow's local-only boundary. Check for HTTP clients, web views, remote URLs, analytics SDKs, telemetry hooks, crash-upload services, account or identity code, cloud-storage access, online asset loading, background network activity, or packages that initialize such behavior implicitly. Also verify that user-facing copy does not promise or imply cloud, collaboration, AI, or online-library functionality that the application does not contain.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 003. Concrete composition-root integrity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review App, MainWindow, and service construction to verify that CutFlow still has one clear composition root with concrete services and no hidden service locator, ambient mutable global state, or accidental second application container. Trace how each long-lived and project-scoped dependency is created, shared, and disposed. Check especially whether any control constructs its own competing persistence, export, import, settings, or logging service in a way that can split state or bypass lifecycle coordination.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 004. MVVM and code-behind responsibility boundary

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the practical MVVM/code-behind split described in PROJECT.md. Verify that serializable state and deterministic edits remain in models, services, utilities, or EditorViewModel, while native media, picker, pointer-capture, Win32, dialog, focus, and visual-tree concerns remain in views and controls. Look for model mutation performed directly by InspectorPanel, TimelineControl, PreviewPane, or other controls without going through the intended typed event or edit transaction, and for UI-only objects leaking into serializable state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 005. Project-scoped dependency ownership

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the ownership and lifetime of EditorView, ThumbnailService, TextOverlayRenderer, CompositionService, PreviewRebuildGate, DebouncedSaveCoordinator, PreviewPane resources, and all project-scoped cancellation tokens. Confirm that opening a different project cannot reuse a cache root, renderer host, composition state, event subscription, save coordinator, or native media object from the previous project. Check both normal navigation and failure or cancellation paths.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 006. Event subscription symmetry

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Perform a repository-wide review of event subscriptions created by MainWindow, HomeView, EditorView, TimelineControl, PreviewPane, InspectorPanel, MediaPanel, and view models. For every subscription, verify the corresponding unsubscription or lifetime guarantee, including lambda subscriptions that cannot be removed easily. Look for duplicate subscriptions after navigation, layout switching, source replacement, or repeated initialization, and for callbacks that can run after the target object has been disposed.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 007. Nullable-reference correctness

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review nullable annotations and null-handling throughout the C# project with nullable reference types enabled. Focus on deserialized documents, optional selections, AppWindow and HWND initialization, native media objects, StorageFile results, picker cancellation, XAML named elements during teardown, event sender assumptions, and control DataContext transitions. Identify places where null-forgiving operators or broad null checks hide a real lifecycle or deserialization defect rather than expressing a proven invariant.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 008. No accidental architectural overgrowth

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review whether the current architecture has accumulated unnecessary abstraction layers, generic repositories, command frameworks, mediator patterns, reflection-based registries, or dependency-injection scaffolding that provide no required boundary for this compact V1 application. Also check the opposite risk: duplicated deterministic logic that should already be shared by an existing helper such as TimelineMath, CompositionPlan, TextStyle normalization, or a state gate. Recommend or implement change only where there is a concrete correctness or maintainability problem.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 009. Generated-output isolation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project and solution files, source includes, copy rules, test discovery, and runtime file enumeration to verify that generated bin and obj trees are never treated as architectural source, project data, importable media, or test fixtures by accident. Check for wildcard includes, recursive directory scans, packaging content rules, and cleanup code that could consume or delete generated output outside its intended scope.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 010. InternalsVisibleTo exposure

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the app project's internal visibility granted to CutFlow.Tests. Verify that the assembly exposure is limited to the intended test assembly and has not encouraged production code to make sensitive implementation details mutable or broadly accessible merely for testing. Look for test-only hooks that can be reached in production, conditional behavior that differs between test and packaged builds, or public APIs created solely because a test could not otherwise reach deterministic logic.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 011. Honest unsupported-tool states

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every rail category that is intentionally unsupported in V1, including Stickers, Effects, Transitions, Captions, Filters, and Adjustment. Verify that these surfaces remain informational empty states and cannot trigger partial commands, hidden model mutations, disabled-looking but focusable actions, stale dialogs, or placeholder code that writes unsupported fields. Check keyboard, automation, context-menu, and navigation routes as well as pointer clicks.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 012. Source-media ownership boundary

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every code path that imports, relinks, removes, duplicates, deletes, exports, cleans caches, or deletes projects to prove that CutFlow never takes ownership of imported source media. Trace normalized absolute source paths through all deletion and overwrite operations. Verify that only project documents, settings, logs, generated caches, operation-specific staging files, and explicitly selected export destinations can be created, replaced, or deleted by the application.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Build configuration, SDK targeting, packaging, and capabilities

### 013. Pinned SDK and project-target consistency

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review global.json, CutFlow.csproj, CutFlow.Tests.csproj, CutFlow.slnx, and any build scripts for consistency with the documented .NET SDK 10.0.302, latestPatch roll-forward, prerelease disabled, and net10.0-windows10.0.26100.0 target. Check that no project silently targets a different framework, architecture, runtime identifier, language version, or SDK behavior that could make local and CI results diverge.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 014. Windows App SDK package consistency

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all NuGet references and generated build assumptions around Microsoft.WindowsAppSDK 2.3.1 and Microsoft.Windows.SDK.BuildTools 10.0.26100.7705. Verify that transitive or duplicate package references do not select incompatible versions, that test execution can load the required WinUI/native assemblies, and that package restore does not depend on an unintended prerelease feed or machine-specific package cache.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 015. Minimum Windows version versus API usage

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every Windows API used by the application against the declared minimum Windows version 10.0.17763.0 and the target SDK level. Look for APIs, contracts, picker types, AppWindow behavior, MediaComposition features, or Win32 calls that require a newer OS without an IsApiContractPresent check, version guard, fallback, or deliberately higher minimum. Distinguish compile-time availability from actual runtime availability on the declared minimum OS.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 016. x64-only configuration integrity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the solution, project files, package manifest, runtime identifier, native dependencies, test settings, and launch instructions to verify that x64 is genuinely the only supported platform and that AnyCPU, x86, or ARM64 configurations cannot be selected accidentally. Check for conditional property groups that leave platform-specific settings undefined and for tests that pass under one architecture while packaged execution would fail under another.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 017. Packaged full-trust entry-point correctness

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Package.appxmanifest, project output settings, executable naming, application ID, and Windows.FullTrustApplication entry point. Verify that the packaged app launches the intended WinUI executable, that runFullTrust is declared correctly and no broader capabilities are present, and that changes to assembly or namespace names cannot silently break package activation while ordinary dotnet build still succeeds.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 018. Picker access without media-library capabilities

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every file-open, file-save, drag-and-drop, relink, and show-source path to confirm that CutFlow correctly relies on initialized user pickers or full-trust local paths rather than undeclared media-library capabilities. Check HWND or WindowId initialization, picker lifetime, cancellation, and storage permission behavior. Look for code that assumes broad library access merely because the process is full trust.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 019. Package identity and asset references

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review package identity CutFlow, version 1.0.0.0, publisher CN=CutFlow, application ID App, logo paths, splash assets, and all manifest resource references. Verify that referenced files exist with correct casing and package build actions, that no development-only asset path escapes the package, and that identity-dependent APIs use the actual packaged identity rather than hard-coded assumptions that differ in tests or unpackaged runs.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 020. PerMonitorV2 manifest behavior

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review app.manifest, WinUI window creation, coordinate conversion helpers, and any native DPI calls for consistency with PerMonitorV2 awareness. Check for duplicate DPI declarations, process-awareness APIs called too late, logical-to-physical conversion performed twice, or WinUI values treated as physical pixels. Verify behavior across startup, monitor changes, and saved geometry restoration.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 021. Canonical Release commands

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the solution and project configuration to ensure the documented restore, Release test, and x64 Release build commands exercise the same code and packaging configuration users rely on. Check whether a passing test command can skip architecture-specific tests or whether a successful build can omit XAML compilation, manifest validation, assets, or generated package layout. Identify hidden Debug-only behavior or conditions not covered by the canonical commands.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 022. Warning-free build credibility

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review compiler, analyzer, XAML, and NuGet warning settings to determine whether the documented zero-warning Release build is meaningful. Check for NoWarn entries, disabled analyzers, broad warning suppression pragmas, TreatWarningsAsErrors inconsistencies, or generated-code exclusions that could hide actionable defects. Do not enable large new analyzer suites merely for process; focus on suppressions that conceal a concrete risk in this codebase.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 023. Repository does not imply a signed installer

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review packaging targets, README instructions, scripts, release artifacts, and UI wording to verify that the repository does not falsely claim to produce a signed distribution installer or Microsoft Store package. Look for stale commands that appear to create a distributable MSIX but omit signing, dependencies, certificates, or identity preparation. Ensure developer registration instructions cannot overwrite or confuse a production installation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 024. Machine-independent build inputs

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project references, content files, native tool paths, test fixtures, environment-variable reads, and scripts for machine-specific absolute paths such as C:\Dev\CutFlow or user profile directories. Verify that a clean checkout can restore, test, and build using only declared dependencies, while runtime project storage appropriately uses ApplicationData. Distinguish intentional documentation examples from paths embedded in compiled code or build metadata.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Application startup, MainWindow initialization, and top-level wiring

### 025. Single MainWindow creation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review App.OnLaunched and all activation paths to verify that exactly one MainWindow is created and activated for the lifetime of the process. Check repeated launch activation, protocol or file activation stubs, exception recovery, and any static window references. Ensure no second window can be created accidentally with separate services, settings state, or project roots.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 026. One initialization task

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review MainWindowLifecycle and MainWindow initialization to prove that asynchronous initialization starts at most once, every caller awaits the same task, and no activation event can race a second load of settings or Home projects. Check faulted and canceled initialization behavior, retry assumptions, and whether a partially initialized window can continue responding to commands.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 027. Native window context readiness

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review how HWND, WindowId, and AppWindow are resolved and awaited before settings restoration, picker creation, title-bar registration, or other native-window-dependent work. Look for code paths that can access these values before first activation, after disposal, or from a stale window generation. Verify failures surface safely instead of leaving null native handles that fail later.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 028. Close-started continuation suppression

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all awaits in startup and initial Home loading for a close-begun guard. Confirm that a continuation completing after close starts cannot replace content, subscribe events, show dialogs, mutate settings, create an EditorView, or touch disposed XAML elements. Include both successful and exceptional completion paths.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 029. First-activation event handling

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the first Window.Activated or AppWindow activation logic to ensure it performs native setup exactly once without ignoring later activation events that are still needed. Look for event handler removal errors, duplicate title-bar initialization, repeated geometry restore, and initialization that never starts if the first event has an unexpected activation state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 030. Custom title-bar integration

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review title-bar setup across App, MainWindow, HomeView, and EditorView. Verify ExtendsContentIntoTitleBar, drag rectangles, interactive controls, caption-button insets, theme colors, and title-bar element replacement remain valid when switching Home and Editor, resizing, changing DPI, or disposing a view. Ensure interactive project-name controls never become part of the drag region.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 031. AppWindow event ownership

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review registration and removal of AppWindow.Closing, Changed, activation, presenter, or geometry-related events. Verify handlers cannot be registered twice, survive longer than MainWindow, or fire while required services are null. Check whether event callbacks correctly marshal to the UI thread and respect initialization and close state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 032. Application-wide Ctrl+N routing

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the top-level Ctrl+N route on Home and its interaction with Editor-level shortcut handling. Verify one keypress creates at most one project, is suppressed in editable controls where appropriate, respects HomeProjectOpenGate and export state, and does not bubble through two handlers. Check key repeat, focus in dialogs, and navigation transitions.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 033. Top-level picker wiring

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review how MainWindow initializes and invokes media, audio, relink, and export pickers on behalf of the current view. Confirm each picker uses the current valid HWND or WindowId, cannot continue into a disposed EditorView, and maps cancellation separately from failure. Ensure an old editor cannot receive results after a newer editor has been shown.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 034. Current-view event wiring

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review MainViewModel.CurrentViewChanged and MainWindow.ShowCurrentView for event order, idempotence, and exception safety. Verify the visual tree, title-bar region, top-level handlers, and current view references always describe the same Home or Editor state, including when view construction fails. Look for transient periods where both views remain wired or neither can handle required commands.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 035. Old EditorView disposal before replacement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review navigation and project switching to prove that an existing EditorView is disposed before a new project-scoped editor is created and exposed. Check whether disposal can reenter navigation, await work, or throw. Verify native player resources, timers, hidden render hosts, save coordinators, thumbnail work, and all event handlers are actually torn down before replacement.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 036. Startup failure recovery

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review failures while loading settings, resolving geometry, loading projects, constructing services, or showing Home. Determine whether the app remains usable, presents an actionable error, or exits cleanly rather than showing an inert blank window. Check whether a failure can leave initialization permanently marked complete while essential state is absent, and whether close still works safely afterward.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Window geometry, DPI handling, and safe close

### 037. Physical-bounds preference

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review window-bound restoration to verify valid versioned physical bounds are preferred over legacy logical bounds and interpreted in the correct coordinate space. Trace serialization, deserialization, display selection, DPI conversion, and AppWindow.MoveAndResize. Look for conversions applied to already physical values or unversioned data accepted as physical without validation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 038. Legacy logical-bounds migration

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review migration of legacy logical window bounds to physical pixels using the target display DPI. Verify monitor selection happens before conversion, rounding is stable, dimensions cannot collapse to zero, and the migrated result is subsequently saved in the current physical format. Check mixed-DPI setups and values created on a monitor that is no longer connected.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 039. Target-display selection

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the algorithm that chooses a DisplayArea for saved bounds. Test mentally and through focused tests cases where the rectangle intersects multiple monitors, is entirely off-screen, has negative coordinates, or belongs to a removed monitor. Verify the chosen display is deterministic and that primary-display fallback does not distort valid secondary-monitor placement.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 040. Work-area clamping

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review geometry clamping against the selected display work area. Confirm the window remains reachable, honors minimum dimensions, does not cover excluded taskbar space unintentionally, and handles saved rectangles larger than the work area. Check arithmetic overflow, negative origins, unusually small work areas, and portrait-oriented displays.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 041. Minimum window dimensions

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review where minimum width and height are enforced during restore, user resize, DPI changes, and timeline or inspector layout transitions. Verify the same effective minimum is used in native sizing hooks and geometry normalization. Look for configurations in which the window can become too small for critical controls or saved invalid dimensions are repeatedly written back.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 042. Per-monitor DPI transitions

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review behavior when the user drags CutFlow between monitors with different scale factors. Check AppWindow geometry events, logical XAML sizes, physical saved bounds, title-bar drag rectangles, resize limits, and any cached DPI. Verify there is no progressive size drift after repeated monitor moves and restarts.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 043. Invalid geometry settings

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review normalization of NaN-equivalent JSON values, infinities if custom converters permit them, negative sizes, extreme coordinates, wrong physical-bound versions, partially missing rectangles, and invalid display data. Ensure malformed settings cannot throw during startup or cause AppWindow calls with impossible values, while valid unusual coordinates are not needlessly discarded.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 044. Saving the correct close-time bounds

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review close-time capture of physical and logical bounds. Confirm the code records the intended restored window rectangle when the window is maximized or minimized, rather than persisting unusable minimized geometry or the full maximized work area unless that is deliberate. Check event timing between the first canceled close, asynchronous flushes, and the final real close.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 045. Display-topology changes during close

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the case where monitors are connected, removed, rotated, or have scaling changed while CutFlow is open or while close coordination awaits export and saves. Verify bounds capture uses current valid display information and cannot throw or persist a rectangle tied to a stale DisplayArea object.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 046. Close cancellation and reentrancy

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review AppWindow.Closing handling to ensure the first close is canceled once, starts exactly one safe-close sequence, and subsequent close attempts cannot launch duplicate sequences or prematurely destroy the window. Verify the approved final close is distinguishable from another user-initiated close and cannot be canceled again indefinitely.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 047. Closing during initialization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review safe close when the user closes immediately after launch. Confirm close awaits the single initialization task without deadlocking the UI thread, then cancels or disposes any work that initialization started. Check faulted initialization, dialogs or pickers not yet shown, missing AppWindow context, and whether settings or project services are safe to flush before full UI setup.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 048. Failure during safe close

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every failure point in export cancellation, render awaiting, project-save flush, geometry capture, workspace-save flush, disposal, and final close. Verify a failure cancels the close attempt, keeps a coherent usable app open, reports an actionable error once, and permits a later retry. Ensure no partial disposal makes the retained window unusable.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Navigation, view replacement, and editor lifetime

### 049. Concurrent Home open and create gating

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review HomeProjectOpenGate and every Home command that opens or creates a project. Verify the gate is acquired before any preparation that can lead to a commit, released on success, failure, and cancellation, and shared by pointer, keyboard, card, menu, and Ctrl+N routes. Check whether two rapid actions can create duplicate EditorView instances, duplicate projects, or overwrite current navigation state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 050. Missing-file refresh before editor construction

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review MainViewModel.OpenEditorAsync to verify every asset's missing flag is refreshed before EditorViewModel and EditorView observe the project. Check cancellation and I/O failures, large asset collections, paths becoming missing immediately after refresh, and whether stale flags can be saved back incorrectly. Ensure the refresh does not mutate source paths or delete references.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 051. Current project and editor state transition

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the exact order in which MainViewModel assigns CurrentProject, creates EditorViewModel, changes navigation state, and raises CurrentViewChanged. Verify observers can never see an Editor state without a matching project or a Home state that still exposes a live editor. Check exceptions during construction and reentrant event handlers.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 052. Back navigation save sequence

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the two-stage save behavior when returning Home: flush pending autosave, then perform an additional immediate save when status is not Saved. Verify the sequence cannot save an older document after a newer one, duplicate a destructive normalization, or report success before the latest revision is durable. Check failure and cancellation at each await.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 053. Save failure blocks Home navigation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all Home-return routes to prove a failed project save leaves the editor visible, current state intact, and user edits recoverable. Verify no project list reload, EditorView disposal, or current-project clearing happens before save success. Check how repeated Back attempts, Escape behavior, and window close interact with an existing SaveFailed state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 054. Export blocks navigation and project creation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every route that can leave the editor or create/open another project while export setup or rendering is active. Distinguish setup from render according to the documented policy, confirm render blocks Home and Ctrl+N, and ensure stale UI enablement cannot bypass ExportOperationState. Check keyboard shortcuts, title-bar Back, Home commands, app close, and completion callbacks.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 055. Create-new-project from Editor

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Ctrl+N or any explicit new-project action while an editor is open. Verify the current project is saved safely before creation, the Home open gate and export state are respected, cancellation or name-creation failure preserves the current editor, and a successfully created project gets one EditorView rather than an intermediate Home state with duplicate event wiring.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 056. View-specific top-level handler detachment

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review handlers that MainWindow attaches to EditorView for import, relink, export, workspace changes, title-bar regions, and navigation. Confirm all handlers are detached from the old view before its disposal and cannot retain MainWindow, services, or the project document. Include anonymous delegates and events raised during disposal.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 057. Title-bar drag region replacement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review drag-region registration whenever HomeView and EditorView are swapped or the editor layout changes. Verify MainWindow does not keep rectangles or FrameworkElement references from a disposed view, and that caption hit testing updates after resize, DPI change, inspector transition, project-name editing, and title-bar content changes.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 058. Project-scoped cache roots

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review creation of ThumbnailService and TextOverlayRenderer to ensure their project directory, cache directories, and retained current result always correspond to the currently open project GUID. Check project duplication, relink, navigation back and forth, and failures during EditorView construction. Prove one project's cache can never be resolved, published, pruned, or deleted through another project's service instance.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 059. Editor lifetime cancellation coverage

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every asynchronous operation started by EditorView or its controls and verify it observes the editor lifetime token or another equally strong generation check before committing UI or model state. Include imports, thumbnail loads, preview rebuilds, relink, text rendering, export setup, dialogs, file pickers, autosave callbacks, and delayed dispatcher work.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 060. Editor disposal order

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review EditorView.Dispose and related disposal paths for a deterministic order that prevents callbacks into partially disposed components. Check cancellation source signaling, export coordination, save flushing assumptions, event detachment, timers, PreviewPane native resources, hidden text-render host, thumbnails, gates, and owned services. Verify disposal is idempotent and exceptions do not skip later critical cleanup.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Project schema, serialization, and model defaults

### 061. Required schemaVersion enforcement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ProjectService deserialization and validation to confirm schemaVersion is truly required and cannot silently default to 1 when absent, null, zero, malformed, or represented with an unexpected JSON type. Verify the error path distinguishes an unsupported or missing schema from generic corruption and never normalizes an unversioned document into a valid current project.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 062. Directory GUID and document ID match

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project loading and all project-directory resolution code to ensure ProjectDocument.Id exactly matches the GUID directory name. Check casing, alternate GUID formats, trailing separators, symbolic links or reparse points, duplicate directories, and documents whose ID changes after normalization. Verify a mismatch is rejected before any cache path is trusted or save occurs.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 063. Enum-as-string serialization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every model enum and System.Text.Json converter to confirm values are serialized as the intended stable strings and unknown strings or numeric values do not silently map to a valid default. Include asset kind, aspect ratio, text alignment, selection kind, and any status-like enums persisted in JSON. Check case sensitivity and forward-version behavior.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 064. Null settings and collection normalization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review handling of JSON that explicitly sets settings, assets, videoItems, audioItems, or textItems to null, as well as omitted properties. Confirm safe defaults are created without sharing mutable list instances across projects and without erasing valid sibling fields. Add attention to nested null items within collections, if deserialization permits them, and whether later LINQ or edit code assumes every element is non-null.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 065. Timestamp semantics

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review CreatedAt and ModifiedAt initialization, serialization, duplication, rename, autosave, failed save, load, and list sorting. Verify timestamps are UTC, ModifiedAt changes only after successful project save, CreatedAt is preserved except for duplication, and DateTimeKind or DateTimeOffset conversions cannot produce local-time drift. Check deterministic tests around clock boundaries.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 066. Project-name invariants

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every ingress for ProjectDocument.Name, including creation, Home rename, editor title rename, duplication, direct JSON load, and normalization. Verify blank and whitespace-only user input is rejected, committed names are trimmed, length constraints are consistent where intended, control characters do not destabilize UI or filenames, and loading an old unusual but nonblank name does not cause unintended data loss.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 067. Asset identity and metadata defaults

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ProjectAsset serialization and default handling for ID, kind, source path, display filename, duration, dimensions, file size, last-write timestamp, thumbnail path, and missing flag. Confirm invalid or absent identity and source data cannot later be treated as a trusted imported file. Check which fields are authoritative after relink and which may be safely reconstructed.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 068. V1 stored-duration compatibility fallback

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review VideoTimelineItem.DurationMilliseconds and normalization for older JSON where explicit duration is absent or nonpositive. Verify fallback to max(0, sourceOut minus sourceIn) is used only where intended, does not overflow, and does not let a malformed stored duration disagree with source ranges after load. Check image items, whose timeline duration can legitimately differ from the asset's five-second metadata duration.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 069. A1 computed-duration serialization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review AudioTimelineItem to ensure duration remains computed from sourceOut minus sourceIn and is not accidentally serialized as an independent field that can drift. Check JSON compatibility if an unknown legacy duration property exists, arithmetic overflow, normalization of reversed source ranges, and every UI or composition call site that reads duration.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 070. T1 safe missing-field defaults

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review TextTimelineItem deserialization defaults for start, duration, text, font family, size, weight, italic, colors, background enabled, opacity, alignment, and normalized coordinates. Confirm omitted properties in schema-version-1 files receive the documented values, while explicit invalid values are normalized. Pay special attention to constructor/property-initializer behavior under System.Text.Json.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 071. ProjectSettings pair invariants

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ProjectSettings serialization and all mutations to Width, Height, FrameRate, AspectRatio, BackgroundColor, VideoTrackVisible, TextTrackVisible, and AudioTrackMuted. Verify aspect-ratio changes update dimensions as one coherent edit, the fixed 30 fps assumption cannot drift through malformed JSON, and partially specified dimensions do not produce an unsupported canvas combination.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 072. Unknown future fields and versions

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review forward-compatibility behavior. Confirm schema version values other than 1 are rejected even if all currently known fields deserialize, while unknown fields inside a schema-1 document are handled deliberately by System.Text.Json rather than causing accidental data loss claims. Verify saving a loaded schema-1 document does not silently reinterpret a future-version document that was incorrectly accepted.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Normalization and edit invariants

### 073. V1 normalization at the 24-hour boundary

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ProjectService.Normalize for ordered magnetic V1 items whose cumulative duration approaches or exceeds 86,400,000 ms. Verify each retained item remains at least 100 ms, the final item is clamped or removed according to the intended policy, all later entries after capacity exhaustion are removed deterministically, and checked arithmetic prevents overflow. Confirm asset/source ranges stay compatible with any duration adjustment.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 074. A1 start, trim, and project-bound normalization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review normalization of every AudioTimelineItem start, sourceIn, sourceOut, duration, and end against zero, minimum duration, known asset duration where available, and the 24-hour project limit. Check items starting near the maximum, negative starts, reversed ranges, huge integer values, and overlap. Ensure left-edge repair does not unexpectedly move the right edge in a way that differs from editing semantics.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 075. T1 start and duration normalization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review normalization of TextTimelineItem start and duration so every item remains inside the 24-hour project domain and has at least 100 ms. Test negative starts, zero duration, start at the maximum, end overflow, and items extending beyond the visual track. Verify normalization preserves text timing as much as safely possible and remains consistent with preset insertion and edge-trim services.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 076. Minimum-duration enforcement everywhere

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all model, service, view-model, pointer, inspector, keyboard, import, split, duplicate, load, and save paths for the 100 ms minimum item duration. Identify any ingress that can create or commit a shorter V1, A1, or T1 item or a split side below minimum. Verify visual previews may temporarily exceed bounds but final edits reject or clamp consistently.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 077. Source-range validation against assets

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review normalization and editing of V1 and A1 sourceIn/sourceOut against the referenced asset's duration. Confirm ranges remain ordered, nonnegative, at least 100 ms where applicable, and no greater than source duration after import or relink. Check missing assets, unknown asset IDs, stale metadata, images with fixed source metadata but independent timeline duration, and arithmetic at exact endpoints.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 078. Finite volume handling

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all per-item volume, global mute, preview mute, JSON normalization, inspector input, and composition calculations for NaN, positive or negative infinity, negative values, values above 1, and floating-point precision. Verify only finite 0 through 1 values reach MediaClip or BackgroundAudioTrack, and mute does not permanently overwrite the stored volume.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 079. Audio fade compatibility fields

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review normalization and serialization of audio fade-in and fade-out fields that currently exist only for compatibility. Verify each fade is clamped from zero through item duration, cannot overflow or become NaN, and is neither accidentally exposed nor partially applied in preview or export. Ensure duplicate, trim, relink, and load behavior cannot create inconsistent fade values even though the feature is intentionally inactive.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 080. ARGB color normalization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review validation of project background, text foreground, and text background colors. Confirm project background accepts only opaque #FFRRGGBB while text colors support the intended #AARRGGBB range, malformed length or hex digits are rejected or normalized safely, casing is stable, and preview/export parse colors identically. Check null, whitespace, shorthand, named colors, and locale-sensitive parsing.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 081. Supported font-family normalization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review TextStyle and project normalization for font family values. Confirm null, blank, unsupported, differently cased, or whitespace-padded families map to a documented supported choice without injecting arbitrary local fonts. Verify InspectorPanel choices, live XAML preview, RenderTargetBitmap export, style hashing, reset style, and old JSON all use the same normalized family.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 082. Text coordinate and alignment normalization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review TextTimelineItem alignment and normalized X/Y handling for invalid enum values, NaN, infinity, negative values, values above 1, and JSON numeric edge cases. Verify all code paths clamp coordinates consistently, unknown alignment cannot crash switches, and preview drag, inspector NumberBox edits, duplicate, undo, and export produce the same canonical values.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 083. Normalization idempotence

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review whether calling ProjectService.Normalize repeatedly produces the same document after the first pass. Look for timestamp changes, list reordering, progressive duration shrinkage, path recasing, color rewrites, floating-point drift, or repeated removal. Add focused tests for representative malformed documents so load-normalize-save-normalize is stable and does not keep changing user data.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 084. Safe repair versus silent destruction

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every normalization rule to determine whether it repairs only safe boundaries rather than silently deleting recoverable user intent. Pay special attention to unknown asset references, malformed V1 order, over-limit items, unsupported fonts, invalid colors, and null collections. Ensure severe structural corruption is rejected with an actionable error when normalization cannot preserve a coherent project, instead of producing a superficially valid but materially altered file.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Project persistence, path confinement, and atomic save

### 085. Direct GUID-child project resolution

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all helpers that resolve Projects/<guid> to prove the result is always a direct GUID-named child of the configured Projects root. Test rooted input, .. segments, alternate directory separators, trailing dots or spaces, GUID-like extra suffixes, case differences, and normalized path comparisons. Ensure callers cannot supply an arbitrary directory through a ProjectDocument.Id or string path.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 086. Reparse-point and link confinement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project and cache deletion or resolution under a full-trust Windows process for junctions, symbolic links, mount points, or other reparse points placed inside the Projects tree. Determine whether ordinary string containment is sufficient for every destructive operation. Verify CutFlow cannot recursively delete or overwrite files outside its owned root through a malicious or accidentally linked project/cache directory.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 087. Project-relative cache path validation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every conversion from a serialized thumbnail or text-overlay relative path to an absolute file path. Verify rooted paths, .. traversal, mixed separators, device paths, UNC paths, trailing-space normalization, percent-like strings, and case-insensitive comparisons cannot escape the current GUID project directory. Check both reads and deletions, not just cache creation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 088. Atomic project-save transaction

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ProjectService.SaveAsync from normalization through serialization, project.json.tmp creation, WriteThrough stream use, managed flush, disk flush, File.Replace or first-file move, and cleanup. Verify no await or exception path can expose a partially written project.json, delete the last valid document, or leave a temp file that a later load mistakes for canonical data.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 089. ModifiedAt rollback on failed save

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the preservation and restoration of ProjectDocument.ModifiedAt when any save step fails or is canceled. Confirm the original value is restored even if normalization or serialization mutated other state, and that an overlapping successful save cannot have its newer timestamp overwritten by a failing older operation. Check reference sharing between snapshots and the live document.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 090. Cancellation at every save phase

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review cancellation behavior before normalization, during serialization/write, after temp flush, during replace or move, and during temp cleanup. Determine which blocking file-system operations cannot observe a token and ensure post-operation state remains correct. A canceled save must not report Saved, must preserve prior JSON, and must not delete another operation's file.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 091. Temporary-file cleanup ownership

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review temp-file naming and cleanup to prove SaveAsync removes only its own project.json.tmp or uniquely owned temporary file. Check stale temp files from crashes, simultaneous calls despite higher-level serialization, unauthorized deletion failures, and a temp path replaced by an unexpected directory or link. Ensure cleanup failure does not mask the primary outcome improperly.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 092. First-save move semantics

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the branch where no project.json exists and the temporary file is moved into place. Verify races with another creator, preexisting directories, move atomicity on the same volume, overwrite behavior, cancellation timing, and the cleanup performed when creation fails after the project directory was made.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 093. Concurrent project-save callers

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review whether all production callers serialize saves strongly enough for ProjectService's fixed temporary filename and live-document timestamp mutation. Trace autosave, Ctrl+S, navigation, close, rename, and any immediate save. If ProjectService itself can be called concurrently, verify no temp-file collision, File.Replace race, stale snapshot commit, or ModifiedAt corruption is possible.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 094. Write-through and durability assumptions

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the implementation and error handling of FileOptions.WriteThrough, stream FlushAsync, and flush-to-disk behavior on Windows. Verify the code uses compatible APIs correctly, disposes handles before File.Replace where required, and does not claim stronger durability than it achieves. Check behavior on package LocalState, antivirus interference, and common I/O exceptions.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 095. ListAsync skips only bad projects

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project enumeration so unreadable, malformed, unsupported, unauthorized, concurrently deleted, or ID-mismatched project directories are skipped independently without aborting the full list and without deleting evidence. Verify expected exception filters are narrow, cancellation still propagates, and one pathological directory cannot cause infinite retries or excessive logging.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 096. No imported-source deletion path

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Perform a targeted data-flow review from ProjectAsset.SourcePath through asset removal, relink cleanup, project deletion, cache cleanup, duplicate cleanup, failed-create cleanup, export staging cleanup, and destination overwrite. Prove no destructive File.Delete, Directory.Delete, File.Replace, or File.Move call can receive an imported source path except the deliberate final export destination after source-equality preflight.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Home workflows and project CRUD

### 097. Create-project failure cleanup

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project creation from GUID allocation through directory creation, default document construction, first save, Home list refresh, and Editor navigation. Verify that if creation fails, only the newly created GUID directory is eligible for cleanup, preexisting directories are never removed, source media is irrelevant, and a partially created project cannot later appear as valid. Check cancellation and a race where another process creates the same path.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 098. GUID-only project discovery

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Home project discovery to ensure only direct child directories with valid GUID names are considered. Check hidden/system directories, files named like GUIDs, GUIDs with braces or suffixes, nested directories, case, leading/trailing whitespace, and reparse points. Verify invalid names are ignored without expensive recursive scanning.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 099. ModifiedAt sorting stability SEURAAVA

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project-card sorting by descending ModifiedAt. Verify UTC values are compared correctly, equal timestamps produce deterministic ordering, invalid or default timestamps cannot throw, and a failed save that restores ModifiedAt does not move a project incorrectly. Check whether localized display strings are ever used for sorting by mistake.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 100. Project-card duration calculation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review how Home cards compute project duration from V1, T1, and A1 semantics. Verify the duration matches the editor/export definition, handles hidden tracks, missing media, overlaps, zero/invalid items after normalization, and the 24-hour limit. Ensure magnetic V1 start derivation and explicit T1/A1 ends are included without integer overflow.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 101. First usable thumbnail selection

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the algorithm that chooses the first usable cached visual thumbnail in asset order. Verify only visual assets are considered, relative paths are project-confined, missing/corrupt/outside-project caches are skipped, and one bad cache cannot prevent later valid thumbnails or the placeholder. Check duplicated projects whose serialized cache references point to a new project root where files were intentionally not copied.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 102. Home rename validation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the Home rename dialog, 120-character TextBox, whitespace validation, service trimming, error handling, and list refresh. Verify Enter, dialog primary action, cancellation, duplicate display names, long Unicode text, control characters, and save failure behave coherently. Ensure a rejected rename does not alter ModifiedAt or leave the card showing an unsaved transient value.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 103. Editor title-bar rename transaction

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project-name editing in the editor for Enter commit, Escape revert, lost-focus commit, undo/redo participation, autosave, drag-region exclusion, and canonical refresh after rejection. Check focus changes caused by dialogs or navigation, IME composition, repeated lost-focus after Enter, blank names, and whether one rename creates exactly one history entry.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 104. Duplicate-project deep independence

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project duplication to prove the clone has a new project GUID, new creation and modification timestamps, independent lists and nested objects, and no shared mutable references with the source document. Verify every timeline item and asset identity policy is intentional, source media remains shared only by absolute path, and editing or normalizing one project cannot mutate the other in memory.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 105. Duplicate cache-reference behavior

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review duplication of thumbnail and text-overlay cache references when generated cache files are intentionally not copied. Verify serialized relative paths resolve only under the new project directory, missing files are treated as cache misses and regenerated, and no fallback reads the source project's cache. Check pruning or asset removal in the duplicate cannot delete cache files belonging to the original.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 106. Delete confirmation and owned scope

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Home project deletion from confirmation copy through service path resolution, recursive deletion, list refresh, and error reporting. Verify the dialog clearly states imported media is untouched, cancel has no side effects, only the direct GUID project directory is deleted, and failures leave the project card recoverable rather than silently removing it from UI.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 107. Concurrent Home mutations

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review races among Open, Rename, Duplicate, Delete, Create, list refresh, and card action menus. Determine which operations share gates or busy state and whether a project can be deleted while opening, renamed after deletion, duplicated from stale data, or opened twice. Verify UI disablement is not the only protection where asynchronous continuations can overlap.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 108. Home and Projects navigation parity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the intentional behavior that Home and Projects show the same complete local project collection with only different headings. Verify navigation does not apply stale filters, separate caches, or different empty/busy/error handling. Ensure future-looking labels or automation names do not imply that Recent projects is actually a recency-limited subset when it is not.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Workspace settings, preferences, and bounded logging

### 109. Non-atomic settings-write failure

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review SettingsService's direct WriteAllTextAsync behavior under process termination, disk-full, access denial, antivirus locking, and cancellation. Confirm a truncated or malformed settings.json is handled safely at next startup with defaults or recovery rather than preventing launch. Determine whether the smaller stakes justify non-atomic writing in the current implementation; change it only if there is a concrete data-loss or startup reliability defect.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 110. Settings normalization defaults

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review normalization of all workspace settings when the file is absent, malformed, partially populated, or contains out-of-range values. Verify defaults are applied independently, valid sibling preferences survive, and no mutable default object is shared unexpectedly. Include physical/logical bounds, zoom, timeline height, loop preference, and last export folder.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 111. Last export folder validation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review persistence and reuse of the last export folder. Confirm invalid, nonexistent, unauthorized, file-not-directory, rooted/relative, or removed paths are discarded safely; a valid folder is passed to the modern save picker in the supported form; and picker cancellation does not overwrite the preference. Check folder changes between selection and subsequent export.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 112. Timeline zoom persistence

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the full path for loading, applying, editing, debouncing, saving, and restoring timeline zoom. Verify values are clamped to 20 through 400 px/s, pointer-centered transient zoom does not save an intermediate invalid value, opening another project uses the workspace preference intentionally, and a settings-save failure does not corrupt the active timeline state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 113. Timeline height persistence

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review timeline height as a workspace setting across startup, editor creation, resize drag, narrow window layouts, min/max 180 through 600 px, and window resizing. Verify pixel values are interpreted in the correct logical coordinate space across DPI changes and that a canceled or lost pointer capture cannot persist a preview-only height.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 114. Preview loop preference persistence

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the loop toggle from workspace settings through PreviewPane native behavior. Confirm it is session/workspace state rather than a project edit, applies when a new editor or media source is created, is saved once per actual change, and does not accidentally set MediaPlayer.IsLoopingEnabled contrary to the documented manual MediaEnded implementation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 115. Settings failure-notification period

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the logic that surfaces workspace-settings save failures once per failure period and permits a new notification only after a successful save. Check rapid edits, repeated failed retries, navigation, multiple EditorView instances over time, close-time flush, and disposal. Verify the app neither spams InfoBars nor permanently suppresses notification of a later independent failure.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 116. Log message single-line sanitation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review SimpleLogService formatting to ensure timestamps are UTC, caller-provided CR, LF, Unicode line separators, or embedded exception text cannot create multiple misleading log records, and each message is truncated at the intended 1,024-character limit without splitting surrogate pairs badly. Verify formatting exceptions do not escape TryWriteAsync.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 117. Two-hundred-entry retention

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the algorithm that retains at most 200 log entries. Verify behavior when the file is absent, empty, contains malformed lines, exceeds the limit by many entries, lacks a final newline, or is concurrently changed. Ensure trimming keeps the newest records, does not grow unbounded due to multi-line content, and writes a coherent file after each operation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 118. Concurrent log writes

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the semaphore and file read-trim-write sequence used by SimpleLogService. Confirm every path releases the semaphore, cancellation semantics are deliberate, multiple service instances cannot corrupt the same cutflow.log if that is possible, and a failed write does not leave the file truncated. Check disposal or app close while a write is queued.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 119. Logging must not break workflows

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all production calls to TryWriteAsync and any direct logging methods to ensure a logging failure is always secondary and cannot replace the user-visible import, save, preview, export, or close outcome. Verify callers do not await logging while holding a critical UI or state gate longer than necessary and do not assume logging succeeded.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 120. Sensitive and excessive log content

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review what data is written to cutflow.log. Look for full project JSON, media contents, stack traces shown as user text, secrets, package tokens, or unbounded lists. Absolute local paths may sometimes be useful for diagnosis, but verify they are logged only when justified, sanitized to one line, bounded, and never uploaded because the product is local-only.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Autosave, save state, undo, and redo

### 121. Edited and saved revision accounting

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review DebouncedSaveCoordinator's monotonically increasing edited and saved revisions. Verify every committed model edit increments exactly once, a save captures the intended revision, and Saved is reported only when savedRevision has caught up to the latest editedRevision. Check integer overflow assumptions, initialization baseline, failed saves, and edits committed while status callbacks are running.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 122. Edit during active save

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the save loop when a second edit commits after a save begins but before it completes. Confirm the first save cannot mark the newer document Saved, the coordinator immediately performs another save without waiting for a fresh debounce, and no stale document reference or snapshot overwrites the newer state. Add deterministic synchronization in tests rather than timing-only sleeps.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 123. Debounce cancellation correctness

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review one-second debounce cancellation under rapid edits, explicit Ctrl+S, navigation FlushAsync, disposal, and a prior failed save. Verify canceling an old delay does not cancel the actual shared save operation or surface an error, and that OperationCanceledException is distinguished between expected debounce replacement and caller-requested cancellation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 124. FlushAsync catch-up guarantee

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review FlushAsync to prove it cancels pending delay work, waits for any active save, and returns only after the latest known revision is durably saved or a real failure is reported. Check concurrent FlushAsync callers from Ctrl+S, Back, and close, plus a new edit arriving during flush. Ensure no deadlock arises from UI-thread status callbacks.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 125. Retry after save failure

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review behavior after a save throws. Confirm status becomes SaveFailed, the unsaved revision remains pending, later edit or explicit flush can retry, and a failed revision is never advanced as saved. Check whether repeated retries serialize correctly and whether the user-visible InfoBar clears or updates after eventual success.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 126. Coordinator disposal semantics

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review DebouncedSaveCoordinator disposal to confirm it cancels only pending debounce work, does not falsely mark unsaved revisions Saved, does not dispose synchronization primitives while a save still uses them, and prevents post-disposal callbacks. Verify the owning editor performs required flushes before disposal rather than relying on Dispose to persist data.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 127. Exact save-label transitions

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review EditorViewModel and UI binding for the exact labels Saved, Saving…, Unsaved, and Save failed. Verify transitions are driven by coordinator state rather than guessed delays, are marshaled to the UI thread, do not regress from Save failed to Saved without a successful save, and are accessible to automation. Check initialization and undo/redo edits.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 128. Undo capacity and eviction

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review UndoHistory's default 50-snapshot capacity. Verify the current pre-edit document is recorded, the oldest undo entry is discarded at capacity, no off-by-one allows 49 or 51 effective undos, and snapshots released by eviction do not retain large object graphs. Test more than 50 heterogeneous edits and subsequent redo behavior.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 129. Redo invalidation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every edit path to ensure committing a new edit after one or more undos clears redo exactly once. Check no-op or rejected edits, selection-only changes, playhead seeks, transient preview mute, track locks, and settings changes so only true project-document edits invalidate redo. Verify undo followed by failed edit does not destroy redo unnecessarily.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 130. Snapshot independence and deserialization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review whole-document JSON snapshots used by undo/redo. Confirm deserialization creates independent lists and nested objects, uses the same enum and nullability rules as persistence, and cannot restore an unsupported or partially normalized state. Check snapshot serialization failure handling and memory cost for projects with many items.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 131. Post-undo selection and playhead repair

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review undo/redo restoration to verify playhead clamps to restored project duration, orphan item selections are cleared, valid selections remain associated with the same GUID where appropriate, controls rebind to the new document instance, and one EditCommitted event triggers autosave and preview rebuild. Check inspector overlay and live text selection.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 132. TryCommitEdit versus CommitEdit call sites

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Audit every EditorViewModel call site of TryCommitEdit and CommitEdit. Verify direct CommitEdit is limited to controlled insertions that cannot violate timeline bounds, while arbitrary user edits use clone-apply-validate-swap semantics. Look for a call that mutates the live document before validation, records history after mutation, or can throw halfway and leave a partially edited model.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Media import, validation, and duplicate prevention

### 133. Local-path requirement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review MediaImportService and all import ingress to ensure a selected StorageFile or dropped item must resolve to a usable local filesystem path before metadata access or model commit. Check empty paths, virtual provider items, cloud placeholders, shell namespace files, UNC/device paths if unsupported, and paths that disappear. Verify rejection is per-file and actionable rather than a batch crash.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 134. Extension gate plus native decoding

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review accepted extension handling for .mp4, .png, .jpg, .jpeg, .mp3, and .wav. Confirm comparison is case-insensitive and only the final extension is considered, but native MediaClip, BitmapDecoder, or BackgroundAudioTrack creation still validates actual content and codecs. Ensure renamed arbitrary files do not enter the model with fabricated metadata merely because the extension passes.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 135. Canonical case-insensitive path dedupe

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review path normalization and duplicate comparison across import, relink, project load, and current-batch preparation. Verify GetFullPath-like canonicalization, separator normalization, trailing separators, relative segments, drive-letter casing, and Windows case-insensitivity are handled consistently. Check hard links or short 8.3 paths if relevant, and avoid claiming identity stronger than the implementation can prove.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 136. Import gate spans preparation through commit

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review EditorImportGate's semaphore scope to prove it covers duplicate checking, asynchronous metadata preparation, and the final UI/model commit. Verify two concurrent picker, drag/drop, or keyboard imports cannot both see an absent path and commit duplicates. Check cancellation, per-file failures, and whether a long codec open unnecessarily blocks unrelated non-import actions.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 137. Duplicate detection within one batch

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review a batch containing the same file multiple times through identical paths, case variants, relative-equivalent paths, or repeated drag items. Confirm exactly one asset can be prepared or committed and each rejected duplicate receives a clear per-file result. Verify a failure for the first occurrence does not incorrectly suppress a later occurrence that might succeed unless that behavior is intentional.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 138. Native metadata duration validation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review duration extraction for video and audio. Verify zero, negative, NaN-like native values, extremely long media, TimeSpan overflow, and files shorter than the minimum supported duration are rejected or normalized according to project limits. Confirm duration conversion to integer milliseconds does not truncate a valid 100 ms file below minimum or overflow 32-bit storage.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 139. Five-second image asset duration

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review image import and relink to confirm every image asset receives exactly 5,000 ms metadata duration while width, height, file size, and last-write timestamp come from the actual file. Verify timeline image-item duration can later differ without changing the asset duration, and duplicate, thumbnail, composition, and inspector logic do not confuse these two concepts.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 140. Imported metadata snapshot

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ProjectAsset creation for normalized source path, display filename, duration, dimensions, file size, UTC last-write timestamp, kind, ID, missing flag, and empty thumbnail reference. Verify metadata is captured after successful native validation, values are internally consistent, and a file changing during import cannot produce a dangerous mixture of old path identity and new metadata without being detected later.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 141. Per-file ImportResult isolation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review batch import so expected failure of one file produces one readable ImportResult and does not abort successful files before or after it. Confirm result ordering matches input ordering, duplicate and unsupported errors identify the right file, cancellation still stops the whole operation promptly, and UI commit includes only successful prepared assets once.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 142. Expected exception filtering

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review exception filters around native metadata and file access. Confirm expected file, codec, access, argument, overflow, and COM failures are translated into useful per-file messages while unexpected programming defects are not swallowed as unsupported media. Verify OutOfMemoryException, StackOverflowException, and cancellation are not accidentally converted into ordinary file errors.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 143. Picker and drop scope parity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every picker and drag/drop target against the documented scopes: Media accepts visual files, Audio accepts audio, preview empty state accepts all supported types, V1 drop accepts visuals, A1 drop accepts audio, and T1 drop rejects with guidance. Check MIME or DataPackage metadata cannot bypass extension validation and that UI accept indicators match final service behavior.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 144. Cancellation before model commit

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review import cancellation after one or more files have completed expensive preparation but before UI commit. Confirm no asset, timeline item, thumbnail path, undo entry, or unsaved state is committed after cancellation or editor disposal. Ensure prepared native objects or temporary resources are released and the import gate is always released.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Missing media, relink, and asset removal

### 145. Missing-flag refresh on open

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the file-existence refresh performed before opening an editor. Verify every asset is checked using its normalized current source path, the Missing flag is updated both from false to true and true to false, expected access errors are handled deliberately, and a missing check does not attempt to decode media or alter metadata. Check cancellation and thousands of assets without freezing the UI unnecessarily.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 146. Relink requires the same asset kind

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review relink validation so a Video asset can only be replaced by a valid video, Image by image, and Audio by audio, using both extension and native metadata validation. Confirm a misleading extension or decoder result cannot change the asset's kind, and a rejected replacement leaves every field and cache reference untouched.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 147. Relink duplicate-source prevention

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review relink duplicate checks against all other project assets using the same canonical case-insensitive path policy as import. Verify the asset being relinked is excluded correctly, case-only relink to the same file behaves intentionally, and concurrent relink/import cannot commit two assets with the same path. Check path aliases that normalization can resolve.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 148. Relink length covers all source references

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review calculation of the maximum existing sourceOut required by every V1 or A1 item referencing an asset. Confirm all references are included, arithmetic is in milliseconds without truncation, exact equality is accepted, and a shorter replacement is rejected before mutation. Check malformed orphan references, images, no-reference assets, and concurrent edits during the picker and metadata await.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 149. Relink preserves asset identity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review successful relink to prove the existing ProjectAsset.Id and all timeline AssetId references remain unchanged while source path, filename, duration, dimensions, size, timestamp, Missing flag, and thumbnail reference are updated appropriately. Verify undo behavior if relink is model-backed, save state, preview rebuild, and inspector selection remain coherent.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 150. Relink metadata refresh

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the metadata assigned from the replacement file, including video dimensions and duration, image dimensions plus fixed five-second asset duration, and audio duration. Confirm stale fields irrelevant to the new file are cleared rather than retained, timestamps are UTC, and native validation completes before any mutation or old-cache deletion.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 151. Old thumbnail deletion after relink

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review deletion of the previous project-owned thumbnail during successful relink. Verify the old relative path is resolved through project containment, deletion is attempted only after the asset can safely commit to the replacement, failure to delete is nonfatal and logged, and a concurrent thumbnail request cannot later republish stale content over the cleared reference.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 152. Missing V1 preview filler

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review preview composition when a referenced visual asset is missing, inaccessible, has an unknown kind, or fails native clip creation. Confirm the item contributes black filler with exactly the same effective timeline duration, later magnetic items retain their positions, project duration remains unchanged, and a warning names the problem without exposing raw native details.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 153. Missing A1 preview omission

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review preview composition when an audio source is missing or fails native creation. Confirm only that background track is omitted, its explicit timing does not shift any other item, the visual duration is still extended when other audio/text requires it, and warnings are bounded. Verify global mute and item mute do not conceal a missing-file report that export still needs.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 154. Strict export missing-media preflight

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review export preflight to ensure every distinct asset referenced by positive-duration V1 or A1 items is rechecked immediately before composition, regardless of stale Missing flags. Confirm missing names are de-duplicated case-insensitively, unreferenced missing library assets do not block export, and no native render starts when a required source is absent.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 155. Asset removal reference protection

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review asset removal so any V1 or A1 reference blocks removal, including hidden, muted, zero-duration malformed, or currently unselected items. Confirm the check uses asset IDs rather than paths, T1 is irrelevant, and a concurrent timeline edit cannot create a reference after validation but before commit. The rejection should leave source and cache untouched.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 156. Source-safe asset removal

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review successful asset removal to prove it deletes only the model reference and, when safe, an owned thumbnail cache file. Confirm the external SourcePath is never passed to deletion, text-overlay caches are not over-broadly removed, cache failure does not roll back an otherwise valid model edit incorrectly, and undo or project reload handles a missing generated thumbnail safely.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Thumbnail cache correctness and races

### 157. Thumbnail key completeness

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ThumbnailService's SHA-256 cache key construction. Verify it includes the uppercased normalized source path, file size, UTC last-write ticks, and requested size in an unambiguous encoding with stable separators or length framing. Check culture-independent number formatting and prove two distinct metadata tuples cannot collide because of string concatenation ambiguity, apart from cryptographic hash collision assumptions.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 158. Thumbnail path format

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review generation and parsing of cache/thumbnails/<64-lowercase-hex>.jpg. Confirm every generated name matches the exact shape, no user filename enters the path, the directory is created lazily inside the project, and serialized paths use stable separators suitable for JSON and Windows resolution. Reject malformed hashes rather than normalizing them into another file.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 159. Thumbnail path confinement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all thumbnail reads, publications, stale-file cleanup, relink deletion, asset removal, and Home-card lookup for project containment. Verify no serialized relative path, malicious project JSON, junction, or changed cache root can lead outside Projects/<guid>. Check that path comparison includes a directory separator boundary and uses the correct Windows case rules.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 160. Requested-size clamping

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review requested thumbnail sizes from UI through native thumbnail creation and cache identity. Confirm nonpositive, extremely large, and overflow values are normalized to the documented default or maximum 1,024, and that the same normalized size is used for keying, native request, decode, and validation. Ensure a caller cannot allocate huge images before the clamp is applied.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 161. Eight-MiB thumbnail budget

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review both encoded and decoded thumbnail limits. Verify stream length, pixel dimensions, stride multiplication, and buffer allocation are checked with overflow-safe arithmetic before reading or decoding more than 8 MiB. Confirm compressed image bombs or a corrupt JPEG cannot bypass the decoded-memory budget merely because the file is small.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 162. Native thumbnail mode and fallback

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review use of PicturesView for images and VideosView for videos. Confirm the correct mode follows ProjectAsset.Kind, unsupported or missing native thumbnails produce a controlled placeholder or error, and no audio path reaches visual thumbnail APIs. Check zero-sized or oddly oriented media and whether orientation metadata is respected consistently.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 163. Temporary generation files

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review temporary thumbnail-file creation, naming, location, publication, and cleanup. Verify temp files remain inside the thumbnail cache, are unique per operation, cannot overwrite a valid winner prematurely, and are deleted on cancellation or failure without touching another request's file. Check crash leftovers and subsequent cache enumeration.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 164. Stale request rejection after relink

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the captured thumbnail request snapshot and asset lock. Confirm source path, file size, last-write timestamp, requested size, and any other relevant identity are revalidated immediately before publication and model thumbnail-path assignment. A relink or metadata refresh must make the old request harmless even if native generation completes later.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 165. Concurrent requests for one asset

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review two or more simultaneous thumbnail requests for the same asset and size. Verify they cannot corrupt the cache, assign inconsistent paths, leak temp files, or race model mutation. Determine whether duplicate work is acceptable; if a valid winner is reused, validate it before reuse and ensure loser cleanup cannot delete the winner.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 166. Cancellation and editor disposal

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review thumbnail work when cards unrealize, searches change, the editor closes, or a project is replaced. Confirm cancellation stops native and file work where possible, no canceled continuation assigns an ImageSource or ProjectAsset.ThumbnailCachePath, and UI updates verify the current asset/card generation. Ensure disposal waits only where necessary and cannot deadlock the UI thread.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 167. Source file changes without relink

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review behavior when an imported source file is modified in place, changing size or last-write time while retaining the same path. Confirm a new key is generated, stale cache is not reused, metadata refresh points are adequate, and an in-flight old request cannot publish after the change. Avoid deleting a still-referenced cache outside project ownership.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 168. Home thumbnail robustness

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Home card thumbnail loading independently of an open EditorView. Verify corrupt, oversized, missing, outside-project, or mismatched cache files are ignored safely, file handles are released, and one slow project does not block listing all others. Confirm the restrained placeholder appears without creating or mutating project caches during simple listing unless explicitly intended.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Timeline duration, geometry, zoom, and ruler math

### 169. Unified project-duration calculation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the single definition of total project duration across EditorViewModel, TimelineMath, Home cards, PreviewPane, CompositionPlan, export preflight, and playhead clamping. Verify it is the maximum of magnetic V1 end and explicit T1/A1 ends as intended, uses positive normalized durations, and cannot disagree between UI, preview, autosave, and export.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 170. Twenty-four-hour limit arithmetic

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every time addition, subtraction, multiplication, conversion, and comparison near 86,400,000 ms. Use checked or wider arithmetic where needed so extreme JSON or pointer values cannot overflow and wrap into an apparently valid time. Confirm the limit applies to V1 cumulative duration and every explicit A1/T1 end, including duplicate and drag/drop operations.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 171. One-hundred-millisecond minimum

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review timeline math helpers for exact handling of the 100 ms minimum at boundaries. Verify equality is accepted, 99 ms is rejected or clamped consistently, split requires at least 100 ms on both sides, and conversion between TimeSpan, double seconds, frames, and integer milliseconds cannot accidentally create 99 ms through truncation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 172. Derived V1 start positions

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every projection and editing helper to prove V1 timeline starts are always calculated by summing preceding effective durations and are never serialized, cached as authoritative mutable state, or independently edited. Look for stale start values in UI event args, drag previews, selection, composition, tests, or inspector displays that could survive a reorder or trim.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 173. Half-open active intervals

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review use of half-open intervals [start, end) for T1 text activity and any equivalent item hit or composition logic. Verify an item is active at its start but not at its exact end, adjacent items do not double-render because of inclusive endpoints, and timer rounding or frame stepping does not create a one-tick flicker at boundaries.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 174. Time-to-pixel conversion precision

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review conversion between integer milliseconds and double pixel coordinates throughout TimelineControl and TimelineLayoutProjection. Check zoom extremes 20 and 400 px/s, large 24-hour times, rounding, negative pointer positions, scroll offsets, DPI-independent units, and reversibility. Ensure small clips remain selectable without changing model timing.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 175. Timeline content width

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review content-width calculation: at least viewport width, otherwise enough for at least one second or current project duration at current zoom, plus 48 px trailing space. Verify empty projects, sub-second projects, 24-hour projects, tiny/zero viewport during layout, zoom changes, and overflow. Ensure the trailing area does not become an editable false duration.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 176. Fixed track geometry

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review XAML and code constants for the 42 px toolbar, 1 px divider, 108 px header column, 30 px ruler, 64 px V1, 44 px T1, 48 px A1, and 186 px logical content height. Verify hit testing, drawing, scrolling, automation, and layout use one coherent set of values rather than subtly duplicated constants that drift.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 177. Visual minimum-width implications

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the 18 px visual minimum for short timeline cards. Confirm rendering width and model duration remain separate, adjacent tiny cards do not overlap in a way that corrupts hit testing or reorder targets, trim handles choose the intended item, and the playhead/time ruler still reflects true time. Add tests for many 100 ms clips at low zoom.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 178. Ruler interval selection

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review candidate intervals 100, 250, 500, 1,000, 2,000, 5,000, 10,000, 30,000, and 60,000 ms and selection of the first producing at least 80 px spacing. Verify zoom boundary behavior, floating-point comparisons, labels, and a sensible fallback at every supported zoom. Ensure no unsupported interval appears from integer division errors.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 179. Visible tick virtualization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ruler materialization so only ticks in the visible horizontal range plus one interval of buffer are created. Check negative or huge scroll offsets, zero-width viewport, rapid zoom, 24-hour duration, and recycling/cleanup of old elements. Confirm the number of XAML elements remains bounded and labels at the edges are not missing due to rounding.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 180. Fit and pointer-centered zoom

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Fit, zoom buttons, slider, and Ctrl+wheel. Fit must compute a bounded scale from duration and viewport then scroll to zero; pointer-centered wheel zoom must preserve the time under the pointer while multiplying or dividing by 1.12. Check clamping at 20/400, empty duration, scroll extent changes, and repeated zoom cycles for drift.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## V1 magnetic visual-track editing

### 181. Adding visual items to V1

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every path that adds an imported video or image to magnetic V1, including asset-card activation, drag/drop, and preview empty-state import. Confirm the item is appended in deterministic order, references the correct asset, uses valid source ranges and image duration semantics, respects the 24-hour capacity before commit, and creates exactly one undoable project edit.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 182. V1 reorder by midpoint

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the reorder target calculation based on pointer time relative to each clip midpoint. Test dragging forward and backward, exact midpoint ties, first and last positions, tiny visually widened clips, scroll offsets, and dragging within the same slot. Verify a no-op reorder creates no history entry and source ranges/items remain unchanged.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 183. Splitting a video item

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review video split at the current playhead. Confirm the selected V1 item's derived start is calculated from current order, the local source offset maps exactly to timeline offset, each side retains at least 100 ms, sourceIn/sourceOut remain contiguous and within the asset, volume/mute are copied, new IDs are deliberate, and the two adjacent durations sum to the original.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 184. Splitting an image item

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review image split semantics. Confirm the same still source range is retained for both new adjacent items while the timeline duration is divided at the playhead, each side is at least 100 ms, style-like item fields such as volume/mute are copied, and the combined magnetic duration remains exactly unchanged. Verify composition does not mistake the split for source trimming.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 185. Left-trimming V1 video

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review left-edge trim of a video V1 item. Verify sourceIn advances by the same amount that effective duration shrinks, sourceOut remains stable, the item stays at its magnetic position because earlier items define start, all later items ripple earlier, and source/minimum/project bounds are enforced in the service rather than trusted from pointer preview.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 186. Right-trimming V1 video

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review right-edge trim of a video V1 item. Verify sourceOut and effective duration change together while sourceIn remains stable, later magnetic items ripple accordingly, exact asset end is supported, and no mismatch can remain between stored duration and source range. Check inspector edits and timeline drag use the same deterministic service semantics.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 187. Changing image timeline duration

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review image-duration edits from inspector and timeline right trim. Confirm duration is independent of the asset's fixed 5,000 ms metadata duration, remains at least 100 ms, keeps the unchanged still source range, respects the 24-hour cumulative limit, ripples all later V1 starts, and triggers one history/save/preview rebuild.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 188. Duplicating V1 items

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review duplication of video and image items. Verify the copy receives a new item GUID, is inserted immediately after the source item, preserves source ranges, duration, volume, and mute, and does not duplicate the asset. Confirm capacity validation includes the full added duration and a rejected duplicate leaves history, selection, and model untouched.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 189. Deleting V1 items

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review deletion of a selected V1 item from toolbar, context menu, keyboard, and any inspector route. Confirm later starts ripple automatically through derived order, playhead and selection are repaired, the asset remains in the library, caches and source files remain untouched, lock state is enforced, and one delete yields one undo snapshot.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 190. V1 volume and mute

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review per-item visual-track audio volume and mute from inspector through model, undo, preview, and export. Confirm volume remains stored when muted, effective native clip volume is zero only while muted, image items with no meaningful audio are handled consistently, reset volume is deterministic, and hidden V1 semantics do not unexpectedly alter stored audio state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 191. Video-track visibility

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review persisted and undoable VideoTrackVisible behavior. When hidden, every V1 item must contribute same-duration black filler rather than disappearing and collapsing time, while audio associated with video clips is treated according to the documented composition semantics. Verify preview, export, duration, thumbnails, inspector, and track header all agree.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 192. V1 edit selection and history

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review selection after add, reorder, split, duplicate, trim, duration change, mute, and delete. Confirm the intended original or new item remains selected by GUID, no orphan selection survives, no-op edits record no history, and one completed gesture or command produces one edit transaction even when multiple fields change internally.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## A1 positioned background-audio editing

### 193. Adding audio at playhead or drop time

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every path that adds an audio asset to A1. Confirm the explicit start comes from the clamped playhead or drop position, sourceIn begins at zero, sourceOut matches validated asset duration unless project-end capacity requires rejection, and the item receives a new GUID with correct default volume, mute, and fade fields. One user action must create one undoable edit.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 194. Moving A1 items

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review A1 move semantics from timeline drag and inspector start input. Verify only explicit timeline start changes, source range and duration stay unchanged, the item may overlap others, negative positions clamp or reject consistently, and end must not exceed 24 hours. Check snapping applies to the intended moving edge and no-op movement creates no history.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 195. Left-trimming A1 with stable right edge

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review A1 left-edge trim in detail. Confirm increasing sourceIn also increases timeline start by the same amount so the right timeline edge remains fixed, while decreasing sourceIn moves start earlier only within source and project bounds. Verify sourceOut remains stable, duration stays at least 100 ms, and rounding cannot shift the right edge by one millisecond.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 196. Right-trimming A1

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review A1 right-edge trim. Confirm sourceOut changes while sourceIn and explicit start remain stable, duration is computed rather than separately stored, sourceOut stays within asset duration, and the resulting end remains within 24 hours. Ensure timeline preview and inspector commit call the same service rule.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 197. Duplicating A1 items

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review A1 duplication so the copy starts exactly at the source item's timeline end, receives a new item GUID, and preserves asset ID, source range, volume, mute, and compatibility fade values. Verify overlap with other audio is allowed, but project-end capacity is checked before commit. A rejected duplicate must not alter redo, selection, or save state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 198. Overlapping native background tracks

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review model, layout, preview, and export handling of multiple overlapping A1 items. Confirm every item remains independently selectable and audible at its explicit delay, native BackgroundAudioTrack collection order does not accidentally suppress another track, volume mixing cannot exceed API expectations in a way that crashes, and project duration uses the furthest end rather than summing overlaps.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 199. Per-item and global audio mute interaction

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review effective A1 volume calculation across item Volume, item IsMuted, and ProjectSettings.AudioTrackMuted. Confirm global mute forces every native background track to zero without overwriting item values, unmuting restores each stored volume, undo affects the correct layer, and preview/export use identical logic. Check reset volume while either mute is active.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 200. A1 source-bound enforcement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every A1 edit against the referenced audio asset duration. Confirm sourceIn and sourceOut stay ordered, exact endpoints are allowed, minimum duration is enforced, relink requirements use the maximum sourceOut, and a missing or orphan asset cannot cause unchecked indexing or fabricated duration. Malformed JSON should be normalized or rejected before native composition.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 201. A1 end at the 24-hour limit

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review moves, trims, additions, duplications, normalization, and snapping when an A1 item ends exactly at 86,400,000 ms. Equality should behave deliberately, one millisecond beyond must not wrap or pass, and a visual drag preview outside the bound must be rejected or clamped only by the final deterministic service. Check double-second inspector conversion near the limit.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 202. Missing A1 source behavior

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review A1 items whose source becomes missing after the project opens or during a preview rebuild. Preview should omit only the failed track and report a warning; export must fail preflight before staging render. Confirm the timeline item remains present, editable where safe, and does not shift or shorten other tracks.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 203. Inactive fade fields remain harmless

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review A1 fade-in and fade-out through create, load, normalize, trim, duplicate, relink, undo, preview, and export. Verify the fields remain serialized compatibility data but are not accidentally applied by one path and ignored by another, exposed through an unintended binding, or allowed to exceed the new item duration after trim.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 204. A1 lock and edit transaction coverage

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review A1 lock enforcement and history for pointer move, edge trim, toolbar delete, context duplicate/delete, keyboard shortcuts, inspector start/source/volume/mute edits, drag/drop insertion, and any programmatic selection command. Locked state is session-only, but every mutation ingress must reject cleanly with no partial UI drift or history entry.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## T1 text items, styles, timing, and positioning

### 205. Text preset creation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Default, Title, Subtitle, and Minimal label preset construction against the documented content, size, weight, Y position, background settings, and three-second duration. Confirm every preset starts from one canonical default style rather than inheriting stale inspector state, gets a new GUID, is selected after insertion, and creates exactly one undoable edit.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 206. Preset insertion at a clamped playhead

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Add text and preset insertion when the playhead is negative, at project end, near the 24-hour maximum, or beyond the current V1 duration. Confirm start is clamped to the legal project domain, the full 3,000 ms duration either fits or is adjusted/rejected consistently, and adding text may legitimately extend total project duration with trailing black filler.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 207. Overlapping T1 half-open timing

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review multiple overlapping text items and active-item selection at playback time. Confirm all items active on [start, start + duration) render in a deterministic visual order, exact endpoints do not double-render, hidden T1 suppresses all live/export overlays without changing duration semantics, and selection borders do not alter z-order unexpectedly.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 208. Moving and edge-trimming T1

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review T1 move, left trim, and right trim from timeline and inspector. Confirm move changes start only, left trim changes start and duration while keeping the right edge stable, right trim changes duration with stable start, and all operations preserve at least 100 ms and the 24-hour end limit. Verify one completed drag produces one model edit.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 209. Duplicating T1 items

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review text duplication. Confirm the copy receives a new GUID, preserves every pixel-affecting and timing field, starts immediately after the original item's end or at the documented chosen position, respects the project limit, and becomes selected intentionally. Ensure style-cache identity includes the new GUID filename behavior without forcing incorrect pixel differences.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 210. Multiline text commit behavior

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Text inspector content editing with plain Enter inserting a newline and Ctrl+Enter committing. Verify IME input, pasted CRLF, empty text, very long text, lost focus, Escape, and switching selection behave predictably. Ensure Ctrl+Enter does not also trigger an editor-level shortcut or insert an unintended newline and that one commit records one history entry.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 211. Font size and weight ranges

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all setters, normalization, NumberBox parsing, presets, reset style, JSON load, and rendering for font size 8 through 400 and weight 1 through 999. Confirm bold uses 700 without destroying a custom weight unexpectedly, values are finite, and preview/export map unsupported native weights consistently. Check exact boundaries and culture-sensitive numeric input.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 212. Supported font-family parity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Segoe UI, Arial, Georgia, Consolas, and Impact choices across InspectorPanel, model normalization, live XAML TextBlock, export renderer, style hashing, duplicate, reset, and JSON serialization. Confirm display names and actual FontFamily strings match, unsupported loaded values normalize deterministically, and missing system font behavior does not make preview and export diverge silently.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 213. Text and background color behavior

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review text foreground and optional background ARGB editing. Confirm valid alpha is preserved, malformed input remains visible with inline validation, disabled background does not accidentally rasterize its color, enabling it uses the stored color, and preview/export parse exactly the same value. Check reset style and Minimal label's #CC000000 background.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 214. Opacity and alignment

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review opacity 0 through 1 and Left, Center, Right alignment across model, slider, percentage label, normalization, live layout, export rasterization, undo, and style hash. Verify opacity is applied once rather than multiplied at nested levels, transparent text remains selectable when appropriate, and alignment affects wrapped text inside the same normalized center container.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 215. Normalized X/Y positioning

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review normalized center coordinates from Inspector NumberBox, live preview drag, model normalization, duplicate, undo, aspect-ratio change, preview resize, and full-resolution export. Confirm values clamp to 0 through 1, represent the text container center, and do not become pixel coordinates accidentally. Check large wrapped text near canvas edges and whether clamping intentionally permits partial off-canvas content.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 216. Reset text style

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Reset style to determine exactly which fields return to documented defaults and which timing, text content, or position fields remain unchanged. Confirm the operation uses one transaction, updates both inspector instances and live/export appearance, invalidates the correct text-overlay cache, and is a no-op when already canonical. Ensure reset does not change item ID or selection.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Timeline pointer editing, snapping, seeking, and autoscroll

### 217. One edit per completed pointer gesture

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review TimelineControl pointer handling so press and move update only a visual preview, while release emits exactly one TimelineEditRequestedEventArgs and one undoable model edit. Confirm pointer-move frequency cannot mutate the document or autosave repeatedly, and rejected release restores canonical rendering without a history entry.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 218. Pointer capture lifecycle

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review pointer capture acquisition, retained pointer ID, release, cancellation, right-button interaction, multitouch, and control disposal. Verify only the initiating pointer can drive a drag, capture failure aborts safely, and a captured gesture cannot continue against a newly rebound project or selected item. Ensure capture is released in every success and failure path.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 219. Lost-capture recovery

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review PointerCaptureLost and related cancellation paths. Confirm drag-preview visuals, trim indicators, cursor state, autoscroll, and pending event arguments are discarded, then the timeline rerenders from the canonical model. No partial move, reorder, or trim may commit merely because capture was lost during window deactivation or layout change.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 220. Eight-pixel trim-edge hit testing

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the rule that pressing within 8 px of a selected or editable card edge begins left or right trim. Check cards narrower than 16 px or visually widened to 18 px, overlapping hit regions, DPI-independent units, right-to-left assumptions, pointer exactly at the threshold, and locked cards. Ensure body drag remains reachable for short items.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 221. Twelve-pixel playhead target

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review hit testing for the 12 px playhead target and ruler/track-background seeking. Confirm seeking does not steal a card drag or trim, works with horizontal scrolling and zoom, clamps to valid project time, captures the pointer for scrub, and does not create undo or mark the project unsaved. Check pointer release outside the control.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 222. Scroll-offset coordinate conversion

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all pointer-to-time and time-to-pointer conversions with HorizontalScrollViewer offsets, header-column separation, ruler origin, zoom, and content padding. Verify dragging after scrolling does not introduce a constant offset, pointer-centered zoom preserves the correct time, and hit testing uses coordinates in the same visual space as rendered cards.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 223. Snapping candidate construction

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review snapping to 100 ms rounding, all item starts and ends, playhead, and total duration. Confirm candidates are built from current canonical model state, V1 starts are derived, duplicate candidates are harmless, missing/invalid items do not add impossible values, and moving an item may intentionally snap to its own edge only when that does not block movement.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 224. Sixty-millisecond threshold and tie rules

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the exact 60 ms snapping threshold and nearest-candidate choice, including the documented rule that an equally near later edge may replace an earlier candidate. Test distances of 59, 60, and 61 ms, candidates on both sides, duplicate times, negative raw values, and deterministic iteration order. Ensure floating-point conversion does not change equality unexpectedly.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 225. Snapping disabled

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the disabled-snapping path to ensure it returns the nonnegative raw value without hidden 100 ms rounding or edge attraction, while final edit services still enforce legal bounds. Confirm the toggle changes automation/name state, is session-only, persists only if explicitly intended, and does not affect current model timing until a new gesture commits.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 226. Ctrl-wheel zoom around pointer

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Ctrl plus mouse-wheel handling for zoom factor 1.12, supported delta signs, high-resolution wheels, trackpads, clamping, and preservation of the time under the pointer. Verify ordinary wheel scrolling remains available without Ctrl, editable controls are not hijacked unexpectedly, and one wheel event does not apply zoom twice through bubbling.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 227. Playhead autoscroll policy

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review timeline autoscroll driven by preview timer and explicit seeks. Confirm it scrolls only when the playhead exits the visible horizontal viewport, does not constantly recenter and fight the user, handles playhead at exact edges, respects content extent, and stops after disposal. User dragging or manual scroll should not create oscillation with timer updates.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 228. Dynamic-card rerender safety

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review creation and replacement of timeline card visual trees after edits, undo/redo, selection, lock toggles, zoom, and model rebinding. Verify old pointer handlers and DataContext references are released, a drag cannot commit against an item removed during rerender, and selected/accent/muted/missing states reflect the current document rather than cached objects.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Timeline commands, locks, shortcuts, menus, and drag/drop

### 229. Editable-control shortcut suppression

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review editor-level key handling to ensure Ctrl+N, Ctrl+S, Ctrl+Z, Ctrl+Y, Ctrl+B, Ctrl+D, Delete, Space, Escape, Home, End, Left, and Right are suppressed appropriately when TextBox, PasswordBox, or RichEditBox has focus. Include nested controls, NumberBox internal TextBox, ContentDialog fields, IME composition, and event routing before or after control-specific handlers.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 230. Undo, redo, save, split, duplicate, and delete shortcuts

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review each documented keyboard shortcut for correct CanExecute-like conditions, event handled state, lock checks, selection kind, export state, and one-action behavior under key repeat. Verify Ctrl+B affects only selected V1, Ctrl+D supports V1/T1/A1, Delete ignores assets/project selection, and failed commands surface concise guidance without mutating history.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 231. Timeline-focus seek shortcuts

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Home, End, Left, and Right so they seek only when the timeline has focus, with Left/Right moving exactly 100 ms and clamping at zero/project duration. Confirm focus inside a timeline card, context menu, trim handle, or nested button is interpreted intentionally, and these keys do not scroll a focused list or move a text caret.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 232. Space key routing

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Space across editor playback, media-card add activation, buttons, checkboxes, and text entry. Confirm a focused addable media card can add with Space without also toggling preview playback, while ordinary editor focus toggles play/pause. Event handling must prevent double action and respect disabled, missing, or locked states.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 233. Lock enforcement across every ingress

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Perform a complete lock-enforcement audit for V1, T1, and A1. Include toolbar commands, context menus, keyboard, inspector edits, timeline pointer move/trim/reorder, preview live-text drag, asset-card activation, drag/drop insertion, preset creation, duplicate, split, delete, and track-state controls. Locked items may remain selectable, but no mutation may partially commit or leave inspector drift.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 234. Context-menu enablement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review timeline card context menus for Split, Duplicate, Delete, and Show source file. Verify only applicable commands appear or enable for each kind, lock and playhead conditions are reevaluated when the menu opens, stale selection cannot target another item, and keyboard invocation works. A command should resolve the item by current GUID, not a captured stale object.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 235. Split availability at exact boundaries

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review split enablement so the playhead leaves at least 100 ms on both sides of the selected V1 item. Test exact 100 ms, one millisecond inside/outside, derived item start after reorder, image/video differences, and playhead at project end. UI enablement and TimelineEditingService must agree, with the service remaining authoritative.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 236. Show-source-file safety

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Show source file from asset and timeline context menus. Confirm it resolves the current referenced asset, handles missing/inaccessible paths, does not execute the media file as a program, uses a safe shell reveal/open-folder behavior, and surfaces failures without raw exceptions. Check paths with spaces, Unicode, shell-special characters, UNC paths, and a file disappearing after menu open.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 237. V1, A1, and T1 drop scopes

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review drag-enter, drag-over, drop, and final import validation for each track. V1 must accept only visual media, A1 only audio, and T1 reject files with guidance to use Text. Verify accepted-operation indicators match actual ability, multiple files preserve order, unsupported items do not block valid siblings incorrectly, and drop time is computed only for the relevant positioned track.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 238. Drop while track locked

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review file drop onto locked V1, T1, or A1. Confirm the UI does not advertise Copy, no picker/import preparation starts unnecessarily, no imported asset is committed without a timeline item unless that is explicitly intended, and a clear lock message appears once. Check the lock changing while asynchronous metadata preparation is in progress.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 239. Session-only lock and snapping reset

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review editor creation and disposal to confirm track locks and snapping state are intentionally session-only and reset at the documented defaults when the editor is recreated. Verify they are absent from project JSON and undo history, not accidentally copied during project duplication, and both wide/narrow UI instances display the same live session state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 240. Track-state command separation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review persisted track visibility/global mute versus session-only track locks and preview mute. Confirm UI controls call the correct model or transient state path, undo applies only to persisted model edits, autosave is not triggered by transient changes, and automation names describe the next action accurately. Look for similarly named IsMuted or IsVisible properties being wired to the wrong layer.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## CompositionPlan and native media-composition semantics

### 241. Preserving magnetic V1 order

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review CompositionPlan construction from ProjectDocument.VideoItems. Confirm the serialized list order is preserved exactly, derived starts are not resorted by any stale field, zero or invalid items are handled before native creation, and black fillers occupy the same slot as failed items. Preview and export must consume the same deterministic order.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 242. Hidden and missing visual filler

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every reason a V1 item becomes filler: hidden video track, missing file, unknown asset, unsupported kind, native creation failure, or other documented degradation. Confirm filler duration equals the item's effective timeline duration exactly, uses the project background or documented black behavior consistently, does not carry source audio accidentally, and accumulates without gaps or overlap.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 243. Video trim and effective volume

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review conversion of a video V1 item into MediaClip. Verify source trim maps from integer sourceIn/sourceOut correctly, effective duration agrees with the model, item mute and volume are applied once, and unsupported native trim values fail into same-duration filler rather than shifting later clips. Check exact source endpoints and media shorter than stale metadata.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 244. Still-image clip duration

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review native creation of image clips. Confirm the timeline item's effective duration, not the asset's fixed 5,000 ms metadata duration, determines clip length after trim, split, duplicate, and inspector edits. Verify the same source image can back multiple items with different durations and no native object is improperly shared or disposed.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 245. A1 delay, source trim, and volume

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review each AudioTimelineItem conversion to BackgroundAudioTrack. Confirm explicit Start becomes the correct delay, sourceIn/sourceOut become native trim, and effective volume combines item volume, item mute, and global track mute consistently. Check overlap, exact zero delay, project-end items, and native APIs that may express delay or trim with different TimeSpan semantics.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 246. Failed A1 isolation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review failure handling for one native background-audio item. Confirm the failed item is omitted and named in build errors while all other audio and visual content remains unchanged. Verify a partially created track is disposed, no collection index assumptions break, and repeated preview rebuilds do not leak native resources for the failing file.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 247. T1 overlay-plan inclusion

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review generation of text overlay plans. Confirm only visible-track, valid text items are included, each plan carries correct delay and duration, and native overlay media is created only when a renderer is supplied. Preview composition must deliberately omit rasterized text so the live XAML layer is the only visible text copy.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 248. Trailing visual filler

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the rule that if A1 or T1 extends beyond V1, a trailing black filler clip extends the native visual composition to the exact total project duration. Confirm no filler is added when V1 already reaches the maximum, zero-length filler is omitted, hidden V1 still preserves its full duration, and arithmetic cannot overshoot by one millisecond.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 249. Exact composition duration

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review whether the resulting native MediaComposition duration matches the model's total duration for empty, V1-only, text-only, audio-only, hidden-track, missing-media, overlapping, and near-24-hour projects. Since export requires positive V1, distinguish preview construction rules from export preflight. Look for native rounding or frame-rate quantization that can produce a visible gap.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 250. Visual failure isolation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review native item creation exceptions for V1. A failed visual should become black filler without aborting the entire preview when possible, while unexpected programming defects should still surface. Confirm the fallback preserves item duration, disposes any partial native object, records a bounded readable error, and behaves the same for an unreadable image and video codec failure.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 251. Preview/export semantic parity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Compare every timeline semantic used by preview and export: V1 order and visibility, trims, image duration, item volume/mute, A1 delay/trim/volume/global mute, T1 timing/style, missing-media policy differences, trailing filler, canvas, and duration. Identify any duplicated logic that has drifted so a previewed edit could render differently in export without an explicit documented reason.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 252. Deliberately unapplied audio fades

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review CompositionPlan and CompositionService for fade fields. Confirm fade-in/out are not partially applied to A1 in preview or export while remaining absent elsewhere, and no native default interprets the stored values indirectly. Tests should make the current no-op compatibility behavior explicit enough that a future fade feature requires a conscious design and parity update.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Preview rebuilds, native source replacement, and resource cleanup

### 253. One-hundred-forty-millisecond rebuild debounce

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review scheduling of preview rebuilds after EditCommitted. Confirm rapid edits cancel or supersede earlier 140 ms delays, selection/playhead-only changes do not rebuild unnecessarily, and a final edit always produces a rebuild. Check disposal, undo/redo bursts, inspector typing commits, and whether debounce callbacks run on an appropriate thread/context.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 254. Monotonic preview lease versions

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review PreviewRebuildGate's version allocation and lease validation. Verify each requested build has a strictly newer generation, only the newest noncanceled lease may replace the active source, and version comparison cannot overflow in realistic lifetime. Check build failure, cancellation, and a newer request arriving during final UI commit.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 255. Cancellation of superseded native builds

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review how cancellation tokens flow through CompositionPlan creation, native file opens, MediaComposition construction, preview stream generation, and UI replacement. Confirm cancellation is observed before publication even where native APIs cannot stop promptly, expected cancellation is not shown as a preview error, and partial native resources are disposed.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 256. Fresh composition per rebuild

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review that every rebuild creates a fresh MediaComposition and does not mutate or append to the currently playing composition. Verify native clips and tracks are not reused across incompatible source generations, old composition references are released after source replacement, and a failed new build leaves the current valid preview usable when that is the intended behavior.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 257. Preview stream size cap

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review preview media-stream profile generation so requested dimensions preserve project aspect ratio, never exceed 1280 by 720, and never upscale above project dimensions. Test 16:9, 9:16, 1:1, unusual normalized JSON dimensions, very small canvases, and integer rounding. Confirm width and height remain valid encoder-aligned values if the native API requires them.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 258. Successful source-replacement order

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the exact successful replacement sequence: invalidate old native-event generation, detach handlers, pause, clear MediaPlayerElement source/state, dispose old MediaSource, install new composition/source, restore clamped position and play intent, then attach current-generation events. Verify no queued callback can observe mixed old/new state and that the element is detached before disposal.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 259. Failure cleanup order

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review cleanup when preparation or source installation fails. Confirm event generation is invalidated, handlers detach, playback pauses, stale state clears, prepared source is disposed, and only then appropriate handlers are reattached. Verify the previous source is not left half-disposed, the timer stops when necessary, and a useful warning reaches the user.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 260. Native-event generation gate

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every MediaPlayer event handler, including playback-state changes, MediaEnded, MediaFailed, position-related callbacks, and any source-open event. Confirm handlers capture or query the correct generation and return without side effects when stale. Check queued dispatcher callbacks that outlive handler detachment and a source replaced multiple times quickly.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 261. Restoring position and play intent

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review rebuild preservation of current position and intended playback. Confirm position clamps to the new duration, starting from a now-shorter end behaves intentionally, play intent is restored without falsely reporting actual Playing before native state changes, and a user pause or seek racing the rebuild wins according to a clear ordering rule.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 262. Disposal during rebuild

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review EditorView or PreviewPane disposal while a debounced or active rebuild is running. Confirm lifetime cancellation invalidates the lease, no source is installed into a disposed MediaPlayerElement, all prepared native objects are disposed, event/timer callbacks cease, and disposal does not deadlock waiting on UI-thread work that itself needs the disposing thread.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 263. Bounded preview warnings

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review how CompositionService build errors become the preview warning InfoBar. Confirm at most the first three meaningful failures are surfaced, duplicate messages are handled sensibly, filenames are safe and concise, hidden or recovered items do not produce misleading severity, and subsequent successful rebuilds clear or update stale warnings.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 264. UI-thread requirements

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every interaction with MediaPlayerElement, XAML Canvas, RenderTargetBitmap hosts, DispatcherQueueTimer, and native source assignment for correct UI-thread affinity. Confirm background composition planning does not touch UI objects, continuations marshal deliberately, and cancellation or close cannot post an unsafe callback after DispatcherQueue shutdown.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Playback state, timecode, transport, and live XAML text

### 265. Single MediaPlayer and timer ownership

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review PreviewPane to confirm it owns exactly one MediaPlayer and one 33 ms DispatcherQueueTimer per editor instance. Verify rebuilds replace only sources rather than creating players or timers, timer start/stop is tied to actual needs, and disposal releases both without static references or event leaks.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 266. Play intent versus native state

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review PreviewPlaybackStateCoordinator and PreviewPane so requested play intent is tracked separately from actual MediaPlayer playback state. Confirm UI play/pause icons and EditorViewModel playback state change only on actual state transitions, delayed native state cannot override a newer user intent, and rebuild restoration does not report false playback.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 267. Play from end resets to zero

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the Play command when current position is at or effectively at project duration. Confirm it seeks to exactly zero before starting, handles frame/time rounding near the end, works after a duration-changing edit, and does not reset when merely close to the end. Check empty or zero-duration preview behavior.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 268. Manual loop implementation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review MediaEnded handling with loop preference on and off. Native IsLoopingEnabled must remain false; loop-on should seek to zero and resume according to intent, while loop-off clears intent, pauses, stops the timer, and leaves position coherent. Verify stale-generation ended events cannot restart a replaced or disposed source.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 269. Frame stepping on a 30 fps grid

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review previous/next-frame logic using fixed 30 fps rather than project FrameRate if malformed. Confirm thirty forward steps equal exactly one second without cumulative floating drift, boundaries clamp, stepping while playing behaves deliberately, and position-to-frame conversion uses the same grid as display timecode/export assumptions.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 270. Transient preview mute

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review preview mute as MediaPlayer session state. Confirm it is not stored in ProjectDocument, not undoable, not autosaved, and does not modify V1/A1 item or track volume. It should survive or reset across source rebuild and editor recreation according to intentional behavior, with automation name reflecting Mute or Unmute.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 271. Timer update feedback loop

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the 33 ms timer path from native MediaPlayer.Position to PreviewPane, EditorViewModel.Seek, TimelineControl, transport timecode, autoscroll, and live-text rendering. Confirm the update does not seek the native player back on every tick, create recursive events, mark the project edited, or continue after pause/disposal. Check jitter and thread affinity.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 272. HH:MM:SS:FF formatter

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review timecode formatting for zero, negative values, exact seconds/minutes/hours, 24 hours, and frame boundaries. Frames must be floor(remainderMilliseconds times frameRate divided by 1000), frameRate must be positive and finite, and output non-drop-frame HH:MM:SS:FF must not show frame 30 at 30 fps. Check overflow and culture-independent digits/separators.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 273. Active live-text interval

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review live text filtering at the playhead using [start, start + duration). Confirm multiple active items render, exact end removes an item, negative or over-limit data is normalized first, hidden T1 removes all visuals, and timer granularity cannot leave stale text because a render optimization skipped a boundary crossing.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 274. Live/export text-style parity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Compare live XAML text styling with TextOverlayRenderer: normalized font, size, weight, italic, foreground/background, opacity, alignment, 90 percent wrapping width, optional 18/8 px padding, and normalized center position. Identify any property applied in a different order or at a different container level that makes preview materially misrepresent export.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 275. Live text drag gating

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review selected text pointer dragging in PreviewPane and LiveTextRenderGate. Confirm the captured visual moves directly without model commits per pointer move, normal render updates cannot replace it, release clamps coordinates and commits one edit, and cancel/lost capture forces canonical rerender. Check selection changing or item deletion during drag.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 276. Canvas-coordinate mapping

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review mapping from pointer positions in the displayed Viewbox/frame to normalized project-canvas X/Y. Account for 20 px preview padding, centered frame, aspect-ratio letterboxing, Viewbox scaling, 1 px border, DPI, text-container size, and pointer capture outside bounds. Verify export uses the same center semantics across all aspect presets.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Text-overlay rasterization and cache validation

### 277. Loaded hidden render-host requirement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review how PreviewPane supplies TextOverlayRenderer with a loaded Panel that remains in the visual tree, visible to layout at 1 by 1, zero opacity, and non-hit-testable. Confirm it is available before export, remains on the UI thread, does not affect layout or accessibility, and is detached only after all rendering work is canceled or complete.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 278. Exact project-pixel output

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review RenderTargetBitmap sizing and rasterization-scale compensation. Confirm a project configured for 1920x1080, 1080x1920, or 1080x1080 produces a PNG with exactly those pixel dimensions regardless of monitor DPI or XAML scale. Check integer rounding, max texture/bitmap constraints, and whether logical canvas size could become zero or enormous from malformed data.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 279. Shared text-container construction

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review whether preview and export construct or style text through the same normalization and layout logic rather than nearly duplicated XAML trees. Verify wrapping, alignment, background padding, font fallback, opacity, and measured bounds are equivalent. Refactor only if an actual parity defect or dangerous duplication exists.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 280. Style-hash field completeness

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review TextOverlayRenderer.CalculateStyleHash and list every field capable of changing output pixels. Confirm it includes cache format version, text, font family, size, weight, italic, foreground, background color and enabled state, opacity, alignment, normalized X/Y, project width/height, and any padding/wrapping constants. Exclude only fields proven not to affect pixels.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 281. Timing intentionally excluded from hash

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the deliberate omission of text Start and Duration from cache identity. Confirm rasterized pixels are reused safely when only timing changes, overlay plans still carry updated delay/duration, filenames or current-result retention do not accidentally couple timing, and tests prove no stale timing is embedded in the PNG or native overlay object.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 282. Filename identity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review text-overlay cache filenames of <text-guid>-<style-hash>.png. Confirm GUID and hash formatting are safe, stable, lowercase or case handling is consistent, no text content enters filenames, and style changes cannot overwrite an unrelated item's file. Verify duplicated text with identical pixels but a new GUID behaves intentionally.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 283. PNG structural validation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review cached PNG validation from minimum/maximum length through signature and ordered chunk parsing. Confirm one IHDR appears first, IDAT data is nonempty, IEND is terminal, chunk lengths use overflow-safe bounds, unknown ancillary chunks are handled correctly, critical ordering violations reject the file, and no trailing bytes are accepted.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 284. CRC validation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review CRC computation for every PNG chunk, including type and data bytes, endian handling, zero-length chunks, large lengths, and stream positioning. Confirm a corrupt IHDR, IDAT, or IEND is rejected before reuse and CRC work remains bounded by the 64 MiB cache-file limit. Add focused corruption tests rather than relying only on decoder failure.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 285. Decode and pixel-format validation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the second-stage decode of a structurally valid cached PNG. Confirm it decodes successfully as premultiplied BGRA, exact expected width/height match, unsupported color/interlace data is handled safely, and decoded allocation uses checked bounds. A file that passes structure but fails decode must be regenerated, not crash export.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 286. Serialized rendering and publication races

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the per-renderer semaphore, unique temp file, overwrite move, and valid-winner reuse. Confirm two requests cannot use the hidden XAML host concurrently, a loser never deletes a winner, a concurrently published file is fully validated before reuse, and cancellation while waiting for the semaphore does not enter the critical section or leak a temp file.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 287. Cache pruning to 256 files

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review pruning after text-overlay publication. Confirm at most 256 PNG files remain, the current result is retained, the most recent valid others are kept using a deterministic timestamp policy, temp/non-PNG files are treated safely, and pruning is confined to the current project's text-overlays directory. Check concurrent render/prune and file-access failures.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 288. Cancellation and invalid-cache regeneration

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review cancellation before layout, during RenderTargetBitmap work, pixel extraction, temp write, validation, publication, and pruning. Confirm canceled work never publishes or returns an invalid path, temp files are cleaned, a previously valid cache is not deleted unnecessarily, and corrupt cache regeneration either succeeds atomically or leaves export with a clear failure.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Export setup, preflight, snapshots, and encoding profiles

### 289. Export enablement predicate

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the predicate that enables Export. Confirm at least one positive-duration V1 item is required, hidden V1 still counts if it will render filler, malformed zero-duration entries do not, and setup or render activity disables all duplicate entry points. Verify UI automation help text accurately distinguishes absent media from an already active export operation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 290. Exclusive export-operation acquisition

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ExportOperationState from button/shortcut activation through setup, render, cancellation, completion, dismissal, close, and disposal. Confirm only one operation can own the state, stale continuations cannot advance a newer operation, and every early return releases or transitions state correctly. Check rapid double-click, picker cancellation, dialog cancellation, and exceptions before rendering.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 291. Fresh deep snapshot and missing refresh

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the first export snapshot. Confirm ProjectDocument is deep-copied so preflight does not race active edits, then every referenced source is rechecked off the UI thread and Missing flags mutate only the snapshot. Verify snapshot serialization cannot include transient selection/lock/player state and later editor edits cannot alter preflight results.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 292. Save flush before export choices

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the requirement to flush project save before showing or proceeding through export configuration. Confirm the latest model revision is durable before render, save failure aborts setup without losing chosen UI state or leaving ExportOperationState stuck, and the save does not accidentally serialize Missing flags or other mutations made only on the export snapshot.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 293. Filename sanitization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review suggested export filename handling: trim whitespace and an existing .mp4 suffix, replace control and invalid characters with spaces, collapse whitespace, remove leading/trailing spaces and periods, limit base to 96 characters, reject reserved device names including CON, NUL, COM1, and LPT1, and fall back to CutFlow export.mp4. Test case-insensitivity, extensions, Unicode, and trailing device-name suffixes.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 294. Export dialog value validation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the dark ContentDialog for filename, resolution tier, and quality. Confirm values are sanitized and validated at commit time, invalid input cannot close the dialog unnoticed, cancellation makes no settings or project edit except intentional preferences, focus and keyboard behavior are correct, and selected combinations map only to the four documented bitrate profiles.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 295. Modern save-picker initialization

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Microsoft.Windows.Storage.Pickers.FileSavePicker setup with the current WindowId, MP4 file type choices, suggested filename, and last export folder. Confirm picker cancellation is not an error, stale window/editor generations cannot consume a result, unsupported folder suggestions are ignored safely, and the chosen path receives an .mp4 extension according to picker contract.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 296. Referenced-source preflight scope

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review preflight enumeration of every distinct asset referenced by V1 and A1. Confirm unreferenced library assets and T1 items do not require source files, duplicate IDs/paths are handled deterministically, orphan asset IDs are reported, hidden/muted references still require files when native composition needs them, and each path is checked immediately before render.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 297. Text-renderer requirement

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review preflight behavior when text items exist. Confirm a TextOverlayRenderer and loaded render host are required only when visible export text must be rasterized according to product semantics, hidden T1 is treated intentionally, and absence fails before staging render with an actionable message. Ensure text-only extension beyond V1 still produces trailing filler when export otherwise qualifies.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 298. Destination cannot equal any source

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review comparison between the selected destination and every imported source path, not merely referenced assets. Confirm normalization is full, case-insensitive, and robust to . and .. segments, alternate separators, existing/nonexisting destination, short names, and case-only differences. No staging or overwrite operation may begin if equality is possible.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 299. Missing-name de-duplication

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review construction of the missing-media error list. Confirm names are de-duplicated case-insensitively, stable and readable when different assets share a filename, bounded so the UI remains usable, and based on current snapshot data. Avoid leaking raw exception details while preserving enough path context for relink.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 300. Encoding-profile exactness

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review profile selection for 720p and 1080p across 16:9, 9:16, and 1:1, Standard and High quality. Verify exact dimensions, H.264, AAC stereo, 30/1 fps, 48 kHz, two channels, 192 kbit/s audio, and 5/8/8/12 Mbit/s video combinations. Check integer units, swapped portrait dimensions, square output, and no hidden hardware/codec option.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Export staging, cancellation, commit, and status UI

### 301. Unique sibling staging path

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review generation of <base>.cutflow-<guid>.mp4 in the same destination directory. Confirm the name is unique, valid, bounded, never equals a source or final path, and cannot be influenced into another directory by the base name. Using the same directory should preserve same-volume commit semantics; verify existing coincidental staging files are never overwritten unsafely.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 302. Precise trimming preference

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review MediaComposition.RenderToFileAsync invocation and MediaTrimmingPreference.Precise. Confirm the selected encoding profile is used, progress/cancellation wiring is correct, and no alternate render path silently uses Fast or direct destination rendering. Check native API result status before treating the staging file as complete.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 303. Final overwrite move off the UI thread

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the final File.Move(staging, destination, overwrite: true) execution. Confirm it occurs only after successful render and validation, runs off the calling UI thread, awaits completion before success UI, and handles destination locks, access denial, cross-volume surprises, and path disappearance. Ensure no UI object is touched from the worker thread.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 304. Destination preserved until commit

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every failure and cancellation point to prove preexisting destination content remains unchanged until the successful final staging commit. Confirm no code truncates, opens for write, deletes, or File.Replace-es the destination earlier, including native picker preparation and cleanup. Add a regression test using known destination bytes across render failure and cancellation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 305. Cleanup deletes only this staging file

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review cleanup ownership after failure or cancellation. The service may permanently delete only the unique staging path created for this operation. Confirm it never deletes the final destination, another operation's staging file, a source file, or a path changed by a stale continuation. Check symlink/reparse replacement of the staging path if relevant to the threat model.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 306. Cleanup failure does not mask primary outcome

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review handling when staging deletion fails after render failure or cancellation. Confirm the original export outcome remains primary, cleanup failure is logged locally and may be mentioned appropriately without being reported as successful cancellation, and state still resets. A leftover staging file must not later be mistaken for a completed export.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 307. Native transcoder error mapping

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review mapping of native RenderToFileAsync statuses and COM/native exceptions to readable messages for unknown error, invalid profile, missing codec, path/access, and I/O failures. Confirm cancellation is not mislabeled as an error, unexpected defects are not swallowed, and raw HRESULT or stack trace is reserved for bounded technical logging rather than user-facing copy.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 308. Render cancellation semantics

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Cancel from the status card through cancellation token/source, native render awaiting, staging cleanup, ExportOperationState transition, and UI message. Confirm cancellation is idempotent, no output file changed, progress callbacks after cancellation are ignored, the editor remains usable, and a subsequent export can start with a fresh token and state.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 309. Close during export

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review close coordination while export setup or rendering is active. Closing must invalidate setup continuations, cancel an active render, await completion and cleanup, then flush the project and settings before real close. Verify dialog/picker results arriving after close start cannot begin render and a native cancellation hang cannot deadlock the UI without any recovery path.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 310. Success shown only after commit

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the transition to export success UI. Confirm it occurs only after native render succeeded and the final staging-to-destination move completed, not merely after progress reached 100 percent. Verify destination existence/path is current, operation state is finalized, and a commit failure produces error state while preserving or cleaning the staging file according to policy.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 311. Progress callback safety

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review export progress reporting for thread affinity, monotonicity, invalid percentages, late callbacks, operation generations, and disposal. Confirm the 380 px status card updates on the UI thread, does not regress wildly or report success, and progress from a canceled/older export cannot update a newer operation. Avoid excessive UI churn from high-frequency callbacks.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 312. Open file and Open folder actions

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review success-card Open file, Open folder, and Dismiss actions. Confirm they use the committed destination, handle the file being moved/deleted after export, use safe shell APIs, cannot execute an unintended staging path, and keep success state coherent. Dismiss should not delete output or clear reusable last-folder preference, and repeated activation should be harmless.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Visual system, Home layout, editor geometry, and responsive behavior

### 313. Dark-theme resource integrity

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review App.xaml and ThemeResources.xaml to verify CutFlow consistently forces the intended dark theme and merges WinUI resources in the correct order. Check controls created dynamically in code, ContentDialogs, context menus, pickers where controllable, InfoBars, and overlay inspectors for missing resources or accidental light-theme defaults. Avoid hard-coded fixes where an existing token should be used.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 314. Palette-token consistency

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review XAML and code-behind for hard-coded colors that should map to the documented window, title bar, surface, elevated, hover, control, border, text, accent, error, warning, selection, preview, and chrome tokens. Determine whether any divergence is intentional state communication or an actual theme inconsistency. Verify alpha values and pressed/hover/focus states remain legible.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 315. Home NavigationView behavior

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the 48 px custom title bar and 208 px Home NavigationView pane across Home, Projects, and Settings. Confirm selection, back behavior, keyboard navigation, pane sizing, title-bar drag area, and content heading update correctly. Home and Projects should share the complete list intentionally, while Settings remains a local-preferences information panel.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 316. Responsive project-card grid

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Home project-card layout with 32/28/32/36 padding, UniformGridLayout, 260 by 224 cards, and 140 px thumbnail area. Check narrow and wide windows, display scaling, long localized names, empty/busy/error states, keyboard focus, and card action-button hit targets. Ensure broad card click and separate actions button do not trigger each other.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 317. Editor top-level row geometry

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the editor root rows of 48 px top bar, flexible workspace, 8 px timeline resize handle, and 180 through 600 px timeline row with 300 px default. Verify saved timeline height, window resizing, DPI, pointer capture, and minimum window size cannot create overlap, negative workspace height, or an unreachable resize handle.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 318. Three-zone editor columns

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the 70 px rail, 1 px divider, 292 px tool panel, 1 px divider, flexible preview with 280 px minimum, optional divider, and 324 px inspector. Confirm GridLength changes are coherent when panels hide, the preview receives remaining space, and separators do not remain visible without their adjacent panel.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 319. 1320 px inspector breakpoint

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the exact transition at editor width 1320 px or less. Confirm desktop inspector collapses and the same logical inspector content appears as a right overlay, above 1320 it can be independently shown/hidden, and crossing the breakpoint closes stale overlay state. Test exact 1320/1321 values, DPI-independent width, resize during text editing, and focus preservation.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 320. Inspector-toggle state

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the top-bar inspector toggle across no selection, project selection, timeline selections, wide mode, narrow overlay, navigation, and undo/redo. Confirm its icon, tooltip, automation name, visibility, and checked state match the actual inspector surface, and toggling cannot leave both InspectorPanel instances visible or neither bound correctly.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 321. Timeline resize interaction

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the 8 px timeline resize handle for pointer capture, min/max clamping, live preview, workspace-setting commit, lost capture, keyboard accessibility if provided, and window resize races. Confirm one drag does not produce excessive settings writes, a canceled drag restores canonical height, and the handle remains discoverable against the dark theme.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 322. Scrollable tool rail

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review vertical scrolling and focus navigation in the 70 px rail containing nine categories. Confirm all buttons remain reachable at small heights and high text scaling, selected state stays visible, scrolling does not steal timeline wheel/zoom unexpectedly, and tooltips/automation remain attached to the correct button after virtualization or layout.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 323. Unsupported category presentation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review each unsupported tool panel for icon, category name, and exact informational message that the category is not included in this version. Confirm no primary-looking disabled actions, fake progress, upsell, or partially wired inspector appears; keyboard and screen-reader users receive the same honest state; and switching back to supported tools restores their state correctly.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 324. Preview minimum and resize behavior

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the flexible central preview at its 280 px minimum, with 20 px padding, centered frame, 820 by 480 cap, 1 px border, Viewbox, and 48 px transport row. Check narrow windows, portrait/square canvases, inspector overlay, high DPI, and timeline resizing. Ensure controls do not overlap the media frame or become clipped/unfocusable.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Inspector events, validation, and canonical synchronization

### 325. Two inspector instances stay equivalent

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the desktop and narrow-overlay InspectorPanel instances. Confirm both receive every current ProjectDocument and EditorSelection update, expose identical controls and validation, emit the same strongly typed events, and do not subscribe twice to model actions. Switching layouts must not preserve stale input in one instance that later overwrites a newer canonical value.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 326. Inspector remains event-producing only

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review InspectorPanel code-behind to verify it never mutates ProjectDocument, timeline items, assets, or settings directly. Every commit should emit a typed request carrying parsed candidate values, with EditorView or EditorViewModel applying the edit transaction. Check reset buttons, toggles, sliders, lost-focus handlers, and initialization guards.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 327. Aspect-ratio pair update

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Project inspector aspect-ratio selection. Confirm 16:9 sets 1920x1080, 9:16 sets 1080x1920, and 1:1 sets 1080x1080 as one undoable edit; width/height cannot update separately; selection initialization does not fire an edit; and preview, text positions, caches, duration, and export profile UI refresh correctly.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 328. Opaque project-background input

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review project background ARGB editing. Confirm only #FFRRGGBB is accepted, input remains visible with concise inline error when invalid, Enter/lost-focus commit and Escape revert work, case/whitespace handling is deliberate, and rejected/no-op commits refresh from the canonical model. Preview filler/background and export must update together.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 329. Video inspector source fields

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Video inspector display and edits for filename, source resolution, source duration, sourceIn, sourceOut, effective timeline duration, volume, mute, and reset. Confirm seconds-to-milliseconds conversion is bounded and culture-aware, source range edits preserve required relationships, item kind is verified, lock state blocks commits, and fields refresh after ripple or relink.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 330. Image inspector duration

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Image inspector behavior for filename, resolution, timeline duration seconds, and reset to five seconds. Confirm reset affects the timeline item only, not asset metadata, duration stays within 100 ms and project capacity, Enter/Escape/lost-focus behave once, and the UI cannot expose video source trim controls for an image because of stale selection.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 331. Audio inspector timing and source fields

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Audio inspector start, sourceIn, sourceOut, volume, mute, reset, filename, and duration. Confirm left-edge semantics remain consistent when editing start versus sourceIn independently, source bounds and project end are enforced, overlaps are allowed, lock/global mute are represented clearly, and rejected values restore all dependent fields from the model.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 332. Text inspector complete field mapping

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every Text inspector control against TextTimelineItem: multiline content, family, size, bold, italic, alignment, foreground/background colors, background enabled, opacity and percentage, normalized X/Y, start, duration, and reset style. Verify no model field is omitted, cross-wired, committed twice, or displayed with a different normalized value than preview/export uses.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 333. Enter, Escape, and lost-focus deduplication

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review commit behavior for ordinary inspector text boxes and NumberBox internals. Enter commits, Escape reverts, and lost focus commits, but one user action must not commit twice because Enter also moves focus. Track an edit-generation or dirty flag if needed; verify no-op second commits do not add history, and dialog/layout focus changes do not save invalid text.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 334. Rejected and no-op canonical refresh

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every inspector edit response. When the service rejects a candidate or finds no change, all controls whose values depend on that field must refresh from the canonical document so UI cannot drift. Check multi-field relationships such as sourceIn/sourceOut/duration, aspect dimensions, opacity label, bold weight, and text position in both inspector instances.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 335. Inline validation live region

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review validation text for concise, field-specific messages and AutomationProperties.LiveSetting=Polite. Confirm errors clear after valid input or selection change, repeated keystrokes do not spam announcements, hidden inspector instances do not announce duplicate errors, and validation does not rely on color alone. Raw exceptions or implementation jargon should not appear.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 336. Numeric parsing and culture

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review conversion of seconds, font sizes, opacity, and normalized coordinates in NumberBox/TextBox controls under different Windows cultures. Confirm decimal separators, grouping, exponent notation, NaN/infinity, empty intermediate text, overflow, and rounding to integer milliseconds are handled deliberately. Previewed display and committed model values should not oscillate from formatting round trips.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Accessibility, focus, automation, and non-color state cues

### 337. Icon-only accessible names and tooltips

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every icon-only Button, ToggleButton, MenuFlyoutItem, transport control, timeline toolbar action, card action, title-bar control, and status-card action. Confirm each has an accurate AutomationProperties.Name and tooltip that describe the action, not merely the icon. Names must update when the action changes and remain available for dynamically created controls.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 338. Dynamic toggle action names

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review automation names, tooltips, and visible labels for Hide/Show tracks, Mute/Unmute item or preview, Lock/Unlock tracks, inspector Show/Hide, loop, snapping, and any expand/collapse state. The accessible name should describe the next action, while current state is also perceivable. Verify updates occur after undo, layout changes, selection, and model rebinding.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 339. Non-color state communication

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review missing, muted, locked, selected, disabled, warning, error, and active states to confirm none relies on color alone. Verify Missing and MUTED text badges, lock glyph/name/tooltip, selection border/background plus automation, disabled explanations, and error/warning text remain present in high contrast or reduced color perception. Check dynamically rendered timeline cards and asset cards.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 340. Visible keyboard focus

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review common button styles and all custom/dynamic controls for visible focus indicators using the documented focus brushes. Confirm accent backgrounds do not erase focus, focus is visible in high contrast, PointerOver/Pressed states do not suppress it, and card-wide clickable surfaces, trim-related controls, rail buttons, and overlay actions have an obvious focus rectangle.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 341. Tab order and focus traps

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review explicit tab indices in the editor top bar, tool rail, and preview transport, plus visual-tree order elsewhere. Verify focus moves logically through tool panel, preview, timeline, inspector, overlays, dialogs, and status card; hidden/collapsed controls are skipped; and no custom title-bar or ScrollViewer creates a keyboard trap. Check both wide and narrow layouts.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 342. Polite live regions

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review export status, inspector validation, current/total timecode automation, InfoBars, and any busy/error announcements. Confirm polite live regions announce meaningful state changes without firing every 33 ms or duplicating from hidden controls, and success/error/cancellation messages remain discoverable. Timecode's automation name should include both current and total values but update at a sensible rate.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 343. Keyboard alternatives to pointer-only edits

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review free text dragging, timeline item move/trim/reorder, playhead scrubbing, timeline resizing, and other pointer-heavy actions for practical keyboard alternatives or discoverable inspector controls. Do not claim full WCAG compliance, but identify any core editing operation that is impossible without precise pointer use despite an obvious feasible keyboard or form-based alternative.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 344. Project-card screen-reader semantics

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Home project cards so screen readers receive project name, modified time, aspect ratio, duration, thumbnail/placeholder meaning, and broad Open action without duplicate or confusing announcements from nested elements. The separate actions button must remain independently focusable and not cause the card Open action when invoked.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 345. Disabled-export explanation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review disabled Export automation help text and tooltip. Confirm it explains whether no positive-duration V1 media exists or an export operation is active, updates immediately after edits/state transitions, and remains available to keyboard and screen-reader users. A disabled control should not be the only place the user can learn how to make export available.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 346. Context-menu and tooltip accessibility

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review context-menu invocation by keyboard, focus placement, accessible labels, disabled-state explanations, and return focus for asset cards and timeline cards. Verify tooltips do not contain information unavailable elsewhere to touch or screen-reader users, and Show source, Split, Duplicate, Delete, Rename, and other actions identify their target unambiguously.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 347. Focus restoration

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review focus after opening/closing ContentDialogs, file pickers, context menus, narrow inspector overlay, export status card, Home/Editor navigation, and validation errors. Confirm focus returns to a live logical control rather than a disposed element, destructive confirmations default safely, and a new selection moves focus only when intentional. Check close or cancellation during async dialogs.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 348. Text scaling and high-contrast resilience

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review XAML for fixed heights, clipping, hard-coded foregrounds, bitmap-only labels, and disabled system brushes that break at increased text scale or Windows high-contrast themes. Focus on 48 px bars, 62 px rail buttons, 124 by 154 asset cells, project cards, inspector forms, InfoBars, and export status. Preserve the design unless a concrete accessibility defect exists.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Error handling, concurrency, path security, and lifecycle races

### 349. Narrow expected-exception filters

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review catch blocks across persistence, settings, import, relink, thumbnails, preview, text rendering, export, shell actions, and close. Confirm only expected I/O, access, invalid-data/argument, unsupported-format, invalid-operation, COM/native, overflow, and cancellation cases are translated locally. Broad Exception catches should rethrow or exist only at a true process/UI boundary with logging and safe recovery.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 350. Fatal-resource exceptions are not swallowed

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review specifically for OutOfMemoryException, StackOverflowException, AccessViolationException, and other fatal or corrupted-state conditions being caught by generic filters. PROJECT.md explicitly notes OutOfMemoryException is not swallowed by broad export handling. Verify the same principle where large bitmaps, media files, JSON, or native composition can exhaust resources.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 351. InfoBar updates respect lifetime

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all asynchronous user-visible error and warning publication. Confirm callbacks marshal to the UI thread, verify the current editor/view generation, do not overwrite a newer more relevant message blindly, and cannot access a disposed InfoBar. Check import batches, autosave, preview rebuilds, missing media, locks, relink, export, and workspace settings.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 352. Raw technical details stay out of routine UI

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review user-facing error construction to ensure stack traces, HRESULT dumps, exception type names, package paths, JSON internals, and other technical details are not shown in routine InfoBars or validation. The bounded local log may retain concise diagnostic detail. Verify messages remain actionable by naming the operation/file safely without exposing excessive local data.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 353. Gate reentrancy and release

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review HomeProjectOpenGate, EditorImportGate, PreviewRebuildGate, EventGenerationGate, LiveTextRenderGate, ExportOperationState, thumbnail asset locks, renderer semaphore, log semaphore, and save semaphore. For each, verify acquisition order, release in finally paths, cancellation, reentrancy assumptions, and whether callbacks invoked while held can try to reacquire the same gate and deadlock.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 354. Semaphore cancellation correctness

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every WaitAsync use. Confirm cancellation before acquisition does not release a semaphore that was never acquired, disposal does not race a waiter, exceptions inside the critical section still release exactly once, and UI-thread code does not synchronously block on asynchronous semaphore work. Add focused tests where existing coverage is timing-fragile.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 355. Destructive path operations and reparse points

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Perform a focused security review of Directory.Delete, File.Delete, File.Move, File.Replace, cache pruning, project cleanup, and staging cleanup under a full-trust packaged process. Verify path normalization and containment remain valid if an attacker or another local process substitutes a junction, symbolic link, hard link, or file between validation and use. Scope mitigations to realistic owned directories and operations.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 356. Source/destination TOCTOU

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review time-of-check/time-of-use races where source media existence, destination inequality, staging identity, or cache containment is checked before later native/file operations. Determine whether a file can be replaced, linked, removed, or redirected after preflight in a way that overwrites source media or escapes owned storage. Preserve user-selected workflow while closing concrete destructive gaps.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 357. Untrusted project JSON resource bounds

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review loading and normalization of manually modified project.json as untrusted local input. Check huge arrays, enormous strings, deeply nested unknown JSON, extreme numeric values, duplicate IDs, repeated asset paths, and oversized text that could cause excessive memory, UI elements, cache renders, or long startup. Add reasonable bounds only where needed and compatible with legitimate projects.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 358. Extreme media metadata

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review conversions from native media properties, StorageFile.BasicProperties, BitmapDecoder dimensions, TimeSpan, file sizes, and timestamps. Confirm extremely large dimensions, durations, sizes, negative/invalid native values, and arithmetic products cannot overflow or allocate before bounds checks. Expected invalid media should fail per file rather than destabilize the editor.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 359. Close, import, export, save, and rebuild races

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Construct and review adversarial sequences: close during import preparation, close during relink picker, close during autosave, close during preview rebuild, close during text rendering, export starting as an edit commits, navigation during settings save, and undo during a pending rebuild. Verify operation generations and lifetime tokens produce one coherent final state without lost edits or post-disposal UI access.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 360. Event-handler and native-resource leaks

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review long-session behavior across repeatedly opening projects, rebuilding previews, toggling inspectors, loading thumbnails, exporting, and returning Home. Trace event handler roots, timers, MediaSource/MediaPlayer/MediaComposition objects, StorageFile references, RenderTargetBitmap buffers, cancellation sources, and XAML visuals. Identify only leaks supported by ownership evidence or profiling-friendly reasoning, not speculative micro-optimizations.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Test quality, regression coverage, and manual acceptance gaps

### 361. Tests assert behavior rather than implementation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review the 342-test suite for cases that merely mirror private implementation details, constants, or serialized intermediate shapes without protecting a user-visible or safety invariant. Ensure critical tests exercise public/internal deterministic behavior and would fail for a real regression, while allowing harmless refactoring. Do not delete useful precise contract tests simply because they are white-box.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 362. Native and WinUI test determinism

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review tests that instantiate WinUI, MediaComposition, StorageFile, RenderTargetBitmap, or generated media fixtures. Confirm they initialize required apartment/package/native context deterministically, do not depend on developer-installed codecs beyond documented fixtures, clean resources, and fail with a meaningful reason on unsupported environments rather than hanging or passing spuriously.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 363. Temporary-directory cleanup in tests

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every test creating project roots, media fixtures, cache files, staging exports, logs, settings, or package-like layouts. Confirm paths are unique, confined to test-owned temporary directories, deleted in finally/cleanup even after assertion failure, and never point at the developer's real LocalState or C:\Dev\CutFlow. Parallel tests must not share filenames unintentionally.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 364. Deterministic cancellation tests

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review cancellation coverage for save, import, thumbnail generation, preview rebuild, text rendering, export, and close coordination. Replace timing-only sleeps with controllable gates, TaskCompletionSource, fake delegates, or deterministic hooks already supported by the architecture where necessary. Tests should prove no commit after cancellation, not only that an OperationCanceledException was observed.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 365. Concurrency tests prove ordering

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review tests for edit-during-save, duplicate imports, stale thumbnails, preview lease replacement, native event generations, text render races, log writes, and export generations. Confirm they force the adverse ordering explicitly and assert the final durable/model/UI-independent state. A test that merely launches two tasks without synchronizing the race may give false confidence.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 366. Serialization compatibility fixtures

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review model/schema tests for absent properties, explicit null, enum strings, required schemaVersion, ID mismatch, malformed JSON, unknown fields, default values, normalization, and save/load round trips. Consider stable hand-written JSON fixtures that represent older schema-1 documents so property initializers and converter changes cannot silently break compatibility.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 367. Corruption and atomic-save tests

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ProjectService tests for prior project.json preservation when normalization, serialization, write, flush, replace, cancellation, or cleanup fails. Confirm ModifiedAt rollback, temp ownership, first-save cleanup, and unreadable-list skipping are asserted using actual file bytes and controlled failures. Include malformed settings recovery separately because settings are non-atomic.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 368. Boundary and invariant test matrix

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review coverage at 0, 99, 100, 101 ms; exact source ends; exact 24-hour end and one beyond; zoom 20/400; snapping 59/60/61; undo 49/50/51; cache 255/256/257; PNG size boundaries; and profile dimensions. Add missing focused cases where a boundary controls correctness, avoiding redundant combinatorial tests with no distinct failure mode.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 369. Manual pointer and focus acceptance plan

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review which behaviors cannot be credibly proven by the automated suite: pointer hit ergonomics, capture loss, drag previews, timeline resize, focus order, keyboard routing in real WinUI controls, dialogs/pickers, inspector breakpoint, high DPI, high contrast, and screen-reader announcements. Ensure code comments or README do not imply these are automated; create a concise repeatable manual checklist only if one does not already exist and it would materially reduce release risk.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 370. Real playback and codec acceptance

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review gaps around playback smoothness, MediaPlayer state timing, installed-codec variation, missing-codec messages, image orientation, audio overlap, loop behavior, and preview rebuild continuity. Verify tests cover deterministic planning and state gates, while release validation includes representative real files on supported Windows versions. Do not add brittle media tests that depend on arbitrary machine codecs.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 371. Rendered-output visual acceptance

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review export tests and identify what they prove versus what requires inspecting real output: duration, dimensions, bitrate/profile metadata, trim boundaries, black filler, audio timing/volume, text typography/position/transparency, aspect ratios, cancellation, and destination safety. Add programmatic assertions where reliable, but retain an explicit manual visual/audio pass for qualities native APIs cannot be fully unit-tested.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 372. Release command and clean-checkout verification

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review whether tests and documentation include a clean restore, Release x64 test, Release x64 build, package registration, and launch acceptance on a fresh checkout. Check hidden reliance on prior bin/obj output, installed development certificates, environment variables, or manually generated fixtures. A passing incremental build should not be the only evidence before release.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

## Cross-cutting consistency, performance, and maintainability

### 373. Canonical path policy across services

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Compare path normalization and case-insensitive equality in ProjectService, MediaImportService, relink, thumbnail keys, cache containment, export source-equality preflight, last-folder settings, shell reveal, and tests. Verify all use compatible Windows semantics where they answer the same question, while distinguishing path identity, containment, and display. Consolidate only if divergence can cause a concrete defect.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 374. Cancellation-token propagation

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Trace cancellation tokens from MainWindow/editor lifetime, user Cancel, gate supersession, navigation, and close into every asynchronous service call. Identify operations that accept a token but ignore it before commit, create CancellationToken.None unnecessarily, or catch OperationCanceledException as an error. Ensure blocking native/file work still has a post-await generation check when direct cancellation is impossible.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 375. UTC and clock consistency

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review every use of DateTime, DateTimeOffset, DateTime.Now/UtcNow, file last-write time, log timestamp, project CreatedAt/ModifiedAt, duplicate timestamps, and localized card display. Confirm persistence and comparisons use UTC, display localizes only at the UI edge, tests control time where order matters, and daylight-saving changes cannot reorder or repeatedly modify projects.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 376. Culture-independent persistence and hashing

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review serialization, filename sanitation, cache-key construction, style hashes, timecode, color parsing, numeric formatting, and path metadata for CurrentCulture dependence. Persistent JSON and hash inputs must be stable across Finnish, Swedish, English, and other locales, while inspector parsing and modified-date display may respect user culture deliberately. Check decimal commas and Unicode casing.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 377. UI-thread affinity audit

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review continuations after Task.Run, native async operations, file I/O, semaphores, dispatcher timers, and event handlers. Confirm ProjectDocument mutations and XAML updates occur on the intended thread, background work does not capture UI objects, and ConfigureAwait choices do not create hidden affinity assumptions. Pay special attention to export commit, thumbnails, preview replacement, and save-state callbacks.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 378. Whole-document clone cost

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review JSON deep cloning in TryCommitEdit, undo snapshots, export snapshots, and project duplication for projects near plausible maximum item counts and text sizes. Determine whether repeated serialization can freeze the UI or create excessive allocations during pointer-release edits. Optimize only if evidence or clear complexity shows a user-visible problem, while preserving snapshot independence and simple architecture.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 379. Large-project UI scaling

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review Home listing, asset GridView realization, filename search, timeline card creation, ruler virtualization, inspector rebinding, live text rendering, missing-file refresh, and preview rebuild with large but valid projects. Identify O(n squared) loops, synchronous file access on the UI thread, unbounded XAML element creation, or repeated full-document work that could make the 24-hour editor unusable.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 380. Native and managed memory lifecycle

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review ownership and disposal of streams, StorageFile handles, MediaClip, BackgroundAudioTrack, MediaComposition, MediaSource, MediaPlayer, RenderTargetBitmap buffers, BitmapDecoder data, thumbnails, cancellation sources, timers, and XAML event roots. Ensure deterministic disposal where available and avoid retaining whole media buffers longer than needed. Do not add disposal calls to objects that are not disposable or would break WinUI ownership.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 381. Avoid unnecessary source-media copies

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review import, preview, thumbnail, relink, composition, and export for accidental full-file reads or copies beyond what native APIs require. Source media must remain referenced in place, not copied into Projects or LocalState. Temporary generated thumbnails, text PNGs, and export staging are legitimate; ensure their size and lifetime are bounded and clearly separated from source ownership.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 382. Documentation and code contract drift

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Compare current source and tests with PROJECT.md and README only after completing the code review. Identify contradictions affecting build commands, supported extensions, limits, shortcuts, export profiles, persistence, UI behavior, or safety claims. Update documentation only for verified current behavior or an intentional fixed defect, and never change correct code merely to make stale prose true.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 383. Dependency and telemetry audit

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review all direct and transitive package dependencies for necessity, licensing/runtime impact, network initialization, telemetry, native architecture support, and compatibility with packaged net10 WinUI. Confirm there is no FFmpeg, AI SDK, web shell, cloud client, DI container, or analytics package contradicting scope. Remove a dependency only when code proves it is unused or harmful.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```

### 384. Dead code and dormant feature paths

```text
Read `PROJECT.md`, then inspect the current CutFlow implementation and relevant tests. Treat the code under `src/CutFlow` and the executable tests under `tests/CutFlow.Tests` as the source of truth. Review unused fields, methods, event args, XAML elements, selection kinds, fade compatibility data, unsupported tool placeholders, and conditional branches. Distinguish intentional compatibility or future-facing model surface from genuinely unreachable code that increases risk. In particular, do not delete audio fades or Asset selection support solely because current UI usage is limited unless their presence causes a concrete defect or false behavior.

Trace every relevant call site and state transition rather than reviewing one method in isolation. If you find a genuine issue, explain the concrete failure mode, make the smallest safe in-scope correction, add or update focused regression tests, and run the relevant tests and Release build. Do not perform unrelated refactoring or redesign. AI code review can hallucinate issues. Do not assume that a defect exists, do not invent a finding, and do not change correct code merely to satisfy this request. If the current implementation is already correct and there is nothing genuinely worth fixing, say so clearly and make no code changes.
```
