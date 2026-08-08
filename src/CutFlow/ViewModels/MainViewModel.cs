using CutFlow.Models;
using CutFlow.Services;

namespace CutFlow.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private readonly MediaImportService _mediaImportService;
    private readonly Func<bool> _canContinue;
    private ProjectDocument? _currentProject;
    private EditorViewModel? _editor;

    public MainViewModel(
        ProjectService projectService,
        MediaImportService? mediaImportService = null,
        Func<bool>? canContinue = null)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _mediaImportService = mediaImportService ?? new MediaImportService();
        _canContinue = canContinue ?? (() => true);
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
        if (!_canContinue())
        {
            return false;
        }

        if (Editor is not null && Editor.SaveStatus != EditorViewModel.SavedStatus)
        {
            try
            {
                await _projectService.SaveAsync(Editor.Project, cancellationToken);
                if (!_canContinue())
                {
                    return false;
                }

                Editor.MarkSaved();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                if (!_canContinue())
                {
                    return false;
                }

                Editor.MarkSaveFailed();
                Home.ReportError(exception.Message);
                return false;
            }
        }

        SetCurrentView(null, null);

        try
        {
            await Home.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (_canContinue())
            {
                Home.ReportError(exception.Message);
            }
        }

        return _canContinue();
    }

    public async Task OpenEditorAsync(ProjectDocument project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!_canContinue())
        {
            return;
        }

        await _mediaImportService.RefreshMissingAsync(project, cancellationToken);
        if (!_canContinue())
        {
            return;
        }

        SetCurrentView(project, new EditorViewModel(project));
    }

    private void SetCurrentView(ProjectDocument? project, EditorViewModel? editor)
    {
        var previousProject = CurrentProject;
        var previousEditor = Editor;
        CurrentProject = project;
        Editor = editor;
        OnPropertyChanged(nameof(IsEditorOpen));

        try
        {
            CurrentViewChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            CurrentProject = previousProject;
            Editor = previousEditor;
            OnPropertyChanged(nameof(IsEditorOpen));
            throw;
        }
    }
}
