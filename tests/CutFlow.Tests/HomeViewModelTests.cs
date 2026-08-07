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

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => viewModel.RenameAsync(project.Id, "  "));

        var loaded = await viewModel.OpenAsync(project.Id);
        Assert.AreEqual("New project", loaded.Name);
    }

    [TestMethod]
    public void ProjectCard_UsesTheFirstUsableCachedVisualThumbnail()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Poster", DateTimeOffset.UtcNow);
        project.Assets.Add(new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Audio,
            FileName = "audio.wav"
        });
        project.Assets.Add(new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Image,
            FileName = "missing.jpg",
            ThumbnailCachePath = @"cache\thumbnails\missing.jpg",
            IsMissing = true
        });
        project.Assets.Add(new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            FileName = "poster.mp4",
            ThumbnailCachePath = @"cache\thumbnails\poster.jpg"
        });
        var thumbnailPath = Path.Combine(directory.Path, @"cache\thumbnails\poster.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        File.WriteAllBytes(thumbnailPath, [0xFF, 0xD8, 0xFF, 0xD9]);

        var card = new ProjectCardViewModel(project, directory.Path);

        Assert.IsTrue(card.HasThumbnail);
        Assert.AreEqual(
            Path.GetFullPath(thumbnailPath),
            card.ThumbnailPath);
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
