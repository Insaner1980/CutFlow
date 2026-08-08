using System.Text.Json;
using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class ProjectSerializationTests
{
    [TestMethod]
    public async Task SaveAndLoadAsync_RoundTripsDocumentAndSchemaVersion()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var createdAt = new DateTimeOffset(2026, 8, 4, 12, 30, 0, TimeSpan.Zero);
        var project = ProjectDocument.CreateNew("Summer edit", createdAt);
        project.Settings.ApplyAspectRatio(AspectRatioPreset.Portrait9By16);
        project.Settings.VideoTrackVisible = false;
        project.Settings.TextTrackVisible = false;
        project.Settings.AudioTrackMuted = true;
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            SourcePath = @"C:\Media\summer.mp4",
            FileName = "summer.mp4",
            DurationMilliseconds = 12_000,
            Width = 1920,
            Height = 1080,
            FileSize = 123_456,
            LastWriteUtc = new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            ThumbnailCachePath = @"cache\thumbnails\summer.jpg",
            IsMissing = true
        };
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            SourceInMilliseconds = 100,
            SourceOutMilliseconds = 4_200
        });
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            StartMilliseconds = 250,
            SourceOutMilliseconds = 2_000
        });
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 300, Text = "Hello" });

        await service.SaveAsync(project);
        var loaded = await service.LoadAsync(project.Id);
        var json = await File.ReadAllTextAsync(Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "project.json"));

        Assert.AreEqual(ProjectDocument.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.AreEqual(project.Id, loaded.Id);
        Assert.AreEqual(project.Name, loaded.Name);
        Assert.AreEqual(1080, loaded.Settings.Width);
        Assert.AreEqual(1920, loaded.Settings.Height);
        Assert.IsFalse(loaded.Settings.VideoTrackVisible);
        Assert.IsFalse(loaded.Settings.TextTrackVisible);
        Assert.IsTrue(loaded.Settings.AudioTrackMuted);
        Assert.AreEqual(1, loaded.Assets.Count);
        Assert.AreEqual(asset.SourcePath, loaded.Assets[0].SourcePath);
        Assert.AreEqual(asset.FileName, loaded.Assets[0].FileName);
        Assert.AreEqual(asset.Width, loaded.Assets[0].Width);
        Assert.AreEqual(asset.Height, loaded.Assets[0].Height);
        Assert.AreEqual(asset.FileSize, loaded.Assets[0].FileSize);
        Assert.AreEqual(asset.LastWriteUtc, loaded.Assets[0].LastWriteUtc);
        Assert.AreEqual(asset.ThumbnailCachePath, loaded.Assets[0].ThumbnailCachePath);
        Assert.IsTrue(loaded.Assets[0].IsMissing);
        Assert.AreEqual(1, loaded.VideoItems.Count);
        Assert.AreEqual(1, loaded.AudioItems.Count);
        Assert.AreEqual(1, loaded.TextItems.Count);
        StringAssert.Contains(json, "\"schemaVersion\": 1");
    }

    [TestMethod]
    public async Task LoadAsync_WhenOptionalPropertiesAreAbsent_UsesModelDefaults()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Older project", "createdAt": "2026-08-04T12:00:00+00:00", "modifiedAt": "2026-08-04T12:00:00+00:00" }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);

        Assert.IsNotNull(loaded.Settings);
        Assert.AreEqual(1920, loaded.Settings.Width);
        Assert.IsTrue(loaded.Settings.VideoTrackVisible);
        Assert.IsTrue(loaded.Settings.TextTrackVisible);
        Assert.IsFalse(loaded.Settings.AudioTrackMuted);
        Assert.HasCount(0, loaded.Assets);
        Assert.HasCount(0, loaded.VideoItems);
        Assert.HasCount(0, loaded.AudioItems);
        Assert.HasCount(0, loaded.TextItems);
    }

    [TestMethod]
    public async Task LoadAsync_WhenAssetMetadataIsAbsent_UsesBackwardCompatibleDefaults()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Older asset", "createdAt": "2026-08-04T12:00:00+00:00", "modifiedAt": "2026-08-04T12:00:00+00:00", "assets": [{ "id": "{{assetId}}", "kind": "Video", "sourcePath": "C:\\Media\\old.mp4", "durationMilliseconds": 1000, "isMissing": false }] }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);
        var asset = loaded.Assets.Single();

        Assert.AreEqual(string.Empty, asset.FileName);
        Assert.AreEqual(0, asset.Width);
        Assert.AreEqual(0, asset.Height);
        Assert.AreEqual(0UL, asset.FileSize);
        Assert.AreEqual(default, asset.LastWriteUtc);
        Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
    }

    [TestMethod]
    public async Task LoadAsync_WhenSettingsAndListsAreExplicitJsonNull_NormalizesSafeDefaults()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Null values", "createdAt": "2026-08-04T12:00:00+00:00", "modifiedAt": "2026-08-04T12:00:00+00:00", "settings": null, "assets": null, "videoItems": null, "audioItems": null, "textItems": null }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);

        Assert.IsNotNull(loaded.Settings);
        Assert.HasCount(0, loaded.Assets);
        Assert.HasCount(0, loaded.VideoItems);
        Assert.HasCount(0, loaded.AudioItems);
        Assert.HasCount(0, loaded.TextItems);
    }

    [DataTestMethod]
    [DataRow("assets")]
    [DataRow("videoItems")]
    [DataRow("audioItems")]
    [DataRow("textItems")]
    public async Task LoadAsync_WhenCollectionContainsNullElement_ThrowsInvalidDataException(string propertyName)
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Null item", "{{propertyName}}": [null] }
            """);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => new ProjectService(directory.Path).LoadAsync(id));
    }

    [TestMethod]
    public async Task LoadAsync_WhenReferenceStringsAreExplicitJsonNull_NormalizesModelDefaults()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": null,
              "assets": [
                {
                  "id": "{{assetId}}",
                  "kind": "Video",
                  "sourcePath": null,
                  "fileName": null,
                  "thumbnailCachePath": null
                }
              ]
            }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);
        var asset = loaded.Assets.Single();

        Assert.AreEqual(string.Empty, loaded.Name);
        Assert.AreEqual(string.Empty, asset.SourcePath);
        Assert.AreEqual(string.Empty, asset.FileName);
        Assert.AreEqual(string.Empty, asset.ThumbnailCachePath);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_NormalizesTimelineAndFontBoundaries()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = ProjectDocument.CreateNew("Unsafe text", DateTimeOffset.UnixEpoch);
        project.TextItems.AddRange(
        [
            new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = -1_000, DurationMilliseconds = 1, FontSize = 1 },
            new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = long.MaxValue, DurationMilliseconds = long.MaxValue, FontSize = 401 },
            new TextTimelineItem { Id = Guid.NewGuid(), FontSize = double.NaN },
            new TextTimelineItem { Id = Guid.NewGuid(), FontSize = double.PositiveInfinity }
        ]);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            SourceInMilliseconds = long.MaxValue,
            SourceOutMilliseconds = long.MaxValue,
            DurationMilliseconds = long.MaxValue
        });
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            StartMilliseconds = long.MaxValue,
            SourceInMilliseconds = long.MaxValue,
            SourceOutMilliseconds = long.MaxValue
        });

        await service.SaveAsync(project);
        var loaded = await service.LoadAsync(project.Id);
        var items = loaded.TextItems;

        Assert.AreEqual(0L, items[0].StartMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, items[0].DurationMilliseconds);
        Assert.AreEqual(8d, items[0].FontSize);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds, items[1].StartMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, items[1].DurationMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, items[1].StartMilliseconds + items[1].DurationMilliseconds);
        Assert.AreEqual(400d, items[1].FontSize);
        Assert.AreEqual(TextTimelineItem.DefaultFontSize, items[2].FontSize);
        Assert.AreEqual(TextTimelineItem.DefaultFontSize, items[3].FontSize);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds, loaded.VideoItems[0].SourceInMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, loaded.VideoItems[0].SourceOutMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, loaded.VideoItems[0].DurationMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds, loaded.AudioItems[0].StartMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds, loaded.AudioItems[0].SourceInMilliseconds);
        Assert.AreEqual(ProjectDocument.MaximumTimelineDurationMilliseconds, loaded.AudioItems[0].SourceOutMilliseconds);
    }

    [TestMethod]
    public async Task SaveAsync_WhenSerializationFails_PreservesPreviousJsonAndLeavesNoTempFile()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Safe project", DateTimeOffset.UnixEpoch);
        var originalModifiedAt = new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero);
        project.ModifiedAt = originalModifiedAt;
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", project.Id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        var temporaryPath = Path.Combine(projectDirectory.FullName, "project.json.tmp");
        const string originalJson = "{\"schemaVersion\":1,\"name\":\"previous\"}";
        await File.WriteAllTextAsync(projectPath, originalJson);
        await File.WriteAllTextAsync(temporaryPath, "stale temporary data");
        var service = new ProjectService(directory.Path, _ => throw new InvalidOperationException("Deterministic serializer failure"));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.SaveAsync(project));

        Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
        Assert.IsFalse(File.Exists(temporaryPath));
    }

    [TestMethod]
    public async Task SaveAsync_WhenPreCanceled_PreservesModifiedAtExistingJsonAndLeavesNoTempFile()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Canceled save", DateTimeOffset.UnixEpoch);
        var originalModifiedAt = new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero);
        project.ModifiedAt = originalModifiedAt;
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", project.Id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        const string originalJson = "{\"schemaVersion\":1,\"name\":\"previous\"}";
        await File.WriteAllTextAsync(projectPath, originalJson);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => new ProjectService(directory.Path).SaveAsync(project, cancellation.Token));

        Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
        Assert.IsFalse(File.Exists(projectPath + ".tmp"));
    }

    [TestMethod]
    public async Task SaveAsync_WhenDestinationIsLocked_PreservesModifiedAtExistingJsonAndLeavesNoTempFile()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = ProjectDocument.CreateNew("Locked destination", DateTimeOffset.UnixEpoch);
        await service.SaveAsync(project);
        var projectPath = Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "project.json");
        var originalJson = await File.ReadAllTextAsync(projectPath);
        var originalModifiedAt = project.ModifiedAt;
        project.Name = "Changed while locked";

        await using (new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsExceptionAsync<IOException>(() => service.SaveAsync(project));
        }

        Assert.AreEqual(originalModifiedAt, project.ModifiedAt);
        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
        Assert.IsFalse(File.Exists(projectPath + ".tmp"));
    }

    [TestMethod]
    public async Task LoadAsync_WhenSchemaVersionIsMissing_ThrowsInvalidDataException()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "id": "{{id}}", "name": "Missing schema", "createdAt": "2026-08-04T12:00:00+00:00", "modifiedAt": "2026-08-04T12:00:00+00:00" }
            """);

        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => new ProjectService(directory.Path).LoadAsync(id));
    }

    [TestMethod]
    public async Task SaveAsync_WhenSchemaVersionIsInvalid_PreservesExistingJsonAndDoesNotCreateTempFile()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Invalid schema", DateTimeOffset.UnixEpoch);
        project.SchemaVersion = ProjectDocument.CurrentSchemaVersion + 1;
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", project.Id.ToString("D")));
        var projectPath = Path.Combine(projectDirectory.FullName, "project.json");
        const string originalJson = "{\"schemaVersion\":1,\"name\":\"previous\"}";
        await File.WriteAllTextAsync(projectPath, originalJson);

        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => new ProjectService(directory.Path).SaveAsync(project));

        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
        Assert.IsFalse(File.Exists(projectPath + ".tmp"));
    }

    [TestMethod]
    public async Task LoadAsync_WhenJsonIsMalformed_ThrowsInvalidDataException()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), "{ invalid JSON");

        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => new ProjectService(directory.Path).LoadAsync(id));
    }

    [TestMethod]
    public async Task SettingsAndLogServices_PersistOnlyBoundedCallerSuppliedContent()
    {
        using var directory = new TemporaryDirectory();
        var settingsService = new SettingsService(directory.Path);
        var settings = new AppSettings { WindowWidth = 1440, WindowHeight = 900, TimelineZoom = 2.5, LoopPlayback = true };
        await settingsService.SaveAsync(settings);

        var loadedSettings = await settingsService.LoadAsync();
        Assert.AreEqual(1440d, loadedSettings.WindowWidth);
        Assert.IsTrue(loadedSettings.LoopPlayback);

        var sourceFile = Path.Combine(directory.Path, "external-media.txt");
        const string sourceContents = "media bytes must not become a log entry";
        await File.WriteAllTextAsync(sourceFile, sourceContents);
        var log = new SimpleLogService(directory.Path, maximumEntries: 2, maximumMessageLength: 24);
        await log.WriteAsync("first technical failure");
        await log.WriteAsync("second technical failure");
        await log.WriteAsync("third technical failure");

        var logText = await File.ReadAllTextAsync(Path.Combine(directory.Path, "cutflow.log"));
        Assert.AreEqual(2, File.ReadLines(Path.Combine(directory.Path, "cutflow.log")).Count());
        Assert.IsFalse(logText.Contains(sourceContents, StringComparison.Ordinal));
        Assert.IsFalse(logText.Contains("first technical failure", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SettingsService_WhenJsonIsMalformed_ThrowsInvalidDataException()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "settings.json"), "{ invalid JSON");

        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => new SettingsService(directory.Path).LoadAsync());
    }

    [TestMethod]
    public async Task SimpleLogService_ConcurrentWritesNormalizeAndTruncateEachLine()
    {
        using var directory = new TemporaryDirectory();
        var log = new SimpleLogService(directory.Path, maximumEntries: 20, maximumMessageLength: 8);

        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => log.WriteAsync("line\r\none and more")));

        var lines = await File.ReadAllLinesAsync(Path.Combine(directory.Path, "cutflow.log"));
        Assert.AreEqual(12, lines.Length);
        Assert.IsTrue(lines.All(line => line.EndsWith("line one", StringComparison.Ordinal)));
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
