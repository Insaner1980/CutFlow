using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class EditorImportGateTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_HoldsGateThroughCommitSoConcurrentDuplicateImportsCommitOnce()
    {
        var gate = new EditorImportGate();
        var importedPaths = new List<string>();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparationCount = 0;
        var commitCount = 0;

        async Task<bool> PrepareAsync(CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref preparationCount);
            if (call == 1)
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }

            return !importedPaths.Contains(@"C:\Media\clip.mp4", StringComparer.OrdinalIgnoreCase);
        }

        void Commit(bool shouldImport)
        {
            if (!shouldImport)
            {
                return;
            }

            importedPaths.Add(@"C:\Media\clip.mp4");
            commitCount++;
        }

        var first = gate.ExecuteAsync(PrepareAsync, Commit, CancellationToken.None);
        await firstEntered.Task;
        var second = gate.ExecuteAsync(PrepareAsync, Commit, CancellationToken.None);
        await Task.Yield();

        Assert.AreEqual(1, Volatile.Read(ref preparationCount));
        Assert.IsFalse(second.IsCompleted);

        releaseFirst.SetResult();
        await Task.WhenAll(first, second);

        Assert.AreEqual(2, preparationCount);
        Assert.AreEqual(1, commitCount);
        Assert.HasCount(1, importedPaths);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExportWaitsForPreparedImportAndThenProtectsItsSource()
    {
        var gate = new EditorImportGate();
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePreparation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceReferenced = false;
        var moved = false;
        var import = gate.ExecuteAsync(async token =>
        {
            prepared.SetResult();
            await releasePreparation.Task.WaitAsync(token);
            return true;
        }, _ => sourceReferenced = true, TestContext.CancellationToken);
        await prepared.Task;

        var export = gate.ExecuteAsync(_ => CutFlow.Services.ExportService.CommitAsync(
            "staging", "destination", (_, _) => moved = true, () => !sourceReferenced),
            TestContext.CancellationToken);
        Assert.IsFalse(export.IsCompleted);
        releasePreparation.SetResult();
        await import;

        Assert.IsFalse(await export);
        Assert.IsFalse(moved);
    }

    [TestMethod]
    public async Task ExecuteAsync_ImportPreparesOnlyAfterInFlightExportReplacement()
    {
        var gate = new EditorImportGate();
        var moving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMove = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contents = "old source";
        var preparedContents = string.Empty;
        var export = gate.ExecuteAsync(async token =>
        {
            moving.SetResult();
            await releaseMove.Task.WaitAsync(token);
            contents = "exported source";
            return true;
        }, TestContext.CancellationToken);
        await moving.Task;

        var import = gate.ExecuteAsync(_ => Task.FromResult(contents),
            value => preparedContents = value, TestContext.CancellationToken);
        Assert.IsFalse(import.IsCompleted);
        releaseMove.SetResult();
        await Task.WhenAll(export, import);
        Assert.AreEqual("exported source", preparedContents);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenLifetimeEndsDuringPreparation_DoesNotCommit()
    {
        var gate = new EditorImportGate();
        using var lifetime = new CancellationTokenSource();
        var committed = false;

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => gate.ExecuteAsync(
            _ =>
            {
                lifetime.Cancel();
                return Task.FromResult(42);
            },
            _ => committed = true,
            lifetime.Token));

        Assert.IsFalse(committed);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenQueuedWaitIsCanceled_DoesNotReleaseUnacquiredGate()
    {
        var gate = new EditorImportGate();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = gate.ExecuteAsync(
            async cancellationToken =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
                return true;
            },
            _ => { },
            CancellationToken.None);
        await firstEntered.Task;

        using var queuedCancellation = new CancellationTokenSource();
        var canceled = gate.ExecuteAsync(
            _ => Task.FromResult(true),
            _ => { },
            queuedCancellation.Token);
        queuedCancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => canceled);

        var third = gate.ExecuteAsync(
            _ =>
            {
                thirdEntered.SetResult();
                return Task.FromResult(true);
            },
            _ => { },
            CancellationToken.None);

        Assert.IsFalse(thirdEntered.Task.IsCompleted);
        releaseFirst.SetResult();
        await Task.WhenAll(first, third);
        Assert.IsTrue(thirdEntered.Task.IsCompletedSuccessfully);
    }
}
