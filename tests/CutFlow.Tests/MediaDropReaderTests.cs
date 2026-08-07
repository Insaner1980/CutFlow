using System.Runtime.InteropServices;
using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Storage;

namespace CutFlow.Tests;

[TestClass]
public sealed class MediaDropReaderTests
{
    [TestMethod]
    public async Task ReadAsync_WhenStorageItemReadFails_ReturnsVisibleFailure()
    {
        var result = await MediaDropReader.ReadAsync(
            () => Task.FromException<IReadOnlyList<IStorageItem>>(new COMException("drop failed")),
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.HasCount(0, result.Files);
        StringAssert.Contains(result.ErrorMessage, "dropped");
    }

    [TestMethod]
    public async Task ReadAsync_WhenLifetimeEndsAfterRead_ThrowsCancellation()
    {
        using var lifetime = new CancellationTokenSource();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => MediaDropReader.ReadAsync(
            () =>
            {
                lifetime.Cancel();
                return Task.FromResult<IReadOnlyList<IStorageItem>>([]);
            },
            lifetime.Token));
    }
}
