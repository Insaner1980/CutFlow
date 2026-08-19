using System.Text.Json;
using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;

namespace CutFlow.Tests;

[TestClass]
public sealed class ProjectDocumentClonerTests
{
    [TestMethod]
    public void Clone_CopiesSerializableStateAndKeepsMutableObjectsIndependent()
    {
        var project = CreateProject(1, "Original text");

        var clone = ProjectDocumentCloner.Clone(project);

        Assert.AreEqual(JsonSerializer.Serialize(project), JsonSerializer.Serialize(clone));
        clone.Settings.Width = 720;
        clone.Assets[0].FileName = "changed.mp4";
        clone.VideoItems[0].Volume = 0.25;
        clone.AudioItems[0].FadeInMilliseconds = 400;
        clone.TextItems[0].Text = "Changed text";

        Assert.AreEqual(1080, project.Settings.Width);
        Assert.AreEqual("source.mp4", project.Assets[0].FileName);
        Assert.AreEqual(0.75, project.VideoItems[0].Volume);
        Assert.AreEqual(100, project.AudioItems[0].FadeInMilliseconds);
        Assert.AreEqual("Original text", project.TextItems[0].Text);
    }

    [TestMethod]
    public void PointerReleaseEdit_NearMaximumDocumentAvoidsMultipleJsonSizedAllocations()
    {
        const int itemCount = 3_000;
        var text = new string('x', ProjectService.MaximumPersistedTextLength);
        var project = CreateProject(itemCount, text);
        var viewModel = new ViewModels.EditorViewModel(project);
        var jsonSize = JsonSerializer.SerializeToUtf8Bytes(project).Length;

        _ = ProjectDocumentCloner.Clone(CreateProject(1, "warmup"));
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        Assert.IsTrue(viewModel.SetTextPosition(project.TextItems[0].Id, 0.25, 0.75));

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.IsLessThan(
            jsonSize * 4L,
            allocated,
            $"A pointer-release edit allocated {allocated:N0} bytes for a {jsonSize:N0}-byte project snapshot.");
    }

    private static ProjectDocument CreateProject(int itemCount, string text)
    {
        var project = ProjectDocument.CreateNew("Clone", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 1080;
        project.Settings.Height = 1920;
        project.Settings.FrameRate = 29.97;
        project.Settings.AspectRatio = AspectRatioPreset.Portrait9By16;
        project.Settings.BackgroundColor = "#FF123456";
        project.Settings.VideoTrackVisible = false;
        project.Settings.TextTrackVisible = false;
        project.Settings.AudioTrackMuted = true;
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            SourcePath = @"C:\Media\source.mp4",
            FileName = "source.mp4",
            DurationMilliseconds = ProjectDocument.MaximumTimelineDurationMilliseconds,
            Width = 1920,
            Height = 1080,
            FileSize = 123_456,
            LastWriteUtc = DateTimeOffset.UnixEpoch,
            ThumbnailCachePath = "cache/thumbnails/source.jpg",
            IsMissing = true
        };
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            SourceInMilliseconds = 100,
            SourceOutMilliseconds = 1_000,
            DurationMilliseconds = 900,
            Volume = 0.75,
            IsMuted = true
        });
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            StartMilliseconds = 200,
            SourceInMilliseconds = 100,
            SourceOutMilliseconds = 900,
            Volume = 0.5,
            FadeInMilliseconds = 100,
            FadeOutMilliseconds = 200,
            IsMuted = true
        });
        for (var index = 0; index < itemCount; index++)
        {
            project.TextItems.Add(new TextTimelineItem
            {
                Id = Guid.NewGuid(),
                StartMilliseconds = index * 100L,
                DurationMilliseconds = 1_000,
                Text = text,
                FontFamily = "Arial",
                FontSize = 72,
                FontWeight = 700,
                IsItalic = true,
                TextColor = "#FFAABBCC",
                BackgroundColor = "#FF112233",
                BackgroundEnabled = true,
                Opacity = 0.8,
                Alignment = TextHorizontalAlignment.Right,
                NormalizedX = 0.4,
                NormalizedY = 0.6
            });
        }

        return project;
    }
}
