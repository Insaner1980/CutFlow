using CutFlow.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class InspectorValidationTests
{
    [TestMethod]
    [DataRow("BackgroundColor", "Background color must use opaque #FFRRGGBB format.")]
    [DataRow("TextColor", "Text color must use #AARRGGBB format.")]
    [DataRow("TextBackground", "Text background color must use #AARRGGBB format.")]
    [DataRow("TextFontSize", "Font size must be from 8 to 400.")]
    [DataRow("VideoSourceIn", "Source in must be between 0 seconds and 24 hours.")]
    [DataRow("VideoSourceOut", "Source out must be between 0 seconds and 24 hours.")]
    [DataRow("ImageDuration", "Image duration must be between 0 seconds and 24 hours.")]
    [DataRow("AudioStart", "Timeline start must be between 0 seconds and 24 hours.")]
    [DataRow("TextDuration", "Text duration must be between 0 seconds and 24 hours.")]
    public void MessageFor_NamesTheInvalidField(string tag, string expected)
    {
        Assert.AreEqual(expected, InspectorValidationPolicy.MessageFor(tag));
    }

    [TestMethod]
    public void ShouldPublish_SuppressesAnAlreadyVisibleDuplicate()
    {
        const string message = "Source in must be between 0 seconds and 24 hours.";

        Assert.IsTrue(InspectorValidationPolicy.ShouldPublish(null, isVisible: false, message));
        Assert.IsTrue(InspectorValidationPolicy.ShouldPublish(message, isVisible: false, message));
        Assert.IsFalse(InspectorValidationPolicy.ShouldPublish(message, isVisible: true, message));
        Assert.IsTrue(InspectorValidationPolicy.ShouldPublish(message, isVisible: true, "Source out must be between 0 seconds and 24 hours."));
    }

    [TestMethod]
    public void InspectorValidation_IsPoliteTextAndDoesNotClearBeforeCheckingInput()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "InspectorPanel.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "InspectorPanel.xaml.cs"));
        var commit = GetMethod(code, "private void CommitTextBox", "private void CommitBackgroundColor");
        var validation = GetMethod(code, "private void ShowValidation", "private void ClearValidation");

        Assert.Contains("x:Name=\"ValidationText\"", xaml);
        Assert.Contains("Foreground=\"{StaticResource ErrorBrush}\"", xaml);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", xaml);
        Assert.IsFalse(commit[..commit.IndexOf("switch (tag)", StringComparison.Ordinal)].Contains("ClearValidation", StringComparison.Ordinal));
        Assert.Contains("InspectorValidationPolicy.ShouldPublish", validation);
    }

    private static string GetMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0 && end > start, $"Could not find method range from {startMarker} to {endMarker}.");
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

        throw new DirectoryNotFoundException("Could not find the CutFlow repository root.");
    }
}
