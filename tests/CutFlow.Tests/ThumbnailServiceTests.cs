using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class ThumbnailServiceTests
{
    [TestMethod]
    public void CacheKey_IsDeterministicAndIncludesNormalizedPathMetadataAndRequestedSize()
    {
        var modified = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);
        var first = ThumbnailService.CreateCacheKey(@"C:\Media\Folder\..\CLIP.mp4", 100, modified, 256);
        var same = ThumbnailService.CreateCacheKey(@"c:\media\clip.mp4", 100, modified, 256);
        var differentSize = ThumbnailService.CreateCacheKey(@"c:\media\clip.mp4", 101, modified, 256);
        var differentRequest = ThumbnailService.CreateCacheKey(@"c:\media\clip.mp4", 100, modified, 128);

        Assert.AreEqual(first, same);
        Assert.AreNotEqual(first, differentSize);
        Assert.AreNotEqual(first, differentRequest);
        Assert.AreEqual(64, first.Length);
    }

    [TestMethod]
    public void RelativeCachePath_IsProjectRelativeAndResolvesInsideProject()
    {
        var key = new string('a', 64);
        var relative = ThumbnailService.CreateRelativeCachePath(key);
        var projectRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var resolved = ThumbnailService.ResolveProjectCachePath(projectRoot, relative);

        Assert.IsFalse(Path.IsPathRooted(relative));
        StringAssert.StartsWith(relative.Replace('/', '\\'), @"cache\thumbnails\");
        StringAssert.EndsWith(relative, ".jpg");
        StringAssert.StartsWith(resolved, Path.GetFullPath(projectRoot), StringComparison.OrdinalIgnoreCase);
        Assert.ThrowsException<InvalidDataException>(() => ThumbnailService.ResolveProjectCachePath(projectRoot, @"..\source.mp4"));
        Assert.ThrowsException<InvalidDataException>(() => ThumbnailService.ResolveProjectCachePath(projectRoot, @"C:\outside.jpg"));
    }

    [TestMethod]
    public async Task CopyBoundedAsync_CopiesInChunksAndRejectsOversizedThumbnail()
    {
        var data = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
        await using var source = new MemoryStream(data);
        await using var destination = new MemoryStream();

        await ThumbnailService.CopyBoundedAsync(source, destination, 64, CancellationToken.None);
        CollectionAssert.AreEqual(data, destination.ToArray());

        await using var tooLarge = new MemoryStream(new byte[65]);
        await using var rejectedDestination = new MemoryStream();
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() =>
            ThumbnailService.CopyBoundedAsync(tooLarge, rejectedDestination, 64, CancellationToken.None));
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

        Assert.IsFalse(service.TryCommitGeneratedThumbnail(asset, request, temporaryPath, relativePath));
        Assert.IsFalse(File.Exists(temporaryPath));
        Assert.IsFalse(File.Exists(finalPath));
        Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
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

    private sealed class TemporaryProjectRoot : IDisposable
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
}
