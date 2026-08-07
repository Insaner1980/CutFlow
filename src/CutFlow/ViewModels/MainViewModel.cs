using CutFlow.Models;
using CutFlow.Services;

namespace CutFlow.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private readonly MediaImportService _mediaImportService;
    private ProjectDocument? _currentProject;
    private EditorViewModel? _editor;

    public MainViewModel(ProjectService projectService, MediaImportService? mediaImportService = null)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _mediaImportService = mediaImportService ?? new MediaImportService();
        Home = new HomeViewModel(_projectService);
    }

    public event EventHandler? CurrentViewChanged;

    public HomeViewModel Home { get; }

    public ProjectDocument? CurrentProject
    {
        get => _currentProject;
        private set => SetProperty(ref _currentProject, value);
    }

    public EditorViewModel? Editor
    {
        get => _editor;
        private set => SetProperty(ref _editor, value);
    }

    public bool IsEditorOpen => Editor is not null;

    public async Task<bool> ShowHomeAsync(CancellationToken cancellationToken = default)
    {
        if (Editor is not null && Editor.SaveStatus != EditorViewModel.SavedStatus)
        {
            try
            {
                await _projectService.SaveAsync(Editor.Project, cancellationToken);
                Editor.MarkSaved();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                Editor.MarkSaveFailed();
                Home.ReportError(exception.Message);
                return false;
            }
        }

        CurrentProject = null;
        Editor = null;
        OnPropertyChanged(nameof(IsEditorOpen));
        CurrentViewChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            await Home.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Home.ReportError(exception.Message);
        }

        return true;
    }

    public async Task OpenEditorAsync(ProjectDocument project, CancellationToken cancellationToken = default)
    {
        CurrentProject = project ?? throw new ArgumentNullException(nameof(project));
        await _mediaImportService.RefreshMissingAsync(project, cancellationToken);
        Editor = new EditorViewModel(project);
        OnPropertyChanged(nameof(IsEditorOpen));
        CurrentViewChanged?.Invoke(this, EventArgs.Empty);
    }
}
