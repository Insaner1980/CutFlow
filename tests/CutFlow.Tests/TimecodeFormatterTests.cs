using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class TimecodeFormatterTests
{
    [TestMethod]
    public void Format_UsesFrameComponentAtThirtyFps()
    {
        Assert.AreEqual("00:00:01:15", TimecodeFormatter.Format(1500, 30));
    }

    [TestMethod]
    public void Format_IncludesHoursMinutesSecondsAndFrames()
    {
        Assert.AreEqual("01:01:01:15", TimecodeFormatter.Format(3_661_500, 30));
    }

    [TestMethod]
    public void Format_ClampsNegativeTimeToZero()
    {
        Assert.AreEqual("00:00:00:00", TimecodeFormatter.Format(-1, 30));
    }

    [TestMethod]
    public void Format_RejectsNonPositiveFrameRate()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TimecodeFormatter.Format(0, 0));
    }
}
