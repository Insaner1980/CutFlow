using CutFlow.Models;
using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class TimelineKeyboardNavigationTests
{
    [TestMethod]
    public void AdjacentClip_TraversesEveryTrackRegardlessOfViewport()
    {
        var project = new ProjectDocument();
        var video = new VideoTimelineItem { Id = Guid.NewGuid() };
        var text = new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 1_000_000 };
        var audio = new AudioTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 2_000_000 };
        project.VideoItems.Add(video);
        project.TextItems.Add(text);
        project.AudioItems.Add(audio);

        Assert.AreEqual(text.Id, TimelineLayoutProjection.GetAdjacentClip(project, video.Id, false)?.ItemId);
        Assert.AreEqual(audio.Id, TimelineLayoutProjection.GetAdjacentClip(project, text.Id, false)?.ItemId);
        Assert.AreEqual(text.Id, TimelineLayoutProjection.GetAdjacentClip(project, audio.Id, true)?.ItemId);
        Assert.AreEqual(video.Id, TimelineLayoutProjection.GetAdjacentClip(project, text.Id, true)?.ItemId);
        Assert.IsNull(TimelineLayoutProjection.GetAdjacentClip(project, video.Id, true));
        Assert.IsNull(TimelineLayoutProjection.GetAdjacentClip(project, audio.Id, false));
        Assert.IsNull(TimelineLayoutProjection.GetAdjacentClip(project, Guid.NewGuid(), false));
    }
}
