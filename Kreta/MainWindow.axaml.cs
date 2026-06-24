using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using Kreta.Core;
using Kreta.Contexts;
using Kreta.Services.AI;
using Kreta.Services.Database;
using Kreta.Services.Evolution;
using Kreta.Services.Git;
using Kreta.Services.Security;

namespace Kreta;

public partial class MainWindow : Window
{
    private AiEvolveResponse? _pendingResponse;
    
    private readonly IDynamicLoader _loader;
    private readonly IGitService _gitService;
    private readonly IEvolutionService _evolutionService;
    private readonly IAiService _aiService;
    private readonly AstAnalyzer _astAnalyzer;

    public MainWindow()
    {
        InitializeComponent();

        // Szolgáltatások példányosítása
        _loader = new DynamicLoader();
        _gitService = new GitService();
        _evolutionService = new EvolutionService();
        _aiService = new AiService();
        _astAnalyzer = new AstAnalyzer();

        // Adatbázis inicializáció és tesztadatok feltöltése
        using (var db = new KretaDbContext())
        {
            db.Seed();
        }

        // Környezet és státusz betöltése
        LoadEnvironment();
        UpdateEvolverStatus();

        // Kattintási és választási események regisztrálása
        AiButton.Click += OnAiClick;
        ApproveButton.Click += OnApproveClick;
        DiscardButton.Click += OnDiscardClick;
        RoleSelector.SelectionChanged += OnRoleSelectionChanged;

        // Kezdő szerepkör betöltése képernyőre
        TriggerInitialRoleLoad();
    }

    private void TriggerInitialRoleLoad()
    {
        // Szimulálunk egy kezdeti szerepkör választást az indítási állapot beállításához
        OnRoleSelectionChanged(null, null!);
    }

