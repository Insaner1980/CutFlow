using System.Text.Json;
using CutFlow.Models;

namespace CutFlow.Services;

public sealed class UndoHistory
{
    private readonly int _capacity;
    private readonly List<string> _undoSnapshots = [];
    private readonly List<string> _redoSnapshots = [];

    public UndoHistory(int capacity = 50)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

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

        PushSnapshot(_redoSnapshots, Serialize(currentProject));
        return Deserialize(PopSnapshot(_undoSnapshots));
    }

    public ProjectDocument? Redo(ProjectDocument currentProject)
    {
        ArgumentNullException.ThrowIfNull(currentProject);

        if (!CanRedo)
        {
            return null;
        }

        PushSnapshot(_undoSnapshots, Serialize(currentProject));
        return Deserialize(PopSnapshot(_redoSnapshots));
    }

    private void PushSnapshot(List<string> snapshots, string snapshot)
    {
        if (snapshots.Count == _capacity)
        {
            snapshots.RemoveAt(0);
        }

        snapshots.Add(snapshot);
    }

    private static string PopSnapshot(List<string> snapshots)
    {
        var index = snapshots.Count - 1;
        var snapshot = snapshots[index];
        snapshots.RemoveAt(index);
        return snapshot;
    }

    private static string Serialize(ProjectDocument project) => JsonSerializer.Serialize(project);

    private static ProjectDocument Deserialize(string snapshot) =>
        JsonSerializer.Deserialize<ProjectDocument>(snapshot)
        ?? throw new InvalidOperationException("A project history snapshot could not be deserialized.");
}
