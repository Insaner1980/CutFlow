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
}
