using System.Globalization;
using CutFlow.Models;
using CutFlow.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace CutFlow.Controls;

public sealed partial class InspectorPanel : UserControl
{
    private const string UnknownValue = "Unknown";

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
        TextHorizontalPositionBox.AddHandler(KeyUpEvent, new KeyEventHandler(TextPositionBox_KeyUp), handledEventsToo: true);
        TextVerticalPositionBox.AddHandler(KeyUpEvent, new KeyEventHandler(TextPositionBox_KeyUp), handledEventsToo: true);
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

    internal InspectorFocusSnapshot? CaptureFocus()
    {
        if (XamlRoot is null || FocusManager.GetFocusedElement(XamlRoot) is not DependencyObject focusedElement)
        {
            return null;
        }

        for (var current = focusedElement; current is not null && !ReferenceEquals(current, this); current = VisualTreeHelper.GetParent(current))
        {
            if (current is not Control control || string.IsNullOrEmpty(control.Name) ||
                !ReferenceEquals(FindName(control.Name), control))
            {
                continue;
            }

            return control is TextBox textBox
                ? new InspectorFocusSnapshot(control.Name, textBox.Text, textBox.SelectionStart, textBox.SelectionLength)
                : new InspectorFocusSnapshot(control.Name);
        }

        return null;
    }

    internal bool RestoreFocus(InspectorFocusSnapshot snapshot)
    {
        if (FindName(snapshot.ControlName) is not Control control)
        {
            return false;
        }

        if (snapshot.Text is null || control is not TextBox textBox)
        {
            return control.Focus(FocusState.Programmatic);
        }

        textBox.Text = snapshot.Text;
        if (!textBox.Focus(FocusState.Programmatic)) return false;

        var selectionStart = Math.Clamp(snapshot.SelectionStart, 0, textBox.Text.Length);
        var selectionLength = Math.Clamp(snapshot.SelectionLength, 0, textBox.Text.Length - selectionStart);
        textBox.Select(selectionStart, selectionLength);
        return true;
    }

    private void Refresh()
    {
        if (_project is null) return;
        _updating = true;
        HidePanels();
        if (!TryShowVideoSelection() && !TryShowAudioSelection() && !TryShowTextSelection())
        {
            ShowProjectSettings();
        }
        _updating = false;
    }

    private void HidePanels()
    {
        ClearValidation();
        ProjectPanel.Visibility = Visibility.Collapsed;
        VideoPanel.Visibility = Visibility.Collapsed;
        ImagePanel.Visibility = Visibility.Collapsed;
        AudioPanel.Visibility = Visibility.Collapsed;
        TextPanel.Visibility = Visibility.Collapsed;
    }

    private bool TryShowVideoSelection()
    {
        if (_selection is not { Kind: EditorSelectionKind.VideoItem, ItemId: Guid videoId } ||
            _project!.VideoItems.FirstOrDefault(item => item.Id == videoId) is not { } video)
        {
            return false;
        }

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
            VideoResolutionText.Text = asset is null ? UnknownValue : FormatResolution(asset);
            VideoSourceDurationText.Text = asset is null ? UnknownValue : FormatDuration(asset.DurationMilliseconds);
            VideoSourceInBox.Text = FormatSeconds(video.SourceInMilliseconds);
            VideoSourceOutBox.Text = FormatSeconds(video.SourceOutMilliseconds);
            VideoTimelineDurationText.Text = FormatDuration(video.DurationMilliseconds);
            VideoVolumeSlider.Value = video.Volume * 100;
            VideoMuteBox.IsChecked = video.IsMuted;
            UpdateMuteBoxPresentation(VideoMuteBox, video.IsMuted, "video");
        }

