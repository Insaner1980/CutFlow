using CutFlow.Models;
using CutFlow.Services;

namespace CutFlow.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private readonly MediaImportService _mediaImportService;
    private readonly Func<bool> _canContinue;
    private ProjectDocument? _currentProject;
    private EditorViewModel? _editor;
    private bool _isChangingCurrentView;
    private bool _isShowingHome;

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

    public ProjectDocument? CurrentProject => _currentProject;

    public EditorViewModel? Editor => _editor;

    public bool IsEditorOpen => Editor is not null;

    public async Task<bool> ShowHomeAsync(CancellationToken cancellationToken = default)
    {
        if (!_canContinue() || _isChangingCurrentView || _isShowingHome)
        {
            return false;
        }

        _isShowingHome = true;
        try
        {
            if (!await SaveEditorBeforeHomeAsync(cancellationToken))
            {
                return false;
            }

            SetCurrentView(null, null);
            await LoadHomeAsync(cancellationToken);
            return _canContinue();
        }
        finally
        {
            _isShowingHome = false;
        }
    }

    private async Task<bool> SaveEditorBeforeHomeAsync(CancellationToken cancellationToken)
    {
        var editor = Editor;
        if (editor is null || editor.SaveStatus == EditorViewModel.SavedStatus) return true;

        try
        {
            while (true)
            {
                var revision = editor.Revision;
                await _projectService.SaveAsync(editor.Project, cancellationToken);
                if (!_canContinue()) return false;
                if (editor.TryMarkSaved(revision)) return true;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (!_canContinue()) return false;
            editor.MarkSaveFailed();
            Home.ReportError(exception.Message);
            return false;
        }
    }

    private async Task LoadHomeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Home.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (_canContinue()) Home.ReportError(exception.Message);
        }
    }

    public async Task OpenEditorAsync(ProjectDocument project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!_canContinue() || _isChangingCurrentView)
        {
            return;
        }

        if (Editor is not null)
        {
            throw new InvalidOperationException("Return Home before opening another project.");
        }

        await _mediaImportService.RefreshMissingAsync(project, cancellationToken);
        if (!_canContinue())
        {
            return;
        }

        SetCurrentView(project, new EditorViewModel(project));
    }

    public async Task ReplaceEditorAsync(ProjectDocument project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!_canContinue() || _isChangingCurrentView || Editor is null)
        {
            return;
        }

        var currentEditor = Editor;
        await _mediaImportService.RefreshMissingAsync(project, cancellationToken);
        if (!_canContinue() || !ReferenceEquals(Editor, currentEditor))
        {
            return;
        }

        SetCurrentView(project, new EditorViewModel(project));
    }

    private void SetCurrentView(ProjectDocument? project, EditorViewModel? editor)
    {
        if ((project is null) != (editor is null) ||
            (editor is not null && !ReferenceEquals(project, editor.Project)))
        {
            throw new ArgumentException("The editor and current project must describe the same view state.");
        }

        var previousProject = _currentProject;
        var previousEditor = _editor;
        _isChangingCurrentView = true;
        try
        {
            ApplyCurrentView(project, editor);
            NotifyCurrentViewPropertiesChanged();
            CurrentViewChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            ApplyCurrentView(previousProject, previousEditor);
            try
            {
                NotifyCurrentViewPropertiesChanged();
                CurrentViewChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Preserve the original transition failure after best-effort rollback notification.
            }

            throw;
        }
        finally
        {
            _isChangingCurrentView = false;
        }
    }

    private void ApplyCurrentView(ProjectDocument? project, EditorViewModel? editor)
    {
        _currentProject = project;
        _editor = editor;
    }

    private void NotifyCurrentViewPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentProject));
        OnPropertyChanged(nameof(Editor));
        OnPropertyChanged(nameof(IsEditorOpen));
    }
}
