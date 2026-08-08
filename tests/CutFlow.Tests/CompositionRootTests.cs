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

        StringAssert.Contains(
            previewPane,
            "TextStyle.ParseBrush(opaqueArgb, ProjectSettings.DefaultBackgroundColor)");
        Assert.IsFalse(previewPane.Contains("private static SolidColorBrush ParseBrush", StringComparison.Ordinal));
    }

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

        CollectionAssert.AreEqual(new[] { "MainWindow.xaml.cs" }, constructionSites);
        StringAssert.Contains(
            mainWindow,
            "new EditorView(editorViewModel, _projectService, _mediaImportService, _logService, _appSettings)");
        StringAssert.Contains(
            editorView,
            "_logService = logService ?? throw new ArgumentNullException(nameof(logService));");
        StringAssert.Contains(
            editorExport,
            "new ExportService(_compositionService, _textOverlayRenderer, _logService)");
        Assert.IsFalse(exportService.Contains("new SimpleLogService(", StringComparison.Ordinal));
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
