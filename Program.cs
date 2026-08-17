using Avalonia;
using System;
using System.Linq;
using System.Threading.Tasks;
using Kreta.Services.AI;
using Kreta.Services.Database;
using Kreta.Services.Evolution;
using Kreta.Services.Security;
using Kreta.Services.Testing;

namespace Kreta;

class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        // dotnet run -- --benchmark
        if (args.Contains("--benchmark"))
        {
            Console.WriteLine("🚀 Automatizált mérési munkamenet indítása CLI módban...");

            try
            {
                var db = new KretaDbContext();
                db.SeedData();

                var evolutionService = new EvolutionService();
                var astAnalyzer = new AstAnalyzer();
                var conformanceVerifier = new PromptConformanceVerifier();

                var runner = new AutomatedTestRunner(evolutionService, astAnalyzer, conformanceVerifier);

                await runner.RunAllTestsAsync(
                    resumeFromCheckpoint: true,
                    delayBetweenTestsMs: 1500,
                    perTestTimeoutMs: 60000,
                    progressCallback: (current, total, result) =>
                    {
                        string status = result.IsSuccess ? "🟢 OK" : "🔴 HIBA";
                        Console.WriteLine($"[{current}/{total}] {result.Prompt} -> {status}");
                    }
                );

                Console.WriteLine("🟢 A mérés befejeződött, a kimeneti fájlok frissültek!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Hiba a tesztfuttatás során: {ex.Message}");
            }

            return; // Kilépés, nem indítjuk el a GUI-t
        }

        // Normál Avalonia GUI indítás
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}