using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CutFlow.Utilities;

internal static class SourceFileReveal
{
    public static bool TryCreateStartInfo(
        string? sourcePath,
        [NotNullWhen(true)] out ProcessStartInfo? startInfo) =>
        TryCreateStartInfo(sourcePath, File.Exists, out startInfo);

    internal static bool TryCreateStartInfo(
        string? sourcePath,
        Func<string, bool> fileExists,
        [NotNullWhen(true)] out ProcessStartInfo? startInfo)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        startInfo = null;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        try
        {
            if (!Path.IsPathFullyQualified(sourcePath))
            {
                return false;
            }

            var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourcePath));
            if (normalizedPath.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                normalizedPath.StartsWith(@"\\.\", StringComparison.Ordinal) ||
                !fileExists(normalizedPath))
            {
                return false;
            }

            var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrWhiteSpace(windowsPath))
            {
                return false;
            }

            startInfo = new ProcessStartInfo(Path.Combine(windowsPath, "explorer.exe"))
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add($"/select,{normalizedPath}");
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
