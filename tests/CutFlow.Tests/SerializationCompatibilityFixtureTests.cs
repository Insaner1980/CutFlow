using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed partial class SerializationCompatibilityFixtureTests
{
    private static readonly Guid FixtureProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [TestMethod]
    public async Task LegacySchema1Fixture_LoadSaveLoad_PreservesCompatibilityDefaults()
    {
        using var directory = new TemporaryDirectory();
        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(directory.Path, "Projects", FixtureProjectId.ToString("D")));
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "SerializationFixtures", "schema-1-legacy-minimal.json");
        File.Copy(fixturePath, Path.Combine(projectDirectory.FullName, "project.json"));
        var service = new ProjectService(directory.Path);

        var loaded = await service.LoadAsync(FixtureProjectId, TestContext.CancellationToken);
        AssertCompatibilityDefaults(loaded);

        await service.SaveAsync(loaded, TestContext.CancellationToken);
        var roundTrip = await service.LoadAsync(FixtureProjectId, TestContext.CancellationToken);

        AssertCompatibilityDefaults(roundTrip);
    }

    private static void AssertCompatibilityDefaults(ProjectDocument project)
    {
        Assert.AreEqual(ProjectDocument.LegacySchemaVersion, project.SchemaVersion);
        Assert.AreEqual(FixtureProjectId, project.Id);
        Assert.AreEqual(1920, project.Settings.Width);
        Assert.AreEqual(1080, project.Settings.Height);
        Assert.AreEqual(30d, project.Settings.FrameRate);
        Assert.AreEqual(AspectRatioPreset.Landscape16By9, project.Settings.AspectRatio);
        Assert.AreEqual(ProjectSettings.DefaultBackgroundColor, project.Settings.BackgroundColor);
        Assert.IsTrue(project.Settings.VideoTrackVisible);
        Assert.IsTrue(project.Settings.TextTrackVisible);
        Assert.IsFalse(project.Settings.AudioTrackMuted);

        var video = Assert.ContainsSingle(project.VideoItems);
        Assert.AreEqual(1_000L, video.DurationMilliseconds);
        Assert.AreEqual(1d, video.Volume);
        Assert.IsFalse(video.IsMuted);

        var audio = Assert.ContainsSingle(project.AudioItems);
        Assert.AreEqual(2_000L, audio.DurationMilliseconds);
        Assert.AreEqual(1d, audio.Volume);
        Assert.AreEqual(0L, audio.FadeInMilliseconds);
        Assert.AreEqual(0L, audio.FadeOutMilliseconds);
        Assert.IsFalse(audio.IsMuted);

        var text = Assert.ContainsSingle(project.TextItems);
        Assert.AreEqual(3_000L, text.DurationMilliseconds);
        Assert.AreEqual(TextTimelineItem.DefaultFontFamily, text.FontFamily);
        Assert.AreEqual(TextTimelineItem.DefaultFontSize, text.FontSize);
        Assert.AreEqual(TextTimelineItem.DefaultFontWeight, text.FontWeight);
        Assert.AreEqual(TextTimelineItem.DefaultTextColor, text.TextColor);
        Assert.AreEqual(TextTimelineItem.DefaultBackgroundColor, text.BackgroundColor);
        Assert.AreEqual(TextTimelineItem.DefaultOpacity, text.Opacity);
        Assert.AreEqual(TextHorizontalAlignment.Center, text.Alignment);
        Assert.AreEqual(0.5d, text.NormalizedX);
        Assert.AreEqual(0.5d, text.NormalizedY);
    }

    private sealed partial class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    public TestContext TestContext { get; set; }
}
