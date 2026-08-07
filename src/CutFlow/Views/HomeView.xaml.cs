using CutFlow.Models;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace CutFlow.Views;

public sealed partial class HomeView : UserControl
{
    private readonly HomeProjectOpenGate _projectOpenGate = new();

    public HomeView(HomeViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        ViewModel.Projects.CollectionChanged += (_, _) => UpdateProjectState();
        ViewModel.PropertyChanged += (_, args) =>
        {
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
        if (IsProjectOperationActive)
        {
            return;
        }

        await _projectOpenGate.RunAsync(async () =>
        {
            BusyIndicator.IsActive = true;
            try
            {
                var project = await ViewModel.CreateAsync();
                await RequestProjectOpenAsync(project);
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
            }
        });
        BusyIndicator.IsActive = IsProjectOperationActive;
    }

    private async void ProjectOpen_Click(object sender, RoutedEventArgs e) => await OpenFromTagAsync((FrameworkElement)sender);

    private async void ProjectOpenMenu_Click(object sender, RoutedEventArgs e) => await OpenFromTagAsync((FrameworkElement)sender);

    private async Task OpenFromTagAsync(FrameworkElement source)
    {
        if (IsProjectOperationActive)
        {
            return;
        }

        if (!TryGetProjectId(source.Tag, out var projectId))
        {
            return;
        }

        await _projectOpenGate.RunAsync(async () =>
        {
            BusyIndicator.IsActive = true;
            try
            {
                await RequestProjectOpenAsync(await ViewModel.OpenAsync(projectId));
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
            }
        });
        BusyIndicator.IsActive = IsProjectOperationActive;
    }

    private Task RequestProjectOpenAsync(ProjectDocument project) =>
        ProjectOpenRequested?.Invoke(project) ?? Task.CompletedTask;

    private async void RenameProject_Click(object sender, RoutedEventArgs e)
    {
        if (IsProjectOperationActive || !TryGetProject((FrameworkElement)sender, out var project))
        {
            return;
        }

        var nameBox = new TextBox
        {
            Text = project.Name,
            SelectionStart = 0,
            SelectionLength = project.Name.Length,
            MaxLength = 120,
            Header = "Project name"
        };
        AutomationProperties.SetName(nameBox, "Project name");
        var dialog = CreateDialog("Rename project", nameBox, "Rename");
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (nameBox.Text.Trim().Length == 0)
            {
                nameBox.Header = "Project name is required";
                args.Cancel = true;
            }
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var name = nameBox.Text.Trim();

            try
            {
                await ViewModel.RenameAsync(project.Id, name);
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
            }
        }
    }

    private async void DuplicateProject_Click(object sender, RoutedEventArgs e)
    {
        if (IsProjectOperationActive || !TryGetProjectId(((FrameworkElement)sender).Tag, out var projectId))
        {
            return;
        }

        try
        {
            await ViewModel.DuplicateAsync(projectId);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private async void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (IsProjectOperationActive || !TryGetProject((FrameworkElement)sender, out var project))
        {
            return;
        }

        var dialog = CreateDialog(
            "Delete project?",
            new TextBlock
            {
                Text = $"Delete '{project.Name}' and its {AppInfo.ProductName} project cache? Imported source media will not be deleted.",
                TextWrapping = TextWrapping.Wrap
            },
            "Delete");
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await ViewModel.DeleteAsync(project.Id);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void HomeNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItemContainer as NavigationViewItem)?.Tag?.ToString();
        var settings = string.Equals(tag, "settings", StringComparison.Ordinal);
        SettingsState.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
        ProjectsState.Visibility = settings ? Visibility.Collapsed : Visibility.Visible;
        ProjectsHeading.Text = string.Equals(tag, "projects", StringComparison.Ordinal) ? "All projects" : "Recent projects";
    }

    private ContentDialog CreateDialog(string title, object content, string primaryText) => new()
    {
        XamlRoot = HomeRoot.XamlRoot,
        Title = title,
        Content = content,
        PrimaryButtonText = primaryText,
        CloseButtonText = "Cancel",
        DefaultButton = ContentDialogButton.Primary
    };

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
        OperationError.Message = message;
        OperationError.IsOpen = true;
    }
}
