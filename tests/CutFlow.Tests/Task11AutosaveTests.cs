using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task11AutosaveTests
{
    [TestMethod]
    public async Task NotifyEdited_RapidRequestsCancelTheEarlierDebounce()
    {
        var delay = new ControlledDelay();
        var saver = new ControlledSaver();
        using var coordinator = new DebouncedSaveCoordinator(saver.SaveAsync, delay.WaitAsync);

        coordinator.NotifyEdited();
        coordinator.NotifyEdited();
        delay.CompleteLatest();

        await saver.WaitForCallsAsync(1);

        Assert.AreEqual(1, delay.CanceledCount);
        Assert.AreEqual(1, saver.Calls);
        Assert.AreEqual(DebouncedSaveState.Saved, coordinator.State);
    }

    [TestMethod]
    public async Task FlushAsync_WhenEditCommitsDuringSave_PersistsTheLatestRevisionWithoutConcurrency()
    {
        var saver = new ControlledSaver(blockFirstCall: true);
        using var coordinator = new DebouncedSaveCoordinator(saver.SaveAsync, _ => Task.CompletedTask);

        coordinator.NotifyEdited();
        await saver.WaitForCallsAsync(1);
        coordinator.NotifyEdited();
        saver.ReleaseFirstCall();

        await coordinator.FlushAsync();

        Assert.AreEqual(2, saver.Calls);
        Assert.AreEqual(1, saver.MaximumConcurrentCalls);
        Assert.AreEqual(DebouncedSaveState.Saved, coordinator.State);
    }

    [TestMethod]
    public async Task FlushAsync_AfterFailedDebouncedSave_RetriesAndMarksSaved()
    {
        var saver = new ControlledSaver(failFirstCall: true);
        using var coordinator = new DebouncedSaveCoordinator(saver.SaveAsync, _ => Task.CompletedTask);

        coordinator.NotifyEdited();
        await WaitUntilAsync(() => coordinator.State == DebouncedSaveState.SaveFailed);

        await coordinator.FlushAsync();

        Assert.AreEqual(2, saver.Calls);
        Assert.AreEqual(DebouncedSaveState.Saved, coordinator.State);
    }

    [TestMethod]
    public void Dispose_CancelsOnlyThePendingDebounce()
    {
        var delay = new ControlledDelay();
        var saver = new ControlledSaver();
        var coordinator = new DebouncedSaveCoordinator(saver.SaveAsync, delay.WaitAsync);

        coordinator.NotifyEdited();
        coordinator.Dispose();

        Assert.AreEqual(1, delay.CanceledCount);
        Assert.AreEqual(0, saver.Calls);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Yield();
        }

        Assert.IsTrue(condition());
    }

    private sealed class ControlledDelay
    {
        private readonly List<Waiter> _waiters = [];

        public int CanceledCount { get; private set; }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            var waiter = new Waiter();
            _waiters.Add(waiter);
            cancellationToken.Register(() =>
            {
                CanceledCount++;
                waiter.Completion.TrySetCanceled(cancellationToken);
            });
            return waiter.Completion.Task;
        }

        public void CompleteLatest() => _waiters[^1].Completion.TrySetResult();

        private sealed class Waiter
        {
            public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class ControlledSaver
    {
        private readonly bool _blockFirstCall;
        private readonly bool _failFirstCall;
        private readonly TaskCompletionSource _firstCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeCalls;

        public ControlledSaver(bool blockFirstCall = false, bool failFirstCall = false)
        {
            _blockFirstCall = blockFirstCall;
            _failFirstCall = failFirstCall;
        }

        public int Calls { get; private set; }
        public int MaximumConcurrentCalls { get; private set; }

        public async Task SaveAsync()
        {
            Calls++;
            MaximumConcurrentCalls = Math.Max(MaximumConcurrentCalls, ++_activeCalls);
            try
            {
                if (Calls == 1)
                {
                    _firstCallStarted.TrySetResult();
                    if (_blockFirstCall) await _releaseFirstCall.Task;
                    if (_failFirstCall) throw new IOException("save failed");
                }
            }
            finally
            {
                _activeCalls--;
            }
        }

        public async Task WaitForCallsAsync(int calls)
        {
            while (Calls < calls)
            {
                await _firstCallStarted.Task;
            }
        }

        public void ReleaseFirstCall() => _releaseFirstCall.TrySetResult();
    }
}
