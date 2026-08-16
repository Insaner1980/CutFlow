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
        var start = source.IndexOf("if (controlDown && e.Key == VirtualKey.N)", StringComparison.Ordinal);
        var end = source.IndexOf("var isSingleActionShortcut", start, StringComparison.Ordinal);
        var route = source[start..end];

        var handled = route.IndexOf("e.Handled = true;", StringComparison.Ordinal);
        var save = route.IndexOf("await SaveAsync()", StringComparison.Ordinal);
        var request = route.IndexOf("NewProjectRequested?.Invoke", StringComparison.Ordinal);
        Assert.IsTrue(handled >= 0 && save > handled && request > save);
        StringAssert.Contains(source, "e.KeyStatus.WasKeyDown");
    }

    [TestMethod]
    public void TimelineMutationShortcuts_BubbleToTheSingleEditorRoute()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Controls",
            "TimelineControl.xaml.cs"));
        var start = source.IndexOf("private void TimelineControl_KeyDown", StringComparison.Ordinal);
        var end = source.IndexOf("private void RequestSeek", start, StringComparison.Ordinal);
        var route = source[start..end];

        Assert.DoesNotContain("TimelineEditKind.SplitVideo", route);
        Assert.DoesNotContain("TimelineEditKind.Duplicate", route);
        Assert.DoesNotContain("TimelineEditKind.Delete", route);
        Assert.DoesNotContain("TimelineEditKind.Undo", route);
        Assert.DoesNotContain("TimelineEditKind.Redo", route);
    }

    [TestMethod]
    public void TimelineCardContextMenu_ReevaluatesCurrentGuidAndSupportsKeyboardFocus()
    {
        var root = FindRepositoryRoot();
        var timeline = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CutFlow",
            "Controls",
            "TimelineControl.xaml.cs"));
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml.cs"));

        StringAssert.Contains(timeline, "private MenuFlyout CreateContextMenu(Guid itemId, EditorSelectionKind kind)");
        StringAssert.Contains(timeline, "menu.Opening +=");
        StringAssert.Contains(timeline, "TimelineContextCommands.Resolve(_project, itemId, _trackLocks, _playheadMilliseconds)");
        StringAssert.Contains(timeline, "IsTabStop = true");
        StringAssert.Contains(editor, "DeleteTimelineItemWithGuidance(commandItemId);");
        StringAssert.Contains(editor, "ShowTimelineSourceInExplorer(commandItemId);");
    }

    [TestMethod]
    public void EditorSingleActionShortcuts_GateKeyRepeatBeforeExecution()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml.cs"));
        var start = source.IndexOf("private async void EditorRoot_KeyDown", StringComparison.Ordinal);
        var end = source.IndexOf("private bool IsEditableControlFocused", start, StringComparison.Ordinal);
        var route = source[start..end];

        var repeatGate = route.IndexOf("if (e.KeyStatus.WasKeyDown)", StringComparison.Ordinal);
        var save = route.IndexOf("await SaveAsync()", repeatGate, StringComparison.Ordinal);
        var undo = route.IndexOf("UndoWithGuidance();", StringComparison.Ordinal);
        var redo = route.IndexOf("RedoWithGuidance();", StringComparison.Ordinal);
        var split = route.IndexOf("SplitSelectionWithGuidance();", StringComparison.Ordinal);
        var duplicate = route.IndexOf("DuplicateSelectionWithGuidance();", StringComparison.Ordinal);
        var delete = route.IndexOf("DeleteSelectionWithGuidance();", StringComparison.Ordinal);

        Assert.IsTrue(repeatGate >= 0);
        Assert.IsTrue(save > repeatGate);
        Assert.IsTrue(undo > repeatGate);
        Assert.IsTrue(redo > repeatGate);
        Assert.IsTrue(split > repeatGate);
        Assert.IsTrue(duplicate > repeatGate);
        Assert.IsTrue(delete > repeatGate);
    }

    [TestMethod]
    public void EditorShortcutFailures_SurfaceGuidanceFromTheCommandResult()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml.cs"));

        StringAssert.Contains(source, "if (!ViewModel.CanUndo)");
        StringAssert.Contains(source, "if (!ViewModel.CanRedo)");
        StringAssert.Contains(source, "if (!ViewModel.SplitSelection())");
        StringAssert.Contains(source, "if (!ViewModel.DuplicateSelection())");
        StringAssert.Contains(source, "if (!ViewModel.DeleteSelection())");
        StringAssert.Contains(source, "Select a V1 item to split.");
        StringAssert.Contains(source, "Select a V1, T1, or A1 item to duplicate.");
    }

    [TestMethod]
    public void MediaCardActivation_IsHandledBeforeAddabilityAndEditorPlaybackRouting()
    {
        var root = FindRepositoryRoot();
        var mediaPanel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CutFlow",
            "Controls",
            "MediaPanel.xaml.cs"));
        var mediaStart = mediaPanel.IndexOf("private void AssetGrid_KeyDown", StringComparison.Ordinal);
        var mediaEnd = mediaPanel.IndexOf("private void AssetCard_DragStarting", mediaStart, StringComparison.Ordinal);
        var mediaRoute = mediaPanel[mediaStart..mediaEnd];
        var handled = mediaRoute.IndexOf("e.Handled = true;", StringComparison.Ordinal);
        var repeatGate = mediaRoute.IndexOf("if (e.KeyStatus.WasKeyDown)", StringComparison.Ordinal);
        var addability = mediaRoute.IndexOf("MediaAssetActivationPolicy.ShouldActivate", StringComparison.Ordinal);
        var add = mediaRoute.IndexOf("AddAssetRequested?.Invoke", StringComparison.Ordinal);

        Assert.IsTrue(handled >= 0 && repeatGate > handled && addability > repeatGate && add > addability);

        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml.cs"));
        var editorStart = editor.IndexOf("private async void EditorRoot_KeyDown", StringComparison.Ordinal);
        var editorEnd = editor.IndexOf("private bool IsEditableControlFocused", editorStart, StringComparison.Ordinal);
        var editorRoute = editor[editorStart..editorEnd];
        var handledGate = editorRoute.IndexOf("if (e.Handled || editableControlFocused)", StringComparison.Ordinal);
        var spaceStart = editorRoute.IndexOf("case VirtualKey.Space:", StringComparison.Ordinal);
        var spaceEnd = editorRoute.IndexOf("case VirtualKey.Escape:", spaceStart, StringComparison.Ordinal);
        var spaceRoute = editorRoute[spaceStart..spaceEnd];
        var spaceHandled = spaceRoute.IndexOf("e.Handled = true;", StringComparison.Ordinal);
        var spaceRepeatGate = spaceRoute.IndexOf("if (!e.KeyStatus.WasKeyDown)", StringComparison.Ordinal);
        var playback = spaceRoute.IndexOf("Preview.PlayPause();", StringComparison.Ordinal);

        Assert.IsTrue(handledGate >= 0 && spaceStart > handledGate);
        Assert.IsTrue(spaceHandled >= 0 && spaceRepeatGate > spaceHandled && playback > spaceRepeatGate);
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
        var newProjectStart = source.IndexOf("if (controlDown && e.Key == VirtualKey.N)", StringComparison.Ordinal);
        var newProjectEnd = source.IndexOf("var isSingleActionShortcut", newProjectStart, StringComparison.Ordinal);
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
