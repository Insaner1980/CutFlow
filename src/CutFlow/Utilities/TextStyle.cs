using CutFlow.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace CutFlow.Utilities;

public static class TextStyle
{
    private static readonly IReadOnlyList<string> FontFamilies = Array.AsReadOnly(
        new[] { "Segoe UI", "Arial", "Georgia", "Consolas", "Impact" });

    public static IReadOnlyList<string> SupportedFontFamilies => FontFamilies;

    public static bool IsArgb(string? value) =>
        value is { Length: 9 } && value[0] == '#' && value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    public static double ClampNormalized(double value, double fallback = 0.5) =>
        Math.Clamp(double.IsFinite(value) ? value : fallback, 0, 1);

    public static double ClampOpacity(double value) =>
        Math.Clamp(double.IsFinite(value) ? value : TextTimelineItem.DefaultOpacity, 0, 1);

    public static string NormalizeFontFamily(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TextTimelineItem.DefaultFontFamily;
        }

        var normalized = value.Trim();
        return FontFamilies.FirstOrDefault(font => string.Equals(font, normalized, StringComparison.OrdinalIgnoreCase))
            ?? TextTimelineItem.DefaultFontFamily;
    }

    public static bool IsBold(int fontWeight) => fontWeight >= TextTimelineItem.BoldFontWeight;

    public static FontStyle ResolveFontStyle(TextTimelineItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsItalic ? FontStyle.Italic : FontStyle.Normal;
    }

    public static TextAlignment ResolveAlignment(TextTimelineItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Alignment switch
        {
            TextHorizontalAlignment.Left => TextAlignment.Left,
            TextHorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Center
        };
    }

    public static string ResolveBackgroundColor(TextTimelineItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.BackgroundEnabled ? item.BackgroundColor : TextTimelineItem.DefaultBackgroundColor;
    }

    internal static void ApplyTextBlockStyle(TextBlock textBlock, TextTimelineItem item, double maxWidth)
    {
        ArgumentNullException.ThrowIfNull(textBlock);
        ArgumentNullException.ThrowIfNull(item);

        textBlock.Text = item.Text ?? string.Empty;
        textBlock.FontFamily = new FontFamily(NormalizeFontFamily(item.FontFamily));
        textBlock.FontSize = double.IsFinite(item.FontSize) && item.FontSize > 0
            ? Math.Clamp(item.FontSize, 8, 400)
            : TextTimelineItem.DefaultFontSize;
        textBlock.FontWeight = new FontWeight
        {
            Weight = (ushort)Math.Clamp(item.FontWeight > 0 ? item.FontWeight : TextTimelineItem.DefaultFontWeight, 1, 999)
        };
        textBlock.FontStyle = ResolveFontStyle(item);
        textBlock.Foreground = ParseBrush(item.TextColor, TextTimelineItem.DefaultTextColor);
        textBlock.Opacity = ClampOpacity(item.Opacity);
        textBlock.TextAlignment = ResolveAlignment(item);
        textBlock.TextWrapping = TextWrapping.Wrap;
        textBlock.MaxWidth = Math.Max(1, maxWidth);
    }

    internal static void ApplyContainerStyle(Border container, TextTimelineItem item, Thickness disabledPadding)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(item);

        container.Background = ParseBrush(
            ResolveBackgroundColor(item),
            TextTimelineItem.DefaultBackgroundColor,
            item.BackgroundEnabled ? ClampOpacity(item.Opacity) : 1);
        container.Padding = item.BackgroundEnabled ? new Thickness(18, 8, 18, 8) : disabledPadding;
    }

    internal static SolidColorBrush ParseBrush(string? value, string fallback, double opacity = 1)
    {
        var normalized = IsArgb(value) ? value! : fallback;
        var alpha = Convert.ToByte(normalized.Substring(1, 2), 16);
        alpha = (byte)Math.Round(alpha * ClampOpacity(opacity), MidpointRounding.AwayFromZero);
        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(
            alpha,
            Convert.ToByte(normalized.Substring(3, 2), 16),
            Convert.ToByte(normalized.Substring(5, 2), 16),
            Convert.ToByte(normalized.Substring(7, 2), 16)));
    }
}
