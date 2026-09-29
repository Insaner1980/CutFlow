using CutFlow.Models;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CutFlow.Views;

public sealed partial class HomeView : UserControl
{
    private readonly HomeProjectOpenGate _projectOpenGate = new();
    private readonly Func<bool> _canContinue;

    public HomeView(HomeViewModel viewModel, Func<bool>? canContinue = null)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _canContinue = canContinue ?? (() => true);
        InitializeComponent();
        ViewModel.Projects.CollectionChanged += (_, _) =>
        {
            if (_canContinue())
            {
                UpdateProjectState();
            }
        };
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (!_canContinue())
            {
                return;
            }

            if (args.PropertyName == nameof(HomeViewModel.IsBusy))
            {
                BusyIndicator.IsActive = IsProjectOperationActive;
            }
            else if (args.PropertyName == nameof(HomeViewModel.ErrorMessage) && ViewModel.HasError)
            {
                ShowError(ViewModel.ErrorMessage!);
            }
        };
    }

    public Func<ProjectDocument, Task>? ProjectOpenRequested { get; set; }

    public HomeViewModel ViewModel { get; }

    public UIElement TitleBarElement => TitleBarDragRegion;

    public bool FocusInitialControl() => HomeItem.Focus(FocusState.Programmatic);

    private bool IsProjectOperationActive => ViewModel.IsBusy || _projectOpenGate.IsActive;

    private void HomeView_Loaded(object sender, RoutedEventArgs e)
    {
        HomeNavigation.SelectedItem = HomeItem;
        UpdateProjectState();
    }

    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        await CreateProjectAsync();
    }

    public async Task CreateProjectAsync()
    {
        await RunProjectOpenGateAsync(async () =>
        {
            try
            {
                var project = await ViewModel.CreateAsync();
                if (!_canContinue())
                {
                    return;
                }

                await RequestProjectOpenAsync(project);
            }
            catch (Exception exception) when (HomeViewModel.IsExpectedProjectOperationFailure(exception))
            {
                ShowError("The project could not be created. Check available disk space and try again.");
            }
        });
    }

    private async void ProjectOpen_Click(object sender, RoutedEventArgs e) => await OpenFromTagAsync((FrameworkElement)sender);

    private async void ProjectOpenMenu_Click(object sender, RoutedEventArgs e) => await OpenFromTagAsync((FrameworkElement)sender);

    private async Task OpenFromTagAsync(FrameworkElement source)
    {
        if (!TryGetProjectId(source.Tag, out var projectId))
        {
            return;
        }

        await RunProjectOpenGateAsync(async () =>
        {
            try
            {
                var project = await ViewModel.OpenAsync(projectId);
                if (!_canContinue())
                {
                    return;
                }

                await RequestProjectOpenAsync(project);
            }
            catch (Exception exception) when (HomeViewModel.IsExpectedProjectOperationFailure(exception))
            {
                ShowError("The project could not be opened. It may be unavailable or damaged.");
            }
        });
    }

    internal async Task<bool> RunProjectOpenGateAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!_canContinue() || IsProjectOperationActive)
        {
            return false;
        }

        var ran = await _projectOpenGate.RunAsync(async () =>
        {
            BusyIndicator.IsActive = true;
            await action();
        });
        if (_canContinue())
        {
            BusyIndicator.IsActive = IsProjectOperationActive;
        }

        return ran;
    }

    private Task RequestProjectOpenAsync(ProjectDocument project) =>
        ProjectOpenRequested?.Invoke(project) ?? Task.CompletedTask;

    private async void RenameProject_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetProject((FrameworkElement)sender, out var project))
        {
            return;
        }

        await RunProjectOpenGateAsync(async () =>
        {
            try
            {
                var nameBox = new TextBox
                {
                    Text = project.Name,
                    SelectionStart = 0,
                    SelectionLength = project.Name.Length,
                    MaxLength = ProjectDocument.MaximumNameLength,
                    Header = "Project name"
                };
                AutomationProperties.SetName(nameBox, "Project name");
                var dialog = CreateDialog("Rename project", nameBox, "Rename");
                dialog.Opened += (_, _) =>
                {
                    nameBox.Focus(FocusState.Programmatic);
                    nameBox.SelectAll();
                };
                dialog.PrimaryButtonClick += (_, args) =>
                {
                    if (nameBox.Text.Trim().Length == 0)
                    {
                        nameBox.Header = "Project name is required";
                        nameBox.Focus(FocusState.Programmatic);
                        args.Cancel = true;
                    }
                };
                var result = await dialog.ShowAsync();
                if (!_canContinue() || result != ContentDialogResult.Primary)
                {
                    return;
                }

                var name = nameBox.Text.Trim();

                try
                {
                    await ViewModel.RenameAsync(project.Id, name);
                }
                catch (Exception exception) when (HomeViewModel.IsExpectedProjectOperationFailure(exception))
                {
                    ShowError("The project could not be renamed. Check app storage and try again.");
                }
            }
            finally
            {
                RestoreProjectFocus(project.Id);
            }
        });
    }

    private async void DuplicateProject_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetProjectId(((FrameworkElement)sender).Tag, out var projectId))
        {
            return;
        }

        await RunProjectOpenGateAsync(async () =>
        {
            try
            {
                await ViewModel.DuplicateAsync(projectId);
            }
            catch (Exception exception) when (HomeViewModel.IsExpectedProjectOperationFailure(exception))
            {
                ShowError("The project could not be duplicated. Check available disk space and try again.");
            }
        });
    }

    private async void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetProject((FrameworkElement)sender, out var project))
        {
            return;
        }

        await RunProjectOpenGateAsync(async () =>
        {
            try
            {
                var dialog = CreateDialog(
                    "Delete project?",
                    new TextBlock
                    {
                        Text = $"Delete '{project.Name}' and its {AppInfo.ProductName} project cache? Imported source media will not be deleted.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    "Delete",
                    ContentDialogButton.Close);
                var result = await dialog.ShowAsync();
                if (!_canContinue() || result != ContentDialogResult.Primary)
                {
                    return;
                }

                try
                {
                    await ViewModel.DeleteAsync(project.Id);
                }
                catch (Exception exception) when (HomeViewModel.IsExpectedProjectOperationFailure(exception))
                {
                    ShowError("The project could not be deleted. Close any app using its files and try again.");
                }
            }
            finally
            {
                RestoreProjectFocus(project.Id);
            }
        });
    }

    private void HomeNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItemContainer as NavigationViewItem)?.Tag?.ToString();
        var settings = string.Equals(tag, "settings", StringComparison.Ordinal);
        SettingsState.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
        ProjectsState.Visibility = settings ? Visibility.Collapsed : Visibility.Visible;
        ProjectsHeading.Text = string.Equals(tag, "projects", StringComparison.Ordinal) ? "All projects" : "Recent projects";
    }

    private ContentDialog CreateDialog(
        string title,
        object content,
        string primaryText,
        ContentDialogButton defaultButton = ContentDialogButton.Primary) =>
        new()
        {
            XamlRoot = HomeRoot.XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Cancel",
            DefaultButton = defaultButton
        };

    private void RestoreProjectFocus(Guid projectId)
    {
        if (!_canContinue())
        {
            return;
        }

        var index = ViewModel.Projects.ToList().FindIndex(project => project.Id == projectId);
        if (index >= 0 &&
            ProjectRepeater.TryGetElement(index) is DependencyObject projectElement &&
            FindProjectActionsButton(projectElement, projectId)?.Focus(FocusState.Programmatic) == true)
        {
            return;
        }

        HomeItem.Focus(FocusState.Programmatic);
    }

    private static Button? FindProjectActionsButton(DependencyObject parent, Guid projectId)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is Button { Flyout: not null } button && TryGetProjectId(button.Tag, out var id) && id == projectId)
            {
                return button;
            }

            if (FindProjectActionsButton(child, projectId) is { } match)
            {
                return match;
            }
        }

        return null;
    }

    private bool TryGetProject(FrameworkElement source, out ProjectCardViewModel project)
    {
        project = null!;
        if (!TryGetProjectId(source.Tag, out var id))
        {
            return false;
        }

        project = ViewModel.Projects.FirstOrDefault(item => item.Id == id)!;
        return project is not null;
    }

    private static bool TryGetProjectId(object? tag, out Guid id) => Guid.TryParse(tag?.ToString(), out id);

    private void UpdateProjectState()
    {
        var hasProjects = ViewModel.Projects.Count > 0;
        EmptyProjects.Visibility = hasProjects ? Visibility.Collapsed : Visibility.Visible;
        ProjectRepeater.Visibility = hasProjects ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        if (!_canContinue())
        {
            return;
        }

        OperationError.Message = message;
        OperationError.IsOpen = true;
    }
}
