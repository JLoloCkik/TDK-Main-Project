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

    // Szimulált bejelentkezett diák, tanár és igazgató ID-ja
    private const int SimulatedStudentId = 1; // Kovács János
    private const int SimulatedTeacherId = 4; // Szabó Mária
    private const int SimulatedDirectorId = 5; // Nagy Péter

    private Role _currentRole = Role.Student;
    
    // Az összes betöltött modul forráskódja és példánya
    private readonly List<IEvolView> _loadedViews = new();
    private readonly Dictionary<IEvolView, string> _viewSourceMap = new();
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
                            loadedCount++;
                        }
                    }
                }
                else
                {
                    // Mentett modul fordítási hibájának konzolra írása
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n[RENDSZERINDÍTÁSI SÚLYOS HIBA] Nem sikerült lefordítani a korábbi modult ({viewName}):");
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
            bool isStudentView = constructors.Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IStudentContext)));
            bool isTeacherView = constructors.Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(ITeacherContext)));
            bool isDirectorView = constructors.Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IDirectorContext)));

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
            // Kontextus-érzékeny prompt összeállítása
            var roleContextName = $"I{_currentRole}Context";
            var contextualPrompt = $"Felhasználó szerepköre: {_currentRole}. " +
                                   $"Az elvárt modul leírása: {prompt}." +
                                   $"\n\nFONTOS TECHNIKAI SZABÁLYOK:" +
                                   $"\n1. Az osztály neve legyen egyedi (pl. MyFeature_{Guid.NewGuid():N}.cs)." +
                                   $"\n2. KÖTELEZŐEN a konstruktorában kérje be a '{roleContextName}' interfészt!" +
                                   $"\n3. Kizárólag a megadott {roleContextName} metódusait használd a mentésre és beolvasásra." +
                                   $"\n4. Építs fel egy szép Avalonia UI vezérlőt tiszta C# kóddal. Ha gombnyomás történik, ments el az adatokat és frissítsd a listákat.";

            // 1. Az eredeti, 3-paraméteres interfész hívása
            AiEvolveResponse response = await _aiService.GenerateFeatureAsync(contextualPrompt, _currentRole, null);

            // 2. Az eredeti, 4-paraméteres aszinkron fordítás hívása
            var result = await _evolutionService.EvolveFeatureAsync(response.ViewName, response.Description, response.SourceCode, response.TestCode);

            if (result.IsSuccess && result.CompiledAssembly != null)
            {
                var newViewType = result.CompiledAssembly.GetTypes()
                    .FirstOrDefault(t => typeof(IEvolView).IsAssignableFrom(t) && !t.IsInterface);

                if (newViewType != null)
                {
                    var newViewInstance = InstantiateViewForRole(newViewType);
                    if (newViewInstance != null)
                    {
                        // Hozzáadjuk az ideiglenesen lefordított listához
                        _loadedViews.Add(newViewInstance);
                        _viewSourceMap[newViewInstance] = response.SourceCode;

                        RefreshSidebarMenu();
                        NavigateToView(newViewInstance);

                        ApproveButton.IsVisible = true;
                        DiscardButton.IsVisible = true;
                        StatusText.Text = "Sikeres generálás! Mentheti vagy elvetheti a módosítást.";
                        StatusText.Foreground = Brushes.Green;
                    }
                }
                else
                {
                    StatusText.Text = "A fordítás sikeres, de nem található IEvolView megvalósítás.";
                    StatusText.Foreground = Brushes.Red;
                }
            }
            else
            {
                StatusText.Text = "Fordítási hiba! Próbálja meg finomítani a promptot.";

                // KIÍRÁS A KONZOLRA (terminálba) - GENERÁLT FORRÁSKÓD ÉS HIBAÜZENETEK IS!
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n========================================================================");
                Console.WriteLine($"[GENERÁLT C# FORRÁSKÓD - {response.ViewName.ToUpper()}]");
                Console.WriteLine("========================================================================");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(response.SourceCode);
                
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("========================================================================");
                Console.WriteLine($"[FORDÍTÁSI HIBAÜZENETEK]");
                Console.WriteLine("========================================================================");
                Console.WriteLine(result.ErrorMessage);
                Console.WriteLine("========================================================================\n");
                Console.ResetColor();

                MainContentArea.Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = $"Fordítási hibák listája:\n{result.ErrorMessage}",
                        Foreground = Brushes.Red,
                        Margin = new Avalonia.Thickness(10)
                    }
                };
            }
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

    private void OnApproveButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedViews.Count == 0) return;

        var lastView = _loadedViews.Last();
        if (_viewSourceMap.TryGetValue(lastView, out var sourceCode))
        {
            try
            {
                // Elmentjük végleges fájlként a lemezre, hogy indításkor is betöltődjön
                var fileName = $"EvolView_{lastView.GetType().Name}.cs";
                var filePath = Path.Combine(_evolViewsDirectory, fileName);
                File.WriteAllText(filePath, sourceCode);

                StatusText.Text = $"Modul ({lastView.Name}) elmentve és rögzítve az iskolarendszerbe.";
                StatusText.Foreground = Brushes.Green;

                ApproveButton.IsVisible = false;
                DiscardButton.IsVisible = false;
                PromptInput.Text = "";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Hiba a mentés során: {ex.Message}";
                StatusText.Foreground = Brushes.Red;
            }
        }
    }

    private void OnDiscardButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedViews.Count > 0)
        {
            var lastView = _loadedViews.Last();
            _loadedViews.Remove(lastView);
            _viewSourceMap.Remove(lastView);

            RefreshSidebarMenu();

            MainContentArea.Content = new TextBlock
            {
                Text = "Generált modul sikeresen elvetve.",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Gray
            };

            StatusText.Text = "Változtatások elvetve.";
            StatusText.Foreground = Brushes.Orange;

            ApproveButton.IsVisible = false;
            DiscardButton.IsVisible = false;
        }
    }

    private void SetBusy(bool isBusy)
    {
        BusyIndicator.IsVisible = isBusy;
        PromptInput.IsEnabled = !isBusy;
        AiButton.IsEnabled = !isBusy;
        RoleSelector.IsEnabled = !isBusy;
    }
}