using System.Collections.ObjectModel;
using System.Globalization;
using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CutFlow.ViewModels;

public sealed class HomeViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private bool _isBusy;
    private string? _errorMessage;

    public HomeViewModel(ProjectService projectService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
    }

    public ObservableCollection<ProjectCardViewModel> Projects { get; } = [];

    public string ProductName => AppInfo.ProductName;

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
            ReplaceProjects(projects);
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
        ReplaceProjects(projects);
    }

    private void ReplaceProjects(IEnumerable<ProjectDocument> projects)
    {
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(new ProjectCardViewModel(project, _projectService.GetProjectPath(project.Id)));
        }
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

public sealed class ProjectCardViewModel
{
    public ProjectCardViewModel(ProjectDocument project, string projectPath)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        ThumbnailPath = ResolveThumbnailPath(project, projectPath);
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

    public string? ThumbnailPath { get; }

    public bool HasThumbnail => ThumbnailPath is not null;

    public ImageSource? ThumbnailSource => ThumbnailPath is null
        ? null
        : new BitmapImage(new Uri(ThumbnailPath));

    private static string? ResolveThumbnailPath(ProjectDocument project, string projectPath)
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
                var path = ThumbnailService.ResolveProjectCachePath(projectPath, asset.ThumbnailCachePath);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            catch (InvalidDataException)
            {
                // Invalid cache references fall back to the restrained placeholder.
            }
        }

        return null;
    }
}
