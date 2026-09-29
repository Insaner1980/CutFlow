using CutFlow.Utilities;
using System.Runtime.InteropServices;

namespace CutFlow.Tests;

[TestClass]
public sealed class ExceptionPolicyTests
{
    [TestMethod]
    public void IsFatal_RecognizesResourceAndCorruptedStateFailures()
    {
        Exception[] fatalExceptions =
        [
            new OutOfMemoryException(),
            new InsufficientMemoryException(),
            new StackOverflowException(),
            new AccessViolationException(),
            new SEHException(),
            new AppDomainUnloadedException(),
            new BadImageFormatException(),
            new InvalidProgramException()
        ];

        Assert.IsTrue(fatalExceptions.All(ExceptionPolicy.IsFatal));
    }

    [TestMethod]
    public void IsFatal_RecognizesWrappedFatalFailure()
    {
        var wrapped = new AggregateException(
            new IOException(),
            new InvalidOperationException("wrapper", new OutOfMemoryException()));

        Assert.IsTrue(ExceptionPolicy.IsFatal(wrapped));
    }

    [TestMethod]
    public void IsFatal_LeavesExpectedRecoverableFailuresCatchable()
    {
        Exception[] recoverableExceptions =
        [
            new IOException(),
            new UnauthorizedAccessException(),
            new InvalidDataException(),
            new ArgumentException(),
            new NotSupportedException(),
            new InvalidOperationException(),
            new COMException(),
            new OperationCanceledException()
        ];

        Assert.IsFalse(recoverableExceptions.Any(ExceptionPolicy.IsFatal));
    }

    [TestMethod]
    public void RecoveringCatchBoundaries_DoNotCatchFatalFailures()
    {
        var root = FindRepositoryRoot();
        var mainWindow = Read(root, "MainWindow.xaml.cs");
        var logService = Read(root, "Services", "SimpleLogService.cs");
        var projectService = Read(root, "Services", "ProjectService.cs");
        var saveCoordinator = Read(root, "Utilities", "DebouncedSaveCoordinator.cs");
        var mainViewModel = Read(root, "ViewModels", "MainViewModel.cs");

        Assert.AreEqual(
            3,
            Count(mainWindow, "catch (Exception exception) when (!ExceptionPolicy.IsFatal(exception))"));
        Assert.Contains(
            "catch (Exception recoveryException) when (!ExceptionPolicy.IsFatal(recoveryException))",
            mainWindow);
        Assert.Contains(
            "catch (Exception exception) when (!ExceptionPolicy.IsFatal(exception))",
            logService);
        Assert.AreEqual(
            2,
            Count(projectService, "catch (Exception cleanupException) when (!ExceptionPolicy.IsFatal(cleanupException))"));
        Assert.Contains(
            "catch (Exception exception) when (!ExceptionPolicy.IsFatal(exception))",
            saveCoordinator);
        Assert.Contains(
            "catch (Exception rollbackException) when (!ExceptionPolicy.IsFatal(rollbackException))",
            mainViewModel);
    }

    private static string Read(string root, params string[] path) =>
        File.ReadAllText(Path.Combine([root, "src", "CutFlow", .. path]));

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

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
