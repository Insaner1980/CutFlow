using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed partial class ThumbnailServiceTests
{
    [TestMethod]
    public void TemporaryThumbnailCopy_DisposesManagedWinRtStreamAdapter()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Services",
            "ThumbnailService.cs"));
        var methodStart = source.IndexOf("private static async Task WriteTemporaryThumbnailAsync", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("public bool TryDeleteCachedThumbnail", methodStart, StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("using var source = encodedThumbnail.AsStreamForRead();", method);
        Assert.Contains("await CopyBoundedAsync(\n            source,", method.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CacheKey_IsDeterministicAndIncludesNormalizedPathMetadataAndRequestedSize()
    {
        var modified = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);
        var first = ThumbnailService.CreateCacheKey(@"C:\Media\Folder\..\CLIP.mp4", 100, modified, 256);
        var same = ThumbnailService.CreateCacheKey(@"c:\media\clip.mp4", 100, modified, 256);
        var differentSize = ThumbnailService.CreateCacheKey(@"c:\media\clip.mp4", 101, modified, 256);
        var differentLastWrite = ThumbnailService.CreateCacheKey(@"c:\media\clip.mp4", 100, modified.AddTicks(1), 256);
        var differentRequest = ThumbnailService.CreateCacheKey(@"c:\media\clip.mp4", 100, modified, 128);

        Assert.AreEqual(first, same);
        Assert.AreNotEqual(first, differentSize);
        Assert.AreNotEqual(first, differentLastWrite);
        Assert.AreNotEqual(first, differentRequest);
        Assert.AreEqual(64, first.Length);
        Assert.IsTrue(first.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [TestMethod]
    public void RequestedSize_IsNormalizedBeforeRequestCaptureAndCacheKeying()
    {
        var modified = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);
        var asset = new Models.ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = Models.ProjectAssetKind.Image,
            SourcePath = @"C:\Media\image.jpg",
            FileSize = 100,
            LastWriteUtc = modified
        };

        var defaultRequest = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
        var maximumRequest = ThumbnailService.CaptureRequest(asset, ThumbnailService.MaximumRequestedSize);

        foreach (var requestedSize in new[] { 0, -1, int.MinValue })
        {
            var request = ThumbnailService.CaptureRequest(asset, requestedSize);

            Assert.AreEqual(ThumbnailService.DefaultRequestedSize, request.RequestedSize);
            Assert.AreEqual(defaultRequest.CacheKey, request.CacheKey);
            Assert.AreEqual(
                defaultRequest.CacheKey,
                ThumbnailService.CreateCacheKey(asset.SourcePath, asset.FileSize, asset.LastWriteUtc, requestedSize));
        }

        foreach (var requestedSize in new[] { ThumbnailService.MaximumRequestedSize + 1, int.MaxValue })
        {
            var request = ThumbnailService.CaptureRequest(asset, requestedSize);

            Assert.AreEqual(ThumbnailService.MaximumRequestedSize, request.RequestedSize);
            Assert.AreEqual(maximumRequest.CacheKey, request.CacheKey);
            Assert.AreEqual(
                maximumRequest.CacheKey,
                ThumbnailService.CreateCacheKey(asset.SourcePath, asset.FileSize, asset.LastWriteUtc, requestedSize));
        }
    }

    [TestMethod]
    public void RelativeCachePath_IsProjectRelativeAndResolvesInsideProject()
    {
        using var project = new TemporaryProjectRoot();
        var key = new string('a', 64);
        var relative = ThumbnailService.CreateRelativeCachePath(key);
        var resolved = ThumbnailService.ResolveProjectCachePath(project.Path, relative);
        var json = System.Text.Json.JsonSerializer.Serialize(new Models.ProjectAsset { ThumbnailCachePath = relative });

        Assert.IsFalse(Path.IsPathRooted(relative));
        Assert.AreEqual($"cache/thumbnails/{key}.jpg", relative);
        Assert.AreEqual(Path.Combine(project.Path, "cache", "thumbnails", $"{key}.jpg"), resolved);
        Assert.Contains($"\"thumbnailCachePath\":\"cache/thumbnails/{key}.jpg\"", json);
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Path, "cache", "thumbnails")));
        Assert.ThrowsExactly<InvalidDataException>(() => ThumbnailService.ResolveProjectCachePath(project.Path, @"..\source.mp4"));
        Assert.ThrowsExactly<InvalidDataException>(() => ThumbnailService.ResolveProjectCachePath(project.Path, @"C:\outside.jpg"));
    }

    [TestMethod]
    public void CachePaths_RejectMalformedHashesAndNoncanonicalSeparators()
    {
        using var project = new TemporaryProjectRoot();
        var lowercaseKey = new string('a', 64);
        var uppercaseKey = new string('A', 64);

        foreach (var malformedKey in new[] { uppercaseKey, new string('a', 63), new string('a', 65), new string('g', 64) })
        {
            Assert.ThrowsExactly<ArgumentException>(() => ThumbnailService.CreateRelativeCachePath(malformedKey));
        }

        foreach (var malformedPath in new[]
                 {
                     $@"cache\thumbnails\{lowercaseKey}.jpg",
                     $"cache/thumbnails/{uppercaseKey}.jpg",
                     $"cache/thumbnails/{new string('g', 64)}.jpg",
                     $"cache/thumbnails/{lowercaseKey}.jpeg",
                     $"cache/thumbnails/nested/{lowercaseKey}.jpg"
                 })
        {
            Assert.ThrowsExactly<InvalidDataException>(() =>
                ThumbnailService.ResolveProjectCachePath(project.Path, malformedPath));
        }
    }

    [TestMethod]
    public async Task TryDeleteCachedThumbnail_LinkedCacheDirectoryRejectsWithoutDeletingTarget()
    {
        using var project = new TemporaryProjectRoot();
        var cacheDirectory = Directory.CreateDirectory(Path.Combine(project.Path, "cache"));
        var externalDirectory = Directory.CreateDirectory(Path.Combine(project.Path, "external"));
        var linkedThumbnailsPath = Path.Combine(cacheDirectory.FullName, "thumbnails");
        Directory.CreateSymbolicLink(linkedThumbnailsPath, externalDirectory.FullName);
        var relativePath = ThumbnailService.CreateRelativeCachePath(new string('a', 64));
        var externalPath = Path.Combine(externalDirectory.FullName, Path.GetFileName(relativePath));
        await File.WriteAllTextAsync(externalPath, "preserve external thumbnail", TestContext.CancellationToken);
        var asset = new Models.ProjectAsset { ThumbnailCachePath = relativePath };

        var service = new ThumbnailService(project.Path);

        Assert.ThrowsExactly<InvalidDataException>(() => service.TryDeleteCachedThumbnail(asset));
        Assert.AreEqual("preserve external thumbnail", await File.ReadAllTextAsync(externalPath, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CopyBoundedAsync_CopiesInChunksAndRejectsOversizedThumbnail()
    {
        var data = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
        await using var source = new MemoryStream(data);
        await using var destination = new MemoryStream();

        await ThumbnailService.CopyBoundedAsync(source, destination, 64, CancellationToken.None);
        Assert.AreSequenceEqual(data, destination.ToArray());

        await using var tooLarge = new MemoryStream(new byte[65]);
        await using var rejectedDestination = new MemoryStream();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            ThumbnailService.CopyBoundedAsync(tooLarge, rejectedDestination, 64, CancellationToken.None));
        Assert.AreEqual(0, tooLarge.Position);
        Assert.AreEqual(0, rejectedDestination.Length);
    }

    [TestMethod]
    public async Task GetOrCreateThumbnailAsync_DoesNotAcceptAnExistingCorruptJpeg()
    {
        using var project = new TemporaryProjectRoot();
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg");
        var sourceInfo = new FileInfo(sourcePath);
        var asset = new Models.ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = Models.ProjectAssetKind.Image,
            SourcePath = sourcePath,
            FileSize = checked((ulong)sourceInfo.Length),
            LastWriteUtc = sourceInfo.LastWriteTimeUtc
        };
        var request = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
        var relativePath = ThumbnailService.CreateRelativeCachePath(request.CacheKey);
        var cachePath = ThumbnailService.ResolveProjectCachePath(project.Path, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllBytesAsync(cachePath, [0xFF, 0xD8, 0xFF, 0xD9], TestContext.CancellationToken);
        var source = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);

        var result = await new ThumbnailService(project.Path).GetOrCreateThumbnailAsync(source, asset, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsTrue(await ThumbnailService.IsUsableCachedThumbnailAsync(result, CancellationToken.None));
    }

    [TestMethod]
    public async Task CachedThumbnailValidation_RejectsCorruptAndOversizedFilesAndReleasesHandles()
    {
        using var project = new TemporaryProjectRoot();
        var validPath = Path.Combine(project.Path, "valid.jpg");
        var corruptPath = Path.Combine(project.Path, "corrupt.jpg");
        var oversizedPath = Path.Combine(project.Path, "oversized.jpg");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), validPath);
        await File.WriteAllTextAsync(corruptPath, "not a jpeg", TestContext.CancellationToken);
        await using (var oversized = new FileStream(oversizedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            oversized.SetLength(ThumbnailService.MaximumCachedThumbnailBytes + 1);
        }

        Assert.IsTrue(await ThumbnailService.IsUsableCachedThumbnailAsync(validPath, CancellationToken.None));
        Assert.IsFalse(await ThumbnailService.IsUsableCachedThumbnailAsync(corruptPath, CancellationToken.None));
        Assert.IsFalse(await ThumbnailService.IsUsableCachedThumbnailAsync(oversizedPath, CancellationToken.None));

        foreach (var path in new[] { validPath, corruptPath, oversizedPath })
        {
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
    }

    [TestMethod]
    public async Task GetOrCreateThumbnailAsync_WhenSourceChangesInPlace_UsesNewKeyAndDeletesOldCache()
    {
        using var project = new TemporaryProjectRoot();
        var sourcePath = Path.Combine(project.Path, "source.jpg");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), sourcePath);
        var originalInfo = new FileInfo(sourcePath);
        var asset = new Models.ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = Models.ProjectAssetKind.Image,
            SourcePath = sourcePath,
            FileSize = checked((ulong)originalInfo.Length),
            LastWriteUtc = originalInfo.LastWriteTimeUtc
        };
        var service = new ThumbnailService(project.Path);
        var source = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
        var originalCachePath = await service.GetOrCreateThumbnailAsync(source, asset, cancellationToken: TestContext.CancellationToken);
        Assert.IsNotNull(originalCachePath);
        var originalRequest = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);

        await using (var stream = new FileStream(sourcePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            await stream.WriteAsync(new byte[] { 0 }, TestContext.CancellationToken);
        }
        File.SetLastWriteTimeUtc(sourcePath, originalInfo.LastWriteTimeUtc.AddMinutes(1));
        source = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);

        var refreshedCachePath = await service.GetOrCreateThumbnailAsync(source, asset, cancellationToken: TestContext.CancellationToken);
        var currentInfo = new FileInfo(sourcePath);
        var refreshedRequest = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);

        Assert.IsNotNull(refreshedCachePath);
        Assert.AreNotEqual(originalRequest.CacheKey, refreshedRequest.CacheKey);
        Assert.AreNotEqual(originalCachePath, refreshedCachePath);
        Assert.AreEqual(checked((ulong)currentInfo.Length), asset.FileSize);
        Assert.AreEqual(currentInfo.LastWriteTimeUtc, asset.LastWriteUtc.UtcDateTime);
        Assert.AreEqual(
            ThumbnailService.CreateRelativeCachePath(refreshedRequest.CacheKey),
            asset.ThumbnailCachePath);
        Assert.IsFalse(File.Exists(originalCachePath));
        Assert.IsTrue(File.Exists(refreshedCachePath));
    }

    [TestMethod]
    public async Task SourceMetadataRefresh_InvalidatesAnOlderInFlightRequest()
    {
        using var project = new TemporaryProjectRoot();
        var sourcePath = Path.Combine(project.Path, "source.jpg");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), sourcePath);
        var originalInfo = new FileInfo(sourcePath);
        var asset = new Models.ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = Models.ProjectAssetKind.Image,
            SourcePath = sourcePath,
            FileSize = checked((ulong)originalInfo.Length),
            LastWriteUtc = originalInfo.LastWriteTimeUtc
        };
        var service = new ThumbnailService(project.Path);
        var oldRequest = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
        var oldRelativePath = ThumbnailService.CreateRelativeCachePath(oldRequest.CacheKey);
        var oldFinalPath = ThumbnailService.ResolveProjectCachePath(project.Path, oldRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(oldFinalPath)!);
        var oldTemporaryPath = oldFinalPath + ".old.tmp";
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), oldTemporaryPath);

        await using (var stream = new FileStream(sourcePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            await stream.WriteAsync(new byte[] { 0 }, TestContext.CancellationToken);
        }
        File.SetLastWriteTimeUtc(sourcePath, originalInfo.LastWriteTimeUtc.AddMinutes(1));
        var source = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);

        Assert.IsNotNull(await service.GetOrCreateThumbnailAsync(source, asset, cancellationToken: TestContext.CancellationToken));
        Assert.IsFalse(service.TryCommitGeneratedThumbnail(asset, oldRequest, oldTemporaryPath, oldRelativePath, TestContext.CancellationToken));
        Assert.IsFalse(File.Exists(oldTemporaryPath));
        Assert.IsFalse(File.Exists(oldFinalPath));
        Assert.AreNotEqual(oldRequest.CacheKey, ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize).CacheKey);
    }

    [TestMethod]
    public void StaleRequestAfterRelink_DoesNotPromoteFileOrRestoreCachePath()
    {
        using var project = new TemporaryProjectRoot();
        var service = new ThumbnailService(project.Path);
        var asset = new Models.ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = Models.ProjectAssetKind.Video,
            SourcePath = @"C:\Media\old.mp4",
            FileSize = 100,
            LastWriteUtc = DateTimeOffset.UnixEpoch
        };
        var request = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
        var relativePath = ThumbnailService.CreateRelativeCachePath(request.CacheKey);
        var finalPath = ThumbnailService.ResolveProjectCachePath(project.Path, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var temporaryPath = finalPath + ".test.tmp";
        File.WriteAllBytes(temporaryPath, [0xFF, 0xD8, 0xFF, 0xD9]);

        asset.SourcePath = @"D:\Media\replacement.mp4";
        asset.FileSize = 200;
        asset.LastWriteUtc = DateTimeOffset.UnixEpoch.AddDays(1);
        asset.ThumbnailCachePath = string.Empty;

        Assert.IsFalse(service.TryCommitGeneratedThumbnail(asset, request, temporaryPath, relativePath, TestContext.CancellationToken));
        Assert.IsFalse(File.Exists(temporaryPath));
        Assert.IsFalse(File.Exists(finalPath));
        Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
    }

    [TestMethod]
    public void CanceledRequest_DoesNotPromoteFileOrAssignCachePath()
    {
        using var project = new TemporaryProjectRoot();
        var service = new ThumbnailService(project.Path);
        var asset = new Models.ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = Models.ProjectAssetKind.Image,
            SourcePath = @"C:\Media\image.png",
            FileSize = 100,
            LastWriteUtc = DateTimeOffset.UnixEpoch
        };
        var request = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
        var relativePath = ThumbnailService.CreateRelativeCachePath(request.CacheKey);
        var finalPath = ThumbnailService.ResolveProjectCachePath(project.Path, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var temporaryPath = finalPath + ".test.tmp";
        File.WriteAllBytes(temporaryPath, [0xFF, 0xD8, 0xFF, 0xD9]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() =>
            service.TryCommitGeneratedThumbnail(asset, request, temporaryPath, relativePath, cancellation.Token));
        Assert.ThrowsExactly<OperationCanceledException>(() =>
            ThumbnailService.TryCommitCacheHit(asset, request, relativePath, cancellation.Token));
        Assert.IsTrue(File.Exists(temporaryPath));
        Assert.IsFalse(File.Exists(finalPath));
        Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
    }

    [TestMethod]
    public void MediaAssetCard_CancellationInvalidatesTheOldGeneration()
    {
        var card = new Controls.MediaAssetCard(new Models.ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = Models.ProjectAssetKind.Image,
            SourcePath = @"C:\Media\image.png"
        });

        var first = card.BeginThumbnailRequest(CancellationToken.None);
        Assert.IsTrue(card.IsCurrentThumbnailRequest(first.Generation));

        card.CancelThumbnailRequest();

        Assert.IsTrue(first.Token.IsCancellationRequested);
        Assert.IsFalse(card.IsCurrentThumbnailRequest(first.Generation));
        Assert.IsFalse(card.ThumbnailRequested);

        var second = card.BeginThumbnailRequest(CancellationToken.None);
        Assert.AreNotEqual(first.Generation, second.Generation);
        Assert.IsFalse(card.IsCurrentThumbnailRequest(first.Generation));
        Assert.IsTrue(card.IsCurrentThumbnailRequest(second.Generation));
        card.CancelThumbnailRequest();
    }

    [TestMethod]
    public void RequestCapturedBeforeRelink_RejectsOldStoragePathAndNewAssetMetadata()
    {
        var asset = new Models.ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = Models.ProjectAssetKind.Image,
            SourcePath = @"C:\Media\old.png",
            FileSize = 100,
            LastWriteUtc = DateTimeOffset.UnixEpoch
        };
        var request = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);

        asset.SourcePath = @"D:\Media\replacement.png";
        asset.FileSize = 200;
        asset.LastWriteUtc = DateTimeOffset.UnixEpoch.AddDays(1);
        var replacementRequest = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);

        Assert.IsFalse(ThumbnailService.IsCurrentRequest(asset, request));
        Assert.IsFalse(ThumbnailService.IsStoragePathForRequest(request, asset.SourcePath));
        Assert.IsTrue(ThumbnailService.IsStoragePathForRequest(request, @"c:\media\old.png"));
        Assert.IsFalse(ThumbnailService.IsStoragePathForRequest(replacementRequest, @"C:\Media\old.png"));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CutFlow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed partial class TemporaryProjectRoot : IDisposable
    {
        public TemporaryProjectRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    public TestContext TestContext { get; set; }
}
