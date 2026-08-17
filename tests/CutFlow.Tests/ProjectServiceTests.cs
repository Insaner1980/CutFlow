using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed partial class ProjectServiceTests
{
    [TestMethod]
    public async Task CreateListAndRenameAsync_ManageProjectsUnderConfiguredRoot()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);

        var project = await service.CreateAsync("First cut", TestContext.CancellationToken);
        var renamed = await service.RenameAsync(project.Id, "Final cut", TestContext.CancellationToken);
        var listed = await service.ListAsync(TestContext.CancellationToken);

        Assert.AreEqual("Final cut", renamed.Name);
        Assert.HasCount(1, listed);
        Assert.AreEqual(project.Id, listed[0].Id);
        Assert.AreEqual("Final cut", listed[0].Name);
        Assert.IsTrue(Directory.Exists(Path.Combine(directory.Path, "Projects", project.Id.ToString("D"))));
        Assert.AreEqual(Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "cache"), service.GetCachePath(project.Id));
    }

    [TestMethod]
    public async Task CreateRenameAndDuplicateAsync_UseExactUtcClockValuesAcrossDayBoundary()
    {
        using var directory = new TemporaryDirectory();
        var timestamps = new Queue<DateTimeOffset>(
        [
            new DateTimeOffset(2026, 8, 9, 23, 59, 59, 999, TimeSpan.Zero).AddTicks(8_999),
            new DateTimeOffset(2026, 8, 9, 23, 59, 59, 999, TimeSpan.Zero).AddTicks(9_999),
            new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero).AddTicks(1),
            new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero).AddTicks(2)
        ]);
        var expected = timestamps.ToArray();
        var service = new ProjectService(directory.Path, utcNow: () => timestamps.Dequeue());

        var created = await service.CreateAsync("Boundary project", TestContext.CancellationToken);
        var renamed = await service.RenameAsync(created.Id, "Renamed project", TestContext.CancellationToken);
        var duplicate = await service.DuplicateAsync(created.Id, cancellationToken: TestContext.CancellationToken);
        var original = await service.LoadAsync(created.Id, TestContext.CancellationToken);

        Assert.AreEqual(expected[0], created.CreatedAt);
        Assert.AreEqual(expected[1], created.ModifiedAt);
        Assert.AreEqual(expected[0], renamed.CreatedAt);
        Assert.AreEqual(expected[2], renamed.ModifiedAt);
        Assert.AreEqual(expected[0], original.CreatedAt);
        Assert.AreEqual(expected[2], original.ModifiedAt);
        Assert.AreEqual(expected[3], duplicate.CreatedAt);
        Assert.AreEqual(expected[4], duplicate.ModifiedAt);
        Assert.IsTrue(new[]
        {
            created.CreatedAt,
            created.ModifiedAt,
            renamed.CreatedAt,
            renamed.ModifiedAt,
            duplicate.CreatedAt,
            duplicate.ModifiedAt
        }.All(timestamp => timestamp.Offset == TimeSpan.Zero));
    }

    [TestMethod]
    public async Task CreateAsync_WhenPreCanceled_DoesNotCreateProjectsDirectory()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new ProjectService(directory.Path).CreateAsync("Canceled", cancellation.Token));

        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Path, "Projects")));
    }

    [TestMethod]
    public async Task CreateAsync_WhenSaveFails_CleansUpOnlyItsNewProjectDirectory()
    {
        using var directory = new TemporaryDirectory();
        var projectsRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects"));
        var existingProject = Directory.CreateDirectory(Path.Combine(projectsRoot.FullName, Guid.NewGuid().ToString("D")));
        var service = new ProjectService(directory.Path, _ => throw new InvalidOperationException("Deterministic serializer failure"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.CreateAsync("Fails", TestContext.CancellationToken));

        var directChildren = Directory.EnumerateDirectories(projectsRoot.FullName).ToList();
        Assert.HasCount(1, directChildren);
        Assert.AreEqual(existingProject.FullName, directChildren[0]);
        Assert.IsEmpty(await service.ListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CreateAsync_WhenAnotherProcessCreatesTheSamePath_PreservesTheOtherDirectory()
    {
        using var directory = new TemporaryDirectory();
        string? competingDirectory = null;
        var service = new ProjectService(
            directory.Path,
            createProjectDirectory: path =>
            {
                competingDirectory = path;
                Directory.CreateDirectory(path);
                throw new IOException("Another process created the project directory first.");
            });

        await Assert.ThrowsExactlyAsync<IOException>(() => service.CreateAsync("Losing creator", TestContext.CancellationToken));

        Assert.IsNotNull(competingDirectory);
        Assert.IsTrue(Directory.Exists(competingDirectory));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(competingDirectory));
        Assert.IsEmpty(await service.ListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CreateAsync_WhenCanceledAfterClaimingDirectory_RemovesOnlyClaimedDirectory()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        var projectsRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects"));
        var existingProject = Directory.CreateDirectory(Path.Combine(projectsRoot.FullName, Guid.NewGuid().ToString("D")));
        var service = new ProjectService(
            directory.Path,
            createProjectDirectory: path =>
            {
                Directory.CreateDirectory(path);
                cancellation.Cancel();
            });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.CreateAsync("Canceled", cancellation.Token));

        var directChildren = Directory.EnumerateDirectories(projectsRoot.FullName).ToList();
        Assert.HasCount(1, directChildren);
        Assert.AreEqual(existingProject.FullName, directChildren[0]);
        Assert.IsEmpty(await service.ListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CreateAsync_WhenAnotherCreatorPublishesFirst_PreservesTheWinningProject()
    {
        using var directory = new TemporaryDirectory();
        const string winningJson = "another creator";
        string? projectPath = null;
        var service = new ProjectService(directory.Path, project =>
        {
            projectPath = Path.Combine(
                directory.Path,
                "Projects",
                project.Id.ToString("D"),
                "project.json");
            File.WriteAllText(projectPath, winningJson);
            return System.Text.Json.JsonSerializer.Serialize(project);
        });

        await Assert.ThrowsExactlyAsync<IOException>(() => service.CreateAsync("Losing creator", TestContext.CancellationToken));

        Assert.IsNotNull(projectPath);
        Assert.AreEqual(winningJson, await File.ReadAllTextAsync(projectPath, TestContext.CancellationToken));
        Assert.IsEmpty(Directory.EnumerateFiles(Path.GetDirectoryName(projectPath)!, "*.tmp"));
    }

    [TestMethod]
    public async Task DuplicateAsync_CreatesDistinctIdentityPathAndName()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var original = await service.CreateAsync("Interview", TestContext.CancellationToken);
        original.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "Opening" });
        await service.SaveAsync(original, TestContext.CancellationToken);

        var duplicate = await service.DuplicateAsync(original.Id, cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(original.Id, duplicate.Id);
        Assert.AreEqual("Interview copy", duplicate.Name);
        Assert.HasCount(1, duplicate.TextItems);
        Assert.IsTrue(File.Exists(Path.Combine(directory.Path, "Projects", original.Id.ToString("D"), "project.json")));
        Assert.IsTrue(File.Exists(Path.Combine(directory.Path, "Projects", duplicate.Id.ToString("D"), "project.json")));
    }

    [TestMethod]
    public async Task CreateRenameAndDuplicateAsync_NormalizeUserNamesAndEnforceTheSharedLengthLimit()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);

        var project = await service.CreateAsync("\tFinal\0cut\r\n", TestContext.CancellationToken);
        Assert.AreEqual("Final cut", project.Name);

        var maximumName = new string('A', ProjectDocument.MaximumNameLength);
        var renamed = await service.RenameAsync(project.Id, $"  {maximumName}  ", TestContext.CancellationToken);
        Assert.AreEqual(maximumName, renamed.Name);

        var duplicate = await service.DuplicateAsync(project.Id, cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual($"{new string('A', ProjectDocument.MaximumNameLength - 5)} copy", duplicate.Name);
        Assert.HasCount(ProjectDocument.MaximumNameLength, duplicate.Name);

        var tooLongName = new string('B', ProjectDocument.MaximumNameLength + 1);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CreateAsync(tooLongName, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RenameAsync(project.Id, tooLongName, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.DuplicateAsync(project.Id, tooLongName, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CreateAsync("\0\r\n", TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.DuplicateAsync(project.Id, " \t ", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DuplicateAsync_UsesNewTimestampsAndCreatesDeeplyIndependentDocument()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var original = ProjectDocument.CreateNew("Interview", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var originalSourcePath = Path.Combine(directory.Path, "original.mp4");
        original.Assets.Add(new ProjectAsset { Id = Guid.NewGuid(), SourcePath = originalSourcePath });
        original.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "Original text" });
        await service.SaveAsync(original, TestContext.CancellationToken);

        var duplicate = await service.DuplicateAsync(original.Id, cancellationToken: TestContext.CancellationToken);
        duplicate.Settings.Width = 100;
        duplicate.Assets[0].SourcePath = Path.Combine(directory.Path, "duplicate.mp4");
        duplicate.TextItems[0].Text = "Duplicate text";
        var loadedOriginal = await service.LoadAsync(original.Id, TestContext.CancellationToken);

        Assert.IsGreaterThan(original.CreatedAt, duplicate.CreatedAt);
        Assert.IsGreaterThanOrEqualTo(duplicate.CreatedAt, duplicate.ModifiedAt);
        Assert.AreNotEqual(original.Id, duplicate.Id);
        Assert.AreEqual(1920, loadedOriginal.Settings.Width);
        Assert.AreEqual(originalSourcePath, loadedOriginal.Assets[0].SourcePath);
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

        var projects = await new ProjectService(directory.Path).ListAsync(TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { newer.Id, older.Id }, projects.Select(project => project.Id).ToArray());
    }

    [TestMethod]
    public async Task ListAsync_EqualModifiedAtUsesProjectIdAsDeterministicTieBreaker()
    {
        using var directory = new TemporaryDirectory();
        var timestamp = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var higher = ProjectDocument.CreateNew("Higher id", timestamp);
        higher.Id = higherId;
        var lower = ProjectDocument.CreateNew("Lower id", timestamp);
        lower.Id = lowerId;
        await WriteProjectJsonAsync(directory.Path, higher);
        await WriteProjectJsonAsync(directory.Path, lower);

        var projects = await new ProjectService(directory.Path).ListAsync(TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { lowerId, higherId }, projects.Select(project => project.Id).ToArray());
    }

    [TestMethod]
    public async Task ListAsync_OrdersOffsetTimestampsByUtcInstant()
    {
        using var directory = new TemporaryDirectory();
        var earlierId = Guid.NewGuid();
        var laterId = Guid.NewGuid();
        var earlierDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", earlierId.ToString("D")));
        var laterDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", laterId.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(earlierDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{earlierId}}", "name": "Earlier", "modifiedAt": "2026-08-11T12:00:00+03:00" }
            """, TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(laterDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{laterId}}", "name": "Later", "modifiedAt": "2026-08-11T10:00:00Z" }
            """, TestContext.CancellationToken);

        var projects = await new ProjectService(directory.Path).ListAsync(TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { laterId, earlierId }, projects.Select(project => project.Id).ToArray());
        Assert.IsTrue(projects.All(project => project.ModifiedAt.Offset == TimeSpan.Zero));
    }

    [TestMethod]
    public async Task ListAsync_DefaultTimestampRemainsUsableAndInvalidTimestampIsSkipped()
    {
        using var directory = new TemporaryDirectory();
        var defaultTimestamp = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            Name = "Default timestamp"
        };
        await WriteProjectJsonAsync(directory.Path, defaultTimestamp);
        var invalidId = Guid.NewGuid();
        var invalidDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", invalidId.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(invalidDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{invalidId}}", "name": "Invalid timestamp", "modifiedAt": "not-a-timestamp" }
            """, TestContext.CancellationToken);

        var projects = await new ProjectService(directory.Path).ListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, projects);
        Assert.AreEqual(defaultTimestamp.Id, projects[0].Id);
        Assert.AreEqual(default(DateTimeOffset), projects[0].ModifiedAt);
    }

    [TestMethod]
    public async Task ListAsync_AfterFailedSavePreservesThePreviousProjectOrder()
    {
        using var directory = new TemporaryDirectory();
        var older = ProjectDocument.CreateNew("Older", new DateTimeOffset(2026, 8, 11, 8, 0, 0, TimeSpan.Zero));
        var newer = ProjectDocument.CreateNew("Newer", new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero));
        await WriteProjectJsonAsync(directory.Path, older);
        await WriteProjectJsonAsync(directory.Path, newer);
        var originalModifiedAt = older.ModifiedAt;
        var failingService = new ProjectService(
            directory.Path,
            serialize: _ => throw new InvalidOperationException("Deterministic serializer failure"),
            utcNow: () => new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => failingService.SaveAsync(older, TestContext.CancellationToken));
        var projects = await new ProjectService(directory.Path).ListAsync(TestContext.CancellationToken);

        Assert.AreEqual(originalModifiedAt, older.ModifiedAt);
        Assert.AreSequenceEqual(new[] { newer.Id, older.Id }, projects.Select(project => project.Id).ToArray());
    }

    [TestMethod]
    public async Task ListAsync_WhenPreCanceledAndProjectsRootDoesNotExist_PropagatesCancellation()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => new ProjectService(directory.Path).ListAsync(cancellation.Token));
    }

    [TestMethod]
    public async Task ListAsync_AlternateGuidDirectoryDoesNotDuplicateCanonicalProject()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Canonical", TestContext.CancellationToken);
        var alternateDirectory = Directory.CreateDirectory(
            Path.Combine(directory.Path, "Projects", project.Id.ToString("N")));
        await File.WriteAllTextAsync(
            Path.Combine(alternateDirectory.FullName, "project.json"),
            System.Text.Json.JsonSerializer.Serialize(project), TestContext.CancellationToken);

        var projects = await service.ListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, projects);
        Assert.AreEqual(project.Id, projects[0].Id);
    }

    [TestMethod]
    public async Task ListAsync_TrailingSpaceGuidDirectoryDoesNotDuplicateCanonicalProject()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Canonical", TestContext.CancellationToken);
        var alternateDirectoryPath = Path.Combine(
            directory.Path,
            "Projects",
            $"{project.Id:D} ");
        var extendedAlternateDirectoryPath = $@"\\?\{alternateDirectoryPath}";
        Directory.CreateDirectory(extendedAlternateDirectoryPath);
        try
        {
            var projects = await service.ListAsync(TestContext.CancellationToken);

            Assert.HasCount(1, projects);
            Assert.AreEqual(project.Id, projects[0].Id);
        }
        finally
        {
            Directory.Delete(extendedAlternateDirectoryPath);
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenDirectoryIdentityDoesNotMatchDocument_ThrowsInvalidDataException()
    {
        using var directory = new TemporaryDirectory();
        var directoryId = Guid.NewGuid();
        var document = ProjectDocument.CreateNew("Wrong id", DateTimeOffset.UnixEpoch);
        await WriteProjectJsonAsync(directory.Path, document, directoryId);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new ProjectService(directory.Path).LoadAsync(directoryId, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ProjectOperations_WhenProjectDirectoryIsSymbolicLink_RejectBeforeReadCacheOrSave()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Linked", DateTimeOffset.UnixEpoch);
        var projectsRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects"));
        var externalDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "ExternalProject"));
        var externalProjectPath = Path.Combine(externalDirectory.FullName, "project.json");
        var originalJson = System.Text.Json.JsonSerializer.Serialize(project);
        await File.WriteAllTextAsync(externalProjectPath, originalJson, TestContext.CancellationToken);
        Directory.CreateDirectory(Path.Combine(externalDirectory.FullName, "cache"));

        var linkPath = Path.Combine(projectsRoot.FullName, project.Id.ToString("D"));
        Directory.CreateSymbolicLink(linkPath, externalDirectory.FullName);
        try
        {
            var service = new ProjectService(directory.Path);

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.LoadAsync(project.Id, TestContext.CancellationToken));
            Assert.ThrowsExactly<InvalidDataException>(() => service.GetCachePath(project.Id));

            project.Name = "Must not be saved";
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.SaveAsync(project, TestContext.CancellationToken));

            Assert.AreEqual(originalJson, await File.ReadAllTextAsync(externalProjectPath, TestContext.CancellationToken));
            Assert.IsFalse(File.Exists(externalProjectPath + ".tmp"));
        }
        finally
        {
            Directory.Delete(linkPath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_WhenUnownedTemporaryFileIsSymbolicLink_IgnoresItWithoutOverwritingTarget()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Linked temporary file", TestContext.CancellationToken);
        var externalPath = Path.Combine(directory.Path, "external.json");
        await File.WriteAllTextAsync(externalPath, "preserve external file", TestContext.CancellationToken);
        var temporaryPath = Path.Combine(service.GetProjectPath(project.Id), "project.json.tmp");
        File.CreateSymbolicLink(temporaryPath, externalPath);

        project.Name = "Saved through an owned temp file";
        await service.SaveAsync(project, TestContext.CancellationToken);

        Assert.AreEqual("preserve external file", await File.ReadAllTextAsync(externalPath, TestContext.CancellationToken));
        Assert.IsTrue(File.Exists(temporaryPath));
        Assert.AreEqual("Saved through an owned temp file", (await service.LoadAsync(project.Id, TestContext.CancellationToken)).Name);
    }

    [TestMethod]
    public async Task ProjectOperations_RejectEmptyGuidAndBlankRename()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Rename", TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.LoadAsync(Guid.Empty, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.SaveAsync(new ProjectDocument(), TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RenameAsync(project.Id, "  \t", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesOnlyProjectDirectoryAndNeverExternalMedia()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var externalMediaPath = Path.Combine(directory.Path, "external-source.mp4");
        await File.WriteAllTextAsync(externalMediaPath, "external media", TestContext.CancellationToken);
        var project = await service.CreateAsync("Delete me", TestContext.CancellationToken);
        project.Assets.Add(new ProjectAsset { Id = Guid.NewGuid(), SourcePath = externalMediaPath });
        await service.SaveAsync(project, TestContext.CancellationToken);
        var cacheFile = Path.Combine(service.GetCachePath(project.Id), "cache-entry.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        await File.WriteAllTextAsync(cacheFile, "cache", TestContext.CancellationToken);

        await service.DeleteAsync(project.Id, TestContext.CancellationToken);

        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Path, "Projects", project.Id.ToString("D"))));
        Assert.IsTrue(File.Exists(externalMediaPath));
        Assert.AreEqual("external media", await File.ReadAllTextAsync(externalMediaPath, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DeleteAsync_NestedCacheDirectoryLinkPreservesExternalTarget()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Linked cache", TestContext.CancellationToken);
        var externalDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "ExternalCache"));
        var externalPath = Path.Combine(externalDirectory.FullName, "preserve.txt");
        await File.WriteAllTextAsync(externalPath, "preserve external cache", TestContext.CancellationToken);
        var linkPath = Path.Combine(service.GetCachePath(project.Id), "linked");
        Directory.CreateSymbolicLink(linkPath, externalDirectory.FullName);

        await service.DeleteAsync(project.Id, TestContext.CancellationToken);

        Assert.IsFalse(Directory.Exists(service.GetProjectPath(project.Id)));
        Assert.AreEqual("preserve external cache", await File.ReadAllTextAsync(externalPath, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DeleteAsync_LegacyManagedSourceReferenceRefusesDeletionAndPreservesSource()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Preserve source", TestContext.CancellationToken);
        var projectPath = service.GetProjectPath(project.Id);
        var sourcePath = Path.Combine(projectPath, "cache", "source.png");
        await File.WriteAllTextAsync(sourcePath, "imported source", TestContext.CancellationToken);
        project.Assets.Add(new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Image,
            SourcePath = sourcePath,
            FileName = "source.png"
        });

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.SaveAsync(project, TestContext.CancellationToken));
        Assert.AreEqual("imported source", await File.ReadAllTextAsync(sourcePath, TestContext.CancellationToken));

        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "project.json"),
            System.Text.Json.JsonSerializer.Serialize(project), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.DeleteAsync(project.Id, TestContext.CancellationToken));

        Assert.IsTrue(Directory.Exists(projectPath));
        Assert.AreEqual("imported source", await File.ReadAllTextAsync(sourcePath, TestContext.CancellationToken));
    }

    private sealed partial class TemporaryDirectory : IDisposable
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

    public TestContext TestContext { get; set; }
}
