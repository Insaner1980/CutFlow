using CutFlow.Models;
using CutFlow.Services;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class HomeViewModelTests
{
    [TestMethod]
    public async Task CreateRenameDuplicateDeleteAndLoad_KeepProjectCardsCurrent()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new HomeViewModel(new ProjectService(directory.Path));

        var created = await viewModel.CreateAsync();
        Assert.AreEqual("New project", created.Name);
        Assert.HasCount(1, viewModel.Projects);

        await viewModel.RenameAsync(created.Id, "  First edit  ");
        Assert.AreEqual("First edit", viewModel.Projects[0].Name);

        var duplicate = await viewModel.DuplicateAsync(created.Id);
        Assert.AreNotEqual(created.Id, duplicate.Id);
        Assert.AreEqual("First edit copy", duplicate.Name);
        Assert.HasCount(2, viewModel.Projects);

        var opened = await viewModel.OpenAsync(duplicate.Id);
        Assert.AreEqual(duplicate.Id, opened.Id);

        await viewModel.DeleteAsync(created.Id);
        Assert.HasCount(1, viewModel.Projects);
        Assert.AreEqual(duplicate.Id, viewModel.Projects[0].Id);

        var reloaded = new HomeViewModel(new ProjectService(directory.Path));
        await reloaded.LoadAsync();
        Assert.HasCount(1, reloaded.Projects);
        Assert.AreEqual(duplicate.Id, reloaded.Projects[0].Id);
    }

    [TestMethod]
    public async Task RenameAsync_RejectsBlankNameWithoutChangingProject()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new HomeViewModel(new ProjectService(directory.Path));
        var project = await viewModel.CreateAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => viewModel.RenameAsync(project.Id, "  "));

        var loaded = await viewModel.OpenAsync(project.Id);
        Assert.AreEqual("New project", loaded.Name);
    }

    [TestMethod]
    public async Task LoadAsync_PublishesAllProjectCardsBeforeASlowThumbnailFinishes()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var slowProject = await service.CreateAsync("Slow thumbnail");
        var fastProject = await service.CreateAsync("Fast thumbnail");
        var fastThumbnailPath = Path.Combine(directory.Path, "fast.jpg");
        var slowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlow = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new HomeViewModel(
            service,
            (project, _, cancellationToken) =>
            {
                if (project.Id != slowProject.Id)
                {
                    return Task.FromResult<string?>(fastThumbnailPath);
                }

                slowStarted.TrySetResult();
                return releaseSlow.Task.WaitAsync(cancellationToken);
            });

        var loadTask = viewModel.LoadAsync();
        await slowStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(loadTask.IsCompleted);
        Assert.HasCount(2, viewModel.Projects);
        CollectionAssert.AreEquivalent(
            new[] { slowProject.Id, fastProject.Id },
            viewModel.Projects.Select(card => card.Id).ToArray());
        Assert.AreEqual(
            fastThumbnailPath,
            viewModel.Projects.Single(card => card.Id == fastProject.Id).ThumbnailPath);

        releaseSlow.SetResult(null);
        await loadTask;
    }

    [TestMethod]
    public async Task ProjectCard_UsesTheFirstUsableCachedVisualThumbnail()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Poster", DateTimeOffset.UtcNow);
        var audioCachePath = CreateThumbnailCachePath('a');
        var missingAsset = CreateVisualAsset(ProjectAssetKind.Image, @"C:\Media\missing.jpg", 20);
        var posterAsset = CreateVisualAsset(ProjectAssetKind.Video, @"C:\Media\poster.mp4", 30);
        project.Assets.Add(new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Audio,
            FileName = "audio.wav",
            ThumbnailCachePath = audioCachePath
        });
        project.Assets.Add(missingAsset);
        project.Assets.Add(posterAsset);
        var thumbnailPath = ThumbnailService.ResolveProjectCachePath(directory.Path, posterAsset.ThumbnailCachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"),
            ThumbnailService.ResolveProjectCachePath(directory.Path, audioCachePath));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), thumbnailPath);

        var card = await ProjectCardViewModel.CreateAsync(project, directory.Path);

        Assert.IsTrue(card.HasThumbnail);
        Assert.AreEqual(
            Path.GetFullPath(thumbnailPath),
            card.ThumbnailPath);
    }

    [TestMethod]
    public async Task ProjectCard_SkipsCorruptCachedThumbnailAndUsesLaterValidVisual()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Poster", DateTimeOffset.UtcNow);
        var corruptAsset = CreateVisualAsset(ProjectAssetKind.Image, @"C:\Media\corrupt.jpg", 40);
        var validAsset = CreateVisualAsset(ProjectAssetKind.Video, @"C:\Media\valid.mp4", 50);
        project.Assets.Add(corruptAsset);
        project.Assets.Add(validAsset);
        Directory.CreateDirectory(Path.Combine(directory.Path, "cache", "thumbnails"));
        File.WriteAllText(ThumbnailService.ResolveProjectCachePath(directory.Path, corruptAsset.ThumbnailCachePath), "not a jpeg");
        var outsidePath = Path.Combine(directory.Path, "outside.jpg");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), outsidePath);
        project.Assets.Insert(1, new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Image,
            ThumbnailCachePath = @"..\..\outside.jpg"
        });
        var validPath = ThumbnailService.ResolveProjectCachePath(directory.Path, validAsset.ThumbnailCachePath);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), validPath);

        var card = await ProjectCardViewModel.CreateAsync(project, directory.Path);

        Assert.AreEqual(validPath, card.ThumbnailPath);
    }

    [TestMethod]
    public async Task ProjectCard_WhenOnlyCachedVisualIsCorrupt_UsesPlaceholder()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Corrupt poster", DateTimeOffset.UtcNow);
        var corruptAsset = CreateVisualAsset(ProjectAssetKind.Image, @"C:\Media\corrupt.jpg", 60);
        project.Assets.Add(corruptAsset);
        var corruptPath = ThumbnailService.ResolveProjectCachePath(directory.Path, corruptAsset.ThumbnailCachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(corruptPath)!);
        File.WriteAllText(corruptPath, "not a jpeg");

        var card = await ProjectCardViewModel.CreateAsync(project, directory.Path);

        Assert.IsFalse(card.HasThumbnail);
        Assert.IsNull(card.ThumbnailPath);
        Assert.IsNull(card.ThumbnailSource);
    }

    [TestMethod]
    public async Task ProjectCard_WhenCachedThumbnailDoesNotMatchAssetSnapshot_UsesPlaceholder()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Mismatched poster", DateTimeOffset.UtcNow);
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Image,
            SourcePath = @"C:\Media\poster.jpg",
            FileSize = 123,
            LastWriteUtc = DateTimeOffset.UnixEpoch,
            ThumbnailCachePath = CreateThumbnailCachePath('a')
        };
        project.Assets.Add(asset);
        var mismatchedPath = ThumbnailService.ResolveProjectCachePath(directory.Path, asset.ThumbnailCachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(mismatchedPath)!);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), mismatchedPath);

        var card = await ProjectCardViewModel.CreateAsync(project, directory.Path);

        Assert.IsFalse(card.HasThumbnail);
        Assert.IsNull(card.ThumbnailPath);
    }

    [TestMethod]
    public async Task DuplicateAsync_WhenCachesAreNotCopied_UsesPlaceholderUnderTheNewProjectRoot()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var original = await service.CreateAsync("Original");
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg");
        var sourceInfo = new FileInfo(sourcePath);
        var originalAsset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Image,
            SourcePath = sourcePath,
            FileSize = checked((ulong)sourceInfo.Length),
            LastWriteUtc = sourceInfo.LastWriteTimeUtc
        };
        originalAsset.ThumbnailCachePath = ThumbnailService.CreateRelativeCachePath(
            ThumbnailService.CaptureRequest(originalAsset, ThumbnailService.DefaultRequestedSize).CacheKey);
        original.Assets.Add(originalAsset);
        await service.SaveAsync(original);
        var originalCachePath = ThumbnailService.ResolveProjectCachePath(service.GetProjectPath(original.Id), originalAsset.ThumbnailCachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(originalCachePath)!);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestMedia", "valid-image.jpg"), originalCachePath);
        var duplicate = await service.DuplicateAsync(original.Id);

        var viewModel = new HomeViewModel(service);
        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.Projects.Single(project => project.Id == original.Id).HasThumbnail);
        Assert.IsFalse(viewModel.Projects.Single(project => project.Id == duplicate.Id).HasThumbnail);
        Assert.IsFalse(File.Exists(ThumbnailService.ResolveProjectCachePath(service.GetProjectPath(duplicate.Id), originalAsset.ThumbnailCachePath)));
    }

    [TestMethod]
    public async Task LoadAsync_WhenThumbnailCacheIsMissing_DoesNotCreateOrMutateProjectCache()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("No cached poster");
        project.Assets.Add(CreateVisualAsset(ProjectAssetKind.Image, @"C:\Media\uncached.jpg", 70));
        await service.SaveAsync(project);
        var projectPath = service.GetProjectPath(project.Id);
        var entriesBefore = Directory.GetFileSystemEntries(projectPath, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var jsonPath = Path.Combine(projectPath, "project.json");
        var jsonBefore = await File.ReadAllBytesAsync(jsonPath);

        var viewModel = new HomeViewModel(service);
        await viewModel.LoadAsync();

        var entriesAfter = Directory.GetFileSystemEntries(projectPath, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(entriesBefore, entriesAfter);
        CollectionAssert.AreEqual(jsonBefore, await File.ReadAllBytesAsync(jsonPath));
        Assert.IsFalse(viewModel.Projects.Single().HasThumbnail);
    }

    private static string CreateThumbnailCachePath(char hexDigit) =>
        ThumbnailService.CreateRelativeCachePath(new string(hexDigit, 64));

    private static ProjectAsset CreateVisualAsset(ProjectAssetKind kind, string sourcePath, ulong fileSize)
    {
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            SourcePath = sourcePath,
            FileSize = fileSize,
            LastWriteUtc = DateTimeOffset.UnixEpoch
        };
        asset.ThumbnailCachePath = ThumbnailService.CreateRelativeCachePath(
            ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize).CacheKey);
        return asset;
    }

    [TestMethod]
    public async Task ProjectCard_DefaultModifiedAtProducesDisplayText()
    {
        var project = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            Name = "Default timestamp"
        };

        var card = await ProjectCardViewModel.CreateAsync(project, string.Empty);

        Assert.IsFalse(string.IsNullOrWhiteSpace(card.ModifiedText));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
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
