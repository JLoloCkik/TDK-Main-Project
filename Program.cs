using System;
using System.Threading.Tasks;
using Avalonia;
using Kreta.Services.AI;
using Kreta.Services.Database;
using Kreta.Services.Evolution;
using Kreta.Services.Security;
using Kreta.Services.Testing;

namespace Kreta;

internal class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        // dotnet run -- --benchmark
        if (args.Contains("--benchmark"))
        {
            Console.WriteLine("[CLI] Starting automated benchmark session in CLI mode...");

            try
            {
                var db = new KretaDbContext();
                db.SeedData();

                var evolutionService = new EvolutionService();
                var astAnalyzer = new AstAnalyzer();
                var conformanceVerifier = new PromptConformanceVerifier();

                var runner = new AutomatedTestRunner(evolutionService, astAnalyzer, conformanceVerifier);

                await runner.RunAllTestsAsync(
                    true,
                    1500,
                    60000,
                    (current, total, result) =>
                    {
                        var status = result.IsSuccess ? "[OK]" : "[ERROR]";
                        Console.WriteLine($"[{current}/{total}] {result.Prompt} -> {status}");
                    }
                );

                Console.WriteLine("[SUCCESS] Benchmark completed, output files updated!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error during test run: {ex.Message}");
            }

            return; // Exit, do not start the GUI
        }

        // Normal Avalonia GUI start
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