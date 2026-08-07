using System.Globalization;
using CutFlow.Models;
using CutFlow.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace CutFlow.Controls;

public sealed partial class InspectorPanel : UserControl
{
    private ProjectDocument? _project;
    private EditorSelection _selection = EditorSelection.None;
    private bool _updating;

    public InspectorPanel()
    {
        InitializeComponent();
        TextFontFamilyBox.ItemsSource = TextStyle.SupportedFontFamilies;
        VideoVolumeSlider.AddHandler(PointerReleasedEvent, new PointerEventHandler(VolumeSlider_PointerReleased), handledEventsToo: true);
        AudioVolumeSlider.AddHandler(PointerReleasedEvent, new PointerEventHandler(VolumeSlider_PointerReleased), handledEventsToo: true);
        TextOpacitySlider.AddHandler(PointerReleasedEvent, new PointerEventHandler(TextOpacitySlider_PointerReleased), handledEventsToo: true);
    }

    public event EventHandler<InspectorEditCommittedEventArgs>? EditCommitted;

    public void SetProject(ProjectDocument project)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        Refresh();
    }

    public void SetSelection(EditorSelection selection)
    {
        _selection = selection;
        Refresh();
    }

    public void FocusTextContent() => TextContentBox.Focus(FocusState.Programmatic);

    private void Refresh()
    {
        if (_project is null)
        {
            return;
        }

        _updating = true;
        ValidationText.Visibility = Visibility.Collapsed;
        ProjectPanel.Visibility = Visibility.Collapsed;
        VideoPanel.Visibility = Visibility.Collapsed;
        ImagePanel.Visibility = Visibility.Collapsed;
        AudioPanel.Visibility = Visibility.Collapsed;
        TextPanel.Visibility = Visibility.Collapsed;

        if (_selection is { Kind: EditorSelectionKind.VideoItem, ItemId: Guid videoId } &&
            _project.VideoItems.FirstOrDefault(item => item.Id == videoId) is { } video)
        {
            var asset = _project.Assets.FirstOrDefault(candidate => candidate.Id == video.AssetId);
            if (asset?.Kind == ProjectAssetKind.Image)
            {
                InspectorTitle.Text = "Image";
                InspectorSubtitle.Text = "Timeline image properties";
                ImagePanel.Visibility = Visibility.Visible;
                ImageFileNameText.Text = asset.FileName;
                ImageResolutionText.Text = FormatResolution(asset);
                ImageDurationBox.Text = FormatSeconds(video.DurationMilliseconds);
            }
            else
            {
                InspectorTitle.Text = "Video";
                InspectorSubtitle.Text = "Source and audio properties";
                VideoPanel.Visibility = Visibility.Visible;
                VideoFileNameText.Text = asset?.FileName ?? "Unknown media";
                VideoResolutionText.Text = asset is null ? "Unknown" : FormatResolution(asset);
                VideoSourceDurationText.Text = asset is null ? "Unknown" : FormatDuration(asset.DurationMilliseconds);
                VideoSourceInBox.Text = FormatSeconds(video.SourceInMilliseconds);
                VideoSourceOutBox.Text = FormatSeconds(video.SourceOutMilliseconds);
                VideoTimelineDurationText.Text = FormatDuration(video.DurationMilliseconds);
                VideoVolumeSlider.Value = video.Volume * 100;
                VideoMuteBox.IsChecked = video.IsMuted;
            }
        }
        else if (_selection is { Kind: EditorSelectionKind.AudioItem, ItemId: Guid audioId } &&
            _project.AudioItems.FirstOrDefault(item => item.Id == audioId) is { } audio)
        {
            var asset = _project.Assets.FirstOrDefault(candidate => candidate.Id == audio.AssetId);
            InspectorTitle.Text = "Audio";
            InspectorSubtitle.Text = "Timeline audio properties";
            AudioPanel.Visibility = Visibility.Visible;
            AudioFileNameText.Text = asset?.FileName ?? "Unknown audio";
            AudioSourceDurationText.Text = asset is null ? "Unknown" : FormatDuration(asset.DurationMilliseconds);
            AudioStartBox.Text = FormatSeconds(audio.StartMilliseconds);
            AudioSourceInBox.Text = FormatSeconds(audio.SourceInMilliseconds);
            AudioSourceOutBox.Text = FormatSeconds(audio.SourceOutMilliseconds);
            AudioVolumeSlider.Value = audio.Volume * 100;
            AudioMuteBox.IsChecked = audio.IsMuted;
        }
        else if (_selection is { Kind: EditorSelectionKind.TextItem, ItemId: Guid textId } &&
            _project.TextItems.FirstOrDefault(item => item.Id == textId) is { } text)
        {
            InspectorTitle.Text = "Text";
            InspectorSubtitle.Text = "Content and timeline properties";
            TextPanel.Visibility = Visibility.Visible;
            TextContentBox.Text = text.Text;
            TextFontFamilyBox.SelectedItem = TextStyle.NormalizeFontFamily(text.FontFamily);
            TextFontSizeBox.Text = text.FontSize.ToString("0.##", CultureInfo.CurrentCulture);
            TextBoldButton.IsChecked = TextStyle.IsBold(text.FontWeight);
            TextItalicButton.IsChecked = text.IsItalic;
            TextColorBox.Text = text.TextColor;
            TextBackgroundBox.Text = text.BackgroundColor;
            TextBackgroundEnabledToggle.IsOn = text.BackgroundEnabled;
            TextBackgroundBox.IsEnabled = text.BackgroundEnabled;
            TextOpacitySlider.Value = TextStyle.ClampOpacity(text.Opacity) * 100;
            TextOpacityValueText.Text = $"{TextOpacitySlider.Value:0}%";
            TextHorizontalPositionBox.Value = TextStyle.ClampNormalized(text.NormalizedX);
            TextVerticalPositionBox.Value = TextStyle.ClampNormalized(text.NormalizedY);
            SetTextAlignmentButtons(text.Alignment);
            TextStartBox.Text = FormatSeconds(text.StartMilliseconds);
            TextDurationBox.Text = FormatSeconds(text.DurationMilliseconds);
        }
        else
        {
            InspectorTitle.Text = "Project";
            InspectorSubtitle.Text = "Canvas settings";
            ProjectPanel.Visibility = Visibility.Visible;
            ProjectNameText.Text = _project.Name;
            ResolutionText.Text = $"{_project.Settings.Width} × {_project.Settings.Height}";
            BackgroundColorBox.Text = _project.Settings.BackgroundColor;
            AspectRatioBox.SelectedIndex = _project.Settings.AspectRatio switch
            {
                AspectRatioPreset.Portrait9By16 => 1,
                AspectRatioPreset.Square1By1 => 2,
                _ => 0
            };
        }

        _updating = false;
    }

    private void AspectRatioBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _project is null || AspectRatioBox.SelectedItem is not ComboBoxItem item ||
            !Enum.TryParse<AspectRatioPreset>(item.Tag?.ToString(), out var preset) || preset == _project.Settings.AspectRatio)
        {
            return;
        }

        RaiseEdit(new InspectorEditCommittedEventArgs(InspectorEditKind.SetAspectRatio, aspectRatio: preset));
    }

    private void EditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            Refresh();
            e.Handled = true;
        }
        else if (InspectorCommitGesture.ShouldCommit(textBox.Tag?.ToString(), e.Key, IsControlDown()))
        {
            CommitTextBox(textBox);
            e.Handled = true;
        }
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_updating && sender is TextBox textBox)
        {
            CommitTextBox(textBox);
        }
    }

    private void CommitTextBox(TextBox textBox)
    {
        if (_updating || _project is null || textBox.Tag is not string tag)
        {
            return;
        }

        ValidationText.Visibility = Visibility.Collapsed;
        if (tag == "BackgroundColor")
        {
            if (!TimelineInput.IsOpaqueArgb(textBox.Text))
            {
                ShowValidation("Enter an opaque ARGB color in #FFRRGGBB form.");
                return;
            }

            RaiseEdit(new InspectorEditCommittedEventArgs(InspectorEditKind.SetBackgroundColor, textValue: textBox.Text.ToUpperInvariant()));
            return;
        }

        if (tag == "TextContent")
        {
            RaiseSelectedEdit(InspectorEditKind.SetTextContent, textValue: textBox.Text);
            return;
        }

        if (tag is "TextColor" or "TextBackground")
        {
            if (!TextStyle.IsArgb(textBox.Text))
            {
                ShowValidation("Enter an ARGB color in #AARRGGBB form.");
                return;
            }
            RaiseSelectedEdit(tag == "TextColor" ? InspectorEditKind.SetTextColor : InspectorEditKind.SetTextBackground, textValue: textBox.Text.ToUpperInvariant());
            return;
        }

        if (tag == "TextFontSize")
        {
            if (!double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var size) || !double.IsFinite(size) || size is < 8 or > 400)
            {
                ShowValidation("Enter a font size from 8 to 400.");
                return;
            }
            RaiseSelectedEdit(InspectorEditKind.SetTextFontSize, doubleValue: size);
            return;
        }

        if (!TimelineInput.TryParseSeconds(textBox.Text, out var milliseconds))
        {
            ShowValidation("Enter a value from 0 seconds to 24 hours.");
            return;
        }

        var kind = tag switch
        {
            "VideoSourceIn" => InspectorEditKind.TrimVideoStart,
            "VideoSourceOut" => InspectorEditKind.TrimVideoEnd,
            "ImageDuration" => InspectorEditKind.SetImageDuration,
            "AudioStart" => InspectorEditKind.MoveAudio,
            "AudioSourceIn" => InspectorEditKind.TrimAudioStart,
            "AudioSourceOut" => InspectorEditKind.TrimAudioEnd,
            "TextStart" => InspectorEditKind.MoveText,
            "TextDuration" => InspectorEditKind.SetTextDuration,
            _ => InspectorEditKind.None
        };
        if (kind != InspectorEditKind.None)
        {
            RaiseSelectedEdit(kind, longValue: milliseconds);
        }
    }

    private void VolumeSlider_PointerReleased(object sender, PointerRoutedEventArgs e) => CommitVolume(sender as Slider);

    private void VolumeSlider_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down or VirtualKey.Home or VirtualKey.End)
        {
            CommitVolume(sender as Slider);
        }
    }

    private void CommitVolume(Slider? slider)
    {
        if (_updating || slider is null)
        {
            return;
        }

        RaiseSelectedEdit(
            slider == VideoVolumeSlider ? InspectorEditKind.SetVideoVolume : InspectorEditKind.SetAudioVolume,
            doubleValue: slider.Value / 100d);
    }

    private void MuteBox_Click(object sender, RoutedEventArgs e)
    {
        if (_updating || sender is not CheckBox checkBox)
        {
            return;
        }

        RaiseSelectedEdit(
            checkBox == VideoMuteBox ? InspectorEditKind.SetVideoMuted : InspectorEditKind.SetAudioMuted,
            boolValue: checkBox.IsChecked == true);
    }

    private void TextFontFamilyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _project is null || TextFontFamilyBox.SelectedItem is not string fontFamily)
        {
            return;
        }

        RaiseSelectedEdit(InspectorEditKind.SetTextFontFamily, textValue: fontFamily);
    }

    private void TextStyleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updating || sender is not ToggleButton button)
        {
            return;
        }

        RaiseSelectedEdit(
            button == TextBoldButton ? InspectorEditKind.SetTextBold : InspectorEditKind.SetTextItalic,
            boolValue: button.IsChecked == true);
    }

    private void TextAlignmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updating || sender is not ToggleButton { Tag: string tag } ||
            !Enum.TryParse<TextHorizontalAlignment>(tag, out var alignment))
        {
            return;
        }

        SetTextAlignmentButtons(alignment);
        RaiseSelectedEdit(InspectorEditKind.SetTextAlignment, textAlignment: alignment);
    }

    private void TextBackgroundEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        TextBackgroundBox.IsEnabled = TextBackgroundEnabledToggle.IsOn;
        RaiseSelectedEdit(InspectorEditKind.SetTextBackgroundEnabled, boolValue: TextBackgroundEnabledToggle.IsOn);
    }

    private void TextOpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (TextOpacityValueText is not null)
        {
            TextOpacityValueText.Text = $"{e.NewValue:0}%";
        }
    }

    private void TextOpacitySlider_PointerReleased(object sender, PointerRoutedEventArgs e) => CommitTextOpacity();

    private void TextOpacitySlider_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down or VirtualKey.Home or VirtualKey.End)
        {
            CommitTextOpacity();
        }
    }

    private void CommitTextOpacity()
    {
        if (!_updating)
        {
            RaiseSelectedEdit(InspectorEditKind.SetTextOpacity, doubleValue: TextOpacitySlider.Value / 100d);
        }
    }

    private void TextPositionBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_updating || !double.IsFinite(args.NewValue) || sender.Tag is not string tag)
        {
            return;
        }

        RaiseSelectedEdit(
            tag == "TextHorizontalPosition"
                ? InspectorEditKind.SetTextHorizontalPosition
                : InspectorEditKind.SetTextVerticalPosition,
            doubleValue: TextStyle.ClampNormalized(args.NewValue));
    }

    private void SetTextAlignmentButtons(TextHorizontalAlignment alignment)
    {
        TextAlignLeftButton.IsChecked = alignment == TextHorizontalAlignment.Left;
        TextAlignCenterButton.IsChecked = alignment == TextHorizontalAlignment.Center;
        TextAlignRightButton.IsChecked = alignment == TextHorizontalAlignment.Right;
    }

    private void ResetVolume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: "VideoVolume" })
        {
            RaiseSelectedEdit(InspectorEditKind.SetVideoVolume, doubleValue: 1);
        }
        else
        {
            RaiseSelectedEdit(InspectorEditKind.SetAudioVolume, doubleValue: 1);
        }
    }

    private void ResetImageDuration_Click(object sender, RoutedEventArgs e) =>
        RaiseSelectedEdit(InspectorEditKind.ResetImageDuration);

    private void ResetTextStyle_Click(object sender, RoutedEventArgs e) =>
        RaiseSelectedEdit(InspectorEditKind.ResetTextStyle);

    private void RaiseSelectedEdit(
        InspectorEditKind kind,
        long longValue = 0,
        double doubleValue = 0,
        bool boolValue = false,
        string? textValue = null,
        TextHorizontalAlignment textAlignment = TextHorizontalAlignment.Center)
    {
        if (_selection.ItemId is Guid itemId)
        {
            RaiseEdit(new InspectorEditCommittedEventArgs(kind, itemId, longValue, doubleValue, boolValue, textValue, textAlignment: textAlignment));
        }
    }

    private void RaiseEdit(InspectorEditCommittedEventArgs args) => EditCommitted?.Invoke(this, args);

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.Visibility = Visibility.Visible;
    }

    private static string FormatResolution(ProjectAsset asset) => asset.Width > 0 && asset.Height > 0 ? $"{asset.Width} × {asset.Height}" : "Unknown";
    private static string FormatDuration(long milliseconds) => $"{Math.Max(0, milliseconds) / 1_000d:0.###} s";
    private static string FormatSeconds(long milliseconds) => (Math.Max(0, milliseconds) / 1_000d).ToString("0.###", CultureInfo.CurrentCulture);
    private static bool IsControlDown() =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
}

