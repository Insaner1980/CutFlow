using System.Text;
using CutFlow.Models;

namespace CutFlow.Services;

public static class ExportPresentation
{
    private static readonly string FallbackFileName = $"{AppInfo.ProductName} export";
    private const int MaximumBaseNameLength = 96;
    private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "COM¹", "COM²", "COM³",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "LPT¹", "LPT²", "LPT³"
    };

    public static string NormalizeSuggestedFileName(string? projectName)
    {
        var value = projectName?.Trim() ?? string.Empty;
        if (value.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^4];
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsControl(character) || invalid.Contains(character) ? ' ' : character);
        }

        value = string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim(' ', '.');
        if (value.Length > MaximumBaseNameLength)
        {
            var length = MaximumBaseNameLength;
            if (char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
            {
                length--;
            }

            value = value[..length].TrimEnd(' ', '.');
        }

        var extensionSeparator = value.IndexOf('.');
        var deviceName = extensionSeparator >= 0 ? value[..extensionSeparator] : value;
        if (value.Length == 0 || ReservedFileNames.Contains(deviceName))
        {
            value = FallbackFileName;
        }

        return $"{value}.mp4";
    }

    internal static bool TryValidateFileName(string? value, out string sanitizedFileName)
    {
        sanitizedFileName = NormalizeSuggestedFileName(value);
        return string.Equals(value, sanitizedFileName, StringComparison.Ordinal);
    }

    internal static bool TryCreateOptions(
        int resolutionIndex,
        int qualityIndex,
        out ExportOptions options)
    {
        options = (resolutionIndex, qualityIndex) switch
        {
            (0, 0) => new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.Standard),
            (0, 1) => new ExportOptions(ExportResolutionTier.Hd720p, ExportQuality.High),
            (1, 0) => new ExportOptions(ExportResolutionTier.FullHd1080p, ExportQuality.Standard),
            (1, 1) => new ExportOptions(ExportResolutionTier.FullHd1080p, ExportQuality.High),
            _ => null!
        };
        return options is not null;
    }

    public static bool CanStartExport(ProjectDocument project, bool isExporting)
    {
        ArgumentNullException.ThrowIfNull(project);
        return !isExporting && project.VideoItems.Any(item => item.DurationMilliseconds > 0);
    }

    public static string GetDisabledHelpText(ProjectDocument project, bool isExporting)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (isExporting)
        {
            return "Finish or cancel the active export operation before exporting again.";
        }

        return project.VideoItems.Any(item => item.DurationMilliseconds > 0)
            ? string.Empty
            : "Add positive-duration visual media to V1 before exporting.";
    }

    public static double NormalizeProgress(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;

    internal static ExportCompletionState ResolveCompletion(
        ExportResultStatus status,
        bool _) =>
        status == ExportResultStatus.Success
            ? ExportCompletionState.Success
            : ExportCompletionState.Failed;
}

internal enum ExportCompletionState
{
    Success,
    Cancelled,
    Failed
}

internal sealed class ExportOperationState
{
    private readonly object _sync = new();
    private long _nextOperation;
    private long? _activeOperation;
    private bool _activeOperationInvalidated;
    private bool _closing;
    private bool _rendering;

    public bool IsActive
    {
        get
        {
            lock (_sync)
            {
                return _activeOperation.HasValue;
            }
        }
    }

    public bool IsRendering
    {
        get
        {
            lock (_sync)
            {
                return _rendering;
            }
        }
    }

    public bool TryBegin(out long operation)
    {
        lock (_sync)
        {
            if (_closing || _activeOperation.HasValue)
            {
                operation = 0;
                return false;
            }

            operation = ++_nextOperation;
            _activeOperation = operation;
            _activeOperationInvalidated = false;
            _rendering = false;
            return true;
        }
    }

    public bool TryBeginRender(long operation)
    {
        lock (_sync)
        {
            if (_activeOperation != operation || _activeOperationInvalidated || _closing || _rendering)
            {
                return false;
            }

            _rendering = true;
            return true;
        }
    }

    public bool CanContinue(long operation)
    {
        lock (_sync)
        {
            return _activeOperation == operation && !_activeOperationInvalidated && !_closing;
        }
    }

    public void BeginClosing()
    {
        lock (_sync)
        {
            _closing = true;
            _activeOperationInvalidated = _activeOperation.HasValue;
        }
    }

    public void CancelClosing()
    {
        lock (_sync)
        {
            _closing = false;
        }
    }

    public void Complete(long operation)
    {
        lock (_sync)
        {
            if (_activeOperation != operation)
            {
                return;
            }

            _activeOperation = null;
            _activeOperationInvalidated = false;
            _rendering = false;
        }
    }
}