        return true;
    }

    private bool TryShowAudioSelection()
    {
        if (_selection is not { Kind: EditorSelectionKind.AudioItem, ItemId: Guid audioId } ||
            _project!.AudioItems.FirstOrDefault(item => item.Id == audioId) is not { } audio)
        {
            return false;
        }

        var asset = _project.Assets.FirstOrDefault(candidate => candidate.Id == audio.AssetId);
        InspectorTitle.Text = "Audio";
        InspectorSubtitle.Text = "Timeline audio properties";
        AudioPanel.Visibility = Visibility.Visible;
        AudioFileNameText.Text = asset?.FileName ?? "Unknown audio";
        AudioSourceDurationText.Text = asset is null ? UnknownValue : FormatDuration(asset.DurationMilliseconds);
        AudioStartBox.Text = FormatSeconds(audio.StartMilliseconds);
        AudioSourceInBox.Text = FormatSeconds(audio.SourceInMilliseconds);
        AudioSourceOutBox.Text = FormatSeconds(audio.SourceOutMilliseconds);
        AudioVolumeSlider.Value = audio.Volume * 100;
        AudioMuteBox.IsChecked = audio.IsMuted;
        UpdateMuteBoxPresentation(AudioMuteBox, audio.IsMuted, "audio");
        return true;
    }

    private bool TryShowTextSelection()
    {
        if (_selection is not { Kind: EditorSelectionKind.TextItem, ItemId: Guid textId } ||
            _project!.TextItems.FirstOrDefault(item => item.Id == textId) is not { } text)
        {
            return false;
        }

        InspectorTitle.Text = "Text";
        InspectorSubtitle.Text = "Content and timeline properties";
        TextPanel.Visibility = Visibility.Visible;
        TextContentBox.Text = text.Text;
        TextFontFamilyBox.SelectedItem = TextStyle.NormalizeFontFamily(text.FontFamily);
        TextFontSizeBox.Text = TimelineInput.FormatDoubleRoundTrip(text.FontSize);
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
        return true;
    }

    private void ShowProjectSettings()
    {
        InspectorTitle.Text = "Project";
        InspectorSubtitle.Text = "Canvas settings";
        ProjectPanel.Visibility = Visibility.Visible;
        ProjectNameText.Text = _project!.Name;
        ResolutionText.Text = $"{_project.Settings.Width} × {_project.Settings.Height}";
        BackgroundColorBox.Text = _project.Settings.BackgroundColor;
        AspectRatioBox.SelectedIndex = _project.Settings.AspectRatio switch
        {
            AspectRatioPreset.Portrait9By16 => 1,
            AspectRatioPreset.Square1By1 => 2,
            _ => 0
        };
    }

    private void AspectRatioBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _project is null || AspectRatioBox.SelectedItem is not ComboBoxItem item ||
            !Enum.TryParse<AspectRatioPreset>(item.Tag?.ToString(), out var preset) || preset == _project.Settings.AspectRatio)
        {
            return;
        }

        RaiseEdit(new InspectorEditCommittedEventArgs(InspectorEditKind.SetAspectRatio) { AspectRatio = preset });
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
            if (ValidationText.Visibility != Visibility.Visible)
            {
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
            }

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

        switch (tag)
        {
            case "BackgroundColor":
                CommitBackgroundColor(textBox.Text);
                break;
            case "TextContent":
                ClearValidation();
                RaiseSelectedEdit(InspectorEditKind.SetTextContent, textValue: textBox.Text);
                break;
            case "TextColor":
            case "TextBackground":
                CommitTextColor(tag, textBox.Text);
                break;
            case "TextFontSize":
                CommitTextFontSize(textBox.Text);
                break;
            default:
                CommitTimelineValue(tag, textBox.Text);
                break;
        }
    }

    private void CommitBackgroundColor(string value)
    {
        if (!TimelineInput.IsOpaqueArgb(value))
        {
            ShowValidation(InspectorValidationPolicy.MessageFor("BackgroundColor"));
            return;
        }

        ClearValidation();
        RaiseEdit(new InspectorEditCommittedEventArgs(InspectorEditKind.SetBackgroundColor) { TextValue = value.ToUpperInvariant() });
    }

    private void CommitTextColor(string tag, string value)
    {
        if (!TextStyle.IsArgb(value))
        {
            ShowValidation(InspectorValidationPolicy.MessageFor(tag));
            return;
        }

        var kind = tag == "TextColor" ? InspectorEditKind.SetTextColor : InspectorEditKind.SetTextBackground;
        ClearValidation();
        RaiseSelectedEdit(kind, textValue: value.ToUpperInvariant());
    }

    private void CommitTextFontSize(string value)
    {
        if (!TimelineInput.TryParseFiniteDouble(value, out var size) || size is < 8 or > 400)
        {
            ShowValidation(InspectorValidationPolicy.MessageFor("TextFontSize"));
            return;
        }

        ClearValidation();
        RaiseSelectedEdit(InspectorEditKind.SetTextFontSize, doubleValue: size);
    }

    private void CommitTimelineValue(string tag, string value)
    {
        if (!TimelineInput.TryParseSeconds(value, out var milliseconds))
        {
            ShowValidation(InspectorValidationPolicy.MessageFor(tag));
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
            ClearValidation();
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

        var isMuted = checkBox.IsChecked == true;
        UpdateMuteBoxPresentation(checkBox, isMuted, checkBox == VideoMuteBox ? "video" : "audio");
        RaiseSelectedEdit(
            checkBox == VideoMuteBox ? InspectorEditKind.SetVideoMuted : InspectorEditKind.SetAudioMuted,
            boolValue: isMuted);
    }

    private static void UpdateMuteBoxPresentation(CheckBox checkBox, bool isMuted, string itemKind)
    {
        var action = isMuted ? "Unmute" : "Mute";
        checkBox.Content = action;
        var name = $"{action} {itemKind} item";
        AutomationProperties.SetName(checkBox, name);
        ToolTipService.SetToolTip(checkBox, name);
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
        if (_updating || sender.Tag is not string tag)
        {
            return;
        }

        if (!double.IsFinite(args.NewValue))
        {
            Refresh();
            return;
        }

        RaiseSelectedEdit(
            tag == "TextHorizontalPosition"
                ? InspectorEditKind.SetTextHorizontalPosition
                : InspectorEditKind.SetTextVerticalPosition,
            doubleValue: TextStyle.ClampNormalized(args.NewValue));
    }

    private static void TextPositionBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
        }
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
            RaiseEdit(new InspectorEditCommittedEventArgs(kind)
            {
                ItemId = itemId,
                LongValue = longValue,
                DoubleValue = doubleValue,
                BoolValue = boolValue,
                TextValue = textValue,
                TextAlignment = textAlignment
            });
        }
    }

    private void RaiseEdit(InspectorEditCommittedEventArgs args) =>
        InspectorEditBoundary.Commit(
            () => EditCommitted?.Invoke(this, args),
            Refresh);

    private void ShowValidation(string message)
    {
        if (!InspectorValidationPolicy.ShouldPublish(
                ValidationText.Text,
                ValidationText.Visibility == Visibility.Visible,
                message))
        {
            return;
        }

        ValidationText.Text = message;
        ValidationText.Visibility = Visibility.Visible;
    }

    private void ClearValidation() => ValidationText.Visibility = Visibility.Collapsed;

    private static string FormatResolution(ProjectAsset asset) => asset.Width > 0 && asset.Height > 0 ? $"{asset.Width} × {asset.Height}" : UnknownValue;
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

