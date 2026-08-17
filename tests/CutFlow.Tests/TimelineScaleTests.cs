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

    [TestMethod]
    [DataRow(800d, 100L)]
    [DataRow(400d, 250L)]
    [DataRow(320d, 250L)]
    [DataRow(160d, 500L)]
    [DataRow(80d, 1000L)]
    [DataRow(40d, 2000L)]
    [DataRow(20d, 5000L)]
    [DataRow(16d, 5000L)]
    [DataRow(8d, 10000L)]
    [DataRow(3d, 30000L)]
    [DataRow(2d, 60000L)]
    [DataRow(0.5d, 60000L)]
    public void GetRulerIntervalMilliseconds_SelectsApprovedAdaptiveInterval(double pixelsPerSecond, long expectedInterval)
    {
        var scale = new TimelineScale(pixelsPerSecond);

        Assert.AreEqual(expectedInterval, scale.GetRulerIntervalMilliseconds());
    }

    [TestMethod]
    public void GetRulerIntervalMilliseconds_UsesExactSpacingBoundaryWithoutFloatingPointTolerance()
    {
        var boundaries = new (double PixelsPerSecond, long AtBoundary, long BelowBoundary)[]
        {
            (40d, 2_000, 5_000),
            (80d, 1_000, 2_000),
            (160d, 500, 1_000),
            (320d, 250, 500)
        };

        foreach (var boundary in boundaries)
        {
            Assert.AreEqual(boundary.AtBoundary, new TimelineScale(boundary.PixelsPerSecond).GetRulerIntervalMilliseconds());
            Assert.AreEqual(boundary.BelowBoundary, new TimelineScale(Math.BitDecrement(boundary.PixelsPerSecond)).GetRulerIntervalMilliseconds());
        }
    }

    [TestMethod]
    public void FormatRulerLabel_SubsecondTicksHaveDistinctMillisecondLabels()
    {
        var scale = new TimelineScale(TimelineScale.MaximumPixelsPerSecond);
        var interval = scale.GetRulerIntervalMilliseconds();
        var labels = new long[] { 0, 250, 500, 750, 1_000 }
            .Select(milliseconds => TimelineScale.FormatRulerLabel(milliseconds, interval))
            .ToArray();

        Assert.HasCount(5, labels);
        Assert.AreEqual("00:00.000", labels[0]);
        Assert.AreEqual("00:00.250", labels[1]);
        Assert.AreEqual("00:00.500", labels[2]);
        Assert.AreEqual("00:00.750", labels[3]);
        Assert.AreEqual("00:01.000", labels[4]);
        Assert.HasCount(labels.Length, labels.Distinct());
    }

    [TestMethod]
    public void FormatRulerLabel_WholeSecondIntervalsKeepCompactLabels()
    {
        Assert.AreEqual("00:05", TimelineScale.FormatRulerLabel(5_000, 1_000));
        Assert.AreEqual("1:02:03", TimelineScale.FormatRulerLabel(3_723_000, 5_000));
    }

    [TestMethod]
    public void Constructor_RejectsNonPositivePixelsPerSecond()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TimelineScale(0));
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
