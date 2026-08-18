using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;
using Kreta.Services;
using Kreta.Services.AI;
using Kreta.Services.Database;
using Kreta.Services.Evolution;
using Kreta.Services.Security;
using Kreta.Services.Testing;

namespace Kreta;

public partial class MainWindow : Window
{
    private readonly IEvolutionService _evolutionService;
    private readonly IDynamicLoader _dynamicLoader;
    private readonly KretaDbContext _dbContext;
    private readonly string _evolViewsDirectory;

    private readonly List<IEvolView> _loadedViews = new();
    private readonly Dictionary<IEvolView, string> _viewFilePathMap = new();

    private EvolveResult? _lastEvolveResult;
    private Role _currentRole = Role.Student;

    /// <summary>
    /// The view currently selected/opened by the user in the sidebar, and its file path.
    /// While set, the next AI request will DIRECTLY modify/fix this view
    /// instead of the system having to guess which one it is from free text.
    /// </summary>
    private IEvolView? _selectedView;
    private string? _selectedViewFilePath;

    public MainWindow()
    {
        InitializeComponent();

        _dbContext = new KretaDbContext();
        _dbContext.SeedData();

        _dynamicLoader = new DynamicLoader();
        _evolutionService = new EvolutionService();
        _evolViewsDirectory = PathHelper.GetEvolViewsDirectory();

        RoleSelector.SelectionChanged += OnRoleSelectorChanged;
        AiButton.Click += OnAiButtonClick;
        ApproveButton.Click += OnApproveButtonClick;
        DiscardButton.Click += OnDiscardButtonClick;
        ClearSelectionButton.Click += OnClearSelectionClick;

        BootAndCompileSavedModules();
    }

    /// <summary>
    /// On system startup, loads and compiles the C# views found on the disk without deleting them.
    /// </summary>
    private void BootAndCompileSavedModules()
    {
        try
        {
            var files = Directory.GetFiles(_evolViewsDirectory, "*.cs");
            if (files.Length == 0) return;

            EvolverStatusText.Text = "🟢 Evolution Engine: Loading...";
            StatusText.Text = $"Loading {files.Length} saved modules from disk...";
            StatusText.Foreground = Brushes.Orange;

            _loadedViews.Clear();
            _viewFilePathMap.Clear();

            int loadedCount = 0;

            foreach (var file in files)
            {
                var sourceCode = File.ReadAllText(file);
                var viewName = Path.GetFileNameWithoutExtension(file);

                var loadResult = _dynamicLoader.LoadViewFromCode(sourceCode);
                if (loadResult.IsSuccess && loadResult.CompiledAssembly != null)
                {
                    var viewTypes = loadResult.CompiledAssembly.GetTypes()
                        .Where(t => typeof(IEvolView).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in viewTypes)
                    {
                        var instance = InstantiateViewForRole(type);
                        if (instance != null)
                        {
                            _loadedViews.Add(instance);
                            _viewFilePathMap[instance] = file;
                            loadedCount++;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[Loading error - {viewName}]: {loadResult?.ErrorMessage}");
                }
            }

            EvolverStatusText.Text = "🟢 Evolution Engine: Active";
            StatusText.Text = $"{loadedCount} modules successfully loaded from disk.";
            StatusText.Foreground = Brushes.Green;

            RefreshSidebarMenu();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Startup error: {ex.Message}";
            StatusText.Foreground = Brushes.Red;
        }
    }

    /// <summary>
    /// Instantiates the compiled C# class with the appropriate database context (Student, Teacher, Director).
    /// </summary>
    private IEvolView? InstantiateViewForRole(Type type)
    {
        try
        {
            var ctors = type.GetConstructors();

            foreach (var ctor in ctors)
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length == 1)
                {
                    var paramType = parameters[0].ParameterType;

                    if (paramType == typeof(IStudentContext))
                    {
                        var context = new SqliteStudentContext(_dbContext, 1);
                        return Activator.CreateInstance(type, context) as IEvolView;
                    }
                    if (paramType == typeof(ITeacherContext))
                    {
                        var context = new SqliteTeacherContext(_dbContext);
                        return Activator.CreateInstance(type, context) as IEvolView;
                    }
                    if (paramType == typeof(IDirectorContext))
                    {
                        var context = new SqliteDirectorContext(_dbContext);
                        return Activator.CreateInstance(type, context) as IEvolView;
                    }
                }
            }

            var defaultCtor = type.GetConstructor(Type.EmptyTypes);
            if (defaultCtor != null)
            {
                return Activator.CreateInstance(type) as IEvolView;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Instantiation Error - {type.Name}]: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Strictly checks if the given view is allowed to appear in the menu of the current role.
    /// </summary>
    private bool IsViewAllowedForRole(IEvolView view, Role role)
    {
        var type = view.GetType();
        var ctors = type.GetConstructors();

        foreach (var ctor in ctors)
        {
            foreach (var param in ctor.GetParameters())
            {
                // Student context view can only be displayed to students
                if (param.ParameterType == typeof(IStudentContext) && role != Role.Student)
                    return false;

                // Teacher context view can only be displayed to teachers
                if (param.ParameterType == typeof(ITeacherContext) && role != Role.Teacher)
                    return false;

                // Director context view can only be displayed to directors
                if (param.ParameterType == typeof(IDirectorContext) && role != Role.Director)
                    return false;
            }
        }

        // Strict filtering based on namespace (e.g. Kreta.Evol.Student)
        if (type.Namespace != null)
        {
            if (type.Namespace.Contains("Student") && role != Role.Student)
                return false;
            if (type.Namespace.Contains("Teacher") && role != Role.Teacher)
                return false;
            if (type.Namespace.Contains("Director") && role != Role.Director)
                return false;
        }

        return true;
    }

    private void RefreshSidebarMenu()
    {
        SidebarMenuPanel.Children.Clear();

        foreach (var view in _loadedViews)
        {
            if (!IsViewAllowedForRole(view, _currentRole))
                continue;

            var btn = new Button
            {
                Content = view.Name,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                Padding = new Avalonia.Thickness(12, 10),
                CornerRadius = new Avalonia.CornerRadius(6),
                Tag = view
            };

            btn.Click += OnSidebarButtonClick;
            SidebarMenuPanel.Children.Add(btn);
        }
    }

    private void OnSidebarButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is IEvolView view)
        {
            try
            {
                var freshInstance = InstantiateViewForRole(view.GetType()) ?? view;
                MainContentArea.Content = freshInstance.CreateView();

                // Select the freshly instantiated view and record its file path
                // - from now on, the next AI request will DIRECTLY modify this view.
                _viewFilePathMap.TryGetValue(view, out var filePath);
                SelectView(freshInstance, filePath, view.Name);

                StatusText.Text = $"View loaded: {view.Name}";
                StatusText.Foreground = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error opening view: {ex.Message}";
                StatusText.Foreground = Brushes.Red;
                Console.WriteLine($"[View opening error]: {ex}");
            }
        }
    }

    /// <summary>
    /// Selects a view: stores its file path and displays the selection banner so the user
    /// can see that the next AI request will directly modify this.
    /// </summary>
    private void SelectView(IEvolView view, string? filePath, string displayName)
    {
        _selectedView = view;
        _selectedViewFilePath = filePath;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            // No known file path (e.g. built-in non-AI view) - cannot edit directly,
            // so we do not display as selected.
            SelectedViewBanner.IsVisible = false;
            return;
        }

        SelectedViewBanner.IsVisible = true;
        SelectedViewText.Text = $"Selected: '{displayName}' — your next request will directly modify/repair THIS view.";
    }

    /// <summary>
    /// Clears the selection: the next AI request will be treated as a brand new feature again.
    /// </summary>
    private void ClearSelection()
    {
        _selectedView = null;
        _selectedViewFilePath = null;
        SelectedViewBanner.IsVisible = false;
    }

    private void OnClearSelectionClick(object? sender, RoutedEventArgs e)
    {
        ClearSelection();
        StatusText.Text = "Selection cleared. The next request will create a new feature.";
        StatusText.Foreground = Brushes.LightBlue;
    }

    private void OnRoleSelectorChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RoleSelector.SelectedItem is ComboBoxItem selectedItem &&
            Enum.TryParse<Role>(selectedItem.Tag?.ToString(), out var role))
        {
            _currentRole = role;
            ClearSelection();
            MainContentArea.Content = null;
            RefreshSidebarMenu();
            StatusText.Text = $"Role switched to: {selectedItem.Content}";
            StatusText.Foreground = Brushes.LightBlue;
        }
    }

    private async void OnAiButtonClick(object? sender, RoutedEventArgs e)
    {
        var prompt = PromptInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            StatusText.Text = "Please enter the description of the requested feature!";
            StatusText.Foreground = Brushes.Orange;
            return;
        }

        SetBusy(true);
        StatusText.Text = string.IsNullOrWhiteSpace(_selectedViewFilePath)
            ? "AI code generation in progress..."
            : $"AI modification in progress on selected view ({_selectedView?.Name})...";
        StatusText.Foreground = Brushes.Cyan;

        try
        {
            var result = await _evolutionService.EvolveAsync(prompt, _currentRole, targetViewFilePath: _selectedViewFilePath);
            _lastEvolveResult = result;

            if (result.IsRejectedAction)
            {
                StatusText.Text = $"❌ Access denied: {result.Description}";
                StatusText.Foreground = Brushes.Red;
                MainContentArea.Content = null;
                ApproveButton.IsVisible = false;
                DiscardButton.IsVisible = false;
                return;
            }

            if (result.IsDeletedAction)
            {
                StatusText.Text = $"🗑️ Module deleted: {result.ViewName}";
                StatusText.Foreground = Brushes.Yellow;
                MainContentArea.Content = null;

                _loadedViews.RemoveAll(v => v.Name.Contains(result.ViewName ?? "", StringComparison.OrdinalIgnoreCase));
                RefreshSidebarMenu();

                // If the deleted view was selected, selection is no longer valid - clear it.
                ClearSelection();

                ApproveButton.IsVisible = false;
                DiscardButton.IsVisible = false;
                return;
            }

            if (result.IsSuccess && result.LoadedControl != null)
            {
                MainContentArea.Content = result.LoadedControl;
                StatusText.Text = $"New feature created: '{result.ViewName}'. You can save or discard it.";
                StatusText.Foreground = Brushes.Green;

                if (result.CompiledAssembly != null)
                {
                    var viewType = result.CompiledAssembly.GetTypes()
                        .FirstOrDefault(t => typeof(IEvolView).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    if (viewType != null)
                    {
                        var instance = InstantiateViewForRole(viewType);
                        if (instance != null && !string.IsNullOrEmpty(result.FilePath))
                        {
                            _loadedViews.RemoveAll(v => v.GetType().Name == viewType.Name);
                            _loadedViews.Add(instance);
                            _viewFilePathMap[instance] = result.FilePath;
                            RefreshSidebarMenu();

                            // Update selection to the FRESHLY generated/modified view so the
                            // next prompt modifies the same file again for iterative refining/repairing.
                            // further - allowing iterative refinement/repairing of a feature.
                            SelectView(instance, result.FilePath, result.ViewName ?? instance.Name);
                        }
                    }
                }

                ApproveButton.IsVisible = true;
                DiscardButton.IsVisible = true;
                PromptInput.Text = string.Empty;
            }
            else
            {
                StatusText.Text = $"Error occurred: {result.ErrorMessage}";
                StatusText.Foreground = Brushes.Red;
                ApproveButton.IsVisible = false;
                DiscardButton.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Unexpected error: {ex.Message}";
            StatusText.Foreground = Brushes.Red;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnApproveButtonClick(object? sender, RoutedEventArgs e)
    {
        await OnApproveButtonClickInternal();
    }

    private async System.Threading.Tasks.Task OnApproveButtonClickInternal()
    {
        if (_lastEvolveResult == null || string.IsNullOrWhiteSpace(_lastEvolveResult.FilePath))
        {
            StatusText.Text = "Nothing to approve.";
            return;
        }

        StatusText.Text = "Approving feature and uploading (Git Push)...";
        StatusText.Foreground = Brushes.Orange;

        bool pushSuccess = await _evolutionService.AcceptAndPushFeatureAsync(
            _lastEvolveResult.FilePath,
            _lastEvolveResult.ViewName ?? "New View"
        );

        if (pushSuccess)
        {
            StatusText.Text = "Successfully saved to disk and pushed to GitHub!";
            StatusText.Foreground = Brushes.Green;
        }
        else
        {
            StatusText.Text = "File saved to disk, but Git Push failed.";
            StatusText.Foreground = Brushes.Yellow;
        }

        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;
    }

    private async void OnDiscardButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_lastEvolveResult == null || string.IsNullOrWhiteSpace(_lastEvolveResult.FilePath))
        {
            StatusText.Text = "Nothing to discard.";
            return;
        }

        bool deleted = await _evolutionService.DiscardFeatureAsync(_lastEvolveResult.FilePath);
        if (deleted)
        {
            StatusText.Text = "Feature discarded and deleted from disk.";
            StatusText.Foreground = Brushes.Yellow;
            MainContentArea.Content = null;

            // If the discarded file was selected, selection is no longer valid - clear it.
            if (string.Equals(_selectedViewFilePath, _lastEvolveResult.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                ClearSelection();
            }

            if (_lastEvolveResult.CompiledAssembly != null)
            {
                var viewType = _lastEvolveResult.CompiledAssembly.GetTypes()
                    .FirstOrDefault(t => typeof(IEvolView).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (viewType != null)
                {
                    _loadedViews.RemoveAll(v => v.GetType().Name == viewType.Name);
                    RefreshSidebarMenu();
                }
            }
        }

        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;
    }
    private async void RunAutomatedBenchmark()
    {
        var runner = new AutomatedTestRunner(_evolutionService, new AstAnalyzer(), new PromptConformanceVerifier());

        // 1. Runs the 100 tests
        var results = await runner.RunAllTestsAsync(
            delayBetweenTestsMs: 1000, 
            progressCallback: (current, total, result) => {
                Console.WriteLine($"[Progress: {current}/{total}] {result.Prompt} -> {(result.IsSuccess ? "OK" : "ERROR")}");
            }
        );

        // 2. Generates the updated Markdown report
        string markdownReport = runner.ExportToMarkdownReport(results);

        // 3. Saves to disk directly over the Markdown file location
        File.WriteAllText("teszt_adatkeszlet_100.md", markdownReport);
        Console.WriteLine("🟢 Measurement data updated in 'teszt_adatkeszlet_100.md' file!");
    }
    

    private void SetBusy(bool isBusy)
    {
        BusyIndicator.IsVisible = isBusy;
        AiButton.IsEnabled = !isBusy;
        PromptInput.IsEnabled = !isBusy;
    }
}