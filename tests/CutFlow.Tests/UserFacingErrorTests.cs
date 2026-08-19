using CutFlow.Services;

namespace CutFlow.Tests;

[TestClass]
public sealed class UserFacingErrorTests
{
    [TestMethod]
    public void RoutineUiBoundaries_DoNotPublishRawExceptionMessages()
    {
        var root = FindRepositoryRoot();
        string[] relativePaths =
        [
            @"MainWindow.xaml.cs",
            @"Views\HomeView.xaml.cs",
            @"Views\EditorView.xaml.cs",
            @"ViewModels\HomeViewModel.cs",
            @"ViewModels\MainViewModel.cs"
        ];

        foreach (var relativePath in relativePaths)
        {
            var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", relativePath));
            Assert.IsFalse(
                source.Contains("exception.Message", StringComparison.Ordinal),
                $"{relativePath} publishes a raw exception message.");
        }
    }

    [TestMethod]
    public void MediaFailureReason_IsActionableWithoutExposingNativeDetails()
    {
        const string technicalDetail = @"System.Runtime.InteropServices.COMException at C:\Packages\CutFlow\project.json; HRESULT=0x80004005";
        Exception[] exceptions =
        [
            new InvalidDataException(technicalDetail),
            new NotSupportedException(technicalDetail),
            new System.Runtime.InteropServices.COMException(technicalDetail, unchecked((int)0x80004005))
        ];

        foreach (var exception in exceptions)
        {
            var message = MediaImportService.ReadableReason(exception);
            Assert.IsFalse(message.Contains(technicalDetail, StringComparison.Ordinal));
            Assert.IsFalse(message.Contains(@"C:\Packages", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(message.Contains("HRESULT", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CutFlow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
