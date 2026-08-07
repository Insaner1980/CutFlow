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
    [DataTestMethod]
    [DataRow("  My:Project?  ", "My Project.mp4")]
    [DataRow("CON", "CutFlow export.mp4")]
    [DataRow("...", "CutFlow export.mp4")]
    [DataRow("travel.mp4", "travel.mp4")]
    public void NormalizeSuggestedFileName_ProducesSafeMp4Name(string projectName, string expected)
    {
        Assert.AreEqual(expected, ExportPresentation.NormalizeSuggestedFileName(projectName));
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

    [DataTestMethod]
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
    public async Task Settings_RoundTripsLastExportFolderAndRejectsInvalidPath()
    {
        await using var directory = new TestDirectory();
        var expected = Path.GetFullPath(directory.Path);
        var service = new SettingsService(directory.Path);

        await service.SaveAsync(new AppSettings { LastExportFolder = $"  {expected}  " });
        var loaded = await service.LoadAsync();

        Assert.AreEqual(expected, loaded.LastExportFolder);
        Assert.AreEqual(string.Empty, AppSettings.Normalize(new AppSettings { LastExportFolder = "\0" }).LastExportFolder);
    }

    [TestMethod]
    public void PickExportFileAsync_ExposesUncreatedDestinationPathContract()
    {
        Func<nint, string, CancellationToken, Task<string?>> picker = FilePickerHelper.PickExportFileAsync;

        Assert.IsNotNull(picker);
    }

    [DataTestMethod]
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
    public async Task ResolveSuggestedExportFolder_NormalizesWithoutTouchingTheFileSystem()
    {
        await using var directory = new TestDirectory();
        var missing = Path.Combine(directory.Path, "offline-share");

        Assert.AreEqual(Path.GetFullPath(directory.Path), FilePickerHelper.ResolveSuggestedExportFolder($"  {directory.Path}  "));
        Assert.AreEqual(Path.GetFullPath(missing), FilePickerHelper.ResolveSuggestedExportFolder(missing));
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

    [DataTestMethod]
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
    public void Validate_ReportsEachReferencedMissingV1OrA1AssetOnce()
    {
        var project = ProjectDocument.CreateNew("References", DateTimeOffset.UnixEpoch);
        var existing = Asset(ProjectAssetKind.Image, "valid-image.jpg", Path.Combine(FindMediaRoot(), "valid-image.jpg"));
        var missingVideo = Asset(ProjectAssetKind.Video, "missing-video.mp4", MissingPath("missing-video.mp4"));
        var missingAudio = Asset(ProjectAssetKind.Audio, "missing-audio.wav", MissingPath("missing-audio.wav"));
        var unreferenced = Asset(ProjectAssetKind.Video, "unused.mp4", MissingPath("unused.mp4"));
        project.Assets.AddRange([existing, missingVideo, missingAudio, unreferenced]);
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = existing.Id, DurationMilliseconds = 1_000 });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = missingVideo.Id, DurationMilliseconds = 1_000 });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = missingVideo.Id, DurationMilliseconds = 1_000 });
        project.AudioItems.Add(new AudioTimelineItem { Id = Guid.NewGuid(), AssetId = missingAudio.Id, SourceOutMilliseconds = 1_000 });
        project.AudioItems.Add(new AudioTimelineItem { Id = Guid.NewGuid(), AssetId = missingAudio.Id, SourceOutMilliseconds = 1_000 });

        var result = ExportPreflight.Validate(project);

        Assert.IsFalse(result.CanExport);
        CollectionAssert.AreEqual(
            new[] { "missing-video.mp4", "missing-audio.wav" },
            result.MissingAssetNames.ToArray());
        Assert.IsFalse(result.ErrorMessage.Contains("unused.mp4", StringComparison.Ordinal));
    }

    [DataTestMethod]
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

    [DataTestMethod]
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

    [DataTestMethod]
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

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExportToPathAsync_TranscodeFailureNeverCreatesOrChangesDestination(bool destinationExists)
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        if (destinationExists) await File.WriteAllTextAsync(destinationPath, "keep destination");
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
        if (destinationExists) Assert.AreEqual("keep destination", await File.ReadAllTextAsync(destinationPath));
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

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExportToPathAsync_CancellationNeverCreatesOrChangesDestination(bool destinationExists)
    {
        await using var directory = new TestDirectory();
        var destinationPath = Path.Combine(directory.Path, "output.mp4");
        if (destinationExists) await File.WriteAllTextAsync(destinationPath, "keep destination");
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
        if (destinationExists) Assert.AreEqual("keep destination", await File.ReadAllTextAsync(destinationPath));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.cutflow-*.mp4"));
    }

    [DataTestMethod]
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
            Path.Combine(directory.Path, ".", "source.mp4"),
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

    [DataTestMethod]
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
        var log = await File.ReadAllTextAsync(Path.Combine(directory.Path, "cutflow.log"));
        StringAssert.Contains(log, nameof(IOException));
        Assert.IsFalse(log.Contains(sensitiveCleanupMessage, StringComparison.Ordinal));
        Assert.IsFalse(log.Contains(directory.Path, StringComparison.OrdinalIgnoreCase));
    }

    [DataTestMethod]
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
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".superpowers", "sdd", "2026-08-04-cutflow-v1", "scratch", "task-7-media");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Task media fixtures were not found.");
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