public sealed class InspectorEditCommittedEventArgs(InspectorEditKind kind) : EventArgs
{
    public InspectorEditKind Kind { get; } = kind;
    public Guid? ItemId { get; init; }
    public long LongValue { get; init; }
    public double DoubleValue { get; init; }
    public bool BoolValue { get; init; }
    public string? TextValue { get; init; }
    public AspectRatioPreset AspectRatio { get; init; } = AspectRatioPreset.Landscape16By9;
    public TextHorizontalAlignment TextAlignment { get; init; } = TextHorizontalAlignment.Center;
}

public static class InspectorCommitGesture
{
    public static bool ShouldCommit(string? tag, VirtualKey key, bool controlDown) =>
        key == VirtualKey.Enter && (!string.Equals(tag, "TextContent", StringComparison.Ordinal) || controlDown);
}

internal static class InspectorEditBoundary
{
    public static void Commit(Action requestEdit, Action refreshCanonicalState)
    {
        requestEdit();
        refreshCanonicalState();
    }
}

internal static class InspectorValidationPolicy
{
    public static string MessageFor(string tag) => tag switch
    {
        "BackgroundColor" => "Background color must use opaque #FFRRGGBB format.",
        "TextColor" => "Text color must use #AARRGGBB format.",
        "TextBackground" => "Text background color must use #AARRGGBB format.",
        "TextFontSize" => "Font size must be from 8 to 400.",
        "VideoSourceIn" or "AudioSourceIn" => "Source in must be between 0 seconds and 24 hours.",
        "VideoSourceOut" or "AudioSourceOut" => "Source out must be between 0 seconds and 24 hours.",
        "ImageDuration" => "Image duration must be between 0 seconds and 24 hours.",
        "AudioStart" or "TextStart" => "Timeline start must be between 0 seconds and 24 hours.",
        "TextDuration" => "Text duration must be between 0 seconds and 24 hours.",
        _ => "Value must be between 0 seconds and 24 hours."
    };

    public static bool ShouldPublish(string? currentMessage, bool isVisible, string nextMessage) =>
        !isVisible || !string.Equals(currentMessage, nextMessage, StringComparison.Ordinal);
}

internal sealed record InspectorFocusSnapshot(
    string ControlName,
    string? Text = null,
    int SelectionStart = 0,
    int SelectionLength = 0)
{
    public InspectorFocusSnapshot FocusOnly() => new(ControlName);
}
