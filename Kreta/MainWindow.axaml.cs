using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using Kreta.Core;
using Kreta.Contexts;
using Kreta.Services;
using Kreta.Services.AI;
using Kreta.Services.Database;
using Kreta.Services.Evolution;
using Kreta.Services.Git;
using Kreta.Services.Security;

namespace Kreta;

public partial class MainWindow : Window
{
    private const int MaxSelfHealAttempts = 3;

    private AiEvolveResponse? _pendingResponse;

    // Ez NEM a régi, egyszer-példányosított _loader többé: minden új
    // generáláskor friss DynamicLoader (= friss ALC) jön létre, a régit
    // előtte leállítjuk. Lásd DynamicLoader.cs megjegyzését.
    private IDynamicLoader? _currentLoader;

    private readonly IGitService _gitService;
    private readonly IEvolutionService _evolutionService;
    private readonly IAiService _aiService;
    private readonly AstAnalyzer _astAnalyzer;

    public MainWindow()
    {
        InitializeComponent();

        _gitService = new GitService();
        _evolutionService = new EvolutionService();
        _aiService = new AiService();
        _astAnalyzer = new AstAnalyzer();

        using (var db = new KretaDbContext())
        {
            db.Seed();
        }

        LoadEnvironment();
        UpdateEvolverStatus();

        AiButton.Click += OnAiClick;
        ApproveButton.Click += OnApproveClick;
        DiscardButton.Click += OnDiscardClick;
        RoleSelector.SelectionChanged += OnRoleSelectionChanged;

        TriggerInitialRoleLoad();
    }

    private void TriggerInitialRoleLoad()
    {
        OnRoleSelectionChanged(null, null!);
    }

    private void LoadEnvironment()
    {
        try
        {
            // A .env-et a projekt gyökeréből töltjük be (nem a CWD-ből!),
            // mert a CWD az indítás módjától függően más és más lehet.
            string? root = PathHelper.FindProjectRoot();
            string envPath = root != null ? Path.Combine(root, ".env") : ".env";

            if (File.Exists(envPath))
            {
                DotNetEnv.Env.Load(envPath);
            }
            else
            {
                // Végső próbálkozás: hátha mégis az aktuális könyvtárban van.
                DotNetEnv.Env.Load();
            }

            var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (string.IsNullOrWhiteSpace(key))
                SetStatus($"⚠️ GEMINI_API_KEY hiányzik! Keresett hely: {envPath}", isError: true);
            else
                SetStatus("✅ API kulcs betöltve. Írj be egy kérést, majd kattints a 🤖 Fejlesztés gombra.");
        }
        catch (Exception ex)
        {
            SetStatus($".env betöltési hiba: {ex.Message}", isError: true);
        }
    }

    private void UpdateEvolverStatus()
    {
        EvolverStatusText.Text = "🟢 Self-evolving: AKTÍV (Azonnali RAM + Háttér mentés)";
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Avalonia.Media.Brushes.OrangeRed : Avalonia.Media.Brushes.Gray;
    }

    private void SetBusy(bool busy)
    {
        AiButton.IsEnabled = !busy;
        PromptInput.IsEnabled = !busy;
        RoleSelector.IsEnabled = !busy;
        BusyIndicator.IsVisible = busy;
    }

