using System.Text.Json;
using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class UndoHistoryTests
{
    [TestMethod]
    public void RecordUndoAndRedo_RestoreIndependentJsonSnapshots()
    {
        var history = new UndoHistory();
        var initial = ProjectDocument.CreateNew("Initial", DateTimeOffset.UnixEpoch);
        initial.Settings.BackgroundColor = "#FF112233";
        initial.Assets.Add(new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            SourcePath = @"C:\Media\initial.mp4"
        });
        initial.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = initial.Assets[0].Id,
            SourceOutMilliseconds = 1_000,
            DurationMilliseconds = 1_000
        });
        initial.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            SourceOutMilliseconds = 1_000
        });
        initial.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "Before" });
        history.Record(initial);

        initial.Settings.BackgroundColor = "#FFFFFFFF";
        initial.Assets[0].SourcePath = @"C:\Media\mutated.mp4";
        initial.VideoItems[0].SourceOutMilliseconds = 500;
        initial.AudioItems[0].SourceOutMilliseconds = 500;
        initial.TextItems[0].Text = "Mutated original";

        var changed = ProjectDocument.CreateNew("Changed", DateTimeOffset.UnixEpoch);
        changed.Id = initial.Id;
        changed.TextItems.Add(new TextTimelineItem { Id = initial.TextItems[0].Id, Text = "After" });

        var undone = history.Undo(changed);

        Assert.IsNotNull(undone);
        Assert.AreEqual("Initial", undone.Name);
        Assert.AreEqual("#FF112233", undone.Settings.BackgroundColor);
        Assert.AreEqual(@"C:\Media\initial.mp4", undone.Assets[0].SourcePath);
        Assert.AreEqual(1_000, undone.VideoItems[0].SourceOutMilliseconds);
        Assert.AreEqual(1_000, undone.AudioItems[0].SourceOutMilliseconds);
        Assert.AreEqual("Before", undone.TextItems[0].Text);
        Assert.AreNotSame(initial.Settings, undone.Settings);
        Assert.AreNotSame(initial.Assets, undone.Assets);
        Assert.AreNotSame(initial.Assets[0], undone.Assets[0]);
        Assert.AreNotSame(initial.VideoItems[0], undone.VideoItems[0]);
        Assert.AreNotSame(initial.AudioItems[0], undone.AudioItems[0]);
        Assert.AreNotSame(initial.TextItems[0], undone.TextItems[0]);
        undone.Name = "Mutated returned value";
        Assert.IsTrue(history.CanRedo);

        var redone = history.Redo(undone);

        Assert.IsNotNull(redone);
        Assert.AreEqual("Changed", redone.Name);
        Assert.AreEqual("After", redone.TextItems[0].Text);
    }

    [TestMethod]
    public void Undo_NormalizesRestoredSnapshotLikePersistence()
    {
        var history = new UndoHistory();
        var snapshot = ProjectDocument.CreateNew("Partially normalized", DateTimeOffset.UnixEpoch);
        snapshot.Settings = null!;
        snapshot.Assets = null!;
        snapshot.VideoItems = null!;
        snapshot.AudioItems = null!;
        snapshot.TextItems = null!;
        history.Record(snapshot);

        var restored = history.Undo(ProjectDocument.CreateNew("Current", DateTimeOffset.UnixEpoch));

        Assert.IsNotNull(restored);
        Assert.IsNotNull(restored.Settings);
        Assert.IsNotNull(restored.Assets);
        Assert.IsNotNull(restored.VideoItems);
        Assert.IsNotNull(restored.AudioItems);
        Assert.IsNotNull(restored.TextItems);
    }

    [TestMethod]
    public void Undo_WhenSnapshotSchemaIsUnsupported_RejectsWithoutChangingHistory()
    {
        var history = new UndoHistory();
        var snapshot = ProjectDocument.CreateNew("Unsupported", DateTimeOffset.UnixEpoch);
        snapshot.SchemaVersion = ProjectDocument.CurrentSchemaVersion + 1;
        history.Record(snapshot);

        Assert.ThrowsExactly<InvalidDataException>(
            () => history.Undo(ProjectDocument.CreateNew("Current", DateTimeOffset.UnixEpoch)));
        Assert.IsTrue(history.CanUndo);
        Assert.IsFalse(history.CanRedo);
    }

    [TestMethod]
    public void Undo_WhenSnapshotContainsNullCollectionItem_RejectsWithoutChangingHistory()
    {
        var history = new UndoHistory();
        var snapshot = ProjectDocument.CreateNew("Null item", DateTimeOffset.UnixEpoch);
        snapshot.TextItems.Add(null!);
        history.Record(snapshot);

        Assert.ThrowsExactly<InvalidDataException>(
            () => history.Undo(ProjectDocument.CreateNew("Current", DateTimeOffset.UnixEpoch)));
        Assert.IsTrue(history.CanUndo);
        Assert.IsFalse(history.CanRedo);
    }

    [TestMethod]
    public void Record_WhenEnumSerializationFails_PreservesRedoHistory()
    {
        var history = new UndoHistory();
        var first = ProjectDocument.CreateNew("First", DateTimeOffset.UnixEpoch);
        var second = ProjectDocument.CreateNew("Second", DateTimeOffset.UnixEpoch);
        history.Record(first);
        var undone = history.Undo(second)!;
        undone.Settings.AspectRatio = (AspectRatioPreset)int.MaxValue;

        Assert.ThrowsExactly<JsonException>(() => history.Record(undone));
        Assert.IsFalse(history.CanUndo);
        Assert.IsTrue(history.CanRedo);

        var redone = history.Redo(first);
        Assert.IsNotNull(redone);
        Assert.AreEqual("Second", redone.Name);
    }

    [TestMethod]
    public void RecordAfterUndo_InvalidatesRedo()
    {
        var history = new UndoHistory();
        var first = ProjectDocument.CreateNew("First", DateTimeOffset.UnixEpoch);
        var second = ProjectDocument.CreateNew("Second", DateTimeOffset.UnixEpoch);
        var third = ProjectDocument.CreateNew("Third", DateTimeOffset.UnixEpoch);
        history.Record(first);
        history.Record(second);

        var undone = history.Undo(third);
        Assert.IsNotNull(undone);
        Assert.IsTrue(history.CanRedo);

        history.Record(undone);

        Assert.IsFalse(history.CanRedo);
        Assert.IsNull(history.Redo(undone));
    }

    [TestMethod]
    public void History_IsBoundedToMostRecentFiftyUndoEntries()
    {
        var history = new UndoHistory();
        for (var index = 0; index < 51; index++)
        {
            history.Record(ProjectDocument.CreateNew($"Snapshot {index}", DateTimeOffset.UnixEpoch));
        }

        var current = ProjectDocument.CreateNew("Current", DateTimeOffset.UnixEpoch);
        for (var expected = 50; expected >= 1; expected--)
        {
            current = history.Undo(current)!;
            Assert.AreEqual($"Snapshot {expected}", current.Name);
        }

        Assert.IsFalse(history.CanUndo);
        Assert.IsNull(history.Undo(current));
    }

    [TestMethod]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new UndoHistory(0));
    }
}
