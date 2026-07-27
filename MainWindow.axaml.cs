using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Avalonia; // CornerRadius feloldásához szükséges
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;
using Kreta.Services.AI;
using Kreta.Services.Database;
using Kreta.Services.Evolution;

namespace Kreta;

public partial class MainWindow : Window
{
    private readonly IAiService _aiService;
    private readonly IEvolutionService _evolutionService;
    private readonly KretaDbContext _dbContext;

    // Az önjavító (Self-Healing) ciklus maximális próbálkozásszáma — ha a fordítás
    // elhasal, a hibaüzenet visszamegy az AI-nak javításra, legfeljebb ennyiszer,
    // mielőtt a felhasználót zavarnánk vele. Lásd OnAiButtonClick.
    private const int MaxSelfHealAttempts = 3;

    // Szimulált bejelentkezett diák, tanár és igazgató ID-ja
    private const int SimulatedStudentId = 1; // Kovács János
    private const int SimulatedTeacherId = 4; // Szabó Mária
    private const int SimulatedDirectorId = 5; // Nagy Péter

    private Role _currentRole = Role.Student;

    // Az összes betöltött modul forráskódja és példánya
    private readonly List<IEvolView> _loadedViews = new();
    private readonly Dictionary<IEvolView, string> _viewFilePathMap = new();
    private readonly string _evolViewsDirectory;

    public MainWindow()
    {
        InitializeComponent();

        _evolViewsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EvolViews");
        if (!Directory.Exists(_evolViewsDirectory))
        {
            Directory.CreateDirectory(_evolViewsDirectory);
        }

        // Adatbázis inicializálás és seeding
        _dbContext = new KretaDbContext();
        _dbContext.SeedData();

        _aiService = new AiService();
        _evolutionService = new EvolutionService();

        // UI Eseménykezelők
        AiButton.Click += OnAiButtonClick;
        ApproveButton.Click += OnApproveButtonClick;
        DiscardButton.Click += OnDiscardButtonClick;
        RoleSelector.SelectionChanged += OnRoleChanged;

        // Mentett fájlok betöltése és fordítása indításkor
        BootAndCompileSavedModules();
        RefreshSidebarMenu();
    }

