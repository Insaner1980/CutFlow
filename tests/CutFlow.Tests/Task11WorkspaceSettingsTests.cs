using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task11WorkspaceSettingsTests
{
    [TestMethod]
    public void SaveFailureNotice_SurfacesOnceUntilSettingsSaveRecovers()
    {
        var notice = new WorkspaceSettingsSaveFailureNotice();

        Assert.IsTrue(notice.Observe(DebouncedSaveState.SaveFailed));
        Assert.IsFalse(notice.Observe(DebouncedSaveState.SaveFailed));
        Assert.IsFalse(notice.Observe(DebouncedSaveState.Saving));
        Assert.IsFalse(notice.Observe(DebouncedSaveState.Saved));
        Assert.IsTrue(notice.Observe(DebouncedSaveState.SaveFailed));
    }

    [TestMethod]
    public void TimelineResize_CanceledOrLostCaptureRestoresCanonicalHeightWithoutPersisting()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml"));
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var moved = GetMethod(source, "private void TimelineResizeHandle_PointerMoved", "private void TimelineResizeHandle_PointerReleased");
        var released = GetMethod(source, "private void TimelineResizeHandle_PointerReleased", "private void TimelineResizeHandle_PointerCanceled");
        var canceled = GetMethod(source, "private void TimelineResizeHandle_PointerCanceled", "private void TimelineResizeHandle_PointerCaptureLost");
        var captureLost = GetMethod(source, "private void TimelineResizeHandle_PointerCaptureLost", "private bool CancelTimelineResize");
        var cancel = GetMethod(source, "private bool CancelTimelineResize", "private void ToolPanel_ImportRequested");

        Assert.Contains("PointerCanceled=\"TimelineResizeHandle_PointerCanceled\"", xaml);
        Assert.Contains("notify: false", moved);
        Assert.Contains("SetTimelineHeight(TimelineHeight, notify: true);", released);
        Assert.Contains("CancelTimelineResize(e.Pointer.PointerId)", canceled);
        Assert.Contains("CancelTimelineResize(e.Pointer.PointerId, releaseCapture: false)", captureLost);
        Assert.Contains("SetTimelineHeight(_workspaceSettings.TimelineHeight, notify: false);", cancel);
    }

    [TestMethod]
    public void WindowResize_ReflowsFromPreferredTimelineHeightWithoutPersistingTheClamp()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml.cs"));
        var sizeChanged = GetMethod(source, "private void EditorView_SizeChanged", "private void InspectorToggle_Click");
        var raiseChanged = GetMethod(source, "private void RaiseWorkspaceSettingsChanged", "private void TimelineResizeHandle_PointerPressed");

        var cancel = sizeChanged.IndexOf("CancelTimelineResize();", StringComparison.Ordinal);
        var reflow = sizeChanged.IndexOf("SetTimelineHeight(_workspaceSettings.TimelineHeight, notify: false);", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, cancel, "A window resize must cancel an active timeline resize.");
        Assert.IsGreaterThan(cancel, reflow, "The canonical height must be reapplied after canceling the drag.");
        Assert.Contains("SetTimelineHeight(_workspaceSettings.TimelineHeight, notify: false);", sizeChanged);
        Assert.Contains("_workspaceSettings.TimelineHeight", raiseChanged);
    }

    [TestMethod]
    public void TimelineResize_HandleKeepsEightPixelHitTargetAndVisibleDarkThemeIndicator()
    {
        var xaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml"));

        Assert.Contains("<RowDefinition Height=\"8\" />", xaml);
        Assert.Contains("x:Name=\"TimelineResizeHandle\"", xaml);
        Assert.Contains("Height=\"1\" VerticalAlignment=\"Center\" Background=\"{StaticResource TextTertiaryBrush}\"", xaml);
    }

    [TestMethod]
    public void PreviewTransport_NarrowWidthKeepsEveryControlReachableByHorizontalScrolling()
    {
        var xaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Controls",
            "PreviewPane.xaml"));

        var transportStart = xaml.IndexOf("x:Name=\"PreviewTransportScrollViewer\"", StringComparison.Ordinal);
        var transportEnd = xaml.IndexOf("</ScrollViewer>", transportStart, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, transportStart);
        Assert.IsGreaterThan(transportStart, transportEnd);

        var transport = xaml[transportStart..transportEnd];
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", transport);
        Assert.Contains("HorizontalScrollMode=\"Enabled\"", transport);
        Assert.Contains("x:Name=\"TimecodeText\"", transport);
        Assert.Contains("x:Name=\"PreviousFrameButton\"", transport);
        Assert.Contains("x:Name=\"PlayButton\"", transport);
        Assert.Contains("x:Name=\"NextFrameButton\"", transport);
        Assert.Contains("x:Name=\"MuteButton\"", transport);
        Assert.Contains("x:Name=\"LoopButton\"", transport);
        Assert.Contains("x:Name=\"FitButton\"", transport);
    }

    [TestMethod]
    [DataRow(1320d, true)]
    [DataRow(1321d, false)]
    public void InspectorBreakpoint_UsesExactLogicalEditorWidth(double logicalWidth, bool expectedOverlay)
    {
        Assert.AreEqual(expectedOverlay, CutFlow.Views.EditorView.UseOverlayInspector(logicalWidth));
    }

    [TestMethod]
    [DataRow(false, false, false, false, false)]
    [DataRow(false, false, true, false, false)]
    [DataRow(false, true, false, true, false)]
    [DataRow(false, true, true, true, false)]
    [DataRow(true, false, false, false, false)]
    [DataRow(true, true, false, false, false)]
    [DataRow(true, false, true, false, true)]
    [DataRow(true, true, true, false, true)]
    public void InspectorVisibility_ActivatesOnlyTheSurfaceForTheCurrentLayout(
        bool isNarrow,
        bool wideVisible,
        bool narrowVisible,
        bool expectedDesktop,
        bool expectedOverlay)
    {
        var (showDesktop, showOverlay) = CutFlow.Views.EditorView.ResolveInspectorVisibility(
            isNarrow,
            wideVisible,
            narrowVisible);

        Assert.AreEqual(expectedDesktop, showDesktop);
        Assert.AreEqual(expectedOverlay, showOverlay);
        Assert.IsFalse(showDesktop && showOverlay);
    }

    [TestMethod]
    public void InspectorToggle_UsesInspectorIconAndTracksEveryVisibilityRoute()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml"));
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var applyLayout = GetMethod(editor, "private void ApplyInspectorLayout", "private void RestorePendingInspectorFocus");
        var toggle = GetMethod(editor, "private void InspectorToggle_Click", "private void CloseOverlayInspector_Click");
        var close = GetMethod(editor, "private void CloseOverlayInspector_Click", "private void Inspector_EditCommitted");

        Assert.Contains("<ToggleButton x:Name=\"InspectorToggleButton\"", xaml);
        Assert.Contains("IsChecked=\"True\"", xaml);
        Assert.Contains("Glyph=\"&#xE90D;\"", xaml);
        Assert.Contains("DesktopInspector.Visibility = showDesktop", applyLayout);
        Assert.Contains("InspectorOverlay.Visibility = showOverlay", applyLayout);
        Assert.Contains("InspectorToggleButton.IsChecked = showDesktop || showOverlay;", applyLayout);
        Assert.Contains("AutomationProperties.SetName(InspectorToggleButton, action);", applyLayout);
        Assert.Contains("ToolTipService.SetToolTip(InspectorToggleButton, action);", applyLayout);
        Assert.Contains("ApplyInspectorLayout();", toggle);
        Assert.Contains("ApplyInspectorLayout();", close);
    }

    [TestMethod]
    public void InspectorBreakpoint_ClosesStaleOverlayAndTransfersAnActiveTextEdit()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml"));
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var inspector = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "InspectorPanel.xaml.cs"));
        var sizeChanged = GetMethod(editor, "private void EditorView_SizeChanged", "internal static bool UseOverlayInspector");
        var applyLayout = GetMethod(editor, "private void ApplyInspectorLayout", "private void RestorePendingInspectorFocus");
        var restoreFocus = GetMethod(editor, "private void RestorePendingInspectorFocus", "private void InspectorToggle_Click");
        var selectionChanged = GetMethod(editor, "private void ViewModel_SelectionChanged", "private void Tool_Click");
        var presentation = GetMethod(editor, "private void UpdateProjectPresentation", "private async Task RebuildPreviewAsync");
        var editCommitted = GetMethod(editor, "private void Inspector_EditCommitted", "private void Timeline_PlayheadChanged");

        Assert.Contains("x:Name=\"DesktopInspector\"", xaml);
        Assert.Contains("x:Name=\"NarrowInspector\"", xaml);
        Assert.AreEqual(2, xaml.Split("EditCommitted=\"Inspector_EditCommitted\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("UseOverlayInspector(e.NewSize.Width)", sizeChanged);
        Assert.Contains("CaptureFocus()", sizeChanged);
        Assert.Contains("_narrowInspectorVisible = false;", sizeChanged);
        Assert.Contains("_narrowInspectorVisible = true;", sizeChanged);
        Assert.Contains("ResolveInspectorVisibility(", applyLayout);
        Assert.Contains("_isNarrow ? NarrowInspector : DesktopInspector", restoreFocus);
        Assert.Contains("RestoreFocus(_pendingInspectorFocus)", restoreFocus);
        Assert.Contains("_pendingInspectorFocus = null;", selectionChanged);
        Assert.Contains("DesktopInspector.SetSelection(e.Selection);", selectionChanged);
        Assert.Contains("NarrowInspector.SetSelection(e.Selection);", selectionChanged);
        Assert.Contains("_pendingInspectorFocus = _pendingInspectorFocus?.FocusOnly();", presentation);
        Assert.Contains("DesktopInspector.SetProject(ViewModel.Project);", presentation);
        Assert.Contains("NarrowInspector.SetProject(ViewModel.Project);", presentation);
        Assert.Contains("_pendingInspectorFocus = _pendingInspectorFocus?.FocusOnly();", editCommitted);
        Assert.Contains("textBox.SelectionStart", inspector);
        Assert.Contains("textBox.SelectionLength", inspector);
        Assert.Contains("textBox.Focus(FocusState.Programmatic)", inspector);
    }

    [TestMethod]
    public void InspectorFocusSnapshot_FocusOnlyDropsCapturedInput()
    {
        var snapshot = new Controls.InspectorFocusSnapshot("TextContentBox", "stale", 2, 3);

        var focusOnly = snapshot.FocusOnly();

        Assert.AreEqual("TextContentBox", focusOnly.ControlName);
        Assert.IsNull(focusOnly.Text);
        Assert.AreEqual(0, focusOnly.SelectionStart);
        Assert.AreEqual(0, focusOnly.SelectionLength);
    }

    [TestMethod]
    public void InspectorEditResponse_RefreshesBothInstancesFromCanonicalProject()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var editCommitted = GetMethod(source, "private void Inspector_EditCommitted", "private void Timeline_PlayheadChanged");

        Assert.Contains("DesktopInspector.SetProject(ViewModel.Project);", editCommitted);
        Assert.Contains("NarrowInspector.SetProject(ViewModel.Project);", editCommitted);
    }

    [TestMethod]
    public void InspectorEnter_MovesFocusAndObservesHandledNumberBoxKeyUp()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Controls", "InspectorPanel.xaml.cs"));

        Assert.Contains("CommitTextBox(textBox);\r\n            FocusManager.TryMoveFocus(FocusNavigationDirection.Next);", source.ReplaceLineEndings("\r\n"));
        Assert.AreEqual(2, source.Split("new KeyEventHandler(TextPositionBox_KeyUp), handledEventsToo: true", StringSplitOptions.None).Length - 1);
        Assert.Contains("if (e.Key == VirtualKey.Enter)", source);
    }

    private static string GetMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0 && end > start, $"Could not isolate {startMarker}.");
        return source[start..end];
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
