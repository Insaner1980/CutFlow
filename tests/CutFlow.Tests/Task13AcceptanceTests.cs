using CutFlow.Controls;
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
    private static readonly string[] expected = new[] { ".mp4", ".png", ".jpg", ".jpeg" };
    private static readonly string[] expectedArray = new[] { ".mp3", ".wav" };

    [TestMethod]
    public void RenameProject_TrimsTheNameAndParticipatesInUndoRedo()
    {
        var project = ProjectDocument.CreateNew("New project", DateTimeOffset.UtcNow);
        var viewModel = new EditorViewModel(project);
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;

        Assert.IsTrue(viewModel.RenameProject("  Final edit  "));
        Assert.AreEqual("Final edit", viewModel.ProjectName);
        Assert.AreEqual(EditorViewModel.UnsavedStatus, viewModel.SaveStatus);
        Assert.AreEqual(1, committed);
        Assert.IsFalse(viewModel.RenameProject("Final edit"));
        Assert.AreEqual(1, committed);

        viewModel.Undo();
        Assert.AreEqual("New project", viewModel.ProjectName);
        Assert.IsFalse(viewModel.CanUndo);

        viewModel.Redo();
        Assert.AreEqual("Final edit", viewModel.ProjectName);
        Assert.IsFalse(viewModel.RenameProject("   "));
        Assert.IsFalse(viewModel.RenameProject(new string('N', ProjectDocument.MaximumNameLength + 1)));
        Assert.AreEqual("Final edit", viewModel.ProjectName);
    }

    [TestMethod]
    public void RenameProject_UnchangedLegacyNameIsNotNormalizedOrAddedToUndoHistory()
    {
        var unusualName = "  Legacy\u0001name  ";
        var viewModel = new EditorViewModel(ProjectDocument.CreateNew(unusualName, DateTimeOffset.UtcNow));

        Assert.IsFalse(viewModel.RenameProject(unusualName));
        Assert.AreEqual(unusualName, viewModel.ProjectName);
        Assert.IsFalse(viewModel.CanUndo);
        Assert.AreEqual(EditorViewModel.SavedStatus, viewModel.SaveStatus);
    }

    [TestMethod]
    public void EditorProjectName_IsOutsideTheWindowDragRegion()
    {
        var editorView = XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Views", "EditorView.xaml"));
        var projectName = editorView
            .Descendants()
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "ProjectNameBox");

        Assert.DoesNotContain(
            element => (string?)element.Attribute(Xaml + "Name") == "TitleBarDragRegion", projectName.Ancestors(),
            "The editable project name must not be inside the non-client drag region.");
    }

    [TestMethod]
    public void EditorClose_CommitsPendingProjectNameBeforeFlushingSave()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.Export.cs"));
        var closeStart = source.IndexOf("public async Task<bool> PrepareToCloseAsync()", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, closeStart, "The editor close preparation route is missing.");
        var closeEnd = source.IndexOf("public void CancelClosePreparation()", closeStart, StringComparison.Ordinal);
        Assert.IsGreaterThan(closeStart, closeEnd, "The editor close preparation route could not be isolated.");
        var closeRoute = source[closeStart..closeEnd];
        var commitIndex = closeRoute.IndexOf("CommitProjectName();", StringComparison.Ordinal);
        var saveIndex = closeRoute.IndexOf("return await SaveAsync();", StringComparison.Ordinal);

        Assert.IsTrue(
            commitIndex >= 0 && saveIndex > commitIndex,
            "Closing must commit the focused title-bar name before flushing the project save.");
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
            Assert.Contains(requiredLabel, buttonLabels, $"The Text panel is missing the '{requiredLabel}' action.");
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

        Assert.AreSequenceEqual(
            expected, FilePickerHelper.GetMediaExtensions(MediaImportScope.Visual).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.AreSequenceEqual(
            expectedArray, FilePickerHelper.GetMediaExtensions(MediaImportScope.Audio).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.AreSequenceEqual(
            supportedExtensions, FilePickerHelper.GetMediaExtensions(MediaImportScope.All).ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.IsTrue(MediaImportScope.Audio.Allows("voice.WAV"));
        Assert.IsFalse(MediaImportScope.Audio.Allows("clip.mp4"));
        Assert.IsTrue(MediaImportScope.Visual.CanAcceptDrop(true, [".mp4"]));
        Assert.IsFalse(MediaImportScope.Visual.CanAcceptDrop(true, [".wav"]));
        Assert.IsTrue(MediaImportScope.Audio.CanAcceptDrop(true, [".wav", ".txt"]));
        Assert.IsFalse(MediaImportScope.Audio.CanAcceptDrop(false, [".wav"]));
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

        Assert.Contains("private bool IsProjectOperationActive", source);
        Assert.Contains("RunProjectOpenGateAsync", GetMethod(source, "public async Task CreateProjectAsync()", "private async void ProjectOpen_Click"));
        Assert.Contains("RunProjectOpenGateAsync", GetMethod(source, "private async Task OpenFromTagAsync", "internal async Task<bool> RunProjectOpenGateAsync"));
        Assert.Contains("RunProjectOpenGateAsync", GetMethod(source, "private async void RenameProject_Click", "private async void DuplicateProject_Click"));
        Assert.Contains("RunProjectOpenGateAsync", GetMethod(source, "private async void DuplicateProject_Click", "private async void DeleteProject_Click"));
        Assert.Contains("RunProjectOpenGateAsync", GetMethod(source, "private async void DeleteProject_Click", "private ContentDialog CreateDialog"));
    }

    [TestMethod]
    public void HomeProjectCard_ActionsUseASeparateFortyPixelTarget()
    {
        var card = ProjectCardViewModel.Create(ProjectDocument.CreateNew("Accessible project", DateTimeOffset.UnixEpoch));
        var homeView = XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Views", "HomeView.xaml"));
        var buttons = homeView.Descendants().Where(element => element.Name.LocalName == "Button").ToArray();
        var openButton = buttons.Single(element => (string?)element.Attribute("Click") == "ProjectOpen_Click");
        var actionsButton = buttons.Single(element =>
            element.Descendants().Any(descendant => descendant.Name.LocalName == "MenuFlyout"));

        Assert.AreEqual("40", (string?)actionsButton.Attribute("Width"));
        Assert.AreEqual("40", (string?)actionsButton.Attribute("Height"));
        Assert.DoesNotContain(openButton, actionsButton.Ancestors());
        Assert.AreEqual(
            $"Open project Accessible project. {card.ModifiedText}. Aspect ratio {card.AspectRatioText}. Duration {card.DurationText}. Thumbnail unavailable.",
            card.OpenAutomationName);
        Assert.AreEqual("Project actions for Accessible project", card.ActionsAutomationName);
        Assert.AreEqual("Open project Accessible project", card.OpenActionName);
        Assert.AreEqual("Rename project Accessible project", card.RenameActionName);
        Assert.AreEqual("Duplicate project Accessible project", card.DuplicateActionName);
        Assert.AreEqual("Delete project Accessible project", card.DeleteActionName);
        Assert.AreEqual("{x:Bind OpenAutomationName, Mode=OneWay}", (string?)openButton.Attribute("AutomationProperties.Name"));
        Assert.AreEqual("{x:Bind ActionsAutomationName}", (string?)actionsButton.Attribute("AutomationProperties.Name"));
        Assert.AreEqual(
            "{x:Bind OpenAutomationName, Mode=OneWay}",
            (string?)openButton.Descendants().Single(element => element.Name.LocalName == "ToolTip").Attribute("Content"));
        Assert.AreEqual(
            "{x:Bind ActionsAutomationName}",
            (string?)actionsButton.Descendants().Single(element => element.Name.LocalName == "ToolTip").Attribute("Content"));
        var actionNames = actionsButton.Descendants()
            .Where(element => element.Name.LocalName == "MenuFlyoutItem")
            .Select(element => (string?)element.Attribute("AutomationProperties.Name"))
            .ToArray();
        Assert.AreSequenceEqual(
            new[]
            {
                "{x:Bind OpenActionName}",
                "{x:Bind RenameActionName}",
                "{x:Bind DuplicateActionName}",
                "{x:Bind DeleteActionName}"
            },
            actionNames);

        var cardRoot = openButton.Ancestors().First(element => element.Name.LocalName == "Border");
        var presentationElements = cardRoot.Descendants()
            .Where(element => element.Name.LocalName is "TextBlock" or "Image" or "FontIcon")
            .ToArray();
        Assert.HasCount(8, presentationElements);
        Assert.IsTrue(presentationElements.All(element =>
            (string?)element.Attribute("AutomationProperties.AccessibilityView") == "Raw"));

        var changedProperties = new List<string?>();
        card.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        card.SetThumbnailPath(@"C:\Thumbnails\accessible.jpg");

        Assert.Contains(nameof(ProjectCardViewModel.OpenAutomationName), changedProperties);
        Assert.EndsWith("Thumbnail available.", card.OpenAutomationName, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AssetCards_ExposeKeyboardContextActionsWithoutTooltipOnlySourceData()
    {
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            SourcePath = @"C:\Media\interview.mp4",
            FileName = "interview.mp4",
            DurationMilliseconds = 1_000,
            Width = 1920,
            Height = 1080,
            IsMissing = true
        };
        var card = new MediaAssetCard(asset);
        var mediaPanelPath = Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Controls", "MediaPanel.xaml");
        var mediaPanel = XDocument.Load(mediaPanelPath);
        var assetCard = mediaPanel.Descendants()
            .Single(element => element.Name.LocalName == "Border" && (string?)element.Attribute("DragStarting") == "AssetCard_DragStarting");
        var source = File.ReadAllText(Path.ChangeExtension(mediaPanelPath, ".xaml.cs"));

        Assert.AreEqual("{Binding FileName}", (string?)assetCard.Attribute("ToolTipService.ToolTip"));
        Assert.IsFalse(assetCard.Descendants().Any(element => element.Name.LocalName == "MenuFlyout"));
        Assert.Contains("Source file missing.", card.AccessibleName, StringComparison.Ordinal);
        Assert.Contains("container.ContextFlyout = CreateAssetContextMenu(card);", source, StringComparison.Ordinal);
        Assert.Contains("RestoreAssetFocus(card.AssetId)", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void IconOnlyControls_HaveExplicitActionNamesAndTooltips()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src", "CutFlow");
        var xamlFiles = Directory.GetFiles(sourceRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var path in xamlFiles)
        {
            var document = XDocument.Load(path);
            var iconOnlyButtons = document.Descendants().Where(element =>
                element.Name.LocalName is "Button" or "ToggleButton" or "MenuFlyoutItem" &&
                element.Descendants().Any(descendant => descendant.Name.LocalName is "FontIcon" or "SymbolIcon" or "PathIcon" or "BitmapIcon") &&
                string.IsNullOrWhiteSpace((string?)element.Attribute("Content")) &&
                !element.Descendants().Any(descendant =>
                    descendant.Name.LocalName == "TextBlock" &&
                    !string.IsNullOrWhiteSpace((string?)descendant.Attribute("Text"))));

            foreach (var button in iconOnlyButtons)
            {
                var identifier = (string?)button.Attribute(Xaml + "Name") ?? button.Name.LocalName;
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace((string?)button.Attribute("AutomationProperties.Name")),
                    $"{Path.GetFileName(path)}: {identifier} has no explicit accessible action name.");
                Assert.Contains(
                    descendant =>
                        descendant.Name.LocalName == "ToolTip" &&
                        !string.IsNullOrWhiteSpace((string?)descendant.Attribute("Content")),
                    button.Descendants(),
                    $"{Path.GetFileName(path)}: {identifier} has no action tooltip.");
            }
        }
    }

    [TestMethod]
    public void TimelineDurationLimit_RejectsUnsafeInputAndClampsTimelineEdits()
    {
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
        Assert.Contains(error => error.Contains("24 hours", StringComparison.Ordinal), plan.Errors);
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

    private static string GetMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0 && end > start, $"Could not isolate {startMarker}.");
        return source[start..end];
    }
}
