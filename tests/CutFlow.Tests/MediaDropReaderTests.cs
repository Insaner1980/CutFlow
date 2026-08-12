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
    public async Task ReadAsync_ExpectedFileAndNativeFailuresReturnVisibleFailure()
    {
        Exception[] expectedFailures =
        [
            new IOException("file failed"),
            new UnauthorizedAccessException("access failed"),
            new InvalidDataException("data failed"),
            new NotSupportedException("codec failed"),
            new ArgumentException("argument failed"),
            new OverflowException("metadata overflowed"),
            new COMException("native failed")
        ];

        foreach (var expected in expectedFailures)
        {
            var result = await MediaDropReader.ReadAsync(
                () => Task.FromException<IReadOnlyList<IStorageItem>>(expected),
                CancellationToken.None);

            Assert.IsFalse(result.IsSuccess, expected.GetType().Name);
            Assert.AreSame(expected, result.Exception);
            StringAssert.Contains(result.ErrorMessage, "dropped");
        }
    }

    [TestMethod]
    public async Task ReadAsync_CriticalAndUnexpectedFailuresPropagate()
    {
        Exception[] failures =
        [
            new InvalidOperationException("programming defect"),
            new OutOfMemoryException("memory exhausted"),
            new StackOverflowException("stack exhausted")
        ];

        foreach (var expected in failures)
        {
            Exception? observed = null;
            try
            {
                await MediaDropReader.ReadAsync(
                    () => Task.FromException<IReadOnlyList<IStorageItem>>(expected),
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                observed = exception;
            }

            Assert.AreSame(expected, observed, expected.GetType().Name);
        }
    }

    [TestMethod]
    public async Task ReadAsync_WhenDropContainsFileAndFolder_PreservesPerItemFolderFailure()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(testRoot, "clip.mp4");
        Directory.CreateDirectory(testRoot);
        File.WriteAllText(filePath, "fixture");
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            var folder = await StorageFolder.GetFolderFromPathAsync(testRoot);

            var result = await MediaDropReader.ReadAsync(
                () => Task.FromResult<IReadOnlyList<IStorageItem>>([file, folder]),
                CancellationToken.None);

            Assert.IsTrue(result.IsSuccess);
            Assert.HasCount(1, result.Files);
            Assert.HasCount(1, result.RejectedItems);
            Assert.AreEqual(folder.Name, result.RejectedItems[0].ItemName);
            StringAssert.Contains(result.RejectedItems[0].Message, "local media files");
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
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
