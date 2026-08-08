using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task13AcceptanceTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void RenameProject_TrimsTheNameAndParticipatesInUndoRedo()
    {
        var project = ProjectDocument.CreateNew("New project", DateTimeOffset.UtcNow);
        var viewModel = new EditorViewModel(project);

        Assert.IsTrue(viewModel.RenameProject("  Final edit  "));
        Assert.AreEqual("Final edit", viewModel.ProjectName);
        Assert.AreEqual(EditorViewModel.UnsavedStatus, viewModel.SaveStatus);

        viewModel.Undo();
        Assert.AreEqual("New project", viewModel.ProjectName);

        viewModel.Redo();
        Assert.AreEqual("Final edit", viewModel.ProjectName);
        Assert.IsFalse(viewModel.RenameProject("   "));
        Assert.AreEqual("Final edit", viewModel.ProjectName);
    }

    [TestMethod]
    public void EditorProjectName_IsOutsideTheWindowDragRegion()
    {
        var editorView = XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Views", "EditorView.xaml"));
        var projectName = editorView
            .Descendants()
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "ProjectNameBox");

        Assert.IsFalse(
            projectName.Ancestors().Any(element => (string?)element.Attribute(Xaml + "Name") == "TitleBarDragRegion"),
            "The editable project name must not be inside the non-client drag region.");
    }

    [TestMethod]
    public void TextPanel_HasPrimaryAddActionAndTheFourLocalPresets()
    {
        var mediaPanel = XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Controls", "MediaPanel.xaml"));
        var buttonLabels = mediaPanel
            .Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => (string?)element.Attribute("Content"))
            .Where(label => label is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var requiredLabel in new[] { "Add text", "Default", "Title", "Subtitle", "Minimal label" })
        {
            Assert.IsTrue(buttonLabels.Contains(requiredLabel), $"The Text panel is missing the '{requiredLabel}' action.");
        }
    }

    [TestMethod]
    public void TimelineCardText_CollapsesEmbeddedLineBreaksAndWhitespace()
    {
        Assert.AreEqual("CutFlow final Ready", TimelineCardText.ToSingleLine("  CutFlow final\r\n  Ready  "));
    }

    [TestMethod]
    public void TrackHeaderStates_AreUndoableAndDriveCompositionOutput()
    {
        var project = ProjectDocument.CreateNew("Tracks", DateTimeOffset.UnixEpoch);
        var visual = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            SourcePath = "video.mp4",
            FileName = "video.mp4",
            DurationMilliseconds = 1_000
        };
        var audio = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Audio,
            SourcePath = "audio.wav",
            FileName = "audio.wav",
            DurationMilliseconds = 1_000
        };
        project.Assets.AddRange([visual, audio]);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = visual.Id,
            SourceOutMilliseconds = 1_000
        });
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = audio.Id,
            SourceOutMilliseconds = 1_000
        });
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "Hidden", DurationMilliseconds = 1_000 });
        var viewModel = new EditorViewModel(project);

        Assert.IsTrue(viewModel.SetVideoTrackVisible(false));
        Assert.IsTrue(viewModel.SetTextTrackVisible(false));
        Assert.IsTrue(viewModel.SetAudioTrackMuted(true));

        var plan = CompositionPlan.Create(viewModel.Project);
        Assert.IsTrue(plan.Visuals.All(item => item.Kind == CompositionVisualKind.Filler));
        Assert.HasCount(0, plan.TextOverlays);
        Assert.AreEqual(0, plan.AudioTracks.Single().Volume);

        viewModel.Undo();
        Assert.IsFalse(viewModel.Project.Settings.AudioTrackMuted);
        viewModel.Redo();
        Assert.IsTrue(viewModel.Project.Settings.AudioTrackMuted);
    }

    [TestMethod]
    public void MediaImportScopes_KeepVisualAndAudioPickersSeparate()
    {
        var supportedExtensions = new[] { ".mp4", ".png", ".jpg", ".jpeg", ".mp3", ".wav" };

        CollectionAssert.AreEquivalent(
            new[] { ".mp4", ".png", ".jpg", ".jpeg" },
            FilePickerHelper.GetMediaExtensions(MediaImportScope.Visual).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { ".mp3", ".wav" },
            FilePickerHelper.GetMediaExtensions(MediaImportScope.Audio).ToArray());
        CollectionAssert.AreEquivalent(
            supportedExtensions,
            FilePickerHelper.GetMediaExtensions(MediaImportScope.All).ToArray());
        Assert.IsTrue(MediaImportScope.Audio.Allows("voice.WAV"));
        Assert.IsFalse(MediaImportScope.Audio.Allows("clip.mp4"));
    }

    [TestMethod]
    public async Task HomeProjectOpenGate_AwaitsOneOpenAndRejectsConcurrentRequests()
    {
        var gate = new HomeProjectOpenGate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = gate.RunAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
        });

        await entered.Task;
        Assert.IsFalse(await gate.RunAsync(() => Task.CompletedTask));

        release.SetResult();
        Assert.IsTrue(await first);
    }

    [TestMethod]
    public void HomeProjectMutations_UseTheSharedOperationState()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Views", "HomeView.xaml.cs"));

        StringAssert.Contains(source, "private bool IsProjectOperationActive");
        Assert.AreEqual(5, source.Split("if (!_canContinue() || IsProjectOperationActive", StringSplitOptions.None).Length - 1);
    }

    [TestMethod]
    public void TimelineDurationLimit_RejectsUnsafeInputAndClampsTimelineEdits()
    {
        Assert.AreEqual(86_400_000L, ProjectDocument.MaximumTimelineDurationMilliseconds);
        Assert.IsTrue(TimelineInput.TryParseSeconds("86400", out var maximum));
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, maximum);
        Assert.IsFalse(TimelineInput.TryParseSeconds("86400.001", out _));

        var project = ProjectDocument.CreateNew("Bounded", DateTimeOffset.UnixEpoch);
        var imageAsset = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Image, DurationMilliseconds = 5_000 };
        var image = new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = imageAsset.Id, DurationMilliseconds = 5_000 };
        var audio = new AudioTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 0, SourceOutMilliseconds = 1_000 };
        var text = new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 1_000, DurationMilliseconds = 3_000 };
        project.Assets.Add(imageAsset);
        project.VideoItems.Add(image);
        project.AudioItems.Add(audio);
        project.TextItems.Add(text);

        Assert.IsTrue(TimelineEditingService.SetImageDuration(project, image.Id, long.MaxValue));
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, image.DurationMilliseconds);
        Assert.IsTrue(TimelineEditingService.MoveAudioItem(project, audio.Id, long.MaxValue));
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - audio.DurationMilliseconds, audio.StartMilliseconds);
        Assert.IsTrue(TimelineEditingService.MoveTextItem(project, text.Id, long.MaxValue));
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - text.DurationMilliseconds, text.StartMilliseconds);
    }

    [TestMethod]
    public void CompositionPlan_RejectsAnOutOfRangeProjectWithoutCreatingUnsafeDurations()
    {
        var project = ProjectDocument.CreateNew("Unsafe", DateTimeOffset.UnixEpoch);
        project.TextItems.Add(new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            StartMilliseconds = ProjectDocument.MaximumTimelineDurationMilliseconds,
            DurationMilliseconds = ProjectDocument.MinimumItemDurationMilliseconds
        });

        var plan = CompositionPlan.Create(project);

        Assert.HasCount(0, plan.Visuals);
        Assert.AreEqual(0, plan.TargetDurationMilliseconds);
        Assert.IsTrue(plan.Errors.Any(error => error.Contains("24 hours", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void EditorTimelineIngress_ClampsLongSourcesAndRejectsAnOverflowingDuplicate()
    {
        var project = ProjectDocument.CreateNew("Ingress", DateTimeOffset.UnixEpoch);
        var text = new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 1_000, DurationMilliseconds = 3_000 };
        project.TextItems.Add(text);
        var viewModel = new EditorViewModel(project);

        Assert.IsTrue(viewModel.SetTextDuration(text.Id, long.MaxValue));
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - 1_000, viewModel.Project.TextItems[0].DurationMilliseconds);

        viewModel.Project.TextItems[0].StartMilliseconds = ProjectDocument.MaximumTimelineDurationMilliseconds - 3_000;
        viewModel.Project.TextItems[0].DurationMilliseconds = 3_000;
        viewModel.Select(new EditorSelection(EditorSelectionKind.TextItem, text.Id));
        Assert.IsFalse(viewModel.DuplicateSelection());
        Assert.HasCount(1, viewModel.Project.TextItems);

        var mediaProject = ProjectDocument.CreateNew("Long source", DateTimeOffset.UnixEpoch);
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            DurationMilliseconds = long.MaxValue
        };
        mediaProject.Assets.Add(asset);
        var mediaViewModel = new EditorViewModel(mediaProject);

        Assert.IsTrue(mediaViewModel.AddAssetToTimeline(asset.Id));
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, mediaViewModel.Project.VideoItems[0].DurationMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, mediaViewModel.Project.VideoItems[0].SourceOutMilliseconds);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CutFlow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the CutFlow repository root.");
    }
}
