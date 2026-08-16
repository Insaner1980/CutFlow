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
    public void TimelineResize_CanceledOrLostCaptureRestoresPreviewWithoutPersisting()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml"));
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var moved = GetMethod(source, "private void TimelineResizeHandle_PointerMoved", "private void TimelineResizeHandle_PointerReleased");
        var released = GetMethod(source, "private void TimelineResizeHandle_PointerReleased", "private void TimelineResizeHandle_PointerCanceled");
        var canceled = GetMethod(source, "private void TimelineResizeHandle_PointerCanceled", "private void TimelineResizeHandle_PointerCaptureLost");
        var captureLost = GetMethod(source, "private void TimelineResizeHandle_PointerCaptureLost", "private bool CancelTimelineResize");
        var cancel = GetMethod(source, "private bool CancelTimelineResize", "private void ToolPanel_ImportRequested");

        StringAssert.Contains(xaml, "PointerCanceled=\"TimelineResizeHandle_PointerCanceled\"");
        StringAssert.Contains(moved, "notify: false");
        StringAssert.Contains(released, "SetTimelineHeight(TimelineHeight, notify: true);");
        StringAssert.Contains(canceled, "CancelTimelineResize(e.Pointer.PointerId)");
        StringAssert.Contains(captureLost, "CancelTimelineResize(e.Pointer.PointerId, releaseCapture: false)");
        StringAssert.Contains(cancel, "SetTimelineHeight(_timelineResizeStartHeight, notify: false);");
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

        StringAssert.Contains(sizeChanged, "SetTimelineHeight(_workspaceSettings.TimelineHeight, notify: false);");
        StringAssert.Contains(raiseChanged, "_workspaceSettings.TimelineHeight");
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
