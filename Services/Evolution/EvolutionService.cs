using System;
using System.IO;
using System.Threading.Tasks;
using Kreta.Core;
using Kreta.Services.AI;
using Kreta.Services.Git;
using Kreta.Services.Security;

namespace Kreta.Services.Evolution;

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

    public EvolutionService(IAiService aiService)
        : this(aiService, new DynamicLoader(), new AstAnalyzer(), new GitService())
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

    public async Task<EvolveResult> EvolveAsync(string prompt, Role currentRole)
    {
        Console.WriteLine($"[Evolúció] Új funkció generálása: '{prompt}' (Szerepkör: {currentRole})...");

        var aiResponse = await _aiService.GenerateFeatureAsync(prompt, currentRole);

        if (string.IsNullOrWhiteSpace(aiResponse.SourceCode))
        {
            return new EvolveResult
            {
                IsSuccess = false,
                ErrorMessage = "A generált kód üres volt."
            };
        }

        return await EvolveFeatureAsync(
            aiResponse.ViewName ?? "Új Nézet", 
            aiResponse.Description ?? "AI által generált funkció", 
            aiResponse.SourceCode, 
            aiResponse.TestCode ?? string.Empty
        );
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

        string filePath = Path.Combine(evolViewsDirectory, $"{safeViewName}.cs");

        await File.WriteAllTextAsync(filePath, sourceCode);
        Console.WriteLine($"[Evolúció] Forráskód elmentve a projektbe: {filePath}");

        var loadResult = _dynamicLoader.LoadViewFromCode(sourceCode);

        if (!loadResult.IsSuccess)
        {
            return new EvolveResult
            {
                IsSuccess = false,
                ErrorMessage = $"Fordítási hiba: {loadResult.ErrorMessage}"
            };
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _gitService.CommitAndPushAsync(
                    filePath: filePath,
                    commitMessage: $"Új funkció hozzáadva/módosítva: {viewName}",
                    branchName: "ai-dev"
                );
            }
            catch (Exception gitEx)
            {
                Console.WriteLine($"[Git Háttér-szinkronizáció Hiba]: {gitEx.Message}");
            }
        });

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
}