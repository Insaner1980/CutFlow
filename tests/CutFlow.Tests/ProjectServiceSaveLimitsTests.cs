using System.Text.Json;
using CutFlow.Models;
using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

public sealed partial class ProjectServiceTests
{
    [TestMethod]
    [DataRow("text")]
    [DataRow("count")]
    [DataRow("json")]
    public async Task SaveAsync_ExceedingLoadLimitsPreservesPreviousSave(string limit)
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Reopenable", TestContext.CancellationToken);
        var path = Path.Combine(service.GetProjectPath(project.Id), "project.json");
        var previous = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);
        var modified = project.ModifiedAt;
        var count = limit == "count" ? ProjectService.MaximumTimelineItemCount + 1 : limit == "json" ? 5_000 : 1;
        var length = limit == "text" ? ProjectService.MaximumPersistedTextLength + 1 : limit == "json" ? ProjectService.MaximumPersistedTextLength : 1;
        for (var i = 0; i < count; i++)
        {
            project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = new string('x', length) });
        }

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.SaveAsync(project, TestContext.CancellationToken));

        CollectionAssert.AreEqual(previous, await File.ReadAllBytesAsync(path, TestContext.CancellationToken));
        Assert.AreEqual(modified, project.ModifiedAt);
        Assert.IsEmpty(Directory.EnumerateFiles(service.GetProjectPath(project.Id), "project.json.*.tmp"));
        var loaded = await service.LoadAsync(project.Id, TestContext.CancellationToken);
        Assert.AreEqual("Reopenable", loaded.Name);
        Assert.IsEmpty(loaded.TextItems);
    }

    [TestMethod]
    public async Task SaveAsync_TextAtSupportedLimitCanBeReopenedWithoutLoss()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Boundary", TestContext.CancellationToken);
        var text = new string('x', ProjectService.MaximumPersistedTextLength);
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = text });

        await service.SaveAsync(project, TestContext.CancellationToken);
        var loaded = await service.LoadAsync(project.Id, TestContext.CancellationToken);

        Assert.AreEqual(text, loaded.TextItems.Single().Text);
    }
    [TestMethod]
    [DataRow(4_097, 1)]
    [DataRow(1, 10_001)]
    [DataRow(16 * 1024 * 1024 + 1, 1)]
    public async Task LegacySchema1_BeyondNewLimits_RemainsListedAndRoundTrips(int textLength, int itemCount)
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = ProjectDocument.CreateNew("Legacy", DateTimeOffset.UtcNow);
        project.SchemaVersion = ProjectDocument.LegacySchemaVersion;
        var text = new string('x', textLength);
        for (var i = 0; i < itemCount; i++)
        {
            project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = text });
        }

        // Simulate a file written by the previous application's unrestricted serializer.
        Directory.CreateDirectory(service.GetProjectPath(project.Id));
        var path = Path.Combine(service.GetProjectPath(project.Id), "project.json");
        var original = JsonSerializer.Serialize(project, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await File.WriteAllTextAsync(path, original, TestContext.CancellationToken);
        var loaded = await service.LoadAsync(project.Id, TestContext.CancellationToken);
        Assert.AreEqual(itemCount, loaded.TextItems.Count);
        Assert.AreEqual(text, loaded.TextItems[0].Text);
        Assert.AreEqual(project.Id, (await service.ListAsync(TestContext.CancellationToken)).Single().Id);
        Assert.AreEqual(original, await File.ReadAllTextAsync(path, TestContext.CancellationToken));

        loaded.Name = "Edited legacy";
        await service.SaveAsync(loaded, TestContext.CancellationToken);
        var reopened = await service.LoadAsync(project.Id, TestContext.CancellationToken);
        Assert.AreEqual(ProjectDocument.LegacySchemaVersion, reopened.SchemaVersion);
        Assert.AreEqual("Edited legacy", reopened.Name);
        Assert.AreEqual(itemCount, reopened.TextItems.Count);
        Assert.AreEqual(text, reopened.TextItems[0].Text);
    }

}