    private void OnRoleSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RoleSelector == null || MainContentArea == null) return;

        var selectedItem = RoleSelector.SelectedItem as ComboBoxItem;
        if (selectedItem == null) return;

        string roleString = selectedItem.Tag?.ToString() ?? "Student";
        Role selectedRole = Enum.Parse<Role>(roleString);

        using var db = new KretaDbContext();

        if (selectedRole == Role.Student)
        {
            var studentContext = new SqliteStudentContext(db, 1);
            var profile = studentContext.GetMyProfile();
            var grades = studentContext.GetMyGrades();

            if (profile != null)
            {
                string gradesLines = string.Join("\n", grades.Select(g =>
                    $"• {g.Subject.Name}: {g.Value} (Súly: {g.Weight}%) - {g.Date:yyyy.MM.dd.}"));

                MainContentArea.Content = BuildProfileCard(
                    "👨‍🎓", profile.Name, profile.Role.ToString(), profile.Email,
                    "📚 Osztályzatok:", gradesLines);
            }
        }
        else if (selectedRole == Role.Teacher)
        {
            var teacher = db.Users.FirstOrDefault(u => u.Id == 2);

            if (teacher != null)
            {
                MainContentArea.Content = BuildProfileCard(
                    "👩‍🏫", teacher.Name, teacher.Role.ToString(), teacher.Email,
                    "🛠️ Engedélyezett műveletek:",
                    "• Osztályzatok beírása és törlése\n• Osztály-statisztikák lekérése\n• Házi feladatok rögzítése");
            }
        }
        else if (selectedRole == Role.Director)
        {
            var director = db.Users.FirstOrDefault(u => u.Id == 3);

            if (director != null)
            {
                MainContentArea.Content = BuildProfileCard(
                    "👑", director.Name, director.Role.ToString(), director.Email,
                    "🛠️ Rendszergazdai műveletek:",
                    "• Globális iskolai naptár és órarend módosítása\n• Új tantárgyak, osztályok és versenyek létrehozása\n• Felhasználók (Tanárok, Diákok) kezelése");
            }
        }
    }

    private static Control BuildProfileCard(string emoji, string name, string role, string email, string sectionTitle, string sectionBody)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(4) };

        panel.Children.Add(new TextBlock
        {
            Text = $"{emoji} Bejelentkezve: {name} ({role})",
            FontSize = 16,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = Avalonia.Media.Brush.Parse("#2C3E50")
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"📧 {email}",
            FontSize = 13,
            Foreground = Avalonia.Media.Brush.Parse("#7F8C8D"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        panel.Children.Add(new TextBlock
        {
            Text = sectionTitle,
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = Avalonia.Media.Brush.Parse("#34495E"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        panel.Children.Add(new TextBlock
        {
            Text = sectionBody,
            FontSize = 14,
            LineHeight = 22,
            Foreground = Avalonia.Media.Brush.Parse("#2C3E50")
        });

        return new Border
        {
            Background = Avalonia.Media.Brushes.White,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20),
            BoxShadow = Avalonia.Media.BoxShadows.Parse("0 2 8 0 #14000000"),
            Child = panel,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
    }

private async void OnAiClick(object? sender, RoutedEventArgs e)
    {
        string prompt = PromptInput.Text ?? "";
        if (string.IsNullOrWhiteSpace(prompt))
        {
            StatusText.Text = "⚠️ Írj be egy kérést a fejlesztéshez!";
            return;
        }

        SetBusy(true);

        string dllPath = Path.Combine(AppContext.BaseDirectory, "Dynamic", "DynamicFeatures.dll");

        var selectedItem = RoleSelector.SelectedItem as ComboBoxItem;
        string roleString = selectedItem?.Tag?.ToString() ?? "Student";
        Role currentRole = Enum.Parse<Role>(roleString);

        string? feedback = null;

        try
        {
            for (int attempt = 1; attempt <= MaxSelfHealAttempts; attempt++)
            {
                StatusText.Text = attempt == 1
                    ? "🤖 AI kód generálása..."
                    : $"🔁 Önjavítás ({attempt}. próbálkozás)...";

                AiEvolveResponse aiResponse;
                try
                {
                    aiResponse = await _aiService.GenerateFeatureAsync(prompt, currentRole, feedback);
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"❌ AI hiba: {ex.Message}";
                    return;
                }

                if (string.Equals(aiResponse.Action, "REJECT", StringComparison.OrdinalIgnoreCase))
                {
                    StatusText.Text = $"🚫 A kérés elutasítva: {aiResponse.Label}";
                    return;
                }

                _pendingResponse = aiResponse;

                // DIAGNOSZTIKA: Kiírjuk a terminálba a kapott kódot!
                Console.WriteLine($"\n================ PRÓBÁLKOZÁS {attempt} ================");
                Console.WriteLine($"[GENERÁLT KÓD]:\n{aiResponse.HandlerMethod}\n");

                if (!_astAnalyzer.IsSafe(aiResponse.HandlerMethod, currentRole, out string reason))
                {
                    feedback = $"Biztonsági szűrő elutasította a kódot: {reason} " +
                               "Írj olyan kódot, ami nem sérti a jogosultsági szabályokat, és nem használ tiltott API-kat!";
                    StatusText.Text = $"🚨 Biztonsági riasztás: {reason}";
                    
                    // DIAGNOSZTIKA: Tűzfal hiba
                    Console.WriteLine($"[🔥 TŰZFAL BLOKKOLTA]: {reason}");
                    Console.WriteLine("==================================================\n");
                    continue;
                }

                var buildResult = await _evolutionService.EvolveFeatureAsync(
                    aiResponse.HandlerName, aiResponse.Label, aiResponse.HandlerMethod, "");

                if (!buildResult.IsSuccess)
                {
                    feedback = buildResult.Message;
                    StatusText.Text = $"⚠️ Fordítási hiba ({attempt}. próbálkozás) — önjavítás indul...";
                    
                    // DIAGNOSZTIKA: Fordítási hiba
                    Console.WriteLine($"[💥 FORDÍTÁSI HIBA]:\n{buildResult.Message}");
                    Console.WriteLine("==================================================\n");
                    continue;
                }

                // Előző generálás ALC-jét leállítjuk, csak UTÁNA hozunk létre újat.
                _currentLoader?.UnloadAssembly();
                _currentLoader = new DynamicLoader();

                var views = _currentLoader.GetViewsFromAssembly(dllPath);
                var loadedView = views.FirstOrDefault();

                if (loadedView is Control control)
                {
                    MainContentArea.Content = control;
                    ApproveButton.IsVisible = true;
                    DiscardButton.IsVisible = true;
                    StatusText.Text = "⚡ RAM Preview aktív. Kérlek hagyd jóvá vagy vesd el!";
                    
                    Console.WriteLine("[✅ SIKERES FORDÍTÁS ÉS MEGJELENÍTÉS!]");
                    Console.WriteLine("==================================================\n");
                    return;
                }

                StatusText.Text = "❌ A generált DLL nem tartalmazott érvényes, megjeleníthető nézetet.";
                Console.WriteLine("[❌ HIBA]: A DLL-ben nem volt Control-ból öröklődő IEvolView!");
                return;
            }

            StatusText.Text = $"❌ Nem sikerült {MaxSelfHealAttempts} próbálkozás alatt sem hibátlan kódot generálni.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnApproveClick(object? sender, RoutedEventArgs e)
    {
        if (_pendingResponse == null) return;

        try
        {
            _gitService.Commit($"EvolKréta: Új ablak hozzáadva ({_pendingResponse.Label})");
            StatusText.Text = $"💾 '{_pendingResponse.Label}' sikeresen rögzítve a Git verziókezelőben!";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"⚠️ Git commit hiba: {ex.Message}";
        }

        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;
        AiButton.IsEnabled = true;
        PromptInput.Text = "";
        _pendingResponse = null;
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        MainContentArea.Content = new TextBlock
        {
            Text = "Fejlesztés elvetve.",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Foreground = Avalonia.Media.Brush.Parse("#7F8C8D")
        };

        _currentLoader?.UnloadAssembly();
        _currentLoader = null;

        try
        {
            _gitService.RevertToLastStable();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"⚠️ Visszaállítási hiba: {ex.Message}";
        }

        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;
        AiButton.IsEnabled = true;
        _pendingResponse = null;

        StatusText.Text = "❌ Változtatások elvetve, a rendszer az utolsó stabil állapotban van.";
    }
}
