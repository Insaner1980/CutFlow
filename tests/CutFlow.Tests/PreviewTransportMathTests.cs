using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class PreviewTransportMathTests
{
    [TestMethod]
    [DataRow(1_000L, 5_000L, false, 967L)]
    [DataRow(1_000L, 5_000L, true, 1_034L)]
    [DataRow(0L, 5_000L, false, 0L)]
    [DataRow(4_990L, 5_000L, true, 5_000L)]
    public void StepByFrame_MovesOneThirtyFpsFrameAndClampsToTimeline(
        long positionMilliseconds,
        long durationMilliseconds,
        bool forward,
        long expectedMilliseconds)
    {
        var result = PreviewTransportMath.StepByFrame(
            positionMilliseconds,
            durationMilliseconds,
            forward);

        Assert.AreEqual(expectedMilliseconds, result);
    }

    [TestMethod]
    public void StepByFrame_ThirtyForwardStepsAdvanceExactlyOneSecond()
    {
        long position = 0;
        for (var frame = 0; frame < 30; frame++)
        {
            position = PreviewTransportMath.StepByFrame(position, 5_000, forward: true);
        }

        Assert.AreEqual(1_000, position);
    }

    [TestMethod]
    [DataRow(0L, 34L)]
    [DataRow(33L, 34L)]
    public void StepByFrame_ForwardStepAdvancesDisplayedThirtyFpsFrame(
        long positionMilliseconds,
        long expectedMilliseconds)
    {
        var position = PreviewTransportMath.StepByFrame(positionMilliseconds, 5_000, forward: true);

        Assert.AreEqual(expectedMilliseconds, position);
        Assert.AreEqual("00:00:00:01", TimecodeFormatter.Format(position, 30));
    }

    [TestMethod]
    [DataRow(1_000d, 500d, 1_920d, 1_080d, 888.8889d, 500d)]
    [DataRow(400d, 900d, 1_080d, 1_920d, 400d, 711.1111d)]
    [DataRow(600d, 600d, 1_080d, 1_080d, 600d, 600d)]
    public void CalculateFitSize_FillsAvailableAreaWithoutChangingAspectRatio(
        double availableWidth,
        double availableHeight,
        double contentWidth,
        double contentHeight,
        double expectedWidth,
        double expectedHeight)
    {
        var result = PreviewTransportMath.CalculateFitSize(
            availableWidth,
            availableHeight,
            contentWidth,
            contentHeight);

        Assert.AreEqual(expectedWidth, result.Width, 0.001);
        Assert.AreEqual(expectedHeight, result.Height, 0.001);
    }
}
