using System.Text.Json;
using CutFlow.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class ProjectModelTests
{
    [TestMethod]
    public void CreateNew_InitializesSchemaIdentityAndDefaultSettings()
    {
        var createdAt = new DateTimeOffset(2026, 8, 4, 10, 30, 0, TimeSpan.Zero);

        var project = ProjectDocument.CreateNew("Summer edit", createdAt);

        Assert.AreEqual(1, project.SchemaVersion);
        Assert.AreNotEqual(Guid.Empty, project.Id);
        Assert.AreEqual("Summer edit", project.Name);
        Assert.AreEqual(createdAt, project.CreatedAt);
        Assert.AreEqual(createdAt, project.ModifiedAt);
        Assert.AreEqual(1920, project.Settings.Width);
        Assert.AreEqual(1080, project.Settings.Height);
        Assert.AreEqual(30d, project.Settings.FrameRate);
        Assert.IsEmpty(project.Assets);
        Assert.IsEmpty(project.VideoItems);
        Assert.IsEmpty(project.AudioItems);
        Assert.IsEmpty(project.TextItems);
    }

    [TestMethod]
    public void CreateNew_NormalizesTimestampToUtcWithoutChangingTheInstant()
    {
        var localTimestamp = new DateTimeOffset(2026, 8, 9, 23, 59, 59, TimeSpan.FromHours(3));

        var project = ProjectDocument.CreateNew("UTC project", localTimestamp);

        Assert.AreEqual(localTimestamp.ToUniversalTime(), project.CreatedAt);
        Assert.AreEqual(TimeSpan.Zero, project.CreatedAt.Offset);
        Assert.AreEqual(project.CreatedAt, project.ModifiedAt);
    }

    [TestMethod]
    public void ApplyAspectRatio_Portrait_Uses1080By1920()
    {
        var settings = new ProjectSettings();

        settings.ApplyAspectRatio(AspectRatioPreset.Portrait9By16);

        Assert.AreEqual(1080, settings.Width);
        Assert.AreEqual(1920, settings.Height);
        Assert.AreEqual(AspectRatioPreset.Portrait9By16, settings.AspectRatio);
    }

    [TestMethod]
    public void ApplyAspectRatio_Square_Uses1080By1080()
    {
        var settings = new ProjectSettings();

        settings.ApplyAspectRatio(AspectRatioPreset.Square1By1);

        Assert.AreEqual(1080, settings.Width);
        Assert.AreEqual(1080, settings.Height);
    }

    [TestMethod]
    public void ProjectSettings_SerializesAspectRatioAsEnumName()
    {
        var settings = new ProjectSettings();
        settings.ApplyAspectRatio(AspectRatioPreset.Portrait9By16);

        var json = JsonSerializer.Serialize(settings);

        Assert.Contains("\"aspectRatio\":\"Portrait9By16\"", json);
    }

    [TestMethod]
    public void EditorSelection_NoneHasNoSelectedItem()
    {
        var selection = EditorSelection.None;

        Assert.AreEqual(EditorSelectionKind.None, selection.Kind);
        Assert.IsNull(selection.ItemId);
    }

    [TestMethod]
    public void VideoDurationFallback_SaturatesWhenSourceRangeExceedsLong()
    {
        var item = new VideoTimelineItem
        {
            SourceInMilliseconds = long.MinValue,
            SourceOutMilliseconds = long.MaxValue
        };

        Assert.AreEqual(long.MaxValue, item.DurationMilliseconds);
    }

    [TestMethod]
    public void AudioDuration_SaturatesWhenSourceRangeExceedsLong()
    {
        var item = new AudioTimelineItem
        {
            SourceInMilliseconds = long.MinValue,
            SourceOutMilliseconds = long.MaxValue
        };

        Assert.AreEqual(long.MaxValue, item.DurationMilliseconds);
    }
}
