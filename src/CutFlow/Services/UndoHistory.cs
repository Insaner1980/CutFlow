using System.Text.Json;
using CutFlow.Models;

namespace CutFlow.Services;

public sealed class UndoHistory
{
    private readonly int _capacity;
    private readonly List<byte[]> _undoSnapshots = [];
    private readonly List<byte[]> _redoSnapshots = [];

    public UndoHistory(int capacity = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
    }

    public bool CanUndo => _undoSnapshots.Count > 0;

    public bool CanRedo => _redoSnapshots.Count > 0;

    public void Record(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);

        PushSnapshot(_undoSnapshots, Serialize(project));
        _redoSnapshots.Clear();
    }

    public ProjectDocument? Undo(ProjectDocument currentProject)
    {
        ArgumentNullException.ThrowIfNull(currentProject);

        if (!CanUndo)
        {
            return null;
        }

        var currentSnapshot = Serialize(currentProject);
        var restored = Deserialize(_undoSnapshots[^1]);
        PushSnapshot(_redoSnapshots, currentSnapshot);
        PopSnapshot(_undoSnapshots);
        return restored;
    }

    public ProjectDocument? Redo(ProjectDocument currentProject)
    {
        ArgumentNullException.ThrowIfNull(currentProject);

        if (!CanRedo)
        {
            return null;
        }

        var currentSnapshot = Serialize(currentProject);
        var restored = Deserialize(_redoSnapshots[^1]);
        PushSnapshot(_undoSnapshots, currentSnapshot);
        PopSnapshot(_redoSnapshots);
        return restored;
    }

    private void PushSnapshot(List<byte[]> snapshots, byte[] snapshot)
    {
        if (snapshots.Count == _capacity)
        {
            snapshots.RemoveAt(0);
        }

        snapshots.Add(snapshot);
    }

    private static void PopSnapshot(List<byte[]> snapshots)
    {
        snapshots.RemoveAt(snapshots.Count - 1);
    }

    private static byte[] Serialize(ProjectDocument project) => JsonSerializer.SerializeToUtf8Bytes(project);

    private static ProjectDocument Deserialize(byte[] snapshot)
    {
        var project = JsonSerializer.Deserialize<ProjectDocument>(snapshot)
            ?? throw new InvalidOperationException("A project history snapshot could not be deserialized.");
        ProjectService.NormalizeSnapshot(project);
        return project;
    }
}
