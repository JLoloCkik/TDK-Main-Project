using System;
using System.IO;
using System.Threading.Tasks;
using Kreta.Core;
using Kreta.Services.AI;
using Kreta.Services.Git;
using Kreta.Services.Security;

namespace Kreta.Services.Evolution;

/// <summary>
/// Az AI kódgenerálás, önjavítás (self-healing), lemezre mentés, törlés és Git push koordinációjáért felelős szolgáltatás.
/// </summary>
public class EvolutionService : IEvolutionService
{
    private readonly IAiService _aiService;
    private readonly IDynamicLoader _dynamicLoader;
    private readonly AstAnalyzer _astAnalyzer;
    private readonly IGitService _gitService;

    public EvolutionService()
        : this(new AiService(), new DynamicLoader(), new AstAnalyzer(), new GitService())
    {
    }

    public EvolutionService(
        IAiService aiService, 
        IDynamicLoader dynamicLoader, 
        AstAnalyzer astAnalyzer,
        IGitService gitService)
    {
        _aiService = aiService;
        _dynamicLoader = dynamicLoader;
        _astAnalyzer = astAnalyzer;
        _gitService = gitService;
    }

    public async Task<EvolveResult> EvolveAsync(string prompt, Role currentRole, int maxAttempts = 3,
        string? targetViewFilePath = null)
    {
        Console.WriteLine($"[Evolúció] Új kérés feldolgozása: '{prompt}' ({currentRole})" +
            (string.IsNullOrWhiteSpace(targetViewFilePath) ? "..." : $" [Kijelölt nézet: {Path.GetFileName(targetViewFilePath)}]..."));

        string? history = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                Console.WriteLine($"[Evolúció - Self-Healing] Újrapróbálkozás ({attempt}/{maxAttempts}) az előző fordítási hiba kijavításával...");
            }

            var aiResponse = await _aiService.GenerateFeatureAsync(prompt, currentRole, history, targetViewFilePath);

            // 1. RBAC Guardrail elutasítás: Nem mentjük lelemezként!
            if (aiResponse.Action == "REJECT")
            {
                Console.WriteLine("[Evolúció] RBAC elutasítás. Nincs fájlmentés.");
                return new EvolveResult
                {
                    IsSuccess = false,
                    IsRejectedAction = true,
                    ViewName = aiResponse.ViewName ?? "Hozzáférés Megtagadva",
                    Description = aiResponse.Description ?? "Nincs jogosultsága ehhez a művelethez.",
                    ErrorMessage = "❌ Hozzáférés megtagadva! (RBAC hiba)"
                };
            }

            // 2. AI által kért törlés kezelése
            if (aiResponse.Action == "DELETE" && !string.IsNullOrWhiteSpace(aiResponse.ViewName))
            {
                string evolDir = PathHelper.GetEvolViewsDirectory();
                var matchedFiles = Directory.GetFiles(evolDir, $"*{aiResponse.ViewName}*.cs");

                foreach (var file in matchedFiles)
                {
                    await DiscardFeatureAsync(file);
                    await _gitService.RemoveAndPushAsync(file, $"[AI Törlés] {aiResponse.ViewName} eltávolítva.");
                }

                return new EvolveResult
                {
                    IsSuccess = true,
                    IsDeletedAction = true,
                    ViewName = aiResponse.ViewName,
                    Description = "A kijelölt funkció törölve lett."
                };
            }

            if (string.IsNullOrWhiteSpace(aiResponse.SourceCode))
            {
                return new EvolveResult
                {
                    IsSuccess = false,
                    ErrorMessage = "A generált kód üres volt."
                };
            }

            var result = await EvolveFeatureAsync(
                aiResponse.ViewName ?? "Új Nézet", 
                aiResponse.Description ?? "AI által generált funkció", 
                aiResponse.SourceCode, 
                aiResponse.TestCode ?? string.Empty
            );

            if (result.IsSuccess)
            {
                return result;
            }

            history = result.ErrorMessage;
            Console.WriteLine($"[Evolúció - Fordítási Hiba az {attempt}. próbálkozásnál]: {history}");
        }

        return new EvolveResult
        {
            IsSuccess = false,
            ErrorMessage = $"Nem sikerült lefordítani a kért funkciót {maxAttempts} próbálkozás után sem. Utolsó hiba: {history}"
        };
    }

    public async Task<EvolveResult> EvolveFeatureAsync(string viewName, string description, string sourceCode, string testCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return new EvolveResult
            {
                IsSuccess = false,
                ErrorMessage = "A megadott forráskód üres."
            };
        }

        if (!_astAnalyzer.IsCodeSafe(sourceCode, out string securityViolation))
        {
            Console.WriteLine($"[Evolúció - Biztonsági Hiba] {securityViolation}");
            return new EvolveResult
            {
                IsSuccess = false,
                ErrorMessage = $"Biztonsági szabálysértés: {securityViolation}"
            };
        }

        string evolViewsDirectory = PathHelper.GetEvolViewsDirectory();
        
        string safeViewName = string.Concat(viewName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        if (string.IsNullOrWhiteSpace(safeViewName))
        {
            safeViewName = $"EvolView_{Guid.NewGuid():N}";
        }

        // Duplikáció szűrés: Töröljük a korábbi meglévő változatot az azonos témájú fájlokból, hogy ne legyenek duplikált fülek
        var basePrefix = safeViewName.Split('_')[0];
        var existingFiles = Directory.GetFiles(evolViewsDirectory, $"*{basePrefix}*.cs");
        foreach (var oldFile in existingFiles)
        {
            try
            {
                Console.WriteLine($"[Evolúció - Módosítás/Takarítás] Korábbi változat törlése: {oldFile}");
                File.Delete(oldFile);
            }
            catch
            {
                // Csendben figyelmen kívül hagyjuk
            }
        }

        string filePath = Path.Combine(evolViewsDirectory, $"{safeViewName}.cs");

        await File.WriteAllTextAsync(filePath, sourceCode);

        var loadResult = _dynamicLoader.LoadViewFromCode(sourceCode);

        if (!loadResult.IsSuccess)
        {
            Console.WriteLine($"[Evolúció - Fordítási Hiba] A kód nem fordult le. Fájl törlése: {filePath}");
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Takarítási hiba]: {ex.Message}");
            }

            return new EvolveResult
            {
                IsSuccess = false,
                ErrorMessage = $"Fordítási hiba: {loadResult.ErrorMessage}"
            };
        }

        return new EvolveResult
        {
            IsSuccess = true,
            ViewName = viewName,
            Description = description,
            LoadedControl = loadResult.ViewControl,
            CompiledAssembly = loadResult.CompiledAssembly,
            FilePath = filePath
        };
    }

    public async Task<bool> DiscardFeatureAsync(string filePath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                Console.WriteLine($"[Evolúció - Elvetés] A funkció törlése a lemezről: {filePath}");
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Elvetési hiba]: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AcceptAndPushFeatureAsync(string filePath, string viewName)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine("[Git Push Hiba] A megadott fájl nem található a lemezen.");
            return false;
        }

        Console.WriteLine($"[Evolúció - Elfogadás] A funkció elfogadva ('{viewName}'). Git push indítása...");

        try
        {
            return await _gitService.CommitAndPushAsync(
                filePath: filePath,
                commitMessage: $"Új elfogadott funkció hozzáadva/módosítva: {viewName}",
                branchName: "ai-dev"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Git Push Hiba]: {ex.Message}");
            return false;
        }
    }
}