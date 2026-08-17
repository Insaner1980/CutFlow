using CutFlow.Models;
using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace CutFlow.Tests;

[TestClass]
public sealed class TimelineDropPolicyTests
{
    [TestMethod]
    [DataRow(-1d, TimelineTrackKind.None)]
    [DataRow(29.999d, TimelineTrackKind.None)]
    [DataRow(30d, TimelineTrackKind.Video)]
    [DataRow(93.999d, TimelineTrackKind.Video)]
    [DataRow(94d, TimelineTrackKind.Text)]
    [DataRow(137.999d, TimelineTrackKind.Text)]
    [DataRow(138d, TimelineTrackKind.Audio)]
    [DataRow(185.999d, TimelineTrackKind.Audio)]
    [DataRow(186d, TimelineTrackKind.None)]
    public void HitTest_MapsPointerYToTheVisibleTrackRows(double pointerY, TimelineTrackKind expected)
    {
        var actual = TimelineDropPolicy.HitTest(pointerY, 30, 64, 44, 48);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(TimelineTrackKind.Video, ProjectAssetKind.Video, true)]
    [DataRow(TimelineTrackKind.Video, ProjectAssetKind.Image, true)]
    [DataRow(TimelineTrackKind.Video, ProjectAssetKind.Audio, false)]
    [DataRow(TimelineTrackKind.Text, ProjectAssetKind.Video, false)]
    [DataRow(TimelineTrackKind.Text, ProjectAssetKind.Image, false)]
    [DataRow(TimelineTrackKind.Text, ProjectAssetKind.Audio, false)]
    [DataRow(TimelineTrackKind.Audio, ProjectAssetKind.Video, false)]
    [DataRow(TimelineTrackKind.Audio, ProjectAssetKind.Image, false)]
    [DataRow(TimelineTrackKind.Audio, ProjectAssetKind.Audio, true)]
    [DataRow(TimelineTrackKind.None, ProjectAssetKind.Video, false)]
    public void Evaluate_AllowsOnlyVisualAssetsOnV1AndAudioOnA1(
        TimelineTrackKind track,
        ProjectAssetKind assetKind,
        bool expectedAllowed)
    {
        var decision = TimelineDropPolicy.Evaluate(track, assetKind, isLocked: false);

        Assert.AreEqual(expectedAllowed, decision.IsAllowed);
        Assert.AreEqual(
            expectedAllowed ? TimelineDropFailure.None : track == TimelineTrackKind.None
                ? TimelineDropFailure.NoTrack
                : TimelineDropFailure.IncompatibleAsset,
            decision.Failure);
    }

    [TestMethod]
    public void Evaluate_RejectsAnOtherwiseCompatibleAssetWhenTrackIsLocked()
    {
        var decision = TimelineDropPolicy.Evaluate(
            TimelineTrackKind.Audio,
            ProjectAssetKind.Audio,
            isLocked: true);

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(TimelineDropFailure.LockedTrack, decision.Failure);
    }

    [TestMethod]
    public void DragOver_AdvertisedTypesMatchTheFinalTrackScope()
    {
        Assert.IsTrue(TimelineDropPolicy.CanAcceptDrag(
            TimelineTrackKind.Video, false, true, false, [".mp4"]));
        Assert.IsFalse(TimelineDropPolicy.CanAcceptDrag(
            TimelineTrackKind.Video, false, true, false, [".wav"]));
        Assert.IsTrue(TimelineDropPolicy.CanAcceptDrag(
            TimelineTrackKind.Audio, false, true, false, [".wav"]));
        Assert.IsFalse(TimelineDropPolicy.CanAcceptDrag(
            TimelineTrackKind.Audio, false, true, false, [".jpg"]));
        Assert.IsFalse(TimelineDropPolicy.CanAcceptDrag(
            TimelineTrackKind.Text, false, true, false, [".mp4"]));
    }

    [TestMethod]
    public void DragOver_UnknownExternalStorageCanBeInspectedButUnknownLibraryPayloadIsRejected()
    {
        Assert.IsTrue(TimelineDropPolicy.CanAcceptDrag(
            TimelineTrackKind.Video, false, false, true, []));
        Assert.IsFalse(TimelineDropPolicy.CanAcceptDrag(
            TimelineTrackKind.Video, false, true, false, []));
        Assert.IsFalse(TimelineDropPolicy.CanAcceptDrag(
            TimelineTrackKind.Video, true, false, true, [".mp4"]));
    }

    [TestMethod]
    public void AdvertisedFileType_CannotBypassFinalAssetKindValidation()
    {
        Assert.IsTrue(TimelineDropPolicy.CanAcceptDrag(
            TimelineTrackKind.Video, false, true, false, [".mp4"]));

        var finalDecision = TimelineDropPolicy.Evaluate(
            TimelineTrackKind.Video,
            ProjectAssetKind.Audio,
            isLocked: false);

        Assert.IsFalse(finalDecision.IsAllowed);
        Assert.AreEqual(TimelineDropFailure.IncompatibleAsset, finalDecision.Failure);
    }

    [TestMethod]
    public void TrackLocks_BlockOnlyItemsOnTheLockedTrack()
    {
        var locks = new TimelineTrackLocks();
        locks.SetLocked(TimelineTrackKind.Video, true);

        Assert.IsFalse(locks.CanEdit(EditorSelectionKind.VideoItem));
        Assert.IsTrue(locks.CanEdit(EditorSelectionKind.TextItem));
        Assert.IsTrue(locks.CanEdit(EditorSelectionKind.AudioItem));

        locks.SetLocked(TimelineTrackKind.Video, false);

        Assert.IsTrue(locks.CanEdit(EditorSelectionKind.VideoItem));
    }

    [TestMethod]
    public void LibraryAssetPayload_RoundTripsOnlyAValidAssetGuid()
    {
        var assetId = Guid.NewGuid();

        Assert.IsTrue(MediaAssetDragPayload.TryParseAssetId(
            MediaAssetDragPayload.Create(assetId),
            out var parsed));
        Assert.AreEqual(assetId, parsed);
        Assert.IsFalse(MediaAssetDragPayload.TryParseAssetId("not-an-asset-id", out _));
        Assert.IsFalse(MediaAssetDragPayload.TryParseAssetId(null, out _));
    }

    [TestMethod]
    public void LibraryAssetPayload_AdvertisesTheActualExtensionForDragOverFeedback()
    {
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Audio,
            SourcePath = @"C:\Media\voice.WAV"
        };
        var data = new Windows.ApplicationModel.DataTransfer.DataPackage();

        MediaAssetDragPayload.Set(data, asset);

        Assert.IsTrue(data.GetView().Contains(MediaAssetDragPayload.FormatId));
        Assert.Contains(".WAV", data.Properties.FileTypes.ToArray());
    }

    [TestMethod]
    [DataRow(VirtualKey.Enter, true, true)]
    [DataRow(VirtualKey.Space, true, true)]
    [DataRow(VirtualKey.Enter, false, false)]
    [DataRow(VirtualKey.Space, false, false)]
    [DataRow(VirtualKey.Escape, true, false)]
    public void MediaCardActivation_UsesEnterOrSpaceOnlyForAnAddableAsset(
        VirtualKey key,
        bool canAdd,
        bool expected)
    {
        Assert.AreEqual(expected, MediaAssetActivationPolicy.ShouldActivate(key, canAdd));
    }
}
