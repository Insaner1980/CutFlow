using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class TimelineScaleTests
{
    [TestMethod]
    public void Scale_RoundTripsMilliseconds()
    {
        var scale = new TimelineScale(80);

        Assert.AreEqual(2750L, scale.PixelsToTime(scale.TimeToPixels(2750)));
    }

    [TestMethod]
    public void TimeToPixels_ConvertsMillisecondsUsingPixelsPerSecond()
    {
        var scale = new TimelineScale(80);

        Assert.AreEqual(220d, scale.TimeToPixels(2750));
    }

    [DataTestMethod]
    [DataRow(80d, 1000L)]
    [DataRow(20d, 5000L)]
    [DataRow(400d, 250L)]
    public void GetRulerIntervalMilliseconds_SelectsApprovedAdaptiveInterval(double pixelsPerSecond, long expectedInterval)
    {
        var scale = new TimelineScale(pixelsPerSecond);

        Assert.AreEqual(expectedInterval, scale.GetRulerIntervalMilliseconds());
    }

    [TestMethod]
    public void Constructor_RejectsNonPositivePixelsPerSecond()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TimelineScale(0));
    }

    [TestMethod]
    public void PixelsToTime_ClampsNegativePixelValuesToZero()
    {
        var scale = new TimelineScale(80);

        Assert.AreEqual(0L, scale.PixelsToTime(-10));
    }

    [TestMethod]
    public void PixelsToTime_ClampsNaNToZero()
    {
        var scale = new TimelineScale(80);

        Assert.AreEqual(0L, scale.PixelsToTime(double.NaN));
    }

    [TestMethod]
    public void PixelsToTime_ClampsPositiveInfinityToMaximumTime()
    {
        var scale = new TimelineScale(80);

        Assert.AreEqual(long.MaxValue, scale.PixelsToTime(double.PositiveInfinity));
    }

    [TestMethod]
    public void PixelsToTime_ClampsFiniteOverflowToMaximumTime()
    {
        var scale = new TimelineScale(80);

        Assert.AreEqual(long.MaxValue, scale.PixelsToTime(double.MaxValue));
    }
}