    private void LoadEnvironment()
    {
        try
        {
            DotNetEnv.Env.Load();
            var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrEmpty(key)) Environment.SetEnvironmentVariable("GOOGLE_API_KEY", key);

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_API_KEY")))
                SetStatus("⚠️ GEMINI_API_KEY / GOOGLE_API_KEY hiányzik a .env-ből!", isError: true);
            else
                SetStatus("✅ API Kulcs betöltve. Írj be kérést → 🤖 Fejlesztés.");
        }
        catch (Exception ex) { SetStatus($".env hiba: {ex.Message}", isError: true); }
    }

    private void UpdateEvolverStatus()
    {
        EvolverStatusText.Text = "🟢 Self-evolving: AKTÍV (Azonnali RAM + Háttér mentés)";
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Avalonia.Media.Brushes.Red : Avalonia.Media.Brushes.Gray;
    }

 private void OnRoleSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RoleSelector == null || MainContentArea == null) return;

        var selectedItem = RoleSelector.SelectedItem as ComboBoxItem;
        if (selectedItem == null) return;

        string roleString = selectedItem.Tag?.ToString() ?? "Student";
        Role selectedRole = Enum.Parse<Role>(roleString);

        using (var db = new KretaDbContext())
        {
            // 1. DIÁK SZIMULÁCIÓ (Kovács János, ID: 1)
            if (selectedRole == Role.Student)
            {
                var studentContext = new SqliteStudentContext(db, 1); 
                var profile = studentContext.GetMyProfile();
                var grades = studentContext.GetMyGrades();

                if (profile != null)
                {
                    string gradesLines = string.Join("\n", grades.Select(g => 
                        $"• {g.Subject.Name}: {g.Value} (Súly: {g.Weight}%) - {g.Date:yyyy.MM.dd.}"));

                    MainContentArea.Content = new TextBlock { 
                        Text = $"👨‍🎓 Bejelentkezve: {profile.Name} ({profile.Role})\n📧 Email: {profile.Email}\n\n📚 Osztályzatok:\n{gradesLines}",
                        FontSize = 14, LineHeight = 22,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                        Margin = new Thickness(20)
                    };
                }
            }
            // 2. TANÁR SZIMULÁCIÓ (Szabó Mária, ID: 2)
            else if (selectedRole == Role.Teacher)
            {
                // Lekérjük a tanár adatait az adatbázisból
                var teacher = db.Users.FirstOrDefault(u => u.Id == 2);
                
                if (teacher != null)
                {
                    MainContentArea.Content = new TextBlock { 
                        Text = $"👩‍🏫 Bejelentkezve: {teacher.Name} ({teacher.Role})\n📧 Email: {teacher.Email}\n\n🛠️ Engedélyezett műveletek:\n• Osztályzatok beírása és törlése\n• Osztály-statisztikák lekérése\n• Házi feladatok rögzítése",
                        FontSize = 14, LineHeight = 22,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                        Margin = new Thickness(20)
                    };
                }
            }
            else if (selectedRole == Role.Director)
            {
                var director = db.Users.FirstOrDefault(u => u.Id == 3);

                if (director != null)
                {
                    MainContentArea.Content = new TextBlock { 
                        Text = $"👑 Bejelentkezve: {director.Name} ({director.Role})\n📧 Email: {director.Email}\n\n🛠️ Rendszergazdai műveletek:\n• Globális iskolai naptár és órarend módosítása\n• Új tantárgyak, osztályok és versenyek létrehozása\n• Felhasználók (Tanárok, Diákok) kezelése",
                        FontSize = 14, LineHeight = 22,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                        Margin = new Thickness(20)
                    };
                }
            }
        }
    }

    private async void OnAiClick(object? sender, RoutedEventArgs e)
    {
        AiButton.IsEnabled = false;
        StatusText.Text = "🤖 AI kód generálása...";
        
        string dllPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Dynamic", "DynamicFeatures.dll");

        try 
        {
            // Lekérjük az aktuális szerepkört a választóból
            var selectedItem = RoleSelector.SelectedItem as ComboBoxItem;
            string roleString = selectedItem?.Tag?.ToString() ?? "Student";
            Role currentRole = Enum.Parse<Role>(roleString);

            // Generálás indítása az aktuális szerepkör átadásával
            var aiResponse = await _aiService.GenerateFeatureAsync(PromptInput.Text ?? "", currentRole);
            _pendingResponse = aiResponse; 
            
            // Biztonsági ellenőrzés (AST Tűzfal)
            if (!_astAnalyzer.IsSafe(aiResponse.HandlerMethod))
            {
                StatusText.Text = "🚨 Biztonsági riasztás: Tiltott kód detektálva!";
                AiButton.IsEnabled = true;
                return;
            }
            
            // Háttér-fordítás
            var buildResult = await _evolutionService.EvolveFeatureAsync(
                aiResponse.HandlerName, 
                aiResponse.Label, 
                aiResponse.HandlerMethod, 
                "");

            if (!buildResult.IsSuccess) {
                StatusText.Text = buildResult.Message;
                AiButton.IsEnabled = true;
                return;
            }
            
            // Dinamikus betöltés a RAM-ba
            var views = _loader.GetViewsFromAssembly(dllPath);
            var loadedView = views.FirstOrDefault();

            if (loadedView != null) {
                MainContentArea.Content = loadedView;
                ApproveButton.IsVisible = true; 
                DiscardButton.IsVisible = true;
                StatusText.Text = "⚡ RAM Preview aktív. Kérlek hagyd jóvá vagy vesd el!";
            }
        }
        catch (Exception ex) {
            StatusText.Text = $"❌ Hiba: {ex.Message}";
            AiButton.IsEnabled = true;
        }
    }

    private void OnApproveClick(object? sender, RoutedEventArgs e)
    {
        if (_pendingResponse == null) return;
        
        // Git commit készítése
        _gitService.Commit($"EvolKréta: Új ablak hozzáadva ({_pendingResponse.Label})");
        
        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;
        AiButton.IsEnabled = true;
        PromptInput.Text = "";
        
        StatusText.Text = $"💾 '{_pendingResponse.Label}' sikeresen rögzítve a Git verziókezelőben!";
        _pendingResponse = null;
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        MainContentArea.Content = new TextBlock { 
            Text = "Fejlesztés elvetve.", 
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, 
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
        };
        
        _loader.UnloadAssembly();
        _gitService.RevertToLastStable();
        
        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;
        AiButton.IsEnabled = true;
        _pendingResponse = null;
        
        StatusText.Text = "❌ Változtatások elvetve, fájlrendszer visszaállítva az utolsó stabil állapotra.";
    }
    
}