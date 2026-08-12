using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task11ShortcutTests
{
    [TestMethod]
    [DataRow(false, false, true, VirtualKey.N, false, true)]
    [DataRow(false, false, true, VirtualKey.N, true, false)]
    [DataRow(true, false, true, VirtualKey.N, false, false)]
    [DataRow(false, true, true, VirtualKey.N, false, false)]
    [DataRow(false, false, false, VirtualKey.N, false, false)]
    [DataRow(false, false, true, VirtualKey.S, false, false)]
    public void CreateNewProjectShortcut_OnlyRoutesUnhandledCtrlNOutsideEditableControls(
        bool alreadyHandled,
        bool editableControlFocused,
        bool controlDown,
        VirtualKey key,
        bool keyWasDown,
        bool expected)
    {
        Assert.AreEqual(expected, GlobalShortcutRouter.ShouldCreateNewProject(
            alreadyHandled,
            editableControlFocused,
            controlDown,
            key,
            keyWasDown));
    }

    [TestMethod]
    public void EditorNewProjectShortcut_IsHandledBeforeItsAsynchronousSave()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml.cs"));
        var start = source.IndexOf("case VirtualKey.N:", StringComparison.Ordinal);
        var end = source.IndexOf("case VirtualKey.Z:", start, StringComparison.Ordinal);
        var route = source[start..end];

        var handled = route.IndexOf("e.Handled = true;", StringComparison.Ordinal);
        var save = route.IndexOf("await SaveAsync()", StringComparison.Ordinal);
        var request = route.IndexOf("NewProjectRequested?.Invoke", StringComparison.Ordinal);
        Assert.IsTrue(handled >= 0 && save > handled && request > save);
        StringAssert.Contains(source, "e.KeyStatus.WasKeyDown");
    }

    [TestMethod]
    public void EditorNavigation_RechecksExportStateAfterAsynchronousSave()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml.cs"));
        var backStart = source.IndexOf("private async void Back_Click", StringComparison.Ordinal);
        var backEnd = source.IndexOf("private void ProjectNameBox_KeyDown", backStart, StringComparison.Ordinal);
        var backRoute = source[backStart..backEnd];
        var newProjectStart = source.IndexOf("case VirtualKey.N:", StringComparison.Ordinal);
        var newProjectEnd = source.IndexOf("case VirtualKey.Z:", newProjectStart, StringComparison.Ordinal);
        var newProjectRoute = source[newProjectStart..newProjectEnd];

        AssertStateIsCheckedBeforeAndAfterSave(backRoute, "if (_isRendering)");
        AssertStateIsCheckedBeforeAndAfterSave(newProjectRoute, "if (_isExporting)");
    }

    private static void AssertStateIsCheckedBeforeAndAfterSave(string route, string check)
    {
        var save = route.IndexOf("await SaveAsync()", StringComparison.Ordinal);
        var firstCheck = route.IndexOf(check, StringComparison.Ordinal);
        var secondCheck = route.IndexOf(check, firstCheck + 1, StringComparison.Ordinal);

        Assert.IsTrue(firstCheck >= 0 && save > firstCheck && secondCheck > save);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "CutFlow.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
