using System.Globalization;
using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
[DoNotParallelize]
public sealed partial class NumericCultureTests
{
    [TestMethod]
    public void FiniteDouble_AcceptsLocalAndInvariantDecimalsAndExponentButRejectsGrouping()
    {
        using var cultureScope = new CurrentCultureScope("fi-FI");

        AssertParsed("12,5", 12.5);
        AssertParsed("12.5", 12.5);
        AssertParsed("1,25e2", 125);
        AssertParsed("1.25e2", 125);

        var groupSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
        Assert.IsFalse(TimelineInput.TryParseFiniteDouble($"1{groupSeparator}234,5", out _));
        Assert.IsFalse(TimelineInput.TryParseFiniteDouble("1,234.5", out _));
    }

    [TestMethod]
    public void FiniteDouble_RejectsEmptyNonFiniteAndOverflow()
    {
        foreach (var value in new[] { "", "NaN", "Infinity", "-Infinity", "1e309" })
        {
            Assert.IsFalse(TimelineInput.TryParseFiniteDouble(value, out _), value);
        }
    }

    [TestMethod]
    public void RoundTripFormatting_PreservesCommittedFontSizeAcrossCultures()
    {
        foreach (var cultureName in new[] { "en-US", "fi-FI", "de-DE" })
        {
            using var cultureScope = new CurrentCultureScope(cultureName);
            foreach (var expected in new[] { 12.345, 12.340000000000002, 399.99999999999994 })
            {
                var formatted = TimelineInput.FormatDoubleRoundTrip(expected);

                Assert.IsTrue(TimelineInput.TryParseFiniteDouble(formatted, out var actual), formatted);
                Assert.AreEqual(
                    BitConverter.DoubleToInt64Bits(expected),
                    BitConverter.DoubleToInt64Bits(actual),
                    $"{cultureName}: {formatted}");
            }
        }
    }

    [TestMethod]
    [DataRow("0.00049", 0L)]
    [DataRow("0.0005", 1L)]
    [DataRow("0.0015", 2L)]
    [DataRow("8.64e4", 86_400_000L)]
    public void Seconds_RoundsToIntegerMillisecondsAwayFromZero(string value, long expected)
    {
        using var cultureScope = new CurrentCultureScope("fi-FI");

        Assert.IsTrue(TimelineInput.TryParseSeconds(value, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void InspectorNumericControls_KeepCanonicalFormattingAndRestoreAnEmptyNumberBox()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "CutFlow", "Controls", "InspectorPanel.xaml.cs"));

        Assert.Contains("TextFontSizeBox.Text = TimelineInput.FormatDoubleRoundTrip(text.FontSize);", source);
        Assert.Contains("TimelineInput.TryParseFiniteDouble(value, out var size)", source);
        Assert.Contains("if (!double.IsFinite(args.NewValue))\r\n        {\r\n            Refresh();", source.ReplaceLineEndings("\r\n"));
    }

    private static void AssertParsed(string text, double expected)
    {
        Assert.IsTrue(TimelineInput.TryParseFiniteDouble(text, out var actual), text);
        Assert.AreEqual(expected, actual, 0.0000001, text);
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

        throw new DirectoryNotFoundException("Could not find the CutFlow repository root.");
    }

    private sealed partial class CurrentCultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

        public CurrentCultureScope(string cultureName)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        }

        public void Dispose() => CultureInfo.CurrentCulture = _originalCulture;
    }
}