public enum InspectorEditKind
{
    None,
    SetAspectRatio,
    SetBackgroundColor,
    TrimVideoStart,
    TrimVideoEnd,
    SetImageDuration,
    ResetImageDuration,
    SetVideoVolume,
    SetVideoMuted,
    MoveAudio,
    TrimAudioStart,
    TrimAudioEnd,
    SetAudioVolume,
    SetAudioMuted,
    SetTextContent,
    SetTextFontFamily,
    SetTextFontSize,
    SetTextFontWeight,
    SetTextBold,
    SetTextItalic,
    SetTextColor,
    SetTextBackground,
    SetTextBackgroundEnabled,
    SetTextOpacity,
    SetTextAlignment,
    SetTextHorizontalPosition,
    SetTextVerticalPosition,
    MoveText,
    SetTextDuration,
    ResetTextStyle
}

public sealed class InspectorEditCommittedEventArgs(
    InspectorEditKind kind,
    Guid? itemId = null,
    long longValue = 0,
    double doubleValue = 0,
    bool boolValue = false,
    string? textValue = null,
    AspectRatioPreset aspectRatio = AspectRatioPreset.Landscape16By9,
    TextHorizontalAlignment textAlignment = TextHorizontalAlignment.Center) : EventArgs
{
    public InspectorEditKind Kind { get; } = kind;
    public Guid? ItemId { get; } = itemId;
    public long LongValue { get; } = longValue;
    public double DoubleValue { get; } = doubleValue;
    public bool BoolValue { get; } = boolValue;
    public string? TextValue { get; } = textValue;
    public AspectRatioPreset AspectRatio { get; } = aspectRatio;
    public TextHorizontalAlignment TextAlignment { get; } = textAlignment;
}

public static class InspectorCommitGesture
{
    public static bool ShouldCommit(string? tag, VirtualKey key, bool controlDown) =>
        key == VirtualKey.Enter && (!string.Equals(tag, "TextContent", StringComparison.Ordinal) || controlDown);
}
