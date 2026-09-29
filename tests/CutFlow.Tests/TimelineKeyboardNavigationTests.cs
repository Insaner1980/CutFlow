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
    [TestMethod]
    public void AdjacentClip_FollowsVisualOrderAndKeepsEqualStartsStable()
    {
        var project = new ProjectDocument();
        var lateText = new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 10_000 };
        var earlyText = new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 1_000 };
        var tiedText = new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 1_000 };
        var lateAudio = new AudioTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 8_000 };
        var earlyAudio = new AudioTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 2_000 };
        project.TextItems.AddRange([lateText, earlyText, tiedText]);
        project.AudioItems.AddRange([lateAudio, earlyAudio]);

        Assert.AreEqual(tiedText.Id, TimelineLayoutProjection.GetAdjacentClip(project, earlyText.Id, false)?.ItemId);
        Assert.AreEqual(lateText.Id, TimelineLayoutProjection.GetAdjacentClip(project, tiedText.Id, false)?.ItemId);
        Assert.AreEqual(earlyAudio.Id, TimelineLayoutProjection.GetAdjacentClip(project, lateText.Id, false)?.ItemId);
        Assert.AreEqual(lateAudio.Id, TimelineLayoutProjection.GetAdjacentClip(project, earlyAudio.Id, false)?.ItemId);
        Assert.AreEqual(earlyAudio.Id, TimelineLayoutProjection.GetAdjacentClip(project, lateAudio.Id, true)?.ItemId);
        Assert.AreEqual(lateText.Id, TimelineLayoutProjection.GetAdjacentClip(project, earlyAudio.Id, true)?.ItemId);
        Assert.AreEqual(lateText.Id, project.TextItems[0].Id);
        Assert.AreEqual(lateAudio.Id, project.AudioItems[0].Id);
    }

}
