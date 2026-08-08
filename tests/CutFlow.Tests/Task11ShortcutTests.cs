using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task11ShortcutTests
{
    [DataTestMethod]
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
        Assert.IsTrue(handled >= 0 && save > handled);
        StringAssert.Contains(source, "e.KeyStatus.WasKeyDown");
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
