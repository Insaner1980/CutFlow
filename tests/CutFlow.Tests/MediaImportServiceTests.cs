using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class MediaImportServiceTests
{
    [DataTestMethod]
    [DataRow(@"C:\Media\clip.MP4", ProjectAssetKind.Video)]
    [DataRow(@"C:\Media\still.png", ProjectAssetKind.Image)]
    [DataRow(@"C:\Media\still.JPEG", ProjectAssetKind.Image)]
    [DataRow(@"C:\Media\song.mp3", ProjectAssetKind.Audio)]
    [DataRow(@"C:\Media\song.WAV", ProjectAssetKind.Audio)]
    public void TryGetKind_RecognizesBaselineExtensionsCaseInsensitively(string path, ProjectAssetKind expected)
    {
        Assert.IsTrue(MediaImportService.TryGetKind(path, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [DataTestMethod]
    [DataRow(@"C:\Media\clip.mov")]
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
    public void CreateAsset_MapsPracticalMetadataAndUsesFiveSecondImageDuration()
    {
        var modified = new DateTimeOffset(2026, 8, 5, 8, 30, 0, TimeSpan.Zero);
        var metadata = new MediaFileMetadata(
            @"C:\Media\still.png", "still.png", ProjectAssetKind.Image,
            DurationMilliseconds: 0, Width: 1920, Height: 1080,
            FileSize: 12_345, LastWriteUtc: modified);

        var asset = MediaImportService.CreateAsset(metadata);

        Assert.AreNotEqual(Guid.Empty, asset.Id);
        Assert.AreEqual("still.png", asset.FileName);
        Assert.AreEqual(ProjectAssetKind.Image, asset.Kind);
        Assert.AreEqual(5_000L, asset.DurationMilliseconds);
        Assert.AreEqual(1920, asset.Width);
        Assert.AreEqual(1080, asset.Height);
        Assert.AreEqual(12_345UL, asset.FileSize);
        Assert.AreEqual(modified, asset.LastWriteUtc);
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

            Assert.ThrowsException<InvalidDataException>(() => MediaImportService.CreateAsset(metadata));
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
        StringAssert.Contains(error, "Video");
        Assert.AreEqual(@"C:\Old\clip.mp4", existing.SourcePath);
        Assert.IsTrue(existing.IsMissing);
    }

    [DataTestMethod]
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
        StringAssert.Contains(error, replacementFileName);
        StringAssert.Contains(error, "timeline");
        Assert.AreEqual(id, existing.Id);
        Assert.AreEqual(kind == ProjectAssetKind.Video ? @"C:\Old\clip.mp4" : @"C:\Old\song.wav", existing.SourcePath);
        Assert.AreEqual(3_000L, existing.DurationMilliseconds);
        Assert.AreEqual(@"cache\thumbnails\old.jpg", existing.ThumbnailCachePath);
        Assert.IsTrue(existing.IsMissing);
        Assert.AreEqual(2_500L, kind == ProjectAssetKind.Video
            ? project.VideoItems.Single().SourceOutMilliseconds
            : project.AudioItems.Single().SourceOutMilliseconds);
    }

    [DataTestMethod]
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
        var present = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\present.mp4", IsMissing = true };
        var missing = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\missing.mp4" };
        var project = new ProjectDocument { Assets = [present, missing] };

        var changed = MediaImportService.RefreshMissing(project, path => path.EndsWith("present.mp4", StringComparison.OrdinalIgnoreCase));

        Assert.AreEqual(2, changed);
        Assert.HasCount(2, project.Assets);
        Assert.IsFalse(present.IsMissing);
        Assert.IsTrue(missing.IsMissing);
    }

    [TestMethod]
    public void RemoveAsset_RefusesReferencedAssetAndNeverTouchesSource()
    {
        var asset = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = @"C:\Media\source.mp4" };
        var project = new ProjectDocument { Assets = [asset] };
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = asset.Id, SourceOutMilliseconds = 1_000 });

        Assert.IsFalse(MediaImportService.TryRemoveAssetReference(project, asset.Id, out var error));
        StringAssert.Contains(error, "timeline");
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
}
