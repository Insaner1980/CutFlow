using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CutFlow.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private readonly Func<ProjectDocument, string, CancellationToken, Task<string?>> _thumbnailPathResolver;
    private bool _isBusy;
    private string? _errorMessage;

    public HomeViewModel(ProjectService projectService)
        : this(projectService, ProjectCardViewModel.ResolveThumbnailPathAsync)
    {
    }

    internal HomeViewModel(
        ProjectService projectService,
        Func<ProjectDocument, string, CancellationToken, Task<string?>> thumbnailPathResolver)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _thumbnailPathResolver = thumbnailPathResolver ?? throw new ArgumentNullException(nameof(thumbnailPathResolver));
    }

    public ObservableCollection<ProjectCardViewModel> Projects { get; } = [];

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "WinUI x:Bind resolves this property through the view-model instance.")]
    [SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "WinUI x:Bind resolves this property through the view-model instance.")]
    public string ProductName => AppInfo.ProductName;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "WinUI x:Bind resolves this property through the view-model instance.")]
    [SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "WinUI x:Bind resolves this property through the view-model instance.")]
    public string LocalSettingsDescription =>
        $"{AppInfo.ProductName} currently uses its dark workspace theme. Projects, settings, and generated caches are stored in the app's local data folder.";

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async () =>
        {
            var projects = await _projectService.ListAsync(cancellationToken);
            await ReplaceProjectsAsync(projects, cancellationToken);
        });
    }

    public async Task<ProjectDocument> CreateAsync(CancellationToken cancellationToken = default)
    {
        ProjectDocument? created = null;
        await RunBusyAsync(async () =>
        {
            created = await _projectService.CreateAsync("New project", cancellationToken);
            await RefreshCoreAsync(cancellationToken);
        });
        return created!;
    }

    public Task<ProjectDocument> OpenAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _projectService.LoadAsync(projectId, cancellationToken);

    public async Task RenameAsync(Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A project name is required.", nameof(name));
        }

        await RunBusyAsync(async () =>
        {
            await _projectService.RenameAsync(projectId, name, cancellationToken);
            await RefreshCoreAsync(cancellationToken);
        });
    }

    public async Task<ProjectDocument> DuplicateAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        ProjectDocument? duplicate = null;
        await RunBusyAsync(async () =>
        {
            duplicate = await _projectService.DuplicateAsync(projectId, cancellationToken: cancellationToken);
            await RefreshCoreAsync(cancellationToken);
        });
        return duplicate!;
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async () =>
        {
            await _projectService.DeleteAsync(projectId, cancellationToken);
            await RefreshCoreAsync(cancellationToken);
        });
    }

    public void ClearError() => ErrorMessage = null;

    public void ReportError(string message) => ErrorMessage = message;

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var projects = await _projectService.ListAsync(cancellationToken);
        await ReplaceProjectsAsync(projects, cancellationToken);
    }

    private async Task ReplaceProjectsAsync(
        IEnumerable<ProjectDocument> projects,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cards = projects.Select(ProjectCardViewModel.Create).ToList();

        Projects.Clear();
        foreach (var card in cards)
        {
            Projects.Add(card);
        }

        await Task.WhenAll(cards.Select(async card =>
        {
            var path = await _thumbnailPathResolver(
                card.Project,
                _projectService.GetProjectPath(card.Id),
                cancellationToken);
            card.SetThumbnailPath(path);
        }));
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ErrorMessage = exception.Message;
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed partial class ProjectCardViewModel : ViewModelBase
{
    private string? _thumbnailPath;

    private ProjectCardViewModel(ProjectDocument project, string? thumbnailPath)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        _thumbnailPath = thumbnailPath;
    }

    internal static ProjectCardViewModel Create(ProjectDocument project) => new(project, null);

    internal static async Task<ProjectCardViewModel> CreateAsync(
        ProjectDocument project,
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var card = Create(project);
        card.SetThumbnailPath(await ResolveThumbnailPathAsync(project, projectPath, cancellationToken));
        return card;
    }

    public ProjectDocument Project { get; }

    public Guid Id => Project.Id;

    public string Name => Project.Name;

    public string ModifiedText => $"Modified {Project.ModifiedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}";

    public string AspectRatioText => Project.Settings.AspectRatio switch
    {
        AspectRatioPreset.Portrait9By16 => "9:16",
        AspectRatioPreset.Square1By1 => "1:1",
        _ => "16:9"
    };

    public string DurationText => TimecodeFormatter.Format(
        TimelineEditingService.CalculateProjectDuration(Project),
        Project.Settings.FrameRate);

    public string? ThumbnailPath => _thumbnailPath;

    public bool HasThumbnail => ThumbnailPath is not null;

    public ImageSource? ThumbnailSource => ThumbnailPath is null
        ? null
        : new BitmapImage(new Uri(ThumbnailPath));

    internal void SetThumbnailPath(string? thumbnailPath)
    {
        if (SetProperty(ref _thumbnailPath, thumbnailPath, nameof(ThumbnailPath)))
        {
            OnPropertyChanged(nameof(HasThumbnail));
            OnPropertyChanged(nameof(ThumbnailSource));
        }
    }

    internal static async Task<string?> ResolveThumbnailPathAsync(
        ProjectDocument project,
        string projectPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        foreach (var asset in project.Assets)
        {
            if (asset.IsMissing ||
                asset.Kind is not (ProjectAssetKind.Video or ProjectAssetKind.Image) ||
                string.IsNullOrWhiteSpace(asset.ThumbnailCachePath))
            {
                continue;
            }

            try
            {
                var request = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
                if (!ThumbnailService.IsCachePathForRequest(request, asset.ThumbnailCachePath))
                {
                    continue;
                }

                var path = ThumbnailService.ResolveProjectCachePath(projectPath, asset.ThumbnailCachePath);
                if (await ThumbnailService.IsUsableCachedThumbnailAsync(path, cancellationToken))
                {
                    return path;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
            {
                // Invalid cache references fall back to the restrained placeholder.
            }
        }

        return null;
    }
}
