using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CutFlow.Models;
using CutFlow.Utilities;
using Windows.Storage;

namespace CutFlow.Services;

public sealed class ProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true
    };

    private readonly string _projectsRootPath;
    private readonly Func<ProjectDocument, string> _serialize;

    public ProjectService(string? rootPath = null, Func<ProjectDocument, string>? serialize = null)
    {
        var localRootPath = rootPath ?? ApplicationData.Current.LocalFolder.Path;
        if (string.IsNullOrWhiteSpace(localRootPath))
        {
            throw new ArgumentException("A local project root path is required.", nameof(rootPath));
        }

        _projectsRootPath = Path.GetFullPath(Path.Combine(localRootPath, "Projects"));
        _serialize = serialize ?? (project => JsonSerializer.Serialize(project, JsonOptions));
    }

    public async Task<ProjectDocument> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var project = ProjectDocument.CreateNew(NormalizeName(name), DateTimeOffset.UtcNow);
        var projectDirectory = GetProjectDirectoryPath(project.Id);
        var createdProjectDirectory = !Directory.Exists(projectDirectory);
        try
        {
            Directory.CreateDirectory(projectDirectory);
            Directory.CreateDirectory(GetCachePath(project.Id));
            await SaveAsync(project, cancellationToken);
            return project;
        }
        catch
        {
            if (createdProjectDirectory && Directory.Exists(projectDirectory))
            {
                Directory.Delete(projectDirectory, recursive: true);
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<ProjectDocument>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_projectsRootPath))
        {
            return [];
        }

        var projects = new List<ProjectDocument>();
        foreach (var directory in Directory.EnumerateDirectories(_projectsRootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(Path.GetFileName(directory), out var projectId))
            {
                continue;
            }

            try
            {
                projects.Add(await LoadAsync(projectId, cancellationToken));
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

        return projects.OrderByDescending(project => project.ModifiedAt).ToList();
    }

    public async Task<ProjectDocument> LoadAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var projectPath = GetProjectFilePath(projectId);
        string json;
        try
        {
            json = await File.ReadAllTextAsync(projectPath, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The project JSON could not be read.", exception);
        }

        ProjectDocument project;
        try
        {
            project = JsonSerializer.Deserialize<ProjectDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("The project JSON did not contain a document.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The project JSON could not be read.", exception);
        }

        if (project.SchemaVersion != ProjectDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported project schema version {project.SchemaVersion}.");
        }

        if (project.Id != projectId)
        {
            throw new InvalidDataException("The project identity does not match its directory.");
        }

        Normalize(project);
        return project;
    }

    public async Task SaveAsync(ProjectDocument project, CancellationToken cancellationToken = default)
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

        Normalize(project);
        var previousModifiedAt = project.ModifiedAt;
        var candidateModifiedAt = DateTimeOffset.UtcNow;
        var projectDirectory = GetProjectDirectoryPath(project.Id);
        var projectPath = GetProjectFilePath(project.Id);
        var temporaryPath = projectPath + ".tmp";

        try
        {
            project.ModifiedAt = candidateModifiedAt;
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(projectDirectory);
            var json = _serialize(project);
            await WriteAndFlushAsync(temporaryPath, json, cancellationToken);
            if (File.Exists(projectPath))
            {
                File.Replace(temporaryPath, projectPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, projectPath);
            }
        }
        catch
        {
            project.ModifiedAt = previousModifiedAt;
            throw;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
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
        var duplicate = ProjectDocumentCloner.Clone(original, JsonOptions);
        duplicate.Id = Guid.NewGuid();
        duplicate.Name = NormalizeName(name ?? $"{original.Name} copy");
        var now = DateTimeOffset.UtcNow;
        duplicate.CreatedAt = now;
        duplicate.ModifiedAt = now;
        await SaveAsync(duplicate, cancellationToken);
        return duplicate;
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectDirectory = GetProjectDirectoryPath(projectId);
        if (Directory.Exists(projectDirectory))
        {
            await Task.Run(() => Directory.Delete(projectDirectory, recursive: true), cancellationToken);
        }
    }

    public string GetCachePath(Guid projectId) => Path.Combine(GetProjectDirectoryPath(projectId), "cache");

    public string GetProjectPath(Guid projectId) => GetProjectDirectoryPath(projectId);

    private string GetProjectFilePath(Guid projectId) => Path.Combine(GetProjectDirectoryPath(projectId), "project.json");

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

        return projectDirectory;
    }

    private static async Task WriteAndFlushAsync(string path, string json, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static void Normalize(ProjectDocument project)
    {
        project.Settings ??= new ProjectSettings();
        if (!TimelineInput.IsOpaqueArgb(project.Settings.BackgroundColor))
        {
            project.Settings.BackgroundColor = ProjectSettings.DefaultBackgroundColor;
        }

        project.Assets ??= [];
        project.VideoItems ??= [];
        project.AudioItems ??= [];
        project.TextItems ??= [];

        long videoStart = 0;
        for (var index = 0; index < project.VideoItems.Count; index++)
        {
            if (videoStart > ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds)
            {
                project.VideoItems.RemoveRange(index, project.VideoItems.Count - index);
                break;
            }

            var item = project.VideoItems[index];
            item.Volume = double.IsFinite(item.Volume) ? Math.Clamp(item.Volume, 0, 1) : 1;
            item.SourceInMilliseconds = Math.Clamp(
                item.SourceInMilliseconds,
                0,
                ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds);
            item.SourceOutMilliseconds = Math.Clamp(
                item.SourceOutMilliseconds,
                item.SourceInMilliseconds + ProjectDocument.MinimumItemDurationMilliseconds,
                ProjectDocument.MaximumTimelineDurationMilliseconds);
            item.DurationMilliseconds = TimelineMath.ClampItemDuration(item.DurationMilliseconds, videoStart);
            videoStart += item.DurationMilliseconds;
        }

        foreach (var item in project.AudioItems)
        {
            item.SourceInMilliseconds = Math.Clamp(
                item.SourceInMilliseconds,
                0,
                ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds);
            item.SourceOutMilliseconds = Math.Clamp(
                item.SourceOutMilliseconds,
                item.SourceInMilliseconds + ProjectDocument.MinimumItemDurationMilliseconds,
                ProjectDocument.MaximumTimelineDurationMilliseconds);
            item.StartMilliseconds = TimelineMath.ClampItemStart(item.StartMilliseconds, item.DurationMilliseconds);
            item.Volume = double.IsFinite(item.Volume) ? Math.Clamp(item.Volume, 0, 1) : 1;
            item.FadeInMilliseconds = Math.Clamp(item.FadeInMilliseconds, 0, item.DurationMilliseconds);
            item.FadeOutMilliseconds = Math.Clamp(item.FadeOutMilliseconds, 0, item.DurationMilliseconds);
        }

        foreach (var item in project.TextItems)
        {
            item.StartMilliseconds = Math.Clamp(
                item.StartMilliseconds,
                0,
                ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds);
            item.DurationMilliseconds = TimelineMath.ClampItemDuration(item.DurationMilliseconds, item.StartMilliseconds);
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

    internal static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length == 0)
        {
            throw new ArgumentException("A project name is required.", nameof(name));
        }

        return normalizedName;
    }

    private static bool PathsEqual(string? first, string second) =>
        first is not null && string.Equals(
            Path.TrimEndingDirectorySeparator(first),
            Path.TrimEndingDirectorySeparator(second),
            StringComparison.OrdinalIgnoreCase);
}
