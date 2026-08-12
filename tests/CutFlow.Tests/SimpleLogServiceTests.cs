using CutFlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class SimpleLogServiceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WriteAsync_WhenLogIsAbsentOrEmpty_WritesOneCompleteEntry(bool createEmptyLog)
    {
        using var directory = new TemporaryDirectory();
        if (createEmptyLog)
        {
            await File.WriteAllTextAsync(directory.LogPath, string.Empty);
        }

        var log = new SimpleLogService(directory.Path);

        await log.WriteAsync("first entry");

        var lines = await File.ReadAllLinesAsync(directory.LogPath);
        Assert.HasCount(1, lines);
        AssertEntry(lines[0], "first entry");
    }

    [TestMethod]
    public async Task WriteAsync_WhenLogHasManyMalformedAndUnterminatedLines_KeepsNewestPhysicalRecords()
    {
        using var directory = new TemporaryDirectory();
        var existingLines = Enumerable.Range(0, 500).Select(index => $"malformed-{index:D3}").ToArray();
        await File.WriteAllTextAsync(directory.LogPath, string.Join(Environment.NewLine, existingLines));
        var log = new SimpleLogService(directory.Path, maximumEntries: 200);

        await log.WriteAsync("new entry\r\nwith another line");

        var lines = await File.ReadAllLinesAsync(directory.LogPath);
        Assert.HasCount(200, lines);
        CollectionAssert.AreEqual(existingLines[^199..], lines[..199]);
        AssertEntry(lines[^1], "new entry with another line");
        Assert.IsTrue((await File.ReadAllTextAsync(directory.LogPath)).EndsWith(Environment.NewLine, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task WriteAsync_FromTwoServiceInstances_KeepsBoundedCoherentFile()
    {
        using var directory = new TemporaryDirectory();
        var first = new SimpleLogService(directory.Path);
        var second = new SimpleLogService(directory.Path);
        var writes = Enumerable.Range(0, 300)
            .Select(index => (index % 2 == 0 ? first : second).WriteAsync($"entry-{index:D3}"));

        await Task.WhenAll(writes);

        var lines = await File.ReadAllLinesAsync(directory.LogPath);
        Assert.HasCount(200, lines);
        Assert.AreEqual(200, lines.Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(lines.All(
            line => TryReadEntry(line, out var message) && message.StartsWith("entry-", StringComparison.Ordinal)));
        Assert.HasCount(0, Directory.EnumerateFiles(directory.Path, "*.tmp").ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_WhenNamedSemaphoreCannotBeOpened_ReleasesInstanceSemaphore()
    {
        using var directory = new TemporaryDirectory();
        var log = new SimpleLogService(directory.Path);
        var lockName = (string)typeof(SimpleLogService)
            .GetField("_processWriteLockName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(log)!;

        using (var conflictingHandle = new EventWaitHandle(false, EventResetMode.ManualReset, lockName))
        {
            await Assert.ThrowsExactlyAsync<WaitHandleCannotBeOpenedException>(() => log.WriteAsync("blocked entry"));
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await log.WriteAsync("recovered entry", timeout.Token);

        var lines = await File.ReadAllLinesAsync(directory.LogPath);
        Assert.HasCount(1, lines);
        AssertEntry(lines[0], "recovered entry");
    }

    [TestMethod]
    public async Task WriteAsync_WhenCanceledDuringReplacement_PreservesPreviousCompleteLog()
    {
        using var directory = new TemporaryDirectory();
        var existingLines = Enumerable.Range(0, 199)
            .Select(index => $"existing-{index:D3}-{new string('x', 64 * 1024)}")
            .ToArray();
        await File.WriteAllLinesAsync(directory.LogPath, existingLines);
        var originalLog = await File.ReadAllTextAsync(directory.LogPath);
        using var cancellation = new CancellationTokenSource();
        using var watcher = new FileSystemWatcher(directory.Path, "*.tmp")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        watcher.Created += (_, _) => cancellation.Cancel();
        watcher.Changed += (_, _) => cancellation.Cancel();
        var log = new SimpleLogService(directory.Path);

        try
        {
            await log.WriteAsync("must not replace the old log", cancellation.Token);
            Assert.Fail("The write should have observed cancellation before replacing the log.");
        }
        catch (OperationCanceledException)
        {
        }

        Assert.AreEqual(originalLog, await File.ReadAllTextAsync(directory.LogPath));
        Assert.HasCount(0, Directory.EnumerateFiles(directory.Path, "*.tmp").ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_UsesUtcAndSanitizesEveryLineEndingInExceptionText()
    {
        using var directory = new TemporaryDirectory();
        var log = new SimpleLogService(directory.Path);
        var exception = new InvalidOperationException("CR\rLF\nCRLF\r\nFF\fNEL\u0085LS\u2028PS\u2029end");

        await log.WriteAsync($"Failure: {exception}");

        var lines = await File.ReadAllLinesAsync(directory.LogPath);
        Assert.HasCount(1, lines);
        var separatorIndex = lines[0].IndexOf(' ');
        Assert.IsTrue(separatorIndex > 0);
        Assert.AreEqual(TimeSpan.Zero, DateTimeOffset.Parse(lines[0][..separatorIndex]).Offset);
        Assert.AreEqual(
            "Failure: System.InvalidOperationException: CR LF CRLF FF NEL LS PS end",
            lines[0][(separatorIndex + 1)..]);
    }

    [TestMethod]
    public async Task WriteAsync_TruncatesAtDefaultLimitWithoutSplittingSurrogatePair()
    {
        using var directory = new TemporaryDirectory();
        var log = new SimpleLogService(directory.Path);

        await log.WriteAsync(new string('a', 1024) + "tail");
        await log.WriteAsync(new string('b', 1023) + "😀tail");

        var messages = (await File.ReadAllLinesAsync(directory.LogPath))
            .Select(line => line[(line.IndexOf(' ') + 1)..])
            .ToArray();
        Assert.AreEqual(new string('a', 1024), messages[0]);
        Assert.AreEqual(new string('b', 1023), messages[1]);
        Assert.IsFalse(messages.Any(message => message.Any(char.IsSurrogate)));
    }

    [TestMethod]
    public async Task TryWriteAsync_WhenMessageValidationFails_DoesNotThrowOrCreateLog()
    {
        using var directory = new TemporaryDirectory();
        var log = new SimpleLogService(directory.Path);

        await log.TryWriteAsync(null!);

        Assert.IsFalse(File.Exists(directory.LogPath));
    }

    private static void AssertEntry(string line, string expectedMessage)
    {
        Assert.IsTrue(TryReadEntry(line, out var message));
        Assert.AreEqual(expectedMessage, message);
    }

    private static bool TryReadEntry(string line, out string message)
    {
        var separatorIndex = line.IndexOf(' ');
        message = separatorIndex >= 0 ? line[(separatorIndex + 1)..] : string.Empty;
        return separatorIndex > 0 &&
            DateTimeOffset.TryParse(line[..separatorIndex], out var timestamp) &&
            timestamp.Offset == TimeSpan.Zero;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string LogPath => System.IO.Path.Combine(Path, "cutflow.log");

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
