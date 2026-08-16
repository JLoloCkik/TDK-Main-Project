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
using Kreta.Services.Database;
using Kreta.Services.Evolution;

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
    /// A felhasználó által az oldalsávban éppen kijelölt/megnyitott nézet, és annak fájlútja.
    /// Amíg be van állítva, a következő AI-kérés KÖZVETLENÜL ezt a nézetet módosítja/javítja
    /// ahelyett, hogy a rendszernek ki kellene találnia a szabad szövegből, melyikről van szó.
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
    /// Rendszerindításkor betölti és lefordítja a lemezen található C# nézeteket anélkül, hogy törölné őket.
    /// </summary>
    private void BootAndCompileSavedModules()
    {
        try
        {
            var files = Directory.GetFiles(_evolViewsDirectory, "*.cs");
            if (files.Length == 0) return;

            EvolverStatusText.Text = "🟢 Evolúciós Motor: Betöltés...";
            StatusText.Text = $"{files.Length} korábbi modul betöltése a lemezről...";
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
                    Console.WriteLine($"[Betöltési hiba - {viewName}]: {loadResult?.ErrorMessage}");
                }
            }

            EvolverStatusText.Text = "🟢 Evolúciós Motor: Aktív";
            StatusText.Text = $"{loadedCount} modul sikeresen betöltve a lemezről.";
            StatusText.Foreground = Brushes.Green;

            RefreshSidebarMenu();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Rendszerindítási hiba: {ex.Message}";
            StatusText.Foreground = Brushes.Red;
        }
    }

    /// <summary>
    /// Példányosítja a lefordított C# osztályt a megfelelő adatbázis-kontextussal (Student, Teacher, Director).
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
            Console.WriteLine($"[Példányosítási Hiba - {type.Name}]: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Szigorúan ellenőrzi, hogy a megadott nézet megjelenhet-e az aktuális szerepkör menüjében.
    /// </summary>
    private bool IsViewAllowedForRole(IEvolView view, Role role)
    {
        var type = view.GetType();
        var ctors = type.GetConstructors();

        foreach (var ctor in ctors)
        {
            foreach (var param in ctor.GetParameters())
            {
                // A diák kontextusú nézet csak diáknak jelenhet meg
                if (param.ParameterType == typeof(IStudentContext) && role != Role.Student)
                    return false;

                // A tanár kontextusú nézet csak tanárnak jelenhet meg
                if (param.ParameterType == typeof(ITeacherContext) && role != Role.Teacher)
                    return false;

                // Az igazgató kontextusú nézet csak igazgatónak jelenhet meg
                if (param.ParameterType == typeof(IDirectorContext) && role != Role.Director)
                    return false;
            }
        }

        // Névtér alapján történő szigorú szűrés (pl. Kreta.Evol.Student)
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

                // A frissen példányosított nézetet jelöljük ki, és a hozzá tartozó fájlútvonalat
                // rögzítjük - innentől a következő AI-kérés KÖZVETLENÜL ezt a nézetet fogja módosítani.
                _viewFilePathMap.TryGetValue(view, out var filePath);
                SelectView(freshInstance, filePath, view.Name);

                StatusText.Text = $"Nézet betöltve: {view.Name}";
                StatusText.Foreground = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Hiba a nézet megnyitásakor: {ex.Message}";
                StatusText.Foreground = Brushes.Red;
                Console.WriteLine($"[Nézet megnyitási hiba]: {ex}");
            }
        }
    }

    /// <summary>
    /// Kijelöl egy nézetet: eltárolja a fájlútját, és megjeleníti a kijelölés-sávot, hogy a felhasználó
    /// lássa, a következő AI-kérés közvetlenül ezt fogja módosítani.
    /// </summary>
    private void SelectView(IEvolView view, string? filePath, string displayName)
    {
        _selectedView = view;
        _selectedViewFilePath = filePath;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            // Nincs ismert fájlútvonal (pl. beépített, nem AI-generált nézet) - nem tudjuk közvetlenül
            // szerkeszteni, ezért nem jelenítjük meg kijelöltként.
            SelectedViewBanner.IsVisible = false;
            return;
        }

        SelectedViewBanner.IsVisible = true;
        SelectedViewText.Text = $"Kijelölve: „{displayName}” — a következő kérésed közvetlenül EZT a nézetet fogja módosítani/javítani.";
    }

    /// <summary>
    /// Törli a kijelölést: a következő AI-kérés újra vadonatúj funkcióként lesz kezelve.
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
        StatusText.Text = "Kijelölés törölve. A következő kérés új funkciót fog létrehozni.";
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
            StatusText.Text = $"Szerepkör átváltva: {selectedItem.Content}";
            StatusText.Foreground = Brushes.LightBlue;
        }
    }

    private async void OnAiButtonClick(object? sender, RoutedEventArgs e)
    {
        var prompt = PromptInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            StatusText.Text = "Kérjük, írja be a kívánt funkció leírását!";
            StatusText.Foreground = Brushes.Orange;
            return;
        }

        SetBusy(true);
        StatusText.Text = string.IsNullOrWhiteSpace(_selectedViewFilePath)
            ? "AI kódgenerálás folyamatban..."
            : $"AI módosítás folyamatban a kijelölt nézeten ({_selectedView?.Name})...";
        StatusText.Foreground = Brushes.Cyan;

        try
        {
            var result = await _evolutionService.EvolveAsync(prompt, _currentRole, targetViewFilePath: _selectedViewFilePath);
            _lastEvolveResult = result;

            if (result.IsRejectedAction)
            {
                StatusText.Text = $"❌ Hozzáférés megtagadva: {result.Description}";
                StatusText.Foreground = Brushes.Red;
                MainContentArea.Content = null;
                ApproveButton.IsVisible = false;
                DiscardButton.IsVisible = false;
                return;
            }

            if (result.IsDeletedAction)
            {
                StatusText.Text = $"🗑️ Modul törölve: {result.ViewName}";
                StatusText.Foreground = Brushes.Yellow;
                MainContentArea.Content = null;

                _loadedViews.RemoveAll(v => v.Name.Contains(result.ViewName ?? "", StringComparison.OrdinalIgnoreCase));
                RefreshSidebarMenu();

                // Ha a törölt nézet volt kijelölve, a kijelölés már érvénytelen - töröljük.
                ClearSelection();

                ApproveButton.IsVisible = false;
                DiscardButton.IsVisible = false;
                return;
            }

            if (result.IsSuccess && result.LoadedControl != null)
            {
                MainContentArea.Content = result.LoadedControl;
                StatusText.Text = $"Új funkció elkészült: '{result.ViewName}'. Mentheti vagy elvetheti.";
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

                            // A kijelölést a FRISSEN generált/módosított nézetre frissítjük, hogy a
                            // következő prompt (pl. "javítsd ki még ezt is") ugyanazt a fájlt módosítsa
                            // tovább - így iteratívan lehet finomítani/repair-elni egy funkciót.
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
                StatusText.Text = $"Hiba történt: {result.ErrorMessage}";
                StatusText.Foreground = Brushes.Red;
                ApproveButton.IsVisible = false;
                DiscardButton.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Váratlan hiba: {ex.Message}";
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
            StatusText.Text = "Nincs mit jóváhagyni.";
            return;
        }

        StatusText.Text = "Funkció jóváhagyása és feltöltése (Git Push)...";
        StatusText.Foreground = Brushes.Orange;

        bool pushSuccess = await _evolutionService.AcceptAndPushFeatureAsync(
            _lastEvolveResult.FilePath,
            _lastEvolveResult.ViewName ?? "Új Nézet"
        );

        if (pushSuccess)
        {
            StatusText.Text = "Sikeresen elmentve a lemezre és feltöltve a GitHub-ra!";
            StatusText.Foreground = Brushes.Green;
        }
        else
        {
            StatusText.Text = "A fájl elmentve a lemezre, de a Git Push sikertelen volt.";
            StatusText.Foreground = Brushes.Yellow;
        }

        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;
    }

    private async void OnDiscardButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_lastEvolveResult == null || string.IsNullOrWhiteSpace(_lastEvolveResult.FilePath))
        {
            StatusText.Text = "Nincs mit elvetni.";
            return;
        }

        bool deleted = await _evolutionService.DiscardFeatureAsync(_lastEvolveResult.FilePath);
        if (deleted)
        {
            StatusText.Text = "Funkció elvetve és törölve a lemezről.";
            StatusText.Foreground = Brushes.Yellow;
            MainContentArea.Content = null;

            // Ha az elvetett fájl volt a kijelölt nézet, a kijelölés már érvénytelen - töröljük.
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

    private void SetBusy(bool isBusy)
    {
        BusyIndicator.IsVisible = isBusy;
        AiButton.IsEnabled = !isBusy;
        PromptInput.IsEnabled = !isBusy;
    }
}