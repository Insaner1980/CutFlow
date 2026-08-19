using CutFlow.Models;

namespace CutFlow.Services;

public sealed partial class PreviewRebuildGate : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _current;
    private long _version;
    private bool _disposed;

    public PreviewRebuildLease Begin(CancellationToken lifetimeToken = default)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _current?.Cancel();
            _current?.Dispose();
            _current = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
            return new PreviewRebuildLease(this, ++_version, _current.Token);
        }
    }

    internal bool IsCurrent(long version)
    {
        lock (_sync)
        {
            return !_disposed && version == _version && _current is { IsCancellationRequested: false };
        }
    }

    internal bool TryCommit(long version, Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        lock (_sync)
        {
            if (_disposed || version != _version || _current is not { IsCancellationRequested: false })
            {
                return false;
            }

            commit();
            return true;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _version++;
            _current?.Cancel();
            _current?.Dispose();
            _current = null;
        }
    }
}

public sealed partial class PreviewRebuildLease : IDisposable
{
    private readonly PreviewRebuildGate _owner;

    internal PreviewRebuildLease(PreviewRebuildGate owner, long version, CancellationToken token)
    {
        _owner = owner;
        Version = version;
        Token = token;
    }

    public long Version { get; }
    public CancellationToken Token { get; }
    public bool IsCurrent => _owner.IsCurrent(Version);
    public bool TryCommit(Action commit) => _owner.TryCommit(Version, commit);
    public void Dispose() { }
}

internal sealed class PreviewCompositionKey : IEquatable<PreviewCompositionKey>
{
    private readonly int _width;
    private readonly int _height;
    private readonly string _backgroundColor;
    private readonly bool _videoTrackVisible;
    private readonly bool _audioTrackMuted;
    private readonly long _durationMilliseconds;
    private readonly PreviewAssetKey[] _assets;
    private readonly PreviewVideoItemKey[] _videoItems;
    private readonly PreviewAudioItemKey[] _audioItems;

    private PreviewCompositionKey(ProjectDocument project)
    {
        _width = project.Settings.Width;
        _height = project.Settings.Height;
        _backgroundColor = project.Settings.BackgroundColor;
        _videoTrackVisible = project.Settings.VideoTrackVisible;
        _audioTrackMuted = project.Settings.AudioTrackMuted;
        _durationMilliseconds = TimelineEditingService.CalculateProjectDuration(project);
        _assets = project.Assets
            .OrderBy(asset => asset.Id)
            .Select(asset => new PreviewAssetKey(
                asset.Id,
                asset.Kind,
                asset.SourcePath,
                asset.FileName,
                asset.DurationMilliseconds,
                asset.Width,
                asset.Height,
                asset.FileSize,
                asset.LastWriteUtc,
                asset.IsMissing))
            .ToArray();
        _videoItems = project.VideoItems
            .Select(item => new PreviewVideoItemKey(
                item.Id,
                item.AssetId,
                item.SourceInMilliseconds,
                item.SourceOutMilliseconds,
                item.DurationMilliseconds,
                item.Volume,
                item.IsMuted))
            .ToArray();
        _audioItems = project.AudioItems
            .Select(item => new PreviewAudioItemKey(
                item.Id,
                item.AssetId,
                item.StartMilliseconds,
                item.SourceInMilliseconds,
                item.SourceOutMilliseconds,
                item.Volume,
                item.IsMuted))
            .ToArray();
    }

    public static PreviewCompositionKey Create(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new PreviewCompositionKey(project);
    }

    public bool Equals(PreviewCompositionKey? other) =>
        other is not null &&
        _width == other._width &&
        _height == other._height &&
        string.Equals(_backgroundColor, other._backgroundColor, StringComparison.Ordinal) &&
        _videoTrackVisible == other._videoTrackVisible &&
        _audioTrackMuted == other._audioTrackMuted &&
        _durationMilliseconds == other._durationMilliseconds &&
        _assets.SequenceEqual(other._assets) &&
        _videoItems.SequenceEqual(other._videoItems) &&
        _audioItems.SequenceEqual(other._audioItems);

    public override bool Equals(object? obj) => Equals(obj as PreviewCompositionKey);

    public override int GetHashCode() => HashCode.Combine(
        _width,
        _height,
        _backgroundColor,
        _videoTrackVisible,
        _audioTrackMuted,
        _durationMilliseconds,
        _assets.Length,
        HashCode.Combine(_videoItems.Length, _audioItems.Length));
}

internal readonly record struct PreviewAssetKey(
    Guid Id,
    ProjectAssetKind Kind,
    string SourcePath,
    string FileName,
    long DurationMilliseconds,
    int Width,
    int Height,
    ulong FileSize,
    DateTimeOffset LastWriteUtc,
    bool IsMissing);

internal readonly record struct PreviewVideoItemKey(
    Guid Id,
    Guid AssetId,
    long SourceInMilliseconds,
    long SourceOutMilliseconds,
    long DurationMilliseconds,
    double Volume,
    bool IsMuted);

internal readonly record struct PreviewAudioItemKey(
    Guid Id,
    Guid AssetId,
    long StartMilliseconds,
    long SourceInMilliseconds,
    long SourceOutMilliseconds,
    double Volume,
    bool IsMuted);
