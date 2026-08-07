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
}
