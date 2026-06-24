using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kreta.Core;
using Kreta.Services.AI;
using Kreta.Services.Evolution;
using Kreta.Services.Git;
using Kreta.Services.Security;

namespace Kreta;

public partial class MainWindow : Window {
    private AiEvolveResponse? _pendingResponse;
    
    private readonly IDynamicLoader _loader;
    private readonly IGitService _gitService;
    private readonly IEvolutionService _evolutionService;
    private readonly IAiService _aiService;
    private readonly AstAnalyzer _astAnalyzer;

    public MainWindow() {
        InitializeComponent();
        
        _loader = new DynamicLoader();
        _gitService = new GitService();
        _evolutionService = new EvolutionService();
        _aiService = new AiService();
        _astAnalyzer = new AstAnalyzer();
        
        AiButton.Click += OnAiClick;
        ApproveButton.Click += OnApproveClick;
        DiscardButton.Click += OnDiscardClick;
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e) {
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
        
        StatusText.Text = "Változtatások elvetve, fájlrendszer visszaállítva az utolsó stabil állapotra.";
    }

    private void OnApproveClick(object? sender, RoutedEventArgs e) {
        if (_pendingResponse == null) return;
        
        _gitService.Commit($"EvolKréta: Új ablak hozzáadva ({_pendingResponse.Label})");
        
        ApproveButton.IsVisible = false;
        DiscardButton.IsVisible = false;
        AiButton.IsEnabled = true;
        PromptInput.Text = "";
        
        StatusText.Text = $"'{_pendingResponse.Label}' sikeresen rögzítve a Git verziókezelőben!";
        _pendingResponse = null;
    }

    private async void OnAiClick(object? sender, RoutedEventArgs e) {
        AiButton.IsEnabled = false;
        StatusText.Text = "AI kód generálása...";
        
        string dllPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Dynamic", "DynamicFeatures.dll");

        try {
            var aiResponse = await _aiService.GenerateFeatureAsync(PromptInput.Text, Role.Teacher);
            
            _pendingResponse = aiResponse; 
            
            if (!_astAnalyzer.IsSafe(aiResponse.HandlerMethod))
            {
                StatusText.Text = "Biztonsági riasztás: Tiltott kód detektálva!";
                AiButton.IsEnabled = true;
                return;
            }
            
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
            
            var views = _loader.GetViewsFromAssembly(dllPath);
            var loadedView = views.FirstOrDefault();

            if (loadedView != null) {
                MainContentArea.Content = loadedView;
                ApproveButton.IsVisible = true; 
                DiscardButton.IsVisible = true;
                StatusText.Text = "RAM Preview aktív. Kérlek hagyd jóvá vagy vesd el!";
            }
        }
        catch (System.Exception ex) {
            StatusText.Text = $"Hiba: {ex.Message}";
            AiButton.IsEnabled = true;
        }
    }
}