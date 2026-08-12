using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;

namespace CutFlow.ViewModels;

public sealed class EditorViewModel : ViewModelBase
{
    public const string SavedStatus = "Saved";
    public const string SavingStatus = "Saving…";
    public const string UnsavedStatus = "Unsaved";
    public const string SaveFailedStatus = "Save failed";

    private readonly UndoHistory _history = new();
    private ProjectDocument _project;
    private EditorTool _selectedTool = EditorTool.Media;
    private EditorSelection _selection = EditorSelection.None;
    private long _playheadMilliseconds;
    private bool _isPlaying;
    private string _saveStatus = SavedStatus;
    private long _revision;

    public EditorViewModel(ProjectDocument project)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
    }

    public event EventHandler? ReturnHomeRequested;
    public event EventHandler? ImportRequested;
    public event EventHandler? AddTextRequested;
    public event EventHandler? ExportRequested;
    public event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<PlayheadChangedEventArgs>? SeekRequested;
    public event EventHandler? EditCommitted;
    public event EventHandler<PlaybackChangedEventArgs>? PlaybackChanged;

    public string ProductName => AppInfo.ProductName;

    public ProjectDocument Project
    {
        get => _project;
        private set
        {
            if (SetProperty(ref _project, value))
            {
                OnPropertyChanged(nameof(ProjectName));
                OnPropertyChanged(nameof(DurationText));
            }
        }
    }

    public string ProjectName => Project.Name;

    public string SaveStatus
    {
        get => _saveStatus;
        private set => SetProperty(ref _saveStatus, value);
    }

    public EditorTool SelectedTool
    {
        get => _selectedTool;
        set => SetProperty(ref _selectedTool, value);
    }

    public EditorSelection Selection
    {
        get => _selection;
        private set
        {
            if (SetProperty(ref _selection, value))
            {
                SelectionChanged?.Invoke(this, new EditorSelectionChangedEventArgs(value));
            }
        }
    }

    public long PlayheadMilliseconds
    {
        get => _playheadMilliseconds;
        private set
        {
            var duration = TimelineEditingService.CalculateProjectDuration(Project);
            if (SetProperty(ref _playheadMilliseconds, TimelinePlaybackMath.ClampPosition(value, duration)))
            {
                OnPropertyChanged(nameof(PlayheadText));
            }
        }
    }

    public string PlayheadText => TimecodeFormatter.Format(PlayheadMilliseconds, Project.Settings.FrameRate);

    public string DurationText => TimecodeFormatter.Format(
        TimelineEditingService.CalculateProjectDuration(Project),
        Project.Settings.FrameRate);

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    internal long Revision => _revision;

    public void RequestReturnHome() => ReturnHomeRequested?.Invoke(this, EventArgs.Empty);

    public void RequestImport() => ImportRequested?.Invoke(this, EventArgs.Empty);

    public bool RenameProject(string name)
    {
        if (string.Equals(name, Project.Name, StringComparison.Ordinal))
        {
            return false;
        }

        string normalizedName;
        try
        {
            normalizedName = ProjectService.NormalizeName(name);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return TryCommitEdit(project =>
        {
            if (string.Equals(project.Name, normalizedName, StringComparison.Ordinal))
            {
                return false;
            }

            project.Name = normalizedName;
            return true;
        });
    }

    public void RequestAddText() => AddTextRequested?.Invoke(this, EventArgs.Empty);

    public TextTimelineItem AddDefaultText() => AddText(TextPreset.Default);

    public TextTimelineItem AddText(TextPreset preset)
    {
        var item = TextPresetFactory.Create(preset, PlayheadMilliseconds);
        CommitEdit(project => project.TextItems.Add(item));
        Select(new EditorSelection(EditorSelectionKind.TextItem, item.Id));
        RequestAddText();
        return item;
    }

    public void AddImportedAssets(IEnumerable<ProjectAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var additions = assets.ToList();
        if (additions.Count == 0)
        {
            return;
        }

        CommitEdit(project => project.Assets.AddRange(additions));
    }

    public bool AddAssetToTimeline(Guid assetId)
    {
        var selection = EditorSelection.None;
        if (!TryCommitEdit(project => TryAddAssetToTimeline(project, assetId, PlayheadMilliseconds, out selection)))
        {
            return false;
        }

        Select(selection);
        return true;
    }

    public IReadOnlyList<bool> AddImportedAssetsToTimeline(
        IEnumerable<ProjectAsset> assets,
        IReadOnlyList<Guid> assetIds)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(assetIds);
        var additions = assets.ToList();
        var added = new bool[assetIds.Count];
        var selection = EditorSelection.None;
        var committed = TryCommitEdit(project =>
        {
            project.Assets.AddRange(additions);
            var changed = additions.Count > 0;
            for (var index = 0; index < assetIds.Count; index++)
            {
                if (!TryAddAssetToTimeline(project, assetIds[index], PlayheadMilliseconds, out var itemSelection))
                {
                    continue;
                }

                added[index] = true;
                selection = itemSelection;
                changed = true;
            }

            return changed;
        });
        if (!committed)
        {
            Array.Clear(added);
            return added;
        }

        if (selection != EditorSelection.None)
        {
            Select(selection);
        }

        return added;
    }

    private static bool TryAddAssetToTimeline(
        ProjectDocument project,
        Guid assetId,
        long playheadMilliseconds,
        out EditorSelection selection)
    {
        selection = EditorSelection.None;
        var asset = project.Assets.FirstOrDefault(candidate => candidate.Id == assetId);
        if (asset is null || asset.IsMissing ||
            asset.DurationMilliseconds < ProjectDocument.MinimumItemDurationMilliseconds)
        {
            return false;
        }

        var timelineStart = asset.Kind == ProjectAssetKind.Audio
            ? Math.Clamp(
                playheadMilliseconds,
                0,
                ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds)
            : TimelineLayoutProjection.GetVideoBounds(project).LastOrDefault().EndMilliseconds;
        if (asset.Kind == ProjectAssetKind.Audio &&
            asset.DurationMilliseconds > ProjectDocument.MaximumTimelineDurationMilliseconds - timelineStart)
        {
            return false;
        }

        var duration = Math.Min(
            asset.DurationMilliseconds,
            ProjectDocument.MaximumTimelineDurationMilliseconds - timelineStart);
        if (duration < ProjectDocument.MinimumItemDurationMilliseconds)
        {
            return false;
        }

        if (asset.Kind == ProjectAssetKind.Audio)
        {
            var item = new AudioTimelineItem
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                StartMilliseconds = timelineStart,
                SourceInMilliseconds = 0,
                SourceOutMilliseconds = duration
            };
            project.AudioItems.Add(item);
            selection = new EditorSelection(EditorSelectionKind.AudioItem, item.Id);
        }
        else
        {
            var item = new VideoTimelineItem
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                SourceInMilliseconds = 0,
                SourceOutMilliseconds = duration,
                DurationMilliseconds = duration
            };
            project.VideoItems.Add(item);
            selection = new EditorSelection(EditorSelectionKind.VideoItem, item.Id);
        }

        return true;
    }

    public bool RemoveAsset(Guid assetId, out string? error)
    {
        if (MediaImportService.IsAssetReferenced(Project, assetId))
        {
            error = "This asset is used on the timeline. Remove its timeline items first.";
            return false;
        }

        if (Project.Assets.All(asset => asset.Id != assetId))
        {
            error = "The asset is no longer in this project.";
            return false;
        }

        CommitEdit(project => MediaImportService.TryRemoveAssetReference(project, assetId, out _));
        if (Selection is { Kind: EditorSelectionKind.Asset, ItemId: var selectedId } && selectedId == assetId)
        {
            Select(EditorSelection.None);
        }

        error = null;
        return true;
    }

    public bool ApplyRelinkedAsset(ProjectAsset updatedAsset)
    {
        ArgumentNullException.ThrowIfNull(updatedAsset);
        var existing = Project.Assets.FirstOrDefault(asset => asset.Id == updatedAsset.Id);
        if (existing is null ||
            existing.Kind != updatedAsset.Kind ||
            updatedAsset.DurationMilliseconds < MediaImportService.GetRequiredSourceOutMilliseconds(Project, updatedAsset.Id) ||
            MediaImportService.ContainsSourcePath(Project, updatedAsset.SourcePath, updatedAsset.Id))
        {
            return false;
        }

        if (string.Equals(existing.SourcePath, updatedAsset.SourcePath, StringComparison.Ordinal) &&
            string.Equals(existing.FileName, updatedAsset.FileName, StringComparison.Ordinal) &&
            existing.DurationMilliseconds == updatedAsset.DurationMilliseconds &&
            existing.Width == updatedAsset.Width &&
            existing.Height == updatedAsset.Height &&
            existing.FileSize == updatedAsset.FileSize &&
            existing.LastWriteUtc == updatedAsset.LastWriteUtc &&
            string.Equals(existing.ThumbnailCachePath, updatedAsset.ThumbnailCachePath, StringComparison.Ordinal) &&
            existing.IsMissing == updatedAsset.IsMissing)
        {
            Select(new EditorSelection(EditorSelectionKind.Asset, updatedAsset.Id));
            return true;
        }

        CommitEdit(project =>
        {
            var target = project.Assets.First(asset => asset.Id == updatedAsset.Id);
            lock (target)
            {
                target.SourcePath = updatedAsset.SourcePath;
                target.FileName = updatedAsset.FileName;
                target.DurationMilliseconds = updatedAsset.DurationMilliseconds;
                target.Width = updatedAsset.Width;
                target.Height = updatedAsset.Height;
                target.FileSize = updatedAsset.FileSize;
                target.LastWriteUtc = updatedAsset.LastWriteUtc;
                target.ThumbnailCachePath = updatedAsset.ThumbnailCachePath;
                target.IsMissing = updatedAsset.IsMissing;
            }
        });
        Select(new EditorSelection(EditorSelectionKind.Asset, updatedAsset.Id));
        return true;
    }

    public void RequestExport() => ExportRequested?.Invoke(this, EventArgs.Empty);

    public void Select(EditorSelection selection) => Selection = selection;

    public void Seek(long positionMilliseconds)
    {
        PlayheadMilliseconds = positionMilliseconds;
        SeekRequested?.Invoke(this, new PlayheadChangedEventArgs(PlayheadMilliseconds));
    }

    public bool ReorderVideoItem(Guid itemId, int targetIndex) =>
        TryCommitEdit(project => TimelineEditingService.ReorderVideoItem(project, itemId, targetIndex));

    public bool SplitVideoItem(Guid itemId, long timelinePositionMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.SplitVideoItem(project, itemId, timelinePositionMilliseconds));

    public bool SplitSelection() => Selection is { Kind: EditorSelectionKind.VideoItem, ItemId: Guid itemId } &&
        SplitVideoItem(itemId, PlayheadMilliseconds);

    public bool TrimVideoStart(Guid itemId, long sourceInMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.TrimVideoStart(project, itemId, sourceInMilliseconds));

    public bool TrimVideoEnd(Guid itemId, long sourceOutMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.TrimVideoEnd(project, itemId, sourceOutMilliseconds));

    public bool SetImageDuration(Guid itemId, long durationMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.SetImageDuration(project, itemId, durationMilliseconds));

    public bool ResetImageDuration(Guid itemId) =>
        TryCommitEdit(project => TimelineEditingService.ResetImageDuration(project, itemId));

    public bool MoveAudioItem(Guid itemId, long startMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.MoveAudioItem(project, itemId, startMilliseconds));

    public bool TrimAudioStart(Guid itemId, long sourceInMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.TrimAudioStart(project, itemId, sourceInMilliseconds));

    public bool TrimAudioEnd(Guid itemId, long sourceOutMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.TrimAudioEnd(project, itemId, sourceOutMilliseconds));

    public bool MoveTextItem(Guid itemId, long startMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.MoveTextItem(project, itemId, startMilliseconds));

    public bool TrimTextStart(Guid itemId, long startMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.TrimTextStart(project, itemId, startMilliseconds));

    public bool TrimTextEnd(Guid itemId, long endMilliseconds) =>
        TryCommitEdit(project => TimelineEditingService.TrimTextEnd(project, itemId, endMilliseconds));

    public bool DeleteSelection()
    {
        var selection = Selection;
        var changed = TryCommitEdit(project => TimelineEditingService.DeleteSelection(project, selection));
        if (changed)
        {
            Select(EditorSelection.None);
        }

        return changed;
    }

    public bool DuplicateSelection() => DuplicateSelection(Selection);

    public bool DuplicateTimelineItem(Guid itemId)
    {
        var selection = Project.VideoItems.Any(item => item.Id == itemId)
            ? new EditorSelection(EditorSelectionKind.VideoItem, itemId)
            : Project.AudioItems.Any(item => item.Id == itemId)
                ? new EditorSelection(EditorSelectionKind.AudioItem, itemId)
                : Project.TextItems.Any(item => item.Id == itemId)
                    ? new EditorSelection(EditorSelectionKind.TextItem, itemId)
                    : EditorSelection.None;
        return DuplicateSelection(selection);
    }

    private bool DuplicateSelection(EditorSelection selection)
    {
        var duplicated = EditorSelection.None;
        var changed = TryCommitEdit(project =>
            TimelineEditingService.DuplicateSelection(project, selection, out duplicated));
        if (changed)
        {
            Select(duplicated);
        }

        return changed;
    }

    public bool SetProjectAspectRatio(AspectRatioPreset aspectRatio) =>
        TryCommitEdit(project =>
        {
            if (!Enum.IsDefined(aspectRatio) || project.Settings.AspectRatio == aspectRatio)
            {
                return false;
            }

            project.Settings.ApplyAspectRatio(aspectRatio);
            return true;
        });

    public bool SetBackgroundColor(string backgroundColor)
    {
        if (!TimelineInput.IsOpaqueArgb(backgroundColor))
        {
            return false;
        }

        var normalized = backgroundColor.ToUpperInvariant();
        return TryCommitEdit(project =>
        {
            if (string.Equals(project.Settings.BackgroundColor, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            project.Settings.BackgroundColor = normalized;
            return true;
        });
    }

    public bool SetVideoTrackVisible(bool isVisible) => TryCommitEdit(project =>
    {
        if (project.Settings.VideoTrackVisible == isVisible) return false;
        project.Settings.VideoTrackVisible = isVisible;
        return true;
    });

    public bool SetTextTrackVisible(bool isVisible) => TryCommitEdit(project =>
    {
        if (project.Settings.TextTrackVisible == isVisible) return false;
        project.Settings.TextTrackVisible = isVisible;
        return true;
    });

    public bool SetAudioTrackMuted(bool isMuted) => TryCommitEdit(project =>
    {
        if (project.Settings.AudioTrackMuted == isMuted) return false;
        project.Settings.AudioTrackMuted = isMuted;
        return true;
    });

    public bool SetVideoVolume(Guid itemId, double volume) =>
        TryCommitEdit(project => TimelineEditingService.SetVideoVolume(project, itemId, volume));

    public bool SetVideoMuted(Guid itemId, bool isMuted) =>
        TryCommitEdit(project => TimelineEditingService.SetVideoMuted(project, itemId, isMuted));

    public bool SetAudioVolume(Guid itemId, double volume) =>
        TryCommitEdit(project => TimelineEditingService.SetAudioVolume(project, itemId, volume));

    public bool SetAudioMuted(Guid itemId, bool isMuted) =>
        TryCommitEdit(project => TimelineEditingService.SetAudioMuted(project, itemId, isMuted));

    public bool SetTextContent(Guid itemId, string text) => TryCommitEdit(project =>
    {
        var item = project.TextItems.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null || string.Equals(item.Text, text, StringComparison.Ordinal))
        {
            return false;
        }

        item.Text = text;
        return true;
    });

    public bool SetTextFontFamily(Guid itemId, string fontFamily) => UpdateText(itemId, item =>
    {
        var normalized = TextStyle.NormalizeFontFamily(fontFamily);
        if (item.FontFamily == normalized) return false;
        item.FontFamily = normalized;
        return true;
    });

    public bool SetTextFontSize(Guid itemId, double fontSize) => UpdateText(itemId, item =>
    {
        var normalized = double.IsFinite(fontSize) ? Math.Clamp(fontSize, 8, 400) : TextTimelineItem.DefaultFontSize;
        if (item.FontSize == normalized) return false;
        item.FontSize = normalized;
        return true;
    });

    public bool SetTextFontWeight(Guid itemId, int fontWeight) => UpdateText(itemId, item =>
    {
        var normalized = Math.Clamp(fontWeight, 1, 999);
        if (item.FontWeight == normalized) return false;
        item.FontWeight = normalized;
        return true;
    });

    public bool SetTextBold(Guid itemId, bool isBold) => UpdateText(itemId, item =>
    {
        if (TextStyle.IsBold(item.FontWeight) == isBold)
        {
            return false;
        }

        item.FontWeight = isBold ? TextTimelineItem.BoldFontWeight : TextTimelineItem.DefaultFontWeight;
        return true;
    });

    public bool SetTextItalic(Guid itemId, bool isItalic) => UpdateText(itemId, item =>
    {
        if (item.IsItalic == isItalic) return false;
        item.IsItalic = isItalic;
        return true;
    });

    public bool SetTextColor(Guid itemId, string color, bool background) => UpdateText(itemId, item =>
    {
        if (!TextStyle.IsArgb(color)) return false;
        var normalized = color.ToUpperInvariant();
        var current = background ? item.BackgroundColor : item.TextColor;
        if (current == normalized) return false;
        if (background) item.BackgroundColor = normalized; else item.TextColor = normalized;
        return true;
    });

    public bool SetTextBackgroundEnabled(Guid itemId, bool isEnabled) => UpdateText(itemId, item =>
    {
        if (item.BackgroundEnabled == isEnabled) return false;
        item.BackgroundEnabled = isEnabled;
        return true;
    });

    public bool SetTextOpacity(Guid itemId, double opacity) => UpdateText(itemId, item =>
    {
        var normalized = TextStyle.ClampOpacity(opacity);
        if (item.Opacity == normalized) return false;
        item.Opacity = normalized;
        return true;
    });

    public bool SetTextAlignment(Guid itemId, TextHorizontalAlignment alignment) => UpdateText(itemId, item =>
    {
        if (!Enum.IsDefined(alignment) || item.Alignment == alignment) return false;
        item.Alignment = alignment;
        return true;
    });

    public bool SetTextDuration(Guid itemId, long durationMilliseconds) => TryCommitEdit(project =>
    {
        var item = project.TextItems.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            return false;
        }

        var clamped = TimelineMath.ClampItemDuration(durationMilliseconds, item.StartMilliseconds);
        if (item.DurationMilliseconds == clamped)
        {
            return false;
        }

        item.DurationMilliseconds = clamped;
        return true;
    });

    public bool SetTextPosition(Guid itemId, double normalizedX, double normalizedY) => TryCommitEdit(project =>
    {
        var item = project.TextItems.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            return false;
        }

        var x = TextStyle.ClampNormalized(normalizedX);
        var y = TextStyle.ClampNormalized(normalizedY);
        if (item.NormalizedX == x && item.NormalizedY == y)
        {
            return false;
        }

        item.NormalizedX = x;
        item.NormalizedY = y;
        return true;
    });

    public bool SetTextHorizontalPosition(Guid itemId, double normalizedX) => UpdateText(itemId, item =>
    {
        var normalized = TextStyle.ClampNormalized(normalizedX);
        if (item.NormalizedX == normalized) return false;
        item.NormalizedX = normalized;
        return true;
    });

    public bool SetTextVerticalPosition(Guid itemId, double normalizedY) => UpdateText(itemId, item =>
    {
        var normalized = TextStyle.ClampNormalized(normalizedY);
        if (item.NormalizedY == normalized) return false;
        item.NormalizedY = normalized;
        return true;
    });

    public bool ResetTextStyle(Guid itemId) => UpdateText(itemId, item =>
    {
        var isDefault =
            item.FontFamily == TextTimelineItem.DefaultFontFamily &&
            item.FontSize == TextTimelineItem.DefaultFontSize &&
            item.FontWeight == TextTimelineItem.DefaultFontWeight &&
            !item.IsItalic &&
            item.TextColor == TextTimelineItem.DefaultTextColor &&
            item.BackgroundColor == TextTimelineItem.DefaultBackgroundColor &&
            !item.BackgroundEnabled &&
            item.Opacity == TextTimelineItem.DefaultOpacity &&
            item.Alignment == TextHorizontalAlignment.Center;
        if (isDefault)
        {
            return false;
        }

        item.FontFamily = TextTimelineItem.DefaultFontFamily;
        item.FontSize = TextTimelineItem.DefaultFontSize;
        item.FontWeight = TextTimelineItem.DefaultFontWeight;
        item.IsItalic = false;
        item.TextColor = TextTimelineItem.DefaultTextColor;
        item.BackgroundColor = TextTimelineItem.DefaultBackgroundColor;
        item.BackgroundEnabled = false;
        item.Opacity = TextTimelineItem.DefaultOpacity;
        item.Alignment = TextHorizontalAlignment.Center;
        return true;
    });

    public void TogglePlayback()
    {
        IsPlaying = !IsPlaying;
        PlaybackChanged?.Invoke(this, new PlaybackChangedEventArgs(IsPlaying));
    }

    private bool UpdateText(Guid itemId, Func<TextTimelineItem, bool> update) => TryCommitEdit(project =>
    {
        var item = project.TextItems.FirstOrDefault(candidate => candidate.Id == itemId);
        return item is not null && update(item);
    });

    public void CommitEdit(Action<ProjectDocument> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        _history.Record(Project);
        edit(Project);
        _revision++;
        ClampPlayheadToProjectDuration();
        SaveStatus = UnsavedStatus;
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(DurationText));
        EditCommitted?.Invoke(this, EventArgs.Empty);
    }

    public bool TryCommitEdit(Func<ProjectDocument, bool> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var candidate = ProjectDocumentCloner.Clone(Project);
        if (!edit(candidate) || !TimelineEditingService.IsWithinProjectDurationLimit(candidate))
        {
            return false;
        }

        _history.Record(Project);
        Project = candidate;
        _revision++;
        ClampPlayheadToProjectDuration();
        SaveStatus = UnsavedStatus;
        NormalizeSelection();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(DurationText));
        EditCommitted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Undo()
    {
        var restored = _history.Undo(Project);
        if (restored is null)
        {
            return;
        }

        Project = restored;
        _revision++;
        ClampPlayheadToProjectDuration();
        NormalizeSelection();
        SaveStatus = UnsavedStatus;
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        EditCommitted?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        var restored = _history.Redo(Project);
        if (restored is null)
        {
            return;
        }

        Project = restored;
        _revision++;
        ClampPlayheadToProjectDuration();
        NormalizeSelection();
        SaveStatus = UnsavedStatus;
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        EditCommitted?.Invoke(this, EventArgs.Empty);
    }

    public void MarkSaved() => SaveStatus = SavedStatus;

    internal bool TryMarkSaved(long revision)
    {
        if (_revision != revision)
        {
            return false;
        }

        MarkSaved();
        return true;
    }

    public void MarkSaving() => SaveStatus = SavingStatus;

    public void MarkSaveFailed() => SaveStatus = SaveFailedStatus;

    private void NormalizeSelection()
    {
        if (Selection.ItemId is not Guid itemId)
        {
            return;
        }

        var exists = Selection.Kind switch
        {
            EditorSelectionKind.Asset => Project.Assets.Any(item => item.Id == itemId),
            EditorSelectionKind.VideoItem => Project.VideoItems.Any(item => item.Id == itemId),
            EditorSelectionKind.AudioItem => Project.AudioItems.Any(item => item.Id == itemId),
            EditorSelectionKind.TextItem => Project.TextItems.Any(item => item.Id == itemId),
            _ => false
        };
        if (!exists)
        {
            Selection = EditorSelection.None;
        }
    }

    private void ClampPlayheadToProjectDuration() => PlayheadMilliseconds = _playheadMilliseconds;
}

public enum EditorTool
{
    Media,
    Audio,
    Text,
    Stickers,
    Effects,
    Transitions,
    Captions,
    Filters,
    Adjustment
}

public sealed class EditorSelectionChangedEventArgs(EditorSelection selection) : EventArgs
{
    public EditorSelection Selection { get; } = selection;
}

public sealed class PlayheadChangedEventArgs(long positionMilliseconds) : EventArgs
{
    public long PositionMilliseconds { get; } = positionMilliseconds;
}

public sealed class PlaybackChangedEventArgs(bool isPlaying) : EventArgs
{
    public bool IsPlaying { get; } = isPlaying;
}
