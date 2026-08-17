using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class CompositionRootTests
{
    [TestMethod]
    public void PreviewBackgroundColor_UsesSharedTextStyleParser()
    {
        var repositoryRoot = FindRepositoryRoot();
        var previewPane = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CutFlow",
            "Controls",
            "PreviewPane.xaml.cs"));

        Assert.Contains(
            "TextStyle.ParseBrush(opaqueArgb, ProjectSettings.DefaultBackgroundColor)",
            previewPane);
        Assert.IsFalse(previewPane.Contains("private static SolidColorBrush ParseBrush", StringComparison.Ordinal));
    }

    private static readonly string[] expected = new[] { "MainWindow.xaml.cs" };

    [TestMethod]
    public void LoggingService_IsConstructedOnceAndSharedWithEditorAndExport()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src", "CutFlow");
        var mainWindow = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs"));
        var editorView = File.ReadAllText(Path.Combine(sourceRoot, "Views", "EditorView.xaml.cs"));
        var editorExport = File.ReadAllText(Path.Combine(sourceRoot, "Views", "EditorView.Export.cs"));
        var exportService = File.ReadAllText(Path.Combine(sourceRoot, "Services", "ExportService.cs"));

        var constructionSites = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .Where(path =>
                !path.StartsWith($"bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith($"obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(Path.Combine(sourceRoot, path)).Contains("new SimpleLogService(", StringComparison.Ordinal))
            .ToArray();

        Assert.AreSequenceEqual(expected, constructionSites);
        Assert.Contains(
            "new EditorView(editorViewModel, _projectService, _mediaImportService, _logService, _appSettings)",
            mainWindow);
        Assert.Contains(
            "_logService = logService ?? throw new ArgumentNullException(nameof(logService));",
            editorView);
        Assert.Contains(
            "new ExportService(_compositionService, _textOverlayRenderer, _logService)",
            editorExport);
        Assert.IsFalse(exportService.Contains("new SimpleLogService(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RelinkThumbnailCleanup_HappensAfterCommitAndLogsNonfatalFailures()
    {
        var editorView = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml.cs"));
        var relinkStart = editorView.IndexOf("public async Task RelinkAssetAsync", StringComparison.Ordinal);
        var relinkEnd = editorView.IndexOf("public void Dispose()", relinkStart, StringComparison.Ordinal);
        var relink = editorView[relinkStart..relinkEnd];
        var commit = relink.IndexOf("ViewModel.ApplyRelinkedAsset(candidate)", StringComparison.Ordinal);
        var cleanup = relink.IndexOf("TryDeleteCache(oldCacheReference);", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, commit);
        Assert.IsGreaterThan(commit, cleanup);

        var cleanupStart = editorView.IndexOf("private void TryDeleteCache", StringComparison.Ordinal);
        var cleanupEnd = editorView.IndexOf("private void CommitImportResults", cleanupStart, StringComparison.Ordinal);
        var cleanupMethod = editorView[cleanupStart..cleanupEnd];

        Assert.Contains("ArgumentException", cleanupMethod);
        Assert.Contains("NotSupportedException", cleanupMethod);
        Assert.Contains("Thumbnail cache cleanup failed:", cleanupMethod);
        Assert.Contains("_logService.TryWriteAsync", cleanupMethod);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CutFlow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the CutFlow repository root.");
    }
}
