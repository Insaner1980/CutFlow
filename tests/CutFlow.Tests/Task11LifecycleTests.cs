using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task11LifecycleTests
{
    [TestMethod]
    public async Task CloseWaitsForTheSingleInitializationTaskAndBlocksItsUiContinuation()
    {
        var releaseInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycle = new MainWindowLifecycle();
        var initializationCalls = 0;
        var initialization = lifecycle.StartInitialization(async () =>
        {
            initializationCalls++;
            await releaseInitialization.Task;
        });

        Assert.IsTrue(lifecycle.TryBeginClosing());
        var closeReady = lifecycle.WaitForInitializationBeforeCloseAsync(initialization);

        Assert.IsFalse(closeReady.IsCompleted);
        Assert.IsFalse(lifecycle.CanContinueInitialization);
        releaseInitialization.SetResult();
        Assert.IsTrue(await closeReady);
        Assert.AreEqual(1, initializationCalls);
        Assert.ThrowsExactly<InvalidOperationException>(() => lifecycle.StartInitialization(() => Task.CompletedTask));
    }

    [TestMethod]
    public async Task ClosedWindowStopsPostAwaitCloseContinuation()
    {
        var lifecycle = new MainWindowLifecycle();
        var initialization = lifecycle.StartInitialization(() => Task.CompletedTask);

        Assert.IsTrue(lifecycle.TryBeginClosing());
        lifecycle.MarkClosed();

        Assert.IsFalse(await lifecycle.WaitForInitializationBeforeCloseAsync(initialization));
        Assert.IsFalse(lifecycle.CanContinueInitialization);
    }

    [TestMethod]
    public void ApprovedCloseUsesTheWindowCloseCallback()
    {
        var lifecycle = new MainWindowLifecycle();
        var closeCalls = 0;

        Assert.IsFalse(lifecycle.TryCommitClose(() => closeCalls++));
        lifecycle.ApproveClose();

        Assert.IsTrue(lifecycle.TryCommitClose(() => closeCalls++));
        Assert.AreEqual(1, closeCalls);
    }
}
