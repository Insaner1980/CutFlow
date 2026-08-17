using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class PointerCaptureLifecycleTests
{
    [TestMethod]
    public void TimelineDrag_CapturesBeforeRetainingStateAndCancelsOnRebindOrDisposal()
    {
        var root = FindRepositoryRoot();
        var timeline = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml.cs"));
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var setTimeline = GetMethod(timeline, "public void SetTimeline(", "public void UpdatePlaybackPosition");
        var setSelection = GetMethod(timeline, "public void SetSelection", "public bool IsSelectionLocked");
        var clipPressed = GetMethod(timeline, "private void Clip_PointerPressed", "private void BeginDrag(");
        var beginDrag = GetMethod(timeline, "private void BeginDrag(", "private void SeekSurface_PointerPressed");
        var seekPressed = GetMethod(timeline, "private void SeekSurface_PointerPressed", "private void Playhead_PointerPressed");
        var playheadPressed = GetMethod(timeline, "private void Playhead_PointerPressed", "private void Drag_PointerMoved");
        var cancel = GetMethod(timeline, "private void CancelPointerInteraction(bool render)", "private void SeekFromPointer");
        var dispose = GetMethod(editor, "public void Dispose()", "private void CleanupEditorResource");

        AssertCapturePrecedesRetainedPointer(beginDrag, "!element.CapturePointer(e.Pointer)");
        AssertCapturePrecedesRetainedPointer(seekPressed, "!surface.CapturePointer(e.Pointer)");
        AssertCapturePrecedesRetainedPointer(playheadPressed, "!element.CapturePointer(e.Pointer)");
        Assert.Contains("_dragPointer is not null", clipPressed);
        Assert.Contains("_dragPointer is not null", seekPressed);
        Assert.Contains("_dragPointer is not null", playheadPressed);
        Assert.Contains("IsLeftButtonPressed", clipPressed);
        Assert.Contains("IsLeftButtonPressed", seekPressed);
        Assert.Contains("IsLeftButtonPressed", playheadPressed);
        Assert.Contains("CancelPointerInteraction();", setTimeline);
        Assert.Contains("CancelPointerInteraction(render: true);", setSelection);
        Assert.Contains("ReleasePointerCapture(capturedPointer);", cancel);
        Assert.Contains("render && _dragOperation != TimelineDragOperation.None", cancel);
        Assert.Contains("Timeline.CancelPointerInteraction", dispose);
    }

    [TestMethod]
    public void TimelineCardRerender_CancelsCapturedDragBeforeClearingVisuals()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Controls",
            "TimelineControl.xaml.cs"));
        var render = GetMethod(source, "private void RenderClips()", "private Border CreateVideoCard");
        var cancel = render.IndexOf("CancelPointerInteraction();", StringComparison.Ordinal);
        var clear = render.IndexOf("VideoCanvas.Children.Clear();", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, cancel, "A rerender must cancel any drag retained by an outgoing card.");
        Assert.IsGreaterThan(cancel, clear, "Pointer capture must be released before old card visuals are removed.");
        Assert.DoesNotContain("DataContext", render);
    }

    [TestMethod]
    public void PreviewTextDrag_RequiresLeftButtonAndCancelsOnRebindOrDisposal()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Controls",
            "PreviewPane.xaml.cs"));
        var setTextItems = GetMethod(source, "public void SetTextItems", "public void Dispose()");
        var dispose = GetMethod(source, "public void Dispose()", "private void AttachPlayerEvents");
        var pressed = GetMethod(source, "private void Text_PointerPressed", "private void Text_PointerMoved");
        var moved = GetMethod(source, "private void Text_PointerMoved", "private void Text_PointerReleased");
        var released = GetMethod(source, "private void Text_PointerReleased", "private void Text_PointerCanceled");
        var cancel = GetMethod(source, "private void CancelTextDrag", "private void ImportButton_Click");

        AssertCapturePrecedesRetainedPointer(pressed, "!visual.CapturePointer(e.Pointer)");
        Assert.Contains("IsLeftButtonPressed", pressed);
        Assert.Contains("_dragTextElement is not null", pressed);
        Assert.Contains("e.Pointer.PointerId != _dragPointerId", moved);
        Assert.Contains("e.Pointer.PointerId != _dragPointerId", released);
        Assert.Contains("!ReferenceEquals(_textProject, project) || _textSelection != selection", setTextItems);
        Assert.Contains("CancelTextDrag(render: false);", setTextItems);
        Assert.Contains("CancelTextDrag(render: false);", dispose);
        Assert.Contains("element.ReleasePointerCapture(pointer);", cancel);
    }

    [TestMethod]
    public void TimelineResize_RequiresLeftButtonAndSinglePointerAndReleasesOnDisposal()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "EditorView.xaml.cs"));
        var dispose = GetMethod(source, "public void Dispose()", "private void CleanupEditorResource");
        var pressed = GetMethod(source, "private void TimelineResizeHandle_PointerPressed", "private void TimelineResizeHandle_PointerMoved");
        var moved = GetMethod(source, "private void TimelineResizeHandle_PointerMoved", "private void TimelineResizeHandle_PointerReleased");
        var release = GetMethod(source, "private void ReleaseTimelineResizeCapture", "private void ClearTimelineResizeCapture");

        AssertCapturePrecedesRetainedPointer(pressed, "!handle.CapturePointer(e.Pointer)", "_timelineResizePointer = e.Pointer;");
        Assert.Contains("IsLeftButtonPressed", pressed);
        Assert.Contains("_timelineResizePointer is not null", pressed);
        Assert.Contains("_timelineResizePointerId != e.Pointer.PointerId", moved);
        Assert.Contains("CancelTimelineResize();", dispose);
        Assert.Contains("element.ReleasePointerCapture(pointer);", release);
    }

    private static void AssertCapturePrecedesRetainedPointer(
        string method,
        string captureMarker,
        string retainedMarker = "_dragPointer = e.Pointer;")
    {
        var capture = method.IndexOf(captureMarker, StringComparison.Ordinal);
        var retained = method.IndexOf(retainedMarker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, capture, $"Missing capture gate '{captureMarker}'.");
        Assert.IsGreaterThan(capture, retained, "Pointer state must be retained only after capture succeeds.");
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
