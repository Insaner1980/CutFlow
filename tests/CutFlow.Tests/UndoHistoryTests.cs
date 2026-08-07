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
        initial.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "Before" });
        history.Record(initial);

        var changed = ProjectDocument.CreateNew("Changed", DateTimeOffset.UnixEpoch);
        changed.Id = initial.Id;
        changed.TextItems.Add(new TextTimelineItem { Id = initial.TextItems[0].Id, Text = "After" });

        var undone = history.Undo(changed);

        Assert.IsNotNull(undone);
        Assert.AreEqual("Initial", undone.Name);
        Assert.AreEqual("Before", undone.TextItems[0].Text);
        undone.Name = "Mutated returned value";
        Assert.IsTrue(history.CanRedo);

        var redone = history.Redo(undone);

        Assert.IsNotNull(redone);
        Assert.AreEqual("Changed", redone.Name);
        Assert.AreEqual("After", redone.TextItems[0].Text);
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
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new UndoHistory(0));
    }
}