    private void OnRoleChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RoleSelector.SelectedItem is ComboBoxItem item && item.Tag is string roleStr)
        {
            if (Enum.TryParse<Role>(roleStr, out var role))
            {
                _currentRole = role;
                MainContentArea.Content = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"Sikeresen átváltott a(z) {role} szerepkörre!",
                            FontSize = 16,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#2C3E50")),
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Válasszon egy hozzárendelt funkciót az oldalsávról.",
                            FontSize = 12,
                            Foreground = Brushes.Gray,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                };
                RefreshSidebarMenu();
            }
        }
    }

    private async void BootAndCompileSavedModules()
    {
        try
        {
            var files = Directory.GetFiles(_evolViewsDirectory, "*.cs");
            if (files.Length == 0) return;

            StatusText.Text = $"{files.Length} korábbi modul betöltése és fordítása...";
            StatusText.Foreground = Brushes.Orange;

            int loadedCount = 0;
            foreach (var file in files)
            {
                var sourceCode = File.ReadAllText(file);
                var viewName = Path.GetFileNameWithoutExtension(file);

                // Meglévő, gyári aszinkron EvolveFeatureAsync hívása egyenként
                var result = await _evolutionService.EvolveFeatureAsync(viewName, "Mentett modul", sourceCode, "");

                if (result.IsSuccess && result.CompiledAssembly != null)
                {
                    var viewTypes = result.CompiledAssembly.GetTypes()
                        .Where(t => typeof(IEvolView).IsAssignableFrom(t) && !t.IsInterface);

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
                    // Mentett modul fordítási hibájának konzolra írása
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(
                        $"\n[RENDSZERINDÍTÁSI SÚLYOS HIBA] Nem sikerült lefordítani a korábbi modult ({viewName}):");
                    Console.WriteLine(result.ErrorMessage);
                    Console.ResetColor();
                }
            }

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

    private void RemoveStaleLoadedViews()
    {
        var stale = _viewFilePathMap
            .Where(kvp => !File.Exists(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var view in stale)
        {
            _loadedViews.Remove(view);
            _viewFilePathMap.Remove(view);
        }
    }

    private IEvolView? InstantiateViewForRole(Type viewType)
    {
        try
        {
            var constructors = viewType.GetConstructors();

            // Megkeressük, milyen interfészt vár a konstruktor
            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length == 1)
                {
                    var paramType = parameters[0].ParameterType;

                    if (paramType == typeof(IStudentContext))
                    {
                        var context = new SqliteStudentContext(_dbContext, SimulatedStudentId);
                        return (IEvolView?)Activator.CreateInstance(viewType, context);
                    }

                    if (paramType == typeof(ITeacherContext))
                    {
                        var context = new SqliteTeacherContext(_dbContext);
                        return (IEvolView?)Activator.CreateInstance(viewType, context);
                    }

                    if (paramType == typeof(IDirectorContext))
                    {
                        var context = new SqliteDirectorContext(_dbContext);
                        return (IEvolView?)Activator.CreateInstance(viewType, context);
                    }
                }
            }

            // Fallback üres konstruktorhoz
            return (IEvolView?)Activator.CreateInstance(viewType);
        }
        catch
        {
            return null;
        }
    }

    private void RefreshSidebarMenu()
    {
        SidebarMenuPanel.Children.Clear();

        foreach (var view in _loadedViews)
        {
            // Konstruktor ellenőrzéssel kiszűrjük a megfelelő szerepköröket
            var constructors = view.GetType().GetConstructors();
            bool isStudentView =
                constructors.Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IStudentContext)));
            bool isTeacherView =
                constructors.Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(ITeacherContext)));
            bool isDirectorView =
                constructors.Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IDirectorContext)));

            bool isVisible = false;
            if (_currentRole == Role.Student && isStudentView) isVisible = true;
            if (_currentRole == Role.Teacher && isTeacherView) isVisible = true;
            if (_currentRole == Role.Director && isDirectorView) isVisible = true;

            if (isVisible)
            {
                var btn = new Button
                {
                    Content = $"🔹 {view.Name}",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Avalonia.Thickness(0, 0, 0, 4),
                    Padding = new Avalonia.Thickness(12, 10),
                    Background = new SolidColorBrush(Color.Parse("#2C3E50")),
                    Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(6)
                };

                btn.Click += (s, e) => NavigateToView(view);
                SidebarMenuPanel.Children.Add(btn);
            }
        }
    }

    private void NavigateToView(IEvolView view)
    {
        try
        {
            MainContentArea.Content = view.CreateView();
            StatusText.Text = $"Aktív modul: {view.Name}";
            StatusText.Foreground = Brushes.Green;
        }
        catch (Exception ex)
        {
            MainContentArea.Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = $"Hiba a nézet kirajzolásakor:\n{ex.Message}\n\n{ex.StackTrace}",
                    Foreground = Brushes.Red,
                    Margin = new Avalonia.Thickness(10)
                }
            };
        }
    }

    private async void OnAiButtonClick(object? sender, RoutedEventArgs e)
    {
        var prompt = PromptInput.Text;
        if (string.IsNullOrWhiteSpace(prompt)) return;

        SetBusy(true);
        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;

        try
        {
            var roleContextName = $"I{_currentRole}Context";
            var contextualPrompt = $"Felhasználó szerepköre: {_currentRole}. " +
                                   $"Az elvárt modul leírása: {prompt}." +
                                   $"\n\nFONTOS TECHNIKAI SZABÁLYOK:" +
                                   $"\n1. Ha ÚJ funkciót hozol létre, az osztály neve legyen egyedi (pl. MyFeature_{Guid.NewGuid():N}). " +
                                   $"Ha egy MEGLÉVŐ, fent felsorolt nézetet MÓDOSÍTASZ, tartsd meg PONTOSAN ugyanazt az osztálynevet!" +
                                   $"\n2. KÖTELEZŐEN a konstruktorában kérje be a '{roleContextName}' interfészt!" +
                                   $"\n3. Kizárólag a megadott {roleContextName} metódusait használd a mentésre és beolvasásra." +
                                   $"\n4. Építs fel egy szép Avalonia UI vezérlőt tiszta C# kóddal. Ha gombnyomás történik, ments el az adatokat és frissítsd a listákat.";

            StatusText.Text = "🤖 AI kód generálása (szükség esetén önjavítással)...";
            StatusText.Foreground = Brushes.Orange;

            EvolveResult result;
            try
            {
                result = await _evolutionService.EvolveAsync(contextualPrompt, _currentRole, MaxSelfHealAttempts);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Hiba az AI hívás közben: {ex.Message}";
                StatusText.Foreground = Brushes.Red;
                return;
            }

            // Ha az AI törölt vagy felülírt egy fájlt, itt szűrjük ki a memóriából
            // azokat a nézeteket, amelyek mögül eltűnt a lemezen lévő forrás.
            RemoveStaleLoadedViews();

            if (result.IsRejectedAction)
            {
                StatusText.Text = $"❌ {result.Description ?? "Hozzáférés megtagadva."}";
                StatusText.Foreground = Brushes.Red;
                RefreshSidebarMenu();
                return;
            }

            if (result.IsDeletedAction)
            {
                StatusText.Text = $"🗑️ Modul törölve: {result.ViewName}";
                StatusText.Foreground = Brushes.Green;
                RefreshSidebarMenu();
                PromptInput.Text = "";
                return;
            }

            if (result.IsSuccess && result.CompiledAssembly != null && result.FilePath != null)
            {
                var newViewType = result.CompiledAssembly.GetTypes()
                    .FirstOrDefault(t => typeof(IEvolView).IsAssignableFrom(t) && !t.IsInterface);

                var newViewInstance = newViewType != null ? InstantiateViewForRole(newViewType) : null;

                if (newViewInstance != null)
                {
                    _loadedViews.Add(newViewInstance);
                    _viewFilePathMap[newViewInstance] = result.FilePath;

                    RefreshSidebarMenu();
                    NavigateToView(newViewInstance);

                    ApproveButton.IsVisible = true;
                    DiscardButton.IsVisible = true;
                    StatusText.Text = "✅ Sikeres generálás! Mentheti (Git push) vagy elvetheti a módosítást.";
                    StatusText.Foreground = Brushes.Green;
                    return;
                }

                StatusText.Text = "A fordítás sikeres volt, de a nézetet nem sikerült példányosítani.";
                StatusText.Foreground = Brushes.Red;
                return;
            }

            // Sikertelen generálás/fordítás az önjavítási kísérletek után is
            StatusText.Text = result.ErrorMessage ?? "Ismeretlen hiba történt a generálás során.";
            StatusText.Foreground = Brushes.Red;

            MainContentArea.Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = $"Fordítási/generálási hiba:\n{result.ErrorMessage}",
                    Foreground = Brushes.Red,
                    Margin = new Avalonia.Thickness(10)
                }
            };
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hiba történt: {ex.Message}";
            StatusText.Foreground = Brushes.Red;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnApproveButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedViews.Count == 0) return;

        var lastView = _loadedViews.Last();
        if (!_viewFilePathMap.TryGetValue(lastView, out var filePath) || !File.Exists(filePath))
        {
            StatusText.Text = "Nem található a modulhoz tartozó fájl a lemezen.";
            StatusText.Foreground = Brushes.Red;
            return;
        }

        StatusText.Text = $"Modul ({lastView.Name}) feltöltése a GitHub 'ai-dev' ágára...";
        StatusText.Foreground = Brushes.Orange;

        try
        {
            bool pushed = await _evolutionService.AcceptAndPushFeatureAsync(filePath, lastView.Name);

            StatusText.Text = pushed
                ? $"Modul ({lastView.Name}) elmentve és sikeresen feltöltve a GitHubra."
                : $"Modul ({lastView.Name}) elmentve, de a Git feltöltés sikertelen volt (lásd konzol).";
            StatusText.Foreground = pushed ? Brushes.Green : Brushes.OrangeRed;

            ApproveButton.IsVisible = false;
            DiscardButton.IsVisible = false;
            PromptInput.Text = "";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hiba a Git feltöltés során: {ex.Message}";
            StatusText.Foreground = Brushes.Red;
        }
    }

    private async void OnDiscardButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedViews.Count == 0) return;

        var lastView = _loadedViews.Last();
        _loadedViews.Remove(lastView);

        if (_viewFilePathMap.TryGetValue(lastView, out var filePath))
        {
            await _evolutionService.DiscardFeatureAsync(filePath);
            _viewFilePathMap.Remove(lastView);
        }

        RefreshSidebarMenu();

        MainContentArea.Content = new TextBlock
        {
            Text = "Generált modul sikeresen elvetve.",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.Gray
        };

        StatusText.Text = "Változtatások elvetve, a fájl törölve a lemezről.";
        StatusText.Foreground = Brushes.Orange;

        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;
    }

    private void SetBusy(bool isBusy)
    {
        BusyIndicator.IsVisible = isBusy;
        PromptInput.IsEnabled = !isBusy;
        AiButton.IsEnabled = !isBusy;
        RoleSelector.IsEnabled = !isBusy;
    }
}