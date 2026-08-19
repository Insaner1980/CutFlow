using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CutFlow.Models;
using CutFlow.Utilities;
using Windows.Storage;

namespace CutFlow.Services;

public sealed partial class ProjectService
{
    internal const int MaximumProjectJsonCharacters = 16 * 1024 * 1024;
    internal const int MaximumAssetCount = 2_000;
    internal const int MaximumTimelineItemCount = 10_000;
    internal const int MaximumPersistedNameLength = 1_024;
    internal const int MaximumPersistedFileNameLength = 1_024;
    internal const int MaximumPersistedSourcePathLength = 32_767;
    internal const int MaximumPersistedTextLength = 4_096;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true,
        MaxDepth = 32
    };

    private readonly string _projectsRootPath;
    private readonly Func<ProjectDocument, string> _serialize;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<string, string, CancellationToken, Task> _writeAndFlushAsync;
    private readonly Action<string> _createProjectDirectory;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public ProjectService()
        : this(null)
    {
    }

    internal ProjectService(
        string? rootPath,
        Func<ProjectDocument, string>? serialize = null,
        Func<DateTimeOffset>? utcNow = null,
        Func<string, string, CancellationToken, Task>? writeAndFlushAsync = null,
        Action<string>? createProjectDirectory = null)
    {
        var localRootPath = rootPath ?? ApplicationData.Current.LocalFolder.Path;
        if (string.IsNullOrWhiteSpace(localRootPath))
        {
            throw new ArgumentException("A local project root path is required.", nameof(rootPath));
        }

        _projectsRootPath = Path.GetFullPath(Path.Combine(localRootPath, "Projects"));
        _serialize = serialize ?? (project => JsonSerializer.Serialize(project, JsonOptions));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _writeAndFlushAsync = writeAndFlushAsync ?? WriteAndFlushAsync;
        _createProjectDirectory = createProjectDirectory ?? CreateNewDirectory;
    }

    public async Task<ProjectDocument> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var project = ProjectDocument.CreateNew(NormalizeName(name), GetUtcNow());
        var projectDirectory = GetProjectDirectoryPath(project.Id);
        var createdProjectDirectory = false;
        try
        {
            Directory.CreateDirectory(_projectsRootPath);
            RejectReparsePoints(_projectsRootPath);
            _createProjectDirectory(projectDirectory);
            createdProjectDirectory = true;
            Directory.CreateDirectory(GetCachePath(project.Id));
            await SaveCoreAsync(project, overwriteExisting: false, cancellationToken: cancellationToken);
            return project;
        }
        catch (Exception exception)
        {
            if (createdProjectDirectory && Directory.Exists(projectDirectory))
            {
                try
                {
                    RejectReparsePoints(projectDirectory);
                    var cacheDirectory = GetCachePath(project.Id);
                    if (Directory.Exists(cacheDirectory))
                    {
                        Directory.Delete(cacheDirectory, recursive: false);
                    }

                    Directory.Delete(projectDirectory, recursive: false);
                }
                catch (Exception cleanupException) when (!ExceptionPolicy.IsFatal(cleanupException))
                {
                    exception.Data["ProjectDirectoryCleanupException"] = cleanupException;
                }
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<ProjectDocument>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() => ListCoreAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<ProjectDocument>> ListCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_projectsRootPath))
        {
            return [];
        }

        var projects = new List<ProjectDocument>();
        foreach (var directory in Directory.EnumerateDirectories(_projectsRootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryName = Path.GetFileName(directory);
            if (!Guid.TryParseExact(directoryName, "D", out var projectId) ||
                !string.Equals(directoryName, projectId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                projects.Add(await LoadCoreAsync(projectId, cancellationToken));
            }
            catch (IOException)
            {
                // A damaged or concurrently removed project cannot be a recent project entry.
            }
            catch (UnauthorizedAccessException)
            {
                // A project that cannot be read is not exposed as a usable recent project.
            }
            catch (InvalidDataException)
            {
                // Invalid JSON is left untouched for a later explicit recovery path.
            }
        }

        return projects
            .OrderByDescending(project => project.ModifiedAt)
            .ThenBy(project => project.Id)
            .ToList();
    }

    public async Task<ProjectDocument> LoadAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() => LoadCoreAsync(projectId, cancellationToken));
    }

    private async Task<ProjectDocument> LoadCoreAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var projectPath = GetProjectFilePath(projectId);
        string json;
        try
        {
            json = await ReadBoundedJsonAsync(projectPath, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The project JSON could not be read.", exception);
        }

        ProjectDocument project;
        try
        {
            using var jsonDocument = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = JsonOptions.MaxDepth });
            var root = jsonDocument.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The project JSON did not contain a document.");
            }

            if (!root.TryGetProperty("schemaVersion", out var schemaVersionElement))
            {
                throw new InvalidDataException("The project schema version is missing.");
            }

            if (schemaVersionElement.ValueKind != JsonValueKind.Number ||
                !schemaVersionElement.TryGetInt32(out var schemaVersion))
            {
                throw new InvalidDataException("The project schema version is invalid.");
            }

            if (schemaVersion != ProjectDocument.CurrentSchemaVersion)
            {
                throw new InvalidDataException($"Unsupported project schema version {schemaVersion}.");
            }

            ValidateJsonResourceBounds(root);
            project = root.Deserialize<ProjectDocument>(JsonOptions)
                ?? throw new InvalidDataException("The project JSON did not contain a document.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The project JSON could not be read.", exception);
        }

        if (project.Id != projectId)
        {
            throw new InvalidDataException("The project identity does not match its directory.");
        }

        Normalize(project, _projectsRootPath);
        return project;
    }

    public Task SaveAsync(ProjectDocument project, CancellationToken cancellationToken = default) =>
        SaveCoreAsync(project, overwriteExisting: true, cancellationToken: cancellationToken);

    private async Task SaveCoreAsync(
        ProjectDocument project,
        bool overwriteExisting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Id == Guid.Empty)
        {
            throw new ArgumentException("A project must have an identity before it can be saved.", nameof(project));
        }

        if (project.SchemaVersion != ProjectDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported project schema version {project.SchemaVersion}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectDirectory = GetProjectDirectoryPath(project.Id);
            Normalize(project, _projectsRootPath);
            cancellationToken.ThrowIfCancellationRequested();
            var candidateModifiedAt = GetUtcNow();
            var snapshot = ProjectDocumentCloner.Clone(project);
            snapshot.ModifiedAt = candidateModifiedAt;
            var projectPath = GetProjectFilePath(project.Id);
            var temporaryPath = $"{projectPath}.{Guid.NewGuid():N}.tmp";
            var replaceExisting = overwriteExisting && File.Exists(projectPath);
            var committed = false;
            Exception? saveException = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(projectDirectory);
                RejectReparsePoints(projectDirectory);
                RejectReparsePoints(projectPath);
                RejectReparsePoints(temporaryPath);
                var json = _serialize(snapshot);
                cancellationToken.ThrowIfCancellationRequested();
                await _writeAndFlushAsync(temporaryPath, json, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                RejectReparsePoints(projectPath);
                RejectReparsePoints(temporaryPath);
                cancellationToken.ThrowIfCancellationRequested();
                if (replaceExisting)
                {
                    File.Replace(temporaryPath, projectPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temporaryPath, projectPath);
                }

                committed = true;
                project.CreatedAt = snapshot.CreatedAt;
                project.ModifiedAt = candidateModifiedAt;
            }
            catch (Exception exception)
            {
                saveException = exception;
                throw;
            }
            finally
            {
                if (!committed && File.Exists(temporaryPath))
                {
                    try
                    {
                        RejectReparsePoints(temporaryPath);
                        File.Delete(temporaryPath);
                    }
                    catch (Exception cleanupException) when (!ExceptionPolicy.IsFatal(cleanupException))
                    {
                        saveException!.Data["TemporaryFileCleanupException"] = cleanupException;
                    }
                }
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public async Task<ProjectDocument> RenameAsync(Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        var project = await LoadAsync(projectId, cancellationToken);
        project.Name = NormalizeName(name);
        await SaveAsync(project, cancellationToken);
        return project;
    }

    public async Task<ProjectDocument> DuplicateAsync(Guid projectId, string? name = null, CancellationToken cancellationToken = default)
    {
        var original = await LoadAsync(projectId, cancellationToken);
        var duplicate = ProjectDocumentCloner.Clone(original);
        duplicate.Id = Guid.NewGuid();
        duplicate.Name = name is null ? CreateDuplicateName(original.Name) : NormalizeName(name);
        var now = GetUtcNow();
        duplicate.CreatedAt = now;
        duplicate.ModifiedAt = now;
        await SaveCoreAsync(duplicate, overwriteExisting: false, cancellationToken: cancellationToken);
        return duplicate;
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectDirectory = GetProjectDirectoryPath(projectId);
        if (Directory.Exists(projectDirectory))
        {
            if (File.Exists(GetProjectFilePath(projectId)))
            {
                await LoadAsync(projectId, cancellationToken);
            }

            RejectReparsePoints(projectDirectory);
            await Task.Run(() => Directory.Delete(projectDirectory, recursive: true), cancellationToken);
        }
    }

    public string GetCachePath(Guid projectId)
    {
        var cachePath = Path.Combine(GetProjectDirectoryPath(projectId), "cache");
        RejectReparsePoints(cachePath);
        return cachePath;
    }

    public string GetProjectPath(Guid projectId) => GetProjectDirectoryPath(projectId);

    internal string GetProjectsPath() => _projectsRootPath;

    private DateTimeOffset GetUtcNow() => _utcNow().ToUniversalTime();

    private string GetProjectFilePath(Guid projectId)
    {
        var projectPath = Path.Combine(GetProjectDirectoryPath(projectId), "project.json");
        RejectReparsePoints(projectPath);
        return projectPath;
    }

    private string GetProjectDirectoryPath(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identity is required.", nameof(projectId));
        }

        var projectDirectory = Path.GetFullPath(Path.Combine(_projectsRootPath, projectId.ToString("D")));
        var parentDirectory = Directory.GetParent(projectDirectory)?.FullName;
        if (!PathsEqual(parentDirectory, _projectsRootPath))
        {
            throw new InvalidOperationException("The resolved project directory is outside the configured Projects directory.");
        }

        RejectReparsePoints(projectDirectory);

        return projectDirectory;
    }

    internal static void RejectReparsePoints(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var currentPath = Path.GetFullPath(path);
        while (true)
        {
            System.IO.FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(currentPath);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                attributes = 0;
            }

            if ((attributes & System.IO.FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Managed project paths cannot contain symbolic links or reparse points.");
            }

            var parentPath = Directory.GetParent(currentPath)?.FullName;
            if (parentPath is null || PathsEqual(parentPath, currentPath))
            {
                return;
            }

            currentPath = parentPath;
        }
    }

    private static async Task WriteAndFlushAsync(string path, string json, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static async Task<string> ReadBoundedJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var json = new StringBuilder((int)Math.Min(stream.Length, MaximumProjectJsonCharacters));
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                return json.ToString();
            }

            if (json.Length > MaximumProjectJsonCharacters - read)
            {
                throw new InvalidDataException("The project JSON exceeds the supported size limit.");
            }

            json.Append(buffer, 0, read);
        }
    }

    private static void CreateNewDirectory(string path)
    {
        if (CreateDirectoryW(path, 0))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        throw new IOException(
            error == 183
                ? "The project directory already exists."
                : "The project directory could not be created.",
            new Win32Exception(error));
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateDirectoryW(string path, nint securityAttributes);

    internal static void NormalizeSnapshot(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.SchemaVersion != ProjectDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported project schema version {project.SchemaVersion}.");
        }

        Normalize(project, projectsRootPath: null);
    }

    private static void Normalize(ProjectDocument project, string? projectsRootPath)
    {
        NormalizeProjectSettings(project);
        InitializeCollections(project);
        var assetsById = NormalizeAssets(project.Assets, projectsRootPath);
        ValidateTimelineItems(project, assetsById);
        NormalizeVideoItems(project.VideoItems, assetsById);
        NormalizeAudioItems(project.AudioItems, assetsById);
        NormalizeTextItems(project.TextItems);
    }

    private static void ValidateJsonResourceBounds(JsonElement root)
    {
        if (JsonStringExceedsLimit(root, "name", MaximumPersistedNameLength))
        {
            throw new InvalidDataException("The project name exceeds the supported length limit.");
        }

        var assetCount = GetJsonArrayLength(root, "assets");
        var videoItemCount = GetJsonArrayLength(root, "videoItems");
        var audioItemCount = GetJsonArrayLength(root, "audioItems");
        var textItemCount = GetJsonArrayLength(root, "textItems");
        if (assetCount > MaximumAssetCount ||
            (long)videoItemCount + audioItemCount + textItemCount > MaximumTimelineItemCount)
        {
            throw new InvalidDataException("The project contains more assets or timeline items than supported.");
        }

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array &&
            assets.EnumerateArray().Any(asset =>
                asset.ValueKind == JsonValueKind.Object &&
                (JsonStringExceedsLimit(asset, "sourcePath", MaximumPersistedSourcePathLength) ||
                 JsonStringExceedsLimit(asset, "fileName", MaximumPersistedFileNameLength))))
        {
            throw new InvalidDataException("A project asset contains an oversized path or file name.");
        }

        if (root.TryGetProperty("textItems", out var textItems) && textItems.ValueKind == JsonValueKind.Array &&
            textItems.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.Object &&
                JsonStringExceedsLimit(item, "text", MaximumPersistedTextLength)))
        {
            throw new InvalidDataException("A text item exceeds the supported content length limit.");
        }
    }

    private static int GetJsonArrayLength(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.GetArrayLength()
            : 0;

    private static bool JsonStringExceedsLimit(JsonElement root, string propertyName, int maximumLength) =>
        root.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String &&
        property.GetString() is { } value &&
        value.Length > maximumLength;

    private static void NormalizeProjectSettings(ProjectDocument project)
    {
        project.Name ??= string.Empty;
        project.Settings ??= new ProjectSettings();
        project.Settings.ApplyAspectRatio(project.Settings.AspectRatio);
        project.Settings.FrameRate = 30;
        project.Settings.BackgroundColor = TimelineInput.IsOpaqueArgb(project.Settings.BackgroundColor)
            ? project.Settings.BackgroundColor.ToUpperInvariant()
            : ProjectSettings.DefaultBackgroundColor;
    }

    private static void InitializeCollections(ProjectDocument project)
    {
        project.Assets ??= [];
        project.VideoItems ??= [];
        project.AudioItems ??= [];
        project.TextItems ??= [];
        if (project.Assets.Any(static item => item is null) ||
            project.VideoItems.Any(static item => item is null) ||
            project.AudioItems.Any(static item => item is null) ||
            project.TextItems.Any(static item => item is null))
        {
            throw new InvalidDataException("Project collections cannot contain null items.");
        }
    }

    private static Dictionary<Guid, ProjectAsset> NormalizeAssets(
        IEnumerable<ProjectAsset> assets,
        string? projectsRootPath)
    {
        var assetsById = new Dictionary<Guid, ProjectAsset>();
        var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            NormalizeAsset(asset, assetsById, projectsRootPath);
            if (asset.SourcePath.Length > 0 && !sourcePaths.Add(asset.SourcePath))
            {
                throw new InvalidDataException("Project assets must reference unique source paths.");
            }
        }

        return assetsById;
    }

    private static void NormalizeAsset(
        ProjectAsset asset,
        Dictionary<Guid, ProjectAsset> assetsById,
        string? projectsRootPath)
    {
        asset.SourcePath ??= string.Empty;
        asset.FileName ??= string.Empty;
        asset.ThumbnailCachePath ??= string.Empty;
        if (asset.ThumbnailCachePath.Length > 0 &&
            !ThumbnailService.IsCanonicalRelativeCachePath(asset.ThumbnailCachePath))
        {
            asset.ThumbnailCachePath = string.Empty;
        }

        if (asset.Id == Guid.Empty || !assetsById.TryAdd(asset.Id, asset))
        {
            throw new InvalidDataException("Project assets must have unique non-empty identities.");
        }

        if (string.IsNullOrWhiteSpace(asset.SourcePath))
        {
            QuarantineAssetSource(asset);
            return;
        }

        try
        {
            NormalizeAssetSource(asset, projectsRootPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            QuarantineAssetSource(asset);
        }
    }

    private static void NormalizeAssetSource(ProjectAsset asset, string? projectsRootPath)
    {
        if (!Path.IsPathFullyQualified(asset.SourcePath))
        {
            QuarantineAssetSource(asset);
            return;
        }

        var normalizedSourcePath = MediaImportService.NormalizePath(asset.SourcePath);
        if (projectsRootPath is not null &&
            MediaImportService.IsPathWithinDirectory(normalizedSourcePath, projectsRootPath))
        {
            throw new InvalidDataException(
                $"Source media must remain outside {AppInfo.ProductName}'s managed project folders.");
        }

        if (!MediaImportService.TryGetKind(normalizedSourcePath, out var sourceKind) || sourceKind != asset.Kind)
        {
            QuarantineAssetSource(asset);
            return;
        }

        asset.SourcePath = normalizedSourcePath;
        if (string.IsNullOrWhiteSpace(asset.FileName))
        {
            asset.FileName = Path.GetFileName(normalizedSourcePath);
        }
    }

    private static void ValidateTimelineItems(
        ProjectDocument project,
        Dictionary<Guid, ProjectAsset> assetsById)
    {

        var timelineItemIds = new HashSet<Guid>();
        if (project.VideoItems.Select(static item => item.Id)
            .Concat(project.AudioItems.Select(static item => item.Id))
            .Concat(project.TextItems.Select(static item => item.Id))
            .Any(itemId => itemId == Guid.Empty || !timelineItemIds.Add(itemId)))
        {
            throw new InvalidDataException("Timeline items must have unique non-empty identities.");
        }

        if (project.VideoItems.Any(item =>
                assetsById.TryGetValue(item.AssetId, out var asset) &&
                asset.Kind is not (ProjectAssetKind.Video or ProjectAssetKind.Image)) ||
            project.AudioItems.Any(item =>
                assetsById.TryGetValue(item.AssetId, out var asset) &&
                asset.Kind != ProjectAssetKind.Audio))
        {
            throw new InvalidDataException("A timeline item references an incompatible asset kind.");
        }
    }

    private static void NormalizeVideoItems(
        List<VideoTimelineItem> videoItems,
        Dictionary<Guid, ProjectAsset> assetsById)
    {
        long videoStart = 0;
        for (var index = 0; index < videoItems.Count; index++)
        {
            if (videoStart > ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds)
            {
                throw new InvalidDataException(
                    "The V1 timeline exceeds the 24-hour limit and cannot be repaired without removing items.");
            }

            var item = videoItems[index];
            item.Volume = double.IsFinite(item.Volume) ? Math.Clamp(item.Volume, 0, 1) : 1;
            var hasKnownVisualAsset = assetsById.TryGetValue(item.AssetId, out var asset) &&
                asset.Kind is ProjectAssetKind.Video or ProjectAssetKind.Image &&
                asset.DurationMilliseconds >= ProjectDocument.MinimumItemDurationMilliseconds;
            var maximumSourceOut = hasKnownVisualAsset
                ? Math.Min(asset!.DurationMilliseconds, ProjectDocument.MaximumTimelineDurationMilliseconds)
                : ProjectDocument.MaximumTimelineDurationMilliseconds;
            item.SourceInMilliseconds = Math.Clamp(
                item.SourceInMilliseconds,
                0,
                maximumSourceOut - ProjectDocument.MinimumItemDurationMilliseconds);
            item.SourceOutMilliseconds = Math.Clamp(
                item.SourceOutMilliseconds,
                item.SourceInMilliseconds + ProjectDocument.MinimumItemDurationMilliseconds,
                maximumSourceOut);
            var isVideo = hasKnownVisualAsset && asset!.Kind == ProjectAssetKind.Video;
            var duration = isVideo
                ? item.SourceOutMilliseconds - item.SourceInMilliseconds
                : item.DurationMilliseconds;
            item.DurationMilliseconds = TimelineMath.ClampItemDuration(duration, videoStart);
            if (isVideo)
            {
                item.SourceOutMilliseconds = item.SourceInMilliseconds + item.DurationMilliseconds;
            }

            videoStart += item.DurationMilliseconds;
        }
    }

    private static void NormalizeAudioItems(
        IEnumerable<AudioTimelineItem> audioItems,
        Dictionary<Guid, ProjectAsset> assetsById)
    {
        foreach (var item in audioItems)
        {
            var originalSourceIn = item.SourceInMilliseconds;
            var maximumSourceOut = assetsById.TryGetValue(item.AssetId, out var asset) &&
                asset.DurationMilliseconds >= ProjectDocument.MinimumItemDurationMilliseconds
                    ? Math.Min(asset.DurationMilliseconds, ProjectDocument.MaximumTimelineDurationMilliseconds)
                    : ProjectDocument.MaximumTimelineDurationMilliseconds;
            item.SourceInMilliseconds = Math.Clamp(
                item.SourceInMilliseconds,
                0,
                maximumSourceOut - ProjectDocument.MinimumItemDurationMilliseconds);
            item.SourceOutMilliseconds = Math.Clamp(
                item.SourceOutMilliseconds,
                item.SourceInMilliseconds + ProjectDocument.MinimumItemDurationMilliseconds,
                maximumSourceOut);

            var shiftedStart = (decimal)item.StartMilliseconds + item.SourceInMilliseconds - originalSourceIn;
            var normalizedStart = (long)decimal.Clamp(shiftedStart, long.MinValue, long.MaxValue);
            if (normalizedStart < 0)
            {
                var availableLeftTrim = item.DurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds;
                var requiredLeftTrim = normalizedStart == long.MinValue ? long.MaxValue : -normalizedStart;
                var leftTrim = Math.Min(availableLeftTrim, requiredLeftTrim);
                item.SourceInMilliseconds += leftTrim;
                normalizedStart = TimelineMath.SaturatingAdd(normalizedStart, leftTrim);
            }

            item.StartMilliseconds = TimelineMath.ClampItemStart(Math.Max(0, normalizedStart), item.DurationMilliseconds);
            item.Volume = double.IsFinite(item.Volume) ? Math.Clamp(item.Volume, 0, 1) : 1;
            item.FadeInMilliseconds = Math.Clamp(item.FadeInMilliseconds, 0, item.DurationMilliseconds);
            item.FadeOutMilliseconds = Math.Clamp(item.FadeOutMilliseconds, 0, item.DurationMilliseconds);
        }
    }

    private static void NormalizeTextItems(IEnumerable<TextTimelineItem> textItems)
    {
        foreach (var item in textItems)
        {
            var duration = item.StartMilliseconds < 0
                ? TimelineMath.SaturatingAdd(item.StartMilliseconds, Math.Max(0, item.DurationMilliseconds))
                : item.DurationMilliseconds;
            item.StartMilliseconds = Math.Clamp(
                item.StartMilliseconds,
                0,
                ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds);
            item.DurationMilliseconds = TimelineMath.ClampItemDuration(duration, item.StartMilliseconds);
            item.Text ??= string.Empty;
            item.FontFamily = TextStyle.NormalizeFontFamily(item.FontFamily);
            item.FontSize = double.IsFinite(item.FontSize) && item.FontSize > 0
                ? Math.Clamp(item.FontSize, 8, 400)
                : TextTimelineItem.DefaultFontSize;
            item.FontWeight = Math.Clamp(item.FontWeight > 0 ? item.FontWeight : TextTimelineItem.DefaultFontWeight, 1, 999);
            item.TextColor = TextStyle.IsArgb(item.TextColor) ? item.TextColor.ToUpperInvariant() : TextTimelineItem.DefaultTextColor;
            item.BackgroundColor = TextStyle.IsArgb(item.BackgroundColor) ? item.BackgroundColor.ToUpperInvariant() : TextTimelineItem.DefaultBackgroundColor;
            item.Opacity = TextStyle.ClampOpacity(item.Opacity);
            item.Alignment = Enum.IsDefined(item.Alignment) ? item.Alignment : TextHorizontalAlignment.Center;
            item.NormalizedX = TextStyle.ClampNormalized(item.NormalizedX, 0.5);
            item.NormalizedY = TextStyle.ClampNormalized(item.NormalizedY, 0.5);
        }
    }

    private static void QuarantineAssetSource(ProjectAsset asset)
    {
        asset.SourcePath = string.Empty;
        asset.ThumbnailCachePath = string.Empty;
        asset.IsMissing = true;
    }

    internal static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalizedName = NormalizeNameCharacters(name);
        if (normalizedName.Length == 0)
        {
            throw new ArgumentException("A project name is required.", nameof(name));
        }

        if (normalizedName.Length > ProjectDocument.MaximumNameLength)
        {
            throw new ArgumentException(
                $"A project name cannot exceed {ProjectDocument.MaximumNameLength} characters.",
                nameof(name));
        }

        return normalizedName;
    }

    private static string CreateDuplicateName(string originalName)
    {
        const string suffix = " copy";
        var baseName = NormalizeNameCharacters(originalName);
        var maximumBaseLength = ProjectDocument.MaximumNameLength - suffix.Length;
        if (baseName.Length > maximumBaseLength)
        {
            baseName = baseName[..maximumBaseLength].TrimEnd();
        }

        return NormalizeName($"{baseName}{suffix}");
    }

    private static string NormalizeNameCharacters(string name) =>
        new string(name.Select(static character => char.IsControl(character) ? ' ' : character).ToArray()).Trim();

    private static bool PathsEqual(string? first, string second) =>
        first is not null && string.Equals(
            Path.TrimEndingDirectorySeparator(first),
            Path.TrimEndingDirectorySeparator(second),
            StringComparison.OrdinalIgnoreCase);
}
