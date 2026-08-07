using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task11ShortcutTests
{
    [DataTestMethod]
    [DataRow(false, false, true, VirtualKey.N, true)]
    [DataRow(true, false, true, VirtualKey.N, false)]
    [DataRow(false, true, true, VirtualKey.N, false)]
    [DataRow(false, false, false, VirtualKey.N, false)]
    [DataRow(false, false, true, VirtualKey.S, false)]
    public void CreateNewProjectShortcut_OnlyRoutesUnhandledCtrlNOutsideEditableControls(
        bool alreadyHandled,
        bool editableControlFocused,
        bool controlDown,
        VirtualKey key,
        bool expected)
    {
        Assert.AreEqual(expected, GlobalShortcutRouter.ShouldCreateNewProject(
            alreadyHandled,
            editableControlFocused,
            controlDown,
            key));
    }
}
