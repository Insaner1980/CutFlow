using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class ProjectServiceTests
{
    [TestMethod]
    public async Task CreateListAndRenameAsync_ManageProjectsUnderConfiguredRoot()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);

        var project = await service.CreateAsync("First cut");
        var renamed = await service.RenameAsync(project.Id, "Final cut");
        var listed = await service.ListAsync();

        Assert.AreEqual("Final cut", renamed.Name);
        Assert.HasCount(1, listed);
        Assert.AreEqual(project.Id, listed[0].Id);
        Assert.AreEqual("Final cut", listed[0].Name);
        Assert.IsTrue(Directory.Exists(Path.Combine(directory.Path, "Projects", project.Id.ToString("D"))));
        Assert.AreEqual(Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "cache"), service.GetCachePath(project.Id));
    }

    [TestMethod]
    public async Task CreateAsync_WhenPreCanceled_DoesNotCreateProjectsDirectory()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => new ProjectService(directory.Path).CreateAsync("Canceled", cancellation.Token));

        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Path, "Projects")));
    }

    [TestMethod]
    public async Task CreateAsync_WhenSaveFails_CleansUpOnlyItsNewProjectDirectory()
    {
        using var directory = new TemporaryDirectory();
        var projectsRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects"));
        var existingProject = Directory.CreateDirectory(Path.Combine(projectsRoot.FullName, Guid.NewGuid().ToString("D")));
        var service = new ProjectService(directory.Path, _ => throw new InvalidOperationException("Deterministic serializer failure"));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.CreateAsync("Fails"));

        var directChildren = Directory.EnumerateDirectories(projectsRoot.FullName).ToList();
        Assert.HasCount(1, directChildren);
        Assert.AreEqual(existingProject.FullName, directChildren[0]);
    }

    [TestMethod]
    public async Task DuplicateAsync_CreatesDistinctIdentityPathAndName()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var original = await service.CreateAsync("Interview");
        original.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "Opening" });
        await service.SaveAsync(original);

        var duplicate = await service.DuplicateAsync(original.Id);

        Assert.AreNotEqual(original.Id, duplicate.Id);
        Assert.AreEqual("Interview copy", duplicate.Name);
        Assert.HasCount(1, duplicate.TextItems);
        Assert.IsTrue(File.Exists(Path.Combine(directory.Path, "Projects", original.Id.ToString("D"), "project.json")));
        Assert.IsTrue(File.Exists(Path.Combine(directory.Path, "Projects", duplicate.Id.ToString("D"), "project.json")));
    }

    [TestMethod]
    public async Task DuplicateAsync_UsesNewTimestampsAndCreatesDeeplyIndependentDocument()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var original = ProjectDocument.CreateNew("Interview", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        original.Assets.Add(new ProjectAsset { Id = Guid.NewGuid(), SourcePath = "original.mp4" });
        original.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "Original text" });
        await service.SaveAsync(original);

        var duplicate = await service.DuplicateAsync(original.Id);
        duplicate.Settings.Width = 100;
        duplicate.Assets[0].SourcePath = "duplicate.mp4";
        duplicate.TextItems[0].Text = "Duplicate text";
        var loadedOriginal = await service.LoadAsync(original.Id);

        Assert.IsTrue(duplicate.CreatedAt > original.CreatedAt);
        Assert.IsTrue(duplicate.ModifiedAt >= duplicate.CreatedAt);
        Assert.AreNotEqual(original.Id, duplicate.Id);
        Assert.AreEqual(1920, loadedOriginal.Settings.Width);
        Assert.AreEqual("original.mp4", loadedOriginal.Assets[0].SourcePath);
        Assert.AreEqual("Original text", loadedOriginal.TextItems[0].Text);
    }

    [TestMethod]
    public async Task ListAsync_ReturnsProjectsInDescendingModifiedAtOrder()
    {
        using var directory = new TemporaryDirectory();
        var older = ProjectDocument.CreateNew("Older", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        older.ModifiedAt = new DateTimeOffset(2020, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var newer = ProjectDocument.CreateNew("Newer", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        newer.ModifiedAt = new DateTimeOffset(2020, 1, 3, 0, 0, 0, TimeSpan.Zero);
        await WriteProjectJsonAsync(directory.Path, older);
        await WriteProjectJsonAsync(directory.Path, newer);

        var projects = await new ProjectService(directory.Path).ListAsync();

        CollectionAssert.AreEqual(new[] { newer.Id, older.Id }, projects.Select(project => project.Id).ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_WhenDirectoryIdentityDoesNotMatchDocument_ThrowsInvalidDataException()
    {
        using var directory = new TemporaryDirectory();
        var directoryId = Guid.NewGuid();
        var document = ProjectDocument.CreateNew("Wrong id", DateTimeOffset.UnixEpoch);
        await WriteProjectJsonAsync(directory.Path, document, directoryId);

        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => new ProjectService(directory.Path).LoadAsync(directoryId));
    }

    [TestMethod]
    public async Task ProjectOperations_RejectEmptyGuidAndBlankRename()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Rename");

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.LoadAsync(Guid.Empty));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.SaveAsync(new ProjectDocument()));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.RenameAsync(project.Id, "  \t"));
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesOnlyProjectDirectoryAndNeverExternalMedia()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var externalMediaPath = Path.Combine(directory.Path, "external-source.mp4");
        await File.WriteAllTextAsync(externalMediaPath, "external media");
        var project = await service.CreateAsync("Delete me");
        project.Assets.Add(new ProjectAsset { Id = Guid.NewGuid(), SourcePath = externalMediaPath });
        await service.SaveAsync(project);
        var cacheFile = Path.Combine(service.GetCachePath(project.Id), "cache-entry.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        await File.WriteAllTextAsync(cacheFile, "cache");

        await service.DeleteAsync(project.Id);

        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Path, "Projects", project.Id.ToString("D"))));
        Assert.IsTrue(File.Exists(externalMediaPath));
        Assert.AreEqual("external media", await File.ReadAllTextAsync(externalMediaPath));
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

    private static async Task WriteProjectJsonAsync(string rootPath, ProjectDocument project, Guid? directoryId = null)
    {
        var projectDirectory = Directory.CreateDirectory(Path.Combine(rootPath, "Projects", (directoryId ?? project.Id).ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), System.Text.Json.JsonSerializer.Serialize(project));
    }
}
