using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using Windows.Media.Editing;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task12ExportTests
{
    [TestMethod]
    [DataRow("  My:Project?  ", "My Project.mp4")]
    [DataRow("CON", "CutFlow export.mp4")]
    [DataRow("nul.backup", "CutFlow export.mp4")]
    [DataRow("lPt1.MP4", "CutFlow export.mp4")]
    [DataRow("COM¹", "CutFlow export.mp4")]
    [DataRow("...", "CutFlow export.mp4")]
    [DataRow("travel.mp4", "travel.mp4")]
    [DataRow("夏の旅", "夏の旅.mp4")]
    [DataRow("Project COM1", "Project COM1.mp4")]
    [DataRow("Archive.NUL", "Archive.NUL.mp4")]
    public void NormalizeSuggestedFileName_ProducesSafeMp4Name(string projectName, string expected)
    {
        Assert.AreEqual(expected, ExportPresentation.NormalizeSuggestedFileName(projectName));
    }

    [TestMethod]
    public void NormalizeSuggestedFileName_DoesNotSplitUnicodeSurrogatePairAtLengthLimit()
    {
        var projectName = $"{new string('a', 95)}🚀z";

        Assert.AreEqual($"{new string('a', 95)}.mp4", ExportPresentation.NormalizeSuggestedFileName(projectName));
    }

    [TestMethod]
    public void NormalizeSuggestedFileName_LimitsBaseTo96Characters()
    {
        var projectName = new string('a', 97);

        Assert.AreEqual($"{new string('a', 96)}.mp4", ExportPresentation.NormalizeSuggestedFileName(projectName));
    }

    [TestMethod]
    [DataRow("Travel.mp4", true, "Travel.mp4")]
    [DataRow("travel", false, "travel.mp4")]
    [DataRow("  My:Project?  ", false, "My Project.mp4")]
    [DataRow("CON.mp4", false, "CutFlow export.mp4")]
    [DataRow("...", false, "CutFlow export.mp4")]
    public void TryValidateFileName_RequiresTheUserToConfirmTheSanitizedName(
        string value,
        bool expectedValid,
        string expectedSanitized)
    {
        var valid = ExportPresentation.TryValidateFileName(value, out var sanitized);

        Assert.AreEqual(expectedValid, valid);
        Assert.AreEqual(expectedSanitized, sanitized);
    }

    [TestMethod]
    public void TryCreateOptions_AcceptsOnlyTheFourDocumentedProfiles()
    {
        var expected = new[]
        {
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.High),
            new ExportOptions(ExportResolutionTier.FullHd1080p, ExportQuality.Standard),
            new ExportOptions(ExportResolutionTier.FullHd1080p, ExportQuality.High)
        };
        var actual = new List<ExportOptions>();

        for (var resolution = -1; resolution <= 2; resolution++)
        {
            for (var quality = -1; quality <= 2; quality++)
            {
                if (ExportPresentation.TryCreateOptions(resolution, quality, out var options))
                {
                    actual.Add(options);
                }
            }
        }

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CanStartExport_RequiresVisualAndIdleState()
    {
        var project = ProjectDocument.CreateNew("Export", DateTimeOffset.UnixEpoch);
        Assert.IsFalse(ExportPresentation.CanStartExport(project, isExporting: false));

        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), DurationMilliseconds = 1_000 });

        Assert.IsTrue(ExportPresentation.CanStartExport(project, isExporting: false));
        Assert.IsFalse(ExportPresentation.CanStartExport(project, isExporting: true));
    }

    [TestMethod]
    [DataRow(-4d, 0d)]
    [DataRow(42.5d, 42.5d)]
    [DataRow(125d, 100d)]
    [DataRow(double.NaN, 0d)]
    public void NormalizeProgress_ClampsToFinitePercentage(double value, double expected)
    {
        Assert.AreEqual(expected, ExportPresentation.NormalizeProgress(value));
    }

    [TestMethod]
    public void CreateProjectSnapshot_DeepCopiesMutableExportState()
    {
        var project = CreateExportableProject();
        project.TextItems.Add(new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            Text = "Original",
            DurationMilliseconds = 1_000
        });

        var snapshot = ExportService.CreateProjectSnapshot(project);
        project.VideoItems[0].DurationMilliseconds = 9_000;
        project.TextItems[0].Text = "Changed";
        project.Assets[0].FileName = "changed.jpg";

        Assert.AreEqual(1_000, snapshot.VideoItems[0].DurationMilliseconds);
        Assert.AreEqual("Original", snapshot.TextItems[0].Text);
        Assert.AreEqual("valid-image.jpg", snapshot.Assets[0].FileName);
    }

    [TestMethod]
    public async Task Settings_RoundTripsValidLastExportFolderAndDiscardsUnusablePaths()
    {
        await using var directory = new TestDirectory();
        var expected = Path.Combine(directory.Path, "exports");
        var missing = Path.Combine(directory.Path, "offline-share");
        var file = Path.Combine(directory.Path, "not-a-folder.txt");
        Directory.CreateDirectory(expected);
        await File.WriteAllTextAsync(file, "file");
        var service = new SettingsService(directory.Path);

        await service.SaveAsync(new AppSettings { LastExportFolder = $"  {expected}  " });
        var loaded = await service.LoadAsync();

        Assert.AreEqual(expected, loaded.LastExportFolder);
        Assert.AreEqual(string.Empty, AppSettings.Normalize(new AppSettings { LastExportFolder = "\0" }).LastExportFolder);
        Assert.AreEqual(string.Empty, AppSettings.Normalize(new AppSettings { LastExportFolder = "relative-folder" }).LastExportFolder);
        Assert.AreEqual(string.Empty, AppSettings.Normalize(new AppSettings { LastExportFolder = missing }).LastExportFolder);
        Assert.AreEqual(string.Empty, AppSettings.Normalize(new AppSettings { LastExportFolder = file }).LastExportFolder);

        Directory.Delete(expected);
        Assert.AreEqual(string.Empty, (await service.LoadAsync()).LastExportFolder);
    }

    [TestMethod]
    public void PickExportFileAsync_ExposesUncreatedDestinationPathContract()
    {
        Func<nint, string, CancellationToken, Task<string?>> picker = FilePickerHelper.PickExportFileAsync;

        Assert.IsNotNull(picker);
    }

    [TestMethod]
    [DataRow("CutFlow export.mp4", "CutFlow export")]
    [DataRow("TRAVEL.MP4", "TRAVEL")]
    [DataRow("draft", "draft")]
    public void ResolvePickerSuggestedFileName_OmitsTheSeparatelyConfiguredExtension(
        string value,
        string expected)
    {
        Assert.AreEqual(expected, FilePickerHelper.ResolvePickerSuggestedFileName(value));
    }

    [TestMethod]
    public void ControlCornerRadius_IsTypedForDirectBorderResourceAssignment()
    {
        var themePath = Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Styles", "ThemeResources.xaml");
        var theme = XDocument.Load(themePath);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var resource = theme.Descendants()
            .Single(element => (string?)element.Attribute(xaml + "Key") == "ControlCornerRadius");

        Assert.AreEqual("CornerRadius", resource.Name.LocalName);
    }

    [TestMethod]
    public async Task ResolveSuggestedExportFolder_ReturnsOnlyAnExistingAbsoluteDirectory()
    {
        await using var directory = new TestDirectory();
        var missing = Path.Combine(directory.Path, "offline-share");
        var file = Path.Combine(directory.Path, "not-a-folder.txt");
        var removed = Path.Combine(directory.Path, "removed");
        await File.WriteAllTextAsync(file, "file");
        Directory.CreateDirectory(removed);

        Assert.AreEqual(Path.GetFullPath(directory.Path), FilePickerHelper.ResolveSuggestedExportFolder($"  {directory.Path}  "));
        Assert.AreEqual(Path.GetFullPath(removed), FilePickerHelper.ResolveSuggestedExportFolder(removed));
        Directory.Delete(removed);

        Assert.IsNull(FilePickerHelper.ResolveSuggestedExportFolder("relative-folder"));
        Assert.IsNull(FilePickerHelper.ResolveSuggestedExportFolder(missing));
        Assert.IsNull(FilePickerHelper.ResolveSuggestedExportFolder(file));
        Assert.IsNull(FilePickerHelper.ResolveSuggestedExportFolder(removed));
        Assert.IsNull(FilePickerHelper.ResolveSuggestedExportFolder("\0"));
    }

    [TestMethod]
    public async Task ValidateAsync_RunsFileProbesOffTheCallingThread()
    {
        var project = CreateExportableProject();
        using var probeStarted = new ManualResetEventSlim();
        using var releaseProbe = new ManualResetEventSlim();
        var callingThread = Environment.CurrentManagedThreadId;
        var probeThread = callingThread;
        bool Probe(string _)
        {
            probeThread = Environment.CurrentManagedThreadId;
            probeStarted.Set();
            Assert.IsTrue(releaseProbe.Wait(TimeSpan.FromSeconds(5)));
            return true;
        }

        var validationTask = ExportPreflight.ValidateAsync(project, refreshMissingFlags: false, Probe, CancellationToken.None);

        Assert.IsTrue(probeStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsFalse(validationTask.IsCompleted);
        Assert.AreNotEqual(callingThread, probeThread);
        releaseProbe.Set();
        Assert.IsTrue((await validationTask).CanExport);
    }

    [TestMethod]
    public void ExportOperationState_InvalidatesSetupDuringCloseAndPreventsOverlap()
    {
        var state = new ExportOperationState();

        Assert.IsTrue(state.TryBegin(out var operation));
        Assert.IsTrue(state.IsActive);
        Assert.IsFalse(state.IsRendering);
        Assert.IsTrue(state.CanContinue(operation));
        Assert.IsFalse(state.TryBegin(out _));
        Assert.IsTrue(state.TryBeginRender(operation));
        Assert.IsTrue(state.IsRendering);

        state.BeginClosing();
        Assert.IsFalse(state.CanContinue(operation));
        state.CancelClosing();
        Assert.IsFalse(state.CanContinue(operation));
        Assert.IsFalse(state.TryBegin(out _));

        state.Complete(operation);
        Assert.IsTrue(state.TryBegin(out var nextOperation));
        Assert.IsTrue(state.CanContinue(nextOperation));
        Assert.IsFalse(state.IsRendering);
        state.Complete(nextOperation);
        Assert.IsFalse(state.IsActive);
    }

    [TestMethod]
    public void ExportSetup_CapturesTheRenderSnapshotImmediatelyAfterTheSaveFlush()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.Export.cs"));
        var exportStart = source.IndexOf("public async Task ExportAsync", StringComparison.Ordinal);
        var exportEnd = source.IndexOf("private bool CanContinueExport", exportStart, StringComparison.Ordinal);
        var route = source[exportStart..exportEnd];
        var save = route.IndexOf("await SaveAsync()", StringComparison.Ordinal);
        var snapshot = route.IndexOf("ExportService.CreateProjectSnapshot(ViewModel.Project)", save, StringComparison.Ordinal);
        var choices = route.IndexOf("ShowExportDialogAsync()", StringComparison.Ordinal);
        var render = route.IndexOf("RunExportAsync(savedProjectSnapshot,", StringComparison.Ordinal);

        Assert.IsTrue(
            save >= 0 && snapshot > save && choices > snapshot && render > choices,
            "Export must render the project snapshot captured immediately after the successful save flush, before showing export choices.");

        var runStart = source.IndexOf("private async Task RunExportAsync", exportEnd, StringComparison.Ordinal);
        var runEnd = source.IndexOf("private async Task<ExportDialogSelection?>", runStart, StringComparison.Ordinal);
        var runRoute = source[runStart..runEnd];
        var serviceCall = runRoute.IndexOf("service.ExportToPathAsync(", StringComparison.Ordinal);
        var renderProject = runRoute.IndexOf("projectSnapshot", serviceCall, StringComparison.Ordinal);
        var sourceGuardProject = runRoute.IndexOf("ViewModel.Project", renderProject, StringComparison.Ordinal);
        var destination = runRoute.IndexOf("destinationPath", sourceGuardProject, StringComparison.Ordinal);

        Assert.IsTrue(
            serviceCall >= 0 && renderProject > serviceCall && sourceGuardProject > renderProject && destination > sourceGuardProject,
            "Export must render the saved snapshot while guarding the destination against the current project's imported sources.");
    }

    [TestMethod]
    public void ExportDialog_ValidatesCommitAndFocusesTheFilenameForKeyboardEditing()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.Export.cs"));
        var dialogStart = source.IndexOf("private async Task<ExportDialogSelection?> ShowExportDialogAsync()", StringComparison.Ordinal);
        var dialogEnd = source.IndexOf("private void ShowExportProgress", dialogStart, StringComparison.Ordinal);
        var route = source[dialogStart..dialogEnd];

        StringAssert.Contains(route, "dialog.Opened +=");
        StringAssert.Contains(route, "fileNameBox.Focus(FocusState.Programmatic);");
        StringAssert.Contains(route, "fileNameBox.SelectAll();");
        StringAssert.Contains(route, "dialog.PrimaryButtonClick +=");
        StringAssert.Contains(route, "ExportPresentation.TryValidateFileName(fileNameBox.Text");
        StringAssert.Contains(route, "ExportPresentation.TryCreateOptions(");
        StringAssert.Contains(route, "args.Cancel = true;");
        StringAssert.Contains(route, "DefaultButton = ContentDialogButton.Primary");
        StringAssert.Contains(route, "CloseButtonText = \"Cancel\"");
    }

    [TestMethod]
    [DataRow(ExportResultStatus.Success, true, 0)]
    [DataRow(ExportResultStatus.Success, false, 0)]
    [DataRow(ExportResultStatus.Failed, true, 1)]
    [DataRow(ExportResultStatus.Failed, false, 2)]
    public void ResolveCompletion_PreservesCommittedSuccessAfterLateCancellation(
        ExportResultStatus status,
        bool cancellationRequested,
        int expected)
    {
        Assert.AreEqual((ExportCompletionState)expected, ExportPresentation.ResolveCompletion(status, cancellationRequested));
    }

    [TestMethod]
    public void Validate_RejectsProjectWithoutV1Visual()
    {
        var project = ProjectDocument.CreateNew("Empty", DateTimeOffset.UnixEpoch);

        var result = ExportPreflight.Validate(project);

        Assert.IsFalse(result.CanExport);
        StringAssert.Contains(result.ErrorMessage, "visual", StringComparison.OrdinalIgnoreCase);
        Assert.HasCount(0, result.MissingAssetNames);
    }

    [TestMethod]
    public void Validate_UsesFreshFileExistenceInsteadOfCachedMissingFlag()
    {
        var project = ProjectDocument.CreateNew("Missing", DateTimeOffset.UnixEpoch);
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            FileName = "offline.mp4",
            SourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "offline.mp4"),
            IsMissing = false
        };
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            DurationMilliseconds = 1_000
        });

        var result = ExportPreflight.Validate(project);

        Assert.IsFalse(result.CanExport);
        CollectionAssert.AreEqual(new[] { "offline.mp4" }, result.MissingAssetNames.ToArray());
    }

    [TestMethod]
    public async Task Validate_RejectsReferencedSourceWhoseImportedSnapshotChanged()
    {
        await using var directory = new TestDirectory();
        var sourcePath = Path.Combine(directory.Path, "source.jpg");
        await File.WriteAllTextAsync(sourcePath, "original");
        var asset = Asset(ProjectAssetKind.Image, "source.jpg", sourcePath);
        asset.FileSize = checked((ulong)new FileInfo(sourcePath).Length);
        asset.LastWriteUtc = File.GetLastWriteTimeUtc(sourcePath);
        var project = ProjectDocument.CreateNew("Changed source", DateTimeOffset.UnixEpoch);
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            DurationMilliseconds = 1_000
        });
        await File.WriteAllTextAsync(sourcePath, "changed source with a different size");

        var result = ExportPreflight.Validate(project);

        Assert.IsFalse(result.CanExport);
        CollectionAssert.AreEqual(new[] { "source.jpg" }, result.MissingAssetNames.ToArray());
    }

    [TestMethod]
    public async Task Validate_DeduplicatesReferencesButDistinguishesSameNamedSourcesInStableOrder()
    {
        var project = ProjectDocument.CreateNew("References", DateTimeOffset.UnixEpoch);
        var first = Asset(ProjectAssetKind.Video, "shared.mp4", @"C:\First\shared.mp4");
        var second = Asset(ProjectAssetKind.Video, "SHARED.MP4", @"D:\Second\SHARED.MP4");
        var duplicateSource = Asset(ProjectAssetKind.Video, "SHARED.mp4", @"c:\first\SHARED.mp4");
        var audio = Asset(ProjectAssetKind.Audio, "missing.wav", @"C:\Audio\missing.wav");
        project.Assets.AddRange([first, second, duplicateSource, audio]);
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = first.Id, DurationMilliseconds = 1_000 });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = first.Id, DurationMilliseconds = 1_000 });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = second.Id, DurationMilliseconds = 1_000 });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = duplicateSource.Id, DurationMilliseconds = 1_000 });
        project.AudioItems.Add(new AudioTimelineItem { Id = Guid.NewGuid(), AssetId = audio.Id, SourceOutMilliseconds = 1_000 });
        project.AudioItems.Add(new AudioTimelineItem { Id = Guid.NewGuid(), AssetId = audio.Id, SourceOutMilliseconds = 1_000 });

        var result = await ExportPreflight.ValidateAsync(
            project,
            refreshMissingFlags: false,
            _ => false,
            CancellationToken.None);

        Assert.IsFalse(result.CanExport);
        CollectionAssert.AreEqual(
            new[]
            {
                @"shared.mp4 (C:\First\shared.mp4)",
                @"SHARED.MP4 (D:\Second\SHARED.MP4)",
                "missing.wav"
            },
            result.MissingAssetNames.ToArray());
    }

    [TestMethod]
    public async Task Validate_BoundsAndSanitizesTheMissingMediaMessage()
    {
        var project = ProjectDocument.CreateNew("Bounded", DateTimeOffset.UnixEpoch);
        var names = new[]
        {
            "one.mp4\r\nsecond line",
            "two.mp4",
            "three.mp4",
            "four.mp4",
            "five.mp4"
        };
        foreach (var name in names)
        {
            var asset = Asset(ProjectAssetKind.Video, name, $@"C:\Missing\{name.ReplaceLineEndings("-")}");
            project.Assets.Add(asset);
            project.VideoItems.Add(new VideoTimelineItem
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                DurationMilliseconds = 1_000
            });
        }

        var result = await ExportPreflight.ValidateAsync(
            project,
            refreshMissingFlags: false,
            _ => false,
            CancellationToken.None);

        Assert.HasCount(5, result.MissingAssetNames);
        Assert.IsFalse(result.ErrorMessage.Contains('\r'));
        Assert.IsFalse(result.ErrorMessage.Contains('\n'));
        StringAssert.Contains(result.ErrorMessage, "one.mp4 second line");
        StringAssert.Contains(result.ErrorMessage, "three.mp4");
        StringAssert.Contains(result.ErrorMessage, "and 2 more");
        Assert.IsFalse(result.ErrorMessage.Contains("four.mp4", StringComparison.Ordinal));
        Assert.IsFalse(result.ErrorMessage.Contains("five.mp4", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Validate_BoundsAnIndividualMissingMediaLabel()
    {
        var project = ProjectDocument.CreateNew("Long name", DateTimeOffset.UnixEpoch);
        var asset = Asset(ProjectAssetKind.Video, $"{new string('a', 300)}.mp4", @"C:\Missing\long.mp4");
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            DurationMilliseconds = 1_000
        });

        var result = await ExportPreflight.ValidateAsync(
            project,
            refreshMissingFlags: false,
            _ => false,
            CancellationToken.None);

        Assert.IsLessThanOrEqualTo(96, result.MissingAssetNames.Single().Length);
        Assert.IsLessThan(200, result.ErrorMessage.Length);
    }

    [TestMethod]
    public async Task Validate_EnumeratesDistinctMutedV1AndA1AssetsAndReportsOrphans()
    {
        var project = ProjectDocument.CreateNew("References", DateTimeOffset.UnixEpoch);
        project.Settings.AudioTrackMuted = true;
        var existing = Asset(ProjectAssetKind.Image, "existing.jpg", @"C:\Media\existing.jpg");
        var missingVideo = Asset(ProjectAssetKind.Video, "missing.mp4", @"C:\Media\missing.mp4");
        var missingAudio = Asset(ProjectAssetKind.Audio, "missing.wav", @"C:\Media\missing.wav");
        var unreferenced = Asset(ProjectAssetKind.Video, "unused.mp4", @"C:\Media\unused.mp4");
        var orphanId = Guid.NewGuid();
        project.Assets.AddRange([existing, missingVideo, missingAudio, unreferenced]);
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = existing.Id, DurationMilliseconds = 1_000 });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = missingVideo.Id, DurationMilliseconds = 1_000, IsMuted = true });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = missingVideo.Id, DurationMilliseconds = 1_000 });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = orphanId, DurationMilliseconds = 1_000 });
        project.AudioItems.Add(new AudioTimelineItem { Id = Guid.NewGuid(), AssetId = missingAudio.Id, SourceOutMilliseconds = 1_000, IsMuted = true });
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "No source asset", DurationMilliseconds = 1_000 });
        var probes = new List<string>();

        var result = await ExportPreflight.ValidateAsync(
            project,
            refreshMissingFlags: false,
            path =>
            {
                probes.Add(path);
                return string.Equals(path, existing.SourcePath, StringComparison.Ordinal);
            },
            CancellationToken.None);

        Assert.IsFalse(result.CanExport);
        CollectionAssert.AreEqual(
            new[] { missingVideo.FileName, $"Unknown asset {orphanId:D}", missingAudio.FileName },
            result.MissingAssetNames.ToArray());
        CollectionAssert.AreEqual(
            new[] { existing.SourcePath, missingVideo.SourcePath, missingAudio.SourcePath },
            probes);
    }

    [TestMethod]
    public async Task Validate_HiddenV1DoesNotRequireSourceFiles()
    {
        var project = ProjectDocument.CreateNew("Hidden V1", DateTimeOffset.UnixEpoch);
        project.Settings.VideoTrackVisible = false;
        var hidden = Asset(ProjectAssetKind.Video, "hidden.mp4", @"C:\Media\hidden.mp4");
        project.Assets.Add(hidden);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = hidden.Id,
            DurationMilliseconds = 1_000,
            IsMuted = true
        });
        project.TextItems.Add(new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            Text = "No source asset",
            DurationMilliseconds = 1_000
        });
        var probes = new List<string>();

        var result = await ExportPreflight.ValidateAsync(
            project,
            refreshMissingFlags: false,
            path =>
            {
                probes.Add(path);
                return false;
            },
            CancellationToken.None);

        Assert.IsTrue(result.CanExport, result.ErrorMessage);
        Assert.HasCount(0, result.MissingAssetNames);
        Assert.HasCount(0, probes);
    }

    [TestMethod]
    public void Validate_IgnoresMissingAssetsReferencedOnlyByNonPositiveDurationItems()
    {
        var project = ProjectDocument.CreateNew("Zero duration", DateTimeOffset.UnixEpoch);
        var existing = Asset(ProjectAssetKind.Image, "valid-image.jpg", Path.Combine(FindMediaRoot(), "valid-image.jpg"));
        var missingVideo = Asset(ProjectAssetKind.Video, "zero-video.mp4", MissingPath("zero-video.mp4"));
        var missingAudio = Asset(ProjectAssetKind.Audio, "zero-audio.wav", MissingPath("zero-audio.wav"));
        project.Assets.AddRange([existing, missingVideo, missingAudio]);
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = existing.Id, DurationMilliseconds = 1_000 });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = missingVideo.Id });
        project.AudioItems.Add(new AudioTimelineItem { Id = Guid.NewGuid(), AssetId = missingAudio.Id });

        var result = ExportPreflight.Validate(project);

        Assert.IsTrue(result.CanExport, result.ErrorMessage);
        Assert.HasCount(0, result.MissingAssetNames);
    }

    [TestMethod]
    [DataRow(AspectRatioPreset.Landscape16By9, ExportResolutionTier.Hd720p, 1280, 720)]
    [DataRow(AspectRatioPreset.Portrait9By16, ExportResolutionTier.Hd720p, 720, 1280)]
    [DataRow(AspectRatioPreset.Square1By1, ExportResolutionTier.Hd720p, 720, 720)]
    [DataRow(AspectRatioPreset.Landscape16By9, ExportResolutionTier.FullHd1080p, 1920, 1080)]
    [DataRow(AspectRatioPreset.Portrait9By16, ExportResolutionTier.FullHd1080p, 1080, 1920)]
    [DataRow(AspectRatioPreset.Square1By1, ExportResolutionTier.FullHd1080p, 1080, 1080)]
    public void CreateProfile_PreservesProjectAspectForResolutionTier(
        AspectRatioPreset aspect,
        ExportResolutionTier resolution,
        int expectedWidth,
        int expectedHeight)
    {
        var profile = ExportEncodingProfile.Create(aspect, new ExportOptions(resolution, ExportQuality.Standard));

        Assert.AreEqual((uint)expectedWidth, profile.Video.Width);
        Assert.AreEqual((uint)expectedHeight, profile.Video.Height);
        Assert.AreEqual(30u, profile.Video.FrameRate.Numerator);
        Assert.AreEqual(1u, profile.Video.FrameRate.Denominator);
        Assert.AreEqual("H264", profile.Video.Subtype);
        Assert.AreEqual("AAC", profile.Audio.Subtype);
    }

    [TestMethod]
    [DataRow(ExportResolutionTier.Hd720p, ExportQuality.Standard, 5_000_000u)]
    [DataRow(ExportResolutionTier.Hd720p, ExportQuality.High, 8_000_000u)]
    [DataRow(ExportResolutionTier.FullHd1080p, ExportQuality.Standard, 8_000_000u)]
    [DataRow(ExportResolutionTier.FullHd1080p, ExportQuality.High, 12_000_000u)]
    public void CreateProfile_AppliesDeterministicQualityBitrate(
        ExportResolutionTier resolution,
        ExportQuality quality,
        uint expectedVideoBitrate)
    {
        var profile = ExportEncodingProfile.Create(
            AspectRatioPreset.Landscape16By9,
            new ExportOptions(resolution, quality));

        Assert.AreEqual(expectedVideoBitrate, profile.Video.Bitrate);
        Assert.AreEqual(192_000u, profile.Audio.Bitrate);
        Assert.AreEqual(48_000u, profile.Audio.SampleRate);
        Assert.AreEqual(2u, profile.Audio.ChannelCount);
    }

    [TestMethod]
    [DataRow(TranscodeFailureReason.None, null)]
    [DataRow(TranscodeFailureReason.Unknown, "unknown transcoding error")]
    [DataRow(TranscodeFailureReason.InvalidProfile, "encoding profile is invalid")]
    [DataRow(TranscodeFailureReason.CodecNotFound, "H.264/AAC codec is unavailable")]
    public void GetFailureMessage_MapsEveryNativeReason(
        TranscodeFailureReason reason,
        string? expectedMessage)
    {
        var actual = ExportFailureMapper.GetMessage(reason);
        if (expectedMessage is null)
        {
            Assert.IsNull(actual);
        }
        else
        {
            StringAssert.Contains(actual, expectedMessage);
        }
    }

    [TestMethod]
    public async Task ExportAsync_ValidationFailureLeavesDestinationUntouched()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        await File.WriteAllTextAsync(destinationPath, "existing destination");
        var destination = await Windows.Storage.StorageFile.GetFileFromPathAsync(destinationPath);
        var project = ProjectDocument.CreateNew("Empty", DateTimeOffset.UnixEpoch);

        var result = await new ExportService().ExportAsync(
            project,
            destination,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.ValidationFailed, result.Status);
        StringAssert.Contains(result.ErrorMessage, "visual", StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual("existing destination", await File.ReadAllTextAsync(destinationPath));
    }

    [TestMethod]
    public async Task ExportAsync_RendersToUniqueStagingThenReplacesDestinationAndReportsProgress()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        await File.WriteAllTextAsync(destinationPath, "existing destination");
        var destination = await Windows.Storage.StorageFile.GetFileFromPathAsync(destinationPath);
        var progressValues = new List<double>();
        string? stagingPath = null;
        ExportRenderAsync render = async (_, staging, _, progress, _) =>
        {
            stagingPath = staging.Path;
            Assert.AreEqual(directory.Path, Path.GetDirectoryName(staging.Path));
            Assert.AreNotEqual(destination.Path, staging.Path);
            progress.Report(42.5);
            await File.WriteAllTextAsync(staging.Path, "rendered output");
            return TranscodeFailureReason.None;
        };
        var service = new ExportService(new CompositionService(), null, render);

        var result = await service.ExportAsync(
            CreateExportableProject(),
            destination,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(progressValues.Add),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.Success, result.Status);
        Assert.AreEqual("rendered output", await File.ReadAllTextAsync(destinationPath));
        CollectionAssert.AreEqual(new[] { 0d, 42.5, 100d }, progressValues);
        Assert.IsNotNull(stagingPath);
        Assert.IsFalse(File.Exists(stagingPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public void CreateStagingFileName_IsBoundedAndCannotContainDestinationDirectories()
    {
        var operationId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var longBaseName = $"{new string('a', ExportService.MaximumStagingBaseNameLength - 1)}🚀outside";
        var destinationPath = Path.Combine(@"C:\exports", "nested", longBaseName + ".mp4");

        var stagingFileName = ExportService.CreateStagingFileName(destinationPath, operationId);
        var otherStagingFileName = ExportService.CreateStagingFileName(destinationPath, Guid.NewGuid());

        Assert.AreEqual(Path.GetFileName(stagingFileName), stagingFileName);
        Assert.IsTrue(stagingFileName.Length <= ExportService.MaximumStagingFileNameLength);
        Assert.IsTrue(stagingFileName.EndsWith(".cutflow-00112233445566778899aabbccddeeff.mp4", StringComparison.Ordinal));
        Assert.IsFalse(stagingFileName.Contains("nested", StringComparison.Ordinal));
        Assert.IsFalse(stagingFileName.Any(Path.GetInvalidFileNameChars().Contains));
        Assert.AreNotEqual(Path.GetFileName(destinationPath), stagingFileName);
        Assert.AreNotEqual(stagingFileName, otherStagingFileName);
    }

    [TestMethod]
    public async Task ExportAsync_PreservesPreExistingSiblingStagingFile()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        await File.WriteAllTextAsync(destinationPath, "existing destination");
        var coincidentalStagingPath = Path.Combine(
            directory.Path,
            "output.cutflow-00112233445566778899aabbccddeeff.mp4");
        await File.WriteAllTextAsync(coincidentalStagingPath, "unrelated staging file");
        var destination = await StorageFile.GetFileFromPathAsync(destinationPath);
        string? operationStagingPath = null;
        ExportRenderAsync render = async (_, staging, _, _, _) =>
        {
            operationStagingPath = staging.Path;
            await File.WriteAllTextAsync(staging.Path, "rendered output");
            return TranscodeFailureReason.None;
        };

        var result = await new ExportService(new CompositionService(), null, render).ExportAsync(
            CreateExportableProject(),
            destination,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.Success, result.Status, result.ErrorMessage);
        Assert.IsNotNull(operationStagingPath);
        Assert.AreNotEqual(coincidentalStagingPath, operationStagingPath);
        Assert.AreEqual("unrelated staging file", await File.ReadAllTextAsync(coincidentalStagingPath));
        Assert.AreEqual("rendered output", await File.ReadAllTextAsync(destinationPath));
    }

    [TestMethod]
    public async Task ExportAsync_TranscodeFailureDeletesOnlyOperationStaging()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        await File.WriteAllTextAsync(destinationPath, "keep destination");
        var destination = await Windows.Storage.StorageFile.GetFileFromPathAsync(destinationPath);
        ExportRenderAsync render = async (_, staging, _, _, _) =>
        {
            await File.WriteAllTextAsync(staging.Path, "partial output");
            return TranscodeFailureReason.InvalidProfile;
        };
        var service = new ExportService(new CompositionService(), null, render);

        var result = await service.ExportAsync(
            CreateExportableProject(),
            destination,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.Failed, result.Status);
        StringAssert.Contains(result.ErrorMessage, "encoding profile is invalid");
        Assert.AreEqual("keep destination", await File.ReadAllTextAsync(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExportToPathAsync_TranscodeFailureNeverCreatesOrChangesDestination(bool destinationExists)
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        byte[] destinationBytes = [0x00, 0x43, 0x75, 0x74, 0x46, 0x6C, 0x6F, 0x77, 0x80, 0xFF];
        if (destinationExists) await File.WriteAllBytesAsync(destinationPath, destinationBytes);
        ExportRenderAsync render = async (_, staging, _, _, _) =>
        {
            await File.WriteAllTextAsync(staging.Path, "partial output");
            return TranscodeFailureReason.InvalidProfile;
        };
        var service = new ExportService(new CompositionService(), null, render);

        var result = await service.ExportToPathAsync(
            CreateExportableProject(),
            destinationPath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.Failed, result.Status);
        Assert.AreEqual(destinationExists, File.Exists(destinationPath));
        if (destinationExists) CollectionAssert.AreEqual(destinationBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public async Task ExportAsync_CancellationDeletesOnlyOperationStagingAndPropagates()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        await File.WriteAllTextAsync(destinationPath, "keep destination");
        var destination = await Windows.Storage.StorageFile.GetFileFromPathAsync(destinationPath);
        using var cancellation = new CancellationTokenSource();
        ExportRenderAsync render = async (_, staging, _, _, token) =>
        {
            await File.WriteAllTextAsync(staging.Path, "partial output");
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return TranscodeFailureReason.None;
        };
        var service = new ExportService(new CompositionService(), null, render);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.ExportAsync(
            CreateExportableProject(),
            destination,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            cancellation.Token));

        Assert.AreEqual("keep destination", await File.ReadAllTextAsync(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExportToPathAsync_CancellationNeverCreatesOrChangesDestination(bool destinationExists)
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        byte[] destinationBytes = [0x00, 0x43, 0x75, 0x74, 0x46, 0x6C, 0x6F, 0x77, 0x80, 0xFF];
        if (destinationExists) await File.WriteAllBytesAsync(destinationPath, destinationBytes);
        using var cancellation = new CancellationTokenSource();
        ExportRenderAsync render = async (_, staging, _, _, token) =>
        {
            await File.WriteAllTextAsync(staging.Path, "partial output");
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return TranscodeFailureReason.None;
        };
        var service = new ExportService(new CompositionService(), null, render);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.ExportToPathAsync(
            CreateExportableProject(),
            destinationPath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            cancellation.Token));

        Assert.AreEqual(destinationExists, File.Exists(destinationPath));
        if (destinationExists) CollectionAssert.AreEqual(destinationBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExportToPathAsync_SuccessCreatesOrReplacesDestination(bool destinationExists)
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        if (destinationExists) await File.WriteAllTextAsync(destinationPath, "old destination");
        ExportRenderAsync render = async (_, staging, _, _, _) =>
        {
            await File.WriteAllTextAsync(staging.Path, "rendered output");
            return TranscodeFailureReason.None;
        };
        var service = new ExportService(new CompositionService(), null, render);

        var result = await service.ExportToPathAsync(
            CreateExportableProject(),
            destinationPath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.Success, result.Status);
        Assert.AreEqual("rendered output", await File.ReadAllTextAsync(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public async Task CommitAsync_RunsTheBlockingMoveOffTheCallingThread()
    {
        using var commitStarted = new ManualResetEventSlim();
        using var releaseCommit = new ManualResetEventSlim();
        var callingThread = Environment.CurrentManagedThreadId;
        var commitThread = callingThread;

        var commitTask = ExportService.CommitAsync("staging", "destination", (_, _) =>
        {
            commitThread = Environment.CurrentManagedThreadId;
            commitStarted.Set();
            Assert.IsTrue(releaseCommit.Wait(TimeSpan.FromSeconds(5)));
        });

        try
        {
            Assert.IsTrue(commitStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(commitTask.IsCompleted);
            Assert.AreNotEqual(callingThread, commitThread);
        }
        finally
        {
            releaseCommit.Set();
        }

        await commitTask;
    }

    [TestMethod]
    public async Task ExportToPathAsync_TextWithoutUsableRendererFailsBeforeNativeRender()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        var project = CreateExportableProject();
        project.TextItems.Add(new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            Text = "Must render",
            DurationMilliseconds = 1_000
        });
        var nativeRenderStarted = false;
        ExportRenderAsync render = (_, _, _, _, _) =>
        {
            nativeRenderStarted = true;
            return Task.FromResult(TranscodeFailureReason.None);
        };
        var service = new ExportService(new CompositionService(), null, render);

        var result = await service.ExportToPathAsync(
            project,
            destinationPath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.Failed, result.Status);
        StringAssert.Contains(result.ErrorMessage, "text", StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(nativeRenderStarted);
        Assert.IsFalse(File.Exists(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public async Task ExportToPathAsync_HiddenTextDoesNotRequireRenderer()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        var project = CreateExportableProject();
        project.Settings.TextTrackVisible = false;
        project.TextItems.Add(new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            Text = "Hidden",
            StartMilliseconds = 1_500,
            DurationMilliseconds = 500
        });
        MediaComposition? renderedComposition = null;
        ExportRenderAsync render = async (composition, staging, _, _, _) =>
        {
            renderedComposition = composition;
            await File.WriteAllTextAsync(staging.Path, "rendered output");
            return TranscodeFailureReason.None;
        };
        var service = new ExportService(new CompositionService(), null, render);

        var result = await service.ExportToPathAsync(
            project,
            destinationPath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.Success, result.Status, result.ErrorMessage);
        Assert.IsNotNull(renderedComposition);
        Assert.HasCount(0, renderedComposition.OverlayLayers);
        Assert.AreEqual(2_000d, renderedComposition.Duration.TotalMilliseconds, 2);
        Assert.AreEqual("rendered output", await File.ReadAllTextAsync(destinationPath));
    }

    [TestMethod]
    public async Task ExportToPathAsync_MissingRequiredSourceFailsBeforeNativeRender()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        var project = ProjectDocument.CreateNew("Missing source", DateTimeOffset.UnixEpoch);
        var missing = Asset(ProjectAssetKind.Video, "missing.mp4", MissingPath("missing.mp4"));
        project.Assets.Add(missing);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = missing.Id,
            DurationMilliseconds = 1_000
        });
        var nativeRenderStarted = false;
        ExportRenderAsync render = (_, _, _, _, _) =>
        {
            nativeRenderStarted = true;
            return Task.FromResult(TranscodeFailureReason.None);
        };

        var result = await new ExportService(new CompositionService(), null, render).ExportToPathAsync(
            project,
            destinationPath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.ValidationFailed, result.Status);
        Assert.IsFalse(nativeRenderStarted);
        Assert.IsFalse(File.Exists(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public async Task ExportToPathAsync_SourceChangedDuringBuildFailsBeforeNativeRender()
    {
        await using var directory = new TestDirectory();
        var sourcePath = Path.Combine(directory.Path, "source.jpg");
        File.Copy(Path.Combine(FindMediaRoot(), "valid-image.jpg"), sourcePath);
        var asset = Asset(ProjectAssetKind.Image, "source.jpg", sourcePath);
        asset.FileSize = checked((ulong)new FileInfo(sourcePath).Length);
        asset.LastWriteUtc = File.GetLastWriteTimeUtc(sourcePath);
        var project = ProjectDocument.CreateNew("Changed source", DateTimeOffset.UnixEpoch);
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            DurationMilliseconds = 1_000
        });
        var nativeRenderStarted = false;
        ExportRenderAsync render = (_, _, _, _, _) =>
        {
            nativeRenderStarted = true;
            return Task.FromResult(TranscodeFailureReason.None);
        };
        var sourceChanged = false;
        var progress = new InlineProgress<double>(value =>
        {
            if (value == 0 && !sourceChanged)
            {
                File.SetLastWriteTimeUtc(sourcePath, asset.LastWriteUtc.UtcDateTime.AddMinutes(1));
                sourceChanged = true;
            }
        });

        var result = await new ExportService(new CompositionService(), null, render).ExportToPathAsync(
            project,
            Path.Combine(directory.Path, "output.mp4"),
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            progress,
            CancellationToken.None);

        Assert.IsTrue(sourceChanged);
        Assert.AreEqual(ExportResultStatus.ValidationFailed, result.Status);
        CollectionAssert.AreEqual(new[] { "source.jpg" }, ExportPreflight.Validate(project).MissingAssetNames.ToArray());
        Assert.IsFalse(nativeRenderStarted);
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public async Task ExportToPathAsync_RefusesToOverwriteReferencedSource()
    {
        await using var directory = new TestDirectory();
        var sourcePath = Path.Combine(directory.Path, "source.mp4");
        File.Copy(Path.Combine(FindMediaRoot(), "valid-video.mp4"), sourcePath);
        var original = await File.ReadAllBytesAsync(sourcePath);
        var asset = Asset(ProjectAssetKind.Video, "source.mp4", sourcePath);
        asset.DurationMilliseconds = 2_000;
        var project = ProjectDocument.CreateNew("Source guard", DateTimeOffset.UnixEpoch);
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            SourceOutMilliseconds = 1_000,
            DurationMilliseconds = 1_000
        });
        var renderStarted = false;
        ExportRenderAsync render = (_, _, _, _, _) =>
        {
            renderStarted = true;
            return Task.FromResult(TranscodeFailureReason.None);
        };

        var result = await new ExportService(new CompositionService(), null, render).ExportToPathAsync(
            project,
            Path.Combine(directory.Path, ".", "unused", "..", "SOURCE.mp4").Replace('\\', '/'),
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.ValidationFailed, result.Status);
        StringAssert.Contains(result.ErrorMessage, "source media", StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(renderStarted);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(sourcePath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public async Task ExportToPathAsync_RefusesToOverwriteUnreferencedImportedSource()
    {
        await using var directory = new TestDirectory();
        var sourcePath = Path.Combine(directory.Path, "unused-source.mp4");
        File.Copy(Path.Combine(FindMediaRoot(), "valid-video.mp4"), sourcePath);
        var original = await File.ReadAllBytesAsync(sourcePath);
        var project = CreateExportableProject();
        project.Assets.Add(Asset(ProjectAssetKind.Video, "unused-source.mp4", sourcePath));
        var renderStarted = false;
        ExportRenderAsync render = (_, _, _, _, _) =>
        {
            renderStarted = true;
            return Task.FromResult(TranscodeFailureReason.None);
        };

        var result = await new ExportService(new CompositionService(), null, render).ExportToPathAsync(
            project,
            sourcePath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.ValidationFailed, result.Status);
        Assert.IsFalse(renderStarted);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(sourcePath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public async Task ExportToPathAsync_SourceImportedDuringBuildBlocksBeforeStaging()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        File.Copy(Path.Combine(FindMediaRoot(), "valid-video.mp4"), destinationPath);
        var original = await File.ReadAllBytesAsync(destinationPath);
        var renderProject = CreateExportableProject();
        var liveProject = ExportService.CreateProjectSnapshot(renderProject);
        var renderStarted = false;
        ExportRenderAsync render = (_, _, _, _, _) =>
        {
            renderStarted = true;
            return Task.FromResult(TranscodeFailureReason.None);
        };
        var sourceImported = false;
        var progress = new InlineProgress<double>(value =>
        {
            if (value == 0 && !sourceImported)
            {
                liveProject.Assets.Add(Asset(ProjectAssetKind.Video, "output.mp4", destinationPath));
                sourceImported = true;
            }
        });

        var result = await new ExportService(new CompositionService(), null, render).ExportToPathAsync(
            renderProject,
            liveProject,
            destinationPath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            progress,
            CancellationToken.None);

        Assert.IsTrue(sourceImported);
        Assert.AreEqual(ExportResultStatus.ValidationFailed, result.Status);
        Assert.IsFalse(renderStarted);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public async Task ExportToPathAsync_SourceImportedDuringRenderBlocksOverwriteAndCleansStaging()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        File.Copy(Path.Combine(FindMediaRoot(), "valid-video.mp4"), destinationPath);
        var original = await File.ReadAllBytesAsync(destinationPath);
        var renderProject = CreateExportableProject();
        var liveProject = ExportService.CreateProjectSnapshot(renderProject);
        ExportRenderAsync render = async (_, staging, _, _, _) =>
        {
            await File.WriteAllTextAsync(staging.Path, "rendered output");
            liveProject.Assets.Add(Asset(ProjectAssetKind.Video, "output.mp4", destinationPath));
            return TranscodeFailureReason.None;
        };

        var result = await new ExportService(new CompositionService(), null, render).ExportToPathAsync(
            renderProject,
            liveProject,
            destinationPath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.ValidationFailed, result.Status);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [TestMethod]
    public async Task ExportToPathAsync_RefreshesStaleMissingFlagBeforeCompositionBuild()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "restored.mp4");
        var project = CreateExportableProject();
        project.Assets[0].IsMissing = true;
        var renderStarted = false;
        ExportRenderAsync render = async (_, staging, _, _, _) =>
        {
            renderStarted = true;
            await File.WriteAllTextAsync(staging.Path, "rendered output");
            return TranscodeFailureReason.None;
        };

        var result = await new ExportService(new CompositionService(), null, render).ExportToPathAsync(
            project,
            destinationPath,
            new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.Success, result.Status, result.ErrorMessage);
        Assert.IsTrue(renderStarted);
        Assert.AreEqual("rendered output", await File.ReadAllTextAsync(destinationPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExportToPathAsync_CleanupFailureIsLoggedWithoutMaskingPrimaryOutcome(bool cancel)
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        await File.WriteAllTextAsync(destinationPath, "keep destination");
        using var cancellation = new CancellationTokenSource();
        ExportRenderAsync render = async (_, staging, _, _, token) =>
        {
            await File.WriteAllTextAsync(staging.Path, "partial output");
            if (cancel)
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }

            return TranscodeFailureReason.InvalidProfile;
        };
        const string sensitiveCleanupMessage = "do not log this cleanup path or detail";
        Func<Windows.Storage.StorageFile, Task> cleanup = _ => throw new IOException(sensitiveCleanupMessage);
        var service = new ExportService(
            new CompositionService(),
            null,
            render,
            cleanup,
            new SimpleLogService(directory.Path));

        if (cancel)
        {
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.ExportToPathAsync(
                CreateExportableProject(),
                destinationPath,
                new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
                new InlineProgress<double>(_ => { }),
                cancellation.Token));
        }
        else
        {
            var result = await service.ExportToPathAsync(
                CreateExportableProject(),
                destinationPath,
                new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
                new InlineProgress<double>(_ => { }),
                CancellationToken.None);

            Assert.AreEqual(ExportResultStatus.Failed, result.Status);
            StringAssert.Contains(result.ErrorMessage, "encoding profile is invalid");
        }

        Assert.AreEqual("keep destination", await File.ReadAllTextAsync(destinationPath));
        var logPath = Path.Combine(directory.Path, "cutflow.log");
        await WaitUntilAsync(() => File.Exists(logPath));
        var log = await File.ReadAllTextAsync(logPath);
        StringAssert.Contains(log, nameof(IOException));
        Assert.IsFalse(log.Contains(sensitiveCleanupMessage, StringComparison.Ordinal));
        Assert.IsFalse(log.Contains(directory.Path, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ExportToPathAsync_BlockedCleanupLoggingDoesNotDelayPrimaryFailure()
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        await File.WriteAllTextAsync(destinationPath, "keep destination");
        var logger = new SimpleLogService(directory.Path);
        var lockName = (string)typeof(SimpleLogService)
            .GetField("_processWriteLockName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(logger)!;
        using var loggingBlocker = new Semaphore(1, 1, lockName);
        Assert.IsTrue(loggingBlocker.WaitOne(0));
        ExportRenderAsync render = (_, _, _, _, _) => Task.FromResult(TranscodeFailureReason.InvalidProfile);
        Func<Windows.Storage.StorageFile, Task> cleanup = _ => throw new IOException("cleanup failed");
        var service = new ExportService(
            new CompositionService(),
            null,
            render,
            cleanup,
            logger);

        try
        {
            var result = await service.ExportToPathAsync(
                    CreateExportableProject(),
                    destinationPath,
                    new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
                    new InlineProgress<double>(_ => { }),
                    CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(2));

            Assert.AreEqual(ExportResultStatus.Failed, result.Status);
        }
        finally
        {
            loggingBlocker.Release();
        }

        await WaitUntilAsync(() => File.Exists(Path.Combine(directory.Path, "cutflow.log")));
    }

    [TestMethod]
    [DataRow(ExportResolutionTier.Hd720p, 1280u, 720u)]
    [DataRow(ExportResolutionTier.FullHd1080p, 1920u, 1080u)]
    public async Task ExportToPathAsync_NativeRenderCreatesReadableVideo(
        ExportResolutionTier resolution,
        uint expectedWidth,
        uint expectedHeight)
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, $"native-{resolution}.mp4");
        var project = CreateExportableProject();
        var audio = Asset(ProjectAssetKind.Audio, "valid-audio.wav", Path.Combine(FindMediaRoot(), "valid-audio.wav"));
        project.Assets.Add(audio);
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = audio.Id,
            SourceOutMilliseconds = 1_000,
            Volume = 0.5
        });

        var result = await new ExportService().ExportToPathAsync(
            project,
            destinationPath,
            new ExportOptions(resolution, ExportQuality.Standard),
            new InlineProgress<double>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ExportResultStatus.Success, result.Status, result.ErrorMessage);
        Assert.IsTrue(new FileInfo(destinationPath).Length > 1_000);
        var file = await StorageFile.GetFileFromPathAsync(destinationPath);
        var properties = await file.Properties.GetVideoPropertiesAsync();
        Assert.AreEqual(expectedWidth, properties.Width);
        Assert.AreEqual(expectedHeight, properties.Height);
        Assert.IsTrue(properties.Duration >= TimeSpan.FromMilliseconds(900));
        var clip = await MediaClip.CreateFromFileAsync(file);
        Assert.IsTrue(clip.OriginalDuration >= TimeSpan.FromMilliseconds(900));
    }

    private static ProjectAsset Asset(ProjectAssetKind kind, string name, string path) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        FileName = name,
        SourcePath = path
    };

    private static string MissingPath(string name) =>
        Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"), name);

    private static string FindMediaRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "TestMedia");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "CutFlow", "Styles", "ThemeResources.xaml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The CutFlow repository root was not found.");
    }

    private static ProjectDocument CreateExportableProject()
    {
        var project = ProjectDocument.CreateNew("Export", DateTimeOffset.UnixEpoch);
        var asset = Asset(ProjectAssetKind.Image, "valid-image.jpg", Path.Combine(FindMediaRoot(), "valid-image.jpg"));
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            DurationMilliseconds = 1_000
        });
        return project;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class TestDirectory : IAsyncDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
