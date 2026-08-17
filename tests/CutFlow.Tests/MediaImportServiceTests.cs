using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class MediaImportServiceTests
{
    [TestMethod]
    [DataRow(@"C:\Media\clip.MP4", ProjectAssetKind.Video)]
    [DataRow(@"C:\Media\still.png", ProjectAssetKind.Image)]
    [DataRow(@"C:\Media\still.JPEG", ProjectAssetKind.Image)]
    [DataRow(@"C:\Media\song.mp3", ProjectAssetKind.Audio)]
    [DataRow(@"C:\Media\song.WAV", ProjectAssetKind.Audio)]
    public void TryGetKind_RecognizesSupportedExtensionsCaseInsensitively(string path, ProjectAssetKind expected)
    {
        Assert.IsTrue(MediaImportService.TryGetKind(path, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(@"C:\Media\clip.mov")]
    [DataRow(@"C:\Media\clip.mkv")]
    [DataRow(@"C:\Media\still.webp")]
    [DataRow(@"C:\Media\song.m4a")]
    [DataRow(@"C:\Media\notes.txt")]
    [DataRow(@"C:\Media\no-extension")]
    public void TryGetKind_RejectsUnsupportedExtensions(string path)
    {
        Assert.IsFalse(MediaImportService.TryGetKind(path, out _));
    }

    [TestMethod]
    public void NormalizePathAndDedupe_UseExactFullPathIgnoringCase()
    {
        var project = new ProjectDocument();
        project.Assets.Add(new ProjectAsset { SourcePath = @"C:\Media\Folder\..\CLIP.mp4" });

        Assert.AreEqual(@"C:\Media\clip.mp4", MediaImportService.NormalizePath(@"c:\media\.\clip.mp4"), ignoreCase: true);
        Assert.IsTrue(MediaImportService.ContainsSourcePath(project, @"c:\media\clip.mp4"));
        Assert.IsFalse(MediaImportService.ContainsSourcePath(project, @"c:\media\clip-copy.mp4"));
    }

    [TestMethod]
    public void RevalidateDuplicateResults_UsesCanonicalCaseInsensitivePathsAtCommit()
    {
        var project = new ProjectDocument
        {
            Assets =
            [
                new ProjectAsset
                {
                    Id = Guid.NewGuid(),
                    SourcePath = @"C:\Media\Folder\..\CLIP.mp4"
                }
            ]
        };
        var unique = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"D:\Media\song.wav" };
        var results = new[]
        {
            ImportResult.Success(new ProjectAsset
            {
                Id = Guid.NewGuid(),
                SourcePath = @"c:/media/./clip.mp4/"
            }),
            ImportResult.Success(unique),
            ImportResult.Success(new ProjectAsset
            {
                Id = Guid.NewGuid(),
                SourcePath = @"d:\MEDIA\folder\..\song.wav"
            })
        };

        var revalidated = MediaImportService.RevalidateDuplicateResults(project, results);

        Assert.IsTrue(revalidated[0].IsDuplicate);
        Assert.AreSame(unique, revalidated[1].Asset);
        Assert.IsTrue(revalidated[2].IsDuplicate);
    }

    [TestMethod]
    public void MergeImportResults_PreservesInputOrderAcrossPerFilePreflightFailures()
    {
        var first = ImportResult.Success(new ProjectAsset { FileName = "first.mp4" });
        var unsupported = ImportResult.Failure("notes.txt", "Supported formats are MP4, PNG, JPEG, MP3, and WAV.");
        var last = ImportResult.Success(new ProjectAsset { FileName = "last.jpg" });

        var merged = MediaImportService.MergeImportResults(
            [null, unsupported, null],
            [first, last]);

        Assert.AreSequenceEqual(
            expected, merged.Select(result => result.FileName).ToArray());
        Assert.AreSame(first.Asset, merged[0].Asset);
        Assert.AreSame(last.Asset, merged[2].Asset);
    }

    [TestMethod]
    public void IsPathWithinDirectory_RejectsOnlyTheManagedDirectoryAndItsDescendants()
    {
        var projectsRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"), "Projects");

        Assert.IsTrue(MediaImportService.IsPathWithinDirectory(
            Path.Combine(projectsRoot, Guid.NewGuid().ToString("D"), "cache", "source.png"),
            Path.Combine(projectsRoot, ".")));
        Assert.IsTrue(MediaImportService.IsPathWithinDirectory(projectsRoot, projectsRoot));
        Assert.IsFalse(MediaImportService.IsPathWithinDirectory(
            Path.Combine(projectsRoot + "-other", "source.png"),
            projectsRoot));
    }

    [TestMethod]
    public void TryResolveLocalSourcePath_RequiresExistingFullyQualifiedNonDevicePath()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(testRoot, "source.png");
        Directory.CreateDirectory(testRoot);
        File.WriteAllText(sourcePath, "source bytes");
        try
        {
            Assert.IsTrue(MediaImportService.TryResolveLocalSourcePath(sourcePath, out var normalized, out var error));
            Assert.AreEqual(Path.GetFullPath(sourcePath), normalized);
            Assert.IsNull(error);

            Assert.IsFalse(MediaImportService.TryResolveLocalSourcePath("source.png", out _, out error));
            Assert.Contains("fully qualified", error!);

            Assert.IsFalse(MediaImportService.TryResolveLocalSourcePath(@"\\?\" + sourcePath, out _, out error));
            Assert.Contains("device paths", error!);

            File.Delete(sourcePath);
            Assert.IsFalse(MediaImportService.TryResolveLocalSourcePath(sourcePath, out _, out error));
            Assert.Contains("local path", error!);
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
    public async Task ImportAsync_WhenOneSelectedPathDisappears_RejectsOnlyThatFileWithActionableDetail()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var missingPath = Path.Combine(testRoot, "missing.jpg");
        var availablePath = Path.Combine(testRoot, "available.jpg");
        Directory.CreateDirectory(testRoot);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), missingPath);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), availablePath);
        try
        {
            var missing = await Windows.Storage.StorageFile.GetFileFromPathAsync(missingPath);
            var available = await Windows.Storage.StorageFile.GetFileFromPathAsync(availablePath);
            File.Delete(missingPath);
            var project = new ProjectDocument();

            var results = await new MediaImportService().ImportAsync(
                [missing, available],
                project,
                Path.Combine(testRoot, "Projects"), TestContext.CancellationToken);

            Assert.HasCount(2, results);
            Assert.IsFalse(results[0].IsSuccess);
            Assert.Contains("local path", results[0].ErrorMessage!);
            Assert.IsTrue(results[1].IsSuccess, results[1].ErrorMessage);
            Assert.HasCount(0, project.Assets);
            var asset = results[1].Asset!;
            Assert.AreNotEqual(Guid.Empty, asset.Id);
            Assert.AreEqual(Path.GetFullPath(availablePath), asset.SourcePath);
            Assert.AreEqual("available.jpg", asset.FileName);
            Assert.AreEqual(ProjectAssetKind.Image, asset.Kind);
            Assert.AreEqual(MediaImportService.DefaultImageDurationMilliseconds, asset.DurationMilliseconds);
            Assert.IsGreaterThan(0, asset.Width);
            Assert.IsGreaterThan(0, asset.Height);
            Assert.AreEqual(checked((ulong)new FileInfo(availablePath).Length), asset.FileSize);
            Assert.AreEqual(File.GetLastWriteTimeUtc(availablePath), asset.LastWriteUtc);
            Assert.AreEqual(TimeSpan.Zero, asset.LastWriteUtc.Offset);
            Assert.IsFalse(asset.IsMissing);
            Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
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
    public async Task ImportAsync_UsesExifOrientedImageDimensions()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(testRoot, "rotated.jpg");
        Directory.CreateDirectory(testRoot);
        try
        {
            var sourceBytes = await File.ReadAllBytesAsync(
                Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), TestContext.CancellationToken);
            var exifOrientationSegment = new byte[]
            {
                0xFF, 0xE1, 0x00, 0x22,
                (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
                (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
                0x01, 0x00,
                0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            };
            var orientedBytes = new byte[sourceBytes.Length + exifOrientationSegment.Length];
            Buffer.BlockCopy(sourceBytes, 0, orientedBytes, 0, 2);
            Buffer.BlockCopy(exifOrientationSegment, 0, orientedBytes, 2, exifOrientationSegment.Length);
            Buffer.BlockCopy(sourceBytes, 2, orientedBytes, 2 + exifOrientationSegment.Length, sourceBytes.Length - 2);
            await File.WriteAllBytesAsync(sourcePath, orientedBytes, TestContext.CancellationToken);

            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
            using (var stream = await file.OpenReadAsync())
            {
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                Assert.AreEqual(320u, decoder.PixelWidth);
                Assert.AreEqual(240u, decoder.PixelHeight);
                Assert.AreEqual(240u, decoder.OrientedPixelWidth);
                Assert.AreEqual(320u, decoder.OrientedPixelHeight);
            }

            var results = await new MediaImportService().ImportAsync(
                [file],
                new ProjectDocument(),
                Path.Combine(testRoot, "Projects"), TestContext.CancellationToken);

            Assert.HasCount(1, results);
            Assert.IsTrue(results[0].IsSuccess, results[0].ErrorMessage);
            var asset = results[0].Asset!;
            Assert.AreEqual(240, asset.Width);
            Assert.AreEqual(320, asset.Height);
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
    public async Task ImportAsync_IsolatesUnsupportedAndDuplicateFilesWithoutReorderingTheBatch()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var firstPath = Path.Combine(testRoot, "first.jpg");
        var unsupportedPath = Path.Combine(testRoot, "notes.txt");
        var duplicatePath = Path.Combine(testRoot, "duplicate.jpg");
        var lastPath = Path.Combine(testRoot, "last.jpg");
        Directory.CreateDirectory(testRoot);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), firstPath);
        File.WriteAllText(unsupportedPath, "not media");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), duplicatePath);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), lastPath);
        try
        {
            var files = new[]
            {
                await Windows.Storage.StorageFile.GetFileFromPathAsync(firstPath),
                await Windows.Storage.StorageFile.GetFileFromPathAsync(unsupportedPath),
                await Windows.Storage.StorageFile.GetFileFromPathAsync(duplicatePath),
                await Windows.Storage.StorageFile.GetFileFromPathAsync(lastPath)
            };
            var project = new ProjectDocument
            {
                Assets = [new ProjectAsset { SourcePath = duplicatePath, FileName = "duplicate.jpg" }]
            };

            var results = await new MediaImportService().ImportAsync(
                files,
                project,
                Path.Combine(testRoot, "Projects"), TestContext.CancellationToken);

            string[] expectedFileNames = ["first.jpg", "notes.txt", "duplicate.jpg", "last.jpg"];
            Assert.AreSequenceEqual(expectedFileNames, results.Select(result => result.FileName).ToArray());
            Assert.IsTrue(results[0].IsSuccess, results[0].ErrorMessage);
            Assert.IsFalse(results[1].IsSuccess);
            Assert.Contains("Supported formats", results[1].ErrorMessage!);
            Assert.IsTrue(results[2].IsDuplicate);
            Assert.Contains("duplicate.jpg", results[2].ErrorMessage!);
            Assert.IsTrue(results[3].IsSuccess, results[3].ErrorMessage);
            Assert.HasCount(1, project.Assets);
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
    [DataRow(ProjectAssetKind.Video, "valid-audio.wav", "replacement.mp4")]
    [DataRow(ProjectAssetKind.Image, "valid-video.mp4", "replacement.jpg")]
    [DataRow(ProjectAssetKind.Image, "valid-video.mp4", "replacement.png")]
    [DataRow(ProjectAssetKind.Audio, "valid-video.mp4", "replacement.wav")]
    [DataRow(ProjectAssetKind.Audio, "valid-video.mp4", "replacement.mp3")]
    public async Task RelinkAsync_MisleadingExtensionCannotReplaceDifferentNativeKindAndLeavesAssetUntouched(
        ProjectAssetKind existingKind,
        string sourceFixture,
        string replacementFileName)
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var replacementPath = Path.Combine(testRoot, replacementFileName);
        Directory.CreateDirectory(testRoot);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", sourceFixture), replacementPath);
        try
        {
            var replacement = await Windows.Storage.StorageFile.GetFileFromPathAsync(replacementPath);
            var existing = new ProjectAsset
            {
                Id = Guid.NewGuid(),
                Kind = existingKind,
                SourcePath = @"C:\Media\original.mp4",
                FileName = "original.mp4",
                DurationMilliseconds = 4_000,
                Width = 1920,
                Height = 1080,
                FileSize = 123_456,
                LastWriteUtc = DateTimeOffset.UnixEpoch.AddDays(1),
                ThumbnailCachePath = @"cache\thumbnails\original.jpg",
                IsMissing = true
            };
            var project = new ProjectDocument { Assets = [existing] };

            var result = await new MediaImportService().RelinkAsync(
                replacement,
                existing,
                project,
                Path.Combine(testRoot, "Projects"), TestContext.CancellationToken);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(existingKind, existing.Kind);
            Assert.AreEqual(@"C:\Media\original.mp4", existing.SourcePath);
            Assert.AreEqual("original.mp4", existing.FileName);
            Assert.AreEqual(4_000, existing.DurationMilliseconds);
            Assert.AreEqual(1920, existing.Width);
            Assert.AreEqual(1080, existing.Height);
            Assert.AreEqual(123_456UL, existing.FileSize);
            Assert.AreEqual(DateTimeOffset.UnixEpoch.AddDays(1), existing.LastWriteUtc);
            Assert.AreEqual(@"cache\thumbnails\original.jpg", existing.ThumbnailCachePath);
            Assert.IsTrue(existing.IsMissing);
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
    [DataRow(ProjectAssetKind.Video, "valid-video.mp4")]
    [DataRow(ProjectAssetKind.Image, "valid-image.jpg")]
    [DataRow(ProjectAssetKind.Audio, "valid-audio.wav")]
    public async Task RelinkAsync_MatchingExtensionAndNativeKindUpdatesTheSameAsset(
        ProjectAssetKind kind,
        string sourceFixture)
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var replacementPath = Path.Combine(testRoot, sourceFixture);
        Directory.CreateDirectory(testRoot);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", sourceFixture), replacementPath);
        try
        {
            var replacement = await Windows.Storage.StorageFile.GetFileFromPathAsync(replacementPath);
            var existing = new ProjectAsset
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                SourcePath = @"C:\Media\original.mp4",
                FileName = "original.mp4",
                ThumbnailCachePath = @"cache\thumbnails\original.jpg",
                IsMissing = true
            };
            var project = new ProjectDocument { Assets = [existing] };

            var result = await new MediaImportService().RelinkAsync(
                replacement,
                existing,
                project,
                Path.Combine(testRoot, "Projects"), TestContext.CancellationToken);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreSame(existing, result.Asset);
            Assert.AreEqual(kind, existing.Kind);
            Assert.AreEqual(replacementPath, existing.SourcePath);
            Assert.AreEqual(sourceFixture, existing.FileName);
            Assert.IsGreaterThanOrEqualTo(ProjectDocument.MinimumItemDurationMilliseconds, existing.DurationMilliseconds);
            if (kind == ProjectAssetKind.Audio)
            {
                Assert.AreEqual(0, existing.Width);
                Assert.AreEqual(0, existing.Height);
            }
            else
            {
                Assert.IsGreaterThan(0, existing.Width);
                Assert.IsGreaterThan(0, existing.Height);
            }

            Assert.AreEqual(string.Empty, existing.ThumbnailCachePath);
            Assert.IsFalse(existing.IsMissing);
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
    public async Task ImportAsync_PreCanceledBatchStopsWithoutReturningPartialResults()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new MediaImportService().ImportAsync(
            [null!],
            new ProjectDocument(),
            Path.Combine(Path.GetTempPath(), "CutFlow.Tests", "Projects"),
            cancellation.Token));
    }

    [TestMethod]
    public async Task ImportAndRelink_RejectManagedProjectSourcesWithoutMutatingThem()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var projectsRoot = Path.Combine(testRoot, "Projects");
        var sourcePath = Path.Combine(projectsRoot, Guid.NewGuid().ToString("D"), "cache", "source.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "source bytes", TestContext.CancellationToken);
        try
        {
            var source = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
            var service = new MediaImportService();
            var project = new ProjectDocument();

            var imported = await service.ImportAsync([source], project, projectsRoot, TestContext.CancellationToken);

            Assert.IsFalse(imported.Single().IsSuccess);
            Assert.Contains("managed project folders", imported.Single().ErrorMessage!);
            Assert.HasCount(0, project.Assets);

            var existing = new ProjectAsset
            {
                Id = Guid.NewGuid(),
                Kind = ProjectAssetKind.Image,
                SourcePath = @"C:\Media\old.png",
                ThumbnailCachePath = @"cache\thumbnails\old.jpg"
            };
            project.Assets.Add(existing);

            var relinked = await service.RelinkAsync(source, existing, project, projectsRoot, TestContext.CancellationToken);

            Assert.IsFalse(relinked.IsSuccess);
            Assert.Contains("managed project folders", relinked.ErrorMessage!);
            Assert.AreEqual(@"C:\Media\old.png", existing.SourcePath);
            Assert.AreEqual(@"cache\thumbnails\old.jpg", existing.ThumbnailCachePath);
            Assert.AreEqual("source bytes", await File.ReadAllTextAsync(sourcePath, TestContext.CancellationToken));
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
    public void CreateAsset_MapsPracticalMetadataAndUsesFiveSecondImageDuration()
    {
        var modified = new DateTimeOffset(2026, 8, 5, 10, 30, 0, TimeSpan.FromHours(2));
        var metadata = new MediaFileMetadata(
            @"C:\Media\Folder\..\still.png", "still.png", ProjectAssetKind.Image,
            DurationMilliseconds: 0, Width: 1920, Height: 1080,
            FileSize: 12_345, LastWriteUtc: modified);

        var asset = MediaImportService.CreateAsset(metadata);

        Assert.AreNotEqual(Guid.Empty, asset.Id);
        Assert.AreEqual(@"C:\Media\still.png", asset.SourcePath);
        Assert.AreEqual("still.png", asset.FileName);
        Assert.AreEqual(ProjectAssetKind.Image, asset.Kind);
        Assert.AreEqual(5_000L, asset.DurationMilliseconds);
        Assert.AreEqual(1920, asset.Width);
        Assert.AreEqual(1080, asset.Height);
        Assert.AreEqual(12_345UL, asset.FileSize);
        Assert.AreEqual(modified.ToUniversalTime(), asset.LastWriteUtc);
        Assert.IsFalse(asset.IsMissing);
        Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
    }

    [TestMethod]
    public void CreateAsset_RejectsSubMinimumVideoAndAudio()
    {
        foreach (var kind in new[] { ProjectAssetKind.Video, ProjectAssetKind.Audio })
        {
            var metadata = new MediaFileMetadata(
                @"C:\Media\short.mp4", "short.mp4", kind,
                DurationMilliseconds: 99, Width: 0, Height: 0,
                FileSize: 1, LastWriteUtc: DateTimeOffset.UnixEpoch);

            Assert.ThrowsExactly<InvalidDataException>(() => MediaImportService.CreateAsset(metadata));
        }
    }

    [TestMethod]
    public void Relink_CompatibleKindKeepsIdentityAndUpdatesMetadata()
    {
        var id = Guid.NewGuid();
        var existing = new ProjectAsset
        {
            Id = id,
            Kind = ProjectAssetKind.Video,
            SourcePath = @"C:\Old\clip.mp4",
            FileName = "clip.mp4",
            DurationMilliseconds = 1_000,
            ThumbnailCachePath = @"cache\thumbnails\old.jpg",
            IsMissing = true
        };
        var replacement = new MediaFileMetadata(
            @"D:\New\replacement.mp4", "replacement.mp4", ProjectAssetKind.Video,
            DurationMilliseconds: 2_000, Width: 1280, Height: 720,
            FileSize: 45_000, LastWriteUtc: DateTimeOffset.UnixEpoch.AddDays(1));

        var project = new ProjectDocument { Assets = [existing] };

        Assert.IsTrue(MediaImportService.TryApplyRelink(existing, replacement, project, out var error));
        Assert.IsNull(error);
        Assert.AreEqual(id, existing.Id);
        Assert.AreEqual(replacement.SourcePath, existing.SourcePath);
        Assert.AreEqual(replacement.FileName, existing.FileName);
        Assert.AreEqual(2_000L, existing.DurationMilliseconds);
        Assert.AreEqual(1280, existing.Width);
        Assert.AreEqual(720, existing.Height);
        Assert.AreEqual(45_000UL, existing.FileSize);
        Assert.AreEqual(DateTimeOffset.UnixEpoch.AddDays(1), existing.LastWriteUtc);
        Assert.AreEqual(string.Empty, existing.ThumbnailCachePath);
        Assert.IsFalse(existing.IsMissing);
    }

    [TestMethod]
    public void Relink_IncompatibleKindLeavesAssetUnchanged()
    {
        var existing = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            SourcePath = @"C:\Old\clip.mp4",
            IsMissing = true
        };
        var replacement = new MediaFileMetadata(
            @"C:\Media\still.png", "still.png", ProjectAssetKind.Image,
            DurationMilliseconds: 0, Width: 100, Height: 100,
            FileSize: 10, LastWriteUtc: DateTimeOffset.UnixEpoch);

        var project = new ProjectDocument { Assets = [existing] };

        Assert.IsFalse(MediaImportService.TryApplyRelink(existing, replacement, project, out var error));
        Assert.Contains("Video", error!);
        Assert.AreEqual(@"C:\Old\clip.mp4", existing.SourcePath);
        Assert.IsTrue(existing.IsMissing);
    }

    [TestMethod]
    [DataRow(ProjectAssetKind.Video, "replacement.mp4")]
    [DataRow(ProjectAssetKind.Audio, "replacement.wav")]
    public void Relink_TooShortForExistingTimelineReference_IsRejectedWithoutMutation(
        ProjectAssetKind kind,
        string replacementFileName)
    {
        var id = Guid.NewGuid();
        var existing = new ProjectAsset
        {
            Id = id,
            Kind = kind,
            SourcePath = kind == ProjectAssetKind.Video ? @"C:\Old\clip.mp4" : @"C:\Old\song.wav",
            FileName = kind == ProjectAssetKind.Video ? "clip.mp4" : "song.wav",
            DurationMilliseconds = 3_000,
            ThumbnailCachePath = @"cache\thumbnails\old.jpg",
            IsMissing = true
        };
        var project = new ProjectDocument { Assets = [existing] };
        if (kind == ProjectAssetKind.Video)
        {
            project.VideoItems.Add(new VideoTimelineItem
            {
                Id = Guid.NewGuid(),
                AssetId = id,
                SourceInMilliseconds = 100,
                SourceOutMilliseconds = 2_500
            });
        }
        else
        {
            project.AudioItems.Add(new AudioTimelineItem
            {
                Id = Guid.NewGuid(),
                AssetId = id,
                SourceInMilliseconds = 100,
                SourceOutMilliseconds = 2_500
            });
        }

        var replacement = new MediaFileMetadata(
            Path.Combine(@"D:\New", replacementFileName), replacementFileName, kind,
            DurationMilliseconds: 2_499, Width: 640, Height: 360,
            FileSize: 45_000, LastWriteUtc: DateTimeOffset.UnixEpoch.AddDays(1));

        Assert.IsFalse(MediaImportService.TryApplyRelink(existing, replacement, project, out var error));
        Assert.Contains(replacementFileName, error!);
        Assert.Contains("timeline", error!);
        Assert.AreEqual(id, existing.Id);
        Assert.AreEqual(kind == ProjectAssetKind.Video ? @"C:\Old\clip.mp4" : @"C:\Old\song.wav", existing.SourcePath);
        Assert.AreEqual(3_000L, existing.DurationMilliseconds);
        Assert.AreEqual(@"cache\thumbnails\old.jpg", existing.ThumbnailCachePath);
        Assert.IsTrue(existing.IsMissing);
        Assert.AreEqual(2_500L, kind == ProjectAssetKind.Video
            ? project.VideoItems.Single().SourceOutMilliseconds
            : project.AudioItems.Single().SourceOutMilliseconds);
    }

    [TestMethod]
    [DataRow(ProjectAssetKind.Video, "replacement.mp4")]
    [DataRow(ProjectAssetKind.Audio, "replacement.wav")]
    public void Relink_ExactTimelineBoundary_SucceedsWithoutChangingReferences(
        ProjectAssetKind kind,
        string replacementFileName)
    {
        var id = Guid.NewGuid();
        var existing = new ProjectAsset
        {
            Id = id,
            Kind = kind,
            SourcePath = kind == ProjectAssetKind.Video ? @"C:\Old\clip.mp4" : @"C:\Old\song.wav",
            DurationMilliseconds = 3_000
        };
        var project = new ProjectDocument { Assets = [existing] };
        if (kind == ProjectAssetKind.Video)
        {
            project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = id, SourceOutMilliseconds = 2_500 });
        }
        else
        {
            project.AudioItems.Add(new AudioTimelineItem { Id = Guid.NewGuid(), AssetId = id, SourceOutMilliseconds = 2_500 });
        }

        var replacement = new MediaFileMetadata(
            Path.Combine(@"D:\New", replacementFileName), replacementFileName, kind,
            DurationMilliseconds: 2_500, Width: 640, Height: 360,
            FileSize: 45_000, LastWriteUtc: DateTimeOffset.UnixEpoch.AddDays(1));

        Assert.IsTrue(MediaImportService.TryApplyRelink(existing, replacement, project, out var error));
        Assert.IsNull(error);
        Assert.AreEqual(id, existing.Id);
        Assert.AreEqual(2_500L, existing.DurationMilliseconds);
        Assert.AreEqual(2_500L, kind == ProjectAssetKind.Video
            ? project.VideoItems.Single().SourceOutMilliseconds
            : project.AudioItems.Single().SourceOutMilliseconds);
    }

    [TestMethod]
    public void Relink_UsesMaximumMatchingReferenceAcrossV1AndA1AndIgnoresOrphans()
    {
        var id = Guid.NewGuid();
        var existing = new ProjectAsset
        {
            Id = id,
            Kind = ProjectAssetKind.Video,
            SourcePath = @"C:\Old\clip.mp4",
            DurationMilliseconds = 3_000
        };
        var project = new ProjectDocument { Assets = [existing] };
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = id, SourceOutMilliseconds = 2_500 });
        project.AudioItems.Add(new AudioTimelineItem { Id = Guid.NewGuid(), AssetId = id, SourceOutMilliseconds = 2_750 });
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = Guid.NewGuid(), SourceOutMilliseconds = 9_000 });
        var replacement = new MediaFileMetadata(
            @"D:\New\replacement.mp4", "replacement.mp4", ProjectAssetKind.Video,
            DurationMilliseconds: 2_749, Width: 640, Height: 360,
            FileSize: 45_000, LastWriteUtc: DateTimeOffset.UnixEpoch.AddDays(1));

        Assert.IsFalse(MediaImportService.TryApplyRelink(existing, replacement, project, out var error));
        Assert.Contains("2750 ms", error!);
        Assert.AreEqual(@"C:\Old\clip.mp4", existing.SourcePath);
        Assert.AreEqual(3_000L, existing.DurationMilliseconds);
    }

    [TestMethod]
    public void Relink_ImageAlwaysUsesExactFiveSecondDuration()
    {
        var id = Guid.NewGuid();
        var existing = new ProjectAsset { Id = id, Kind = ProjectAssetKind.Image, SourcePath = @"C:\Old\still.png" };
        var project = new ProjectDocument { Assets = [existing] };
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = id, SourceOutMilliseconds = 5_000 });
        var replacement = new MediaFileMetadata(
            @"D:\New\still.jpg", "still.jpg", ProjectAssetKind.Image,
            DurationMilliseconds: 1, Width: 320, Height: 240,
            FileSize: 500, LastWriteUtc: DateTimeOffset.UnixEpoch);

        Assert.IsTrue(MediaImportService.TryApplyRelink(existing, replacement, project, out var error));
        Assert.IsNull(error);
        Assert.AreEqual(5_000L, existing.DurationMilliseconds);
        Assert.AreEqual(5_000L, project.VideoItems.Single().SourceOutMilliseconds);
    }

    [TestMethod]
    public void RefreshMissing_UpdatesFlagsWithoutRemovingAssets()
    {
        var present = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\folder\..\present.mp4", IsMissing = true };
        var missing = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\missing.mp4" };
        var project = new ProjectDocument { Assets = [present, missing] };
        var checkedPaths = new List<string>();

        var changed = MediaImportService.RefreshMissing(
            project,
            path =>
            {
                checkedPaths.Add(path);
                return path.EndsWith("present.mp4", StringComparison.OrdinalIgnoreCase);
            }, TestContext.CancellationToken);

        Assert.AreEqual(2, changed);
        Assert.HasCount(2, project.Assets);
        Assert.IsFalse(present.IsMissing);
        Assert.IsTrue(missing.IsMissing);
        Assert.AreSequenceEqual(
            expectedArray, checkedPaths);
    }

    [TestMethod]
    public async Task RefreshMissingAsync_ExistingChangedSourceClearsMissingWithoutChangingMetadata()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(testRoot, "source.png");
        Directory.CreateDirectory(testRoot);
        await File.WriteAllTextAsync(sourcePath, "original", TestContext.CancellationToken);
        try
        {
            var asset = new ProjectAsset
            {
                Id = Guid.NewGuid(),
                Kind = ProjectAssetKind.Image,
                SourcePath = Path.Combine(testRoot, ".", "source.png"),
                FileName = "recorded-name.png",
                DurationMilliseconds = 7_500,
                Width = 640,
                Height = 360,
                FileSize = checked((ulong)new FileInfo(sourcePath).Length),
                LastWriteUtc = File.GetLastWriteTimeUtc(sourcePath),
                IsMissing = true
            };
            var project = new ProjectDocument { Assets = [asset] };
            await File.WriteAllTextAsync(sourcePath, "changed source with a different size", TestContext.CancellationToken);
            var recordedFileSize = asset.FileSize;
            var recordedLastWriteUtc = asset.LastWriteUtc;

            await new MediaImportService().RefreshMissingAsync(project, TestContext.CancellationToken);

            Assert.IsFalse(asset.IsMissing);
            Assert.AreEqual("recorded-name.png", asset.FileName);
            Assert.AreEqual(7_500, asset.DurationMilliseconds);
            Assert.AreEqual(640, asset.Width);
            Assert.AreEqual(360, asset.Height);
            Assert.AreEqual(recordedFileSize, asset.FileSize);
            Assert.AreEqual(recordedLastWriteUtc, asset.LastWriteUtc);
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
    public void RefreshMissing_WhenAccessIsDenied_MarksMissingAndContinues()
    {
        var denied = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\denied.mp4" };
        var present = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\present.mp4", IsMissing = true };
        var project = new ProjectDocument { Assets = [denied, present] };

        var changed = MediaImportService.RefreshMissing(project, path =>
        {
            if (path.EndsWith("denied.mp4", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException();
            }

            return true;
        }, TestContext.CancellationToken);

        Assert.AreEqual(2, changed);
        Assert.IsTrue(denied.IsMissing);
        Assert.IsFalse(present.IsMissing);
    }

    [TestMethod]
    public void RefreshMissing_WhenCanceledDuringLastProbe_DoesNotCommitThatResult()
    {
        var asset = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\present.mp4", IsMissing = true };
        var project = new ProjectDocument { Assets = [asset] };
        using var cancellation = new CancellationTokenSource();

        Assert.ThrowsExactly<OperationCanceledException>(() => MediaImportService.RefreshMissing(
            project,
            _ =>
            {
                cancellation.Cancel();
                return true;
            },
            cancellation.Token));

        Assert.IsTrue(asset.IsMissing);
    }

    [TestMethod]
    public void RemoveAsset_RefusesReferencedAssetAndNeverTouchesSource()
    {
        var asset = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\source.mp4" };
        var project = new ProjectDocument { Assets = [asset] };
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = asset.Id, SourceOutMilliseconds = 1_000 });

        Assert.IsFalse(MediaImportService.TryRemoveAssetReference(project, asset.Id, out var error));
        Assert.Contains("timeline", error!);
        Assert.HasCount(1, project.Assets);
    }

    [TestMethod]
    public void RemoveAsset_UnreferencedRemovesOnlyProjectReference()
    {
        var asset = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\source.mp4" };
        var project = new ProjectDocument { Assets = [asset] };

        Assert.IsTrue(MediaImportService.TryRemoveAssetReference(project, asset.Id, out var error));
        Assert.IsNull(error);
        Assert.HasCount(0, project.Assets);
    }

    public TestContext TestContext { get; set; }

    private static readonly string[] expected = new[] { "first.mp4", "notes.txt", "last.jpg" };
    private static readonly string[] expectedArray = new[] { @"C:\Media\present.mp4", @"C:\Media\missing.mp4" };
}
