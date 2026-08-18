using System;
using System.IO;
using System.Threading.Tasks;
using Kreta.Core;
using Kreta.Services.AI;
using Kreta.Services.Git;
using Kreta.Services.Security;

namespace Kreta.Services.Evolution;

/// <summary>
///     Service responsible for coordinating AI code generation, self-healing, saving to disk, deletion, and Git push.
/// </summary>
public class EvolutionService : IEvolutionService
{
    private readonly IAiService _aiService;
    private readonly AstAnalyzer _astAnalyzer;
    private readonly IDynamicLoader _dynamicLoader;
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
        Console.WriteLine($"[Evolution] Processing new request: '{prompt}' ({currentRole})" +
                          (string.IsNullOrWhiteSpace(targetViewFilePath)
                              ? "..."
                              : $" [Selected view: {Path.GetFileName(targetViewFilePath)}]..."));

        string? history = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
                Console.WriteLine(
                    $"[Evolution - Self-Healing] Retry ({attempt}/{maxAttempts}) fixing previous compilation error...");

            var aiResponse = await _aiService.GenerateFeatureAsync(prompt, currentRole, history, targetViewFilePath);

            // 1. RBAC Guardrail rejection: Do not save to disk!
            if (aiResponse.Action == "REJECT")
            {
                Console.WriteLine("[Evolution] RBAC rejection. No file saved.");
                return new EvolveResult
                {
                    IsSuccess = false,
                    IsRejectedAction = true,
                    ViewName = aiResponse.ViewName ?? "Access Denied",
                    Description = aiResponse.Description ?? "You do not have permission for this operation.",
                    ErrorMessage = "Access denied! (RBAC error)"
                };
            }

            // 2. Handle deletion requested by AI
            if (aiResponse.Action == "DELETE" && !string.IsNullOrWhiteSpace(aiResponse.ViewName))
            {
                var evolDir = PathHelper.GetEvolViewsDirectory();
                var matchedFiles = Directory.GetFiles(evolDir, $"*{aiResponse.ViewName}*.cs");

                foreach (var file in matchedFiles)
                {
                    await DiscardFeatureAsync(file);
                    await _gitService.RemoveAndPushAsync(file, $"[AI Deletion] {aiResponse.ViewName} removed.");
                }

                return new EvolveResult
                {
                    IsSuccess = true,
                    IsDeletedAction = true,
                    ViewName = aiResponse.ViewName,
                    Description = "The selected feature was deleted."
                };
            }

            if (string.IsNullOrWhiteSpace(aiResponse.SourceCode))
                return new EvolveResult
                {
                    IsSuccess = false,
                    ErrorMessage = "The generated code was empty."
                };

            var result = await EvolveFeatureAsync(
                aiResponse.ViewName ?? "New View",
                aiResponse.Description ?? "AI generated feature",
                aiResponse.SourceCode,
                aiResponse.TestCode ?? string.Empty
            );

            if (result.IsSuccess) return result;

            history = result.ErrorMessage;
            Console.WriteLine($"[Evolution - Compilation Error on attempt #{attempt}]: {history}");
        }

        return new EvolveResult
        {
            IsSuccess = false,
            ErrorMessage = $"Failed to compile requested feature after {maxAttempts} attempts. Last error: {history}"
        };
    }

    public async Task<EvolveResult> EvolveFeatureAsync(string viewName, string description, string sourceCode,
        string testCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            return new EvolveResult
            {
                IsSuccess = false,
                ErrorMessage = "The provided source code is empty."
            };

        if (!_astAnalyzer.IsCodeSafe(sourceCode, out var securityViolation))
        {
            Console.WriteLine($"[Evolution - Security Error] {securityViolation}");
            return new EvolveResult
            {
                IsSuccess = false,
                ErrorMessage = $"Security violation: {securityViolation}"
            };
        }

        var evolViewsDirectory = PathHelper.GetEvolViewsDirectory();

        var safeViewName = string.Concat(viewName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        if (string.IsNullOrWhiteSpace(safeViewName)) safeViewName = $"EvolView_{Guid.NewGuid():N}";

        // Deduplication filtering: Delete previous existing version of files with the same topic to avoid duplicate tabs
        var basePrefix = safeViewName.Split('_')[0];
        var existingFiles = Directory.GetFiles(evolViewsDirectory, $"*{basePrefix}*.cs");
        foreach (var oldFile in existingFiles)
            try
            {
                Console.WriteLine($"[Evolution - Modify/Cleanup] Deleting previous version: {oldFile}");
                File.Delete(oldFile);
            }
            catch
            {
                // Silently ignore
            }

        var filePath = Path.Combine(evolViewsDirectory, $"{safeViewName}.cs");

        await File.WriteAllTextAsync(filePath, sourceCode);

        var loadResult = _dynamicLoader.LoadViewFromCode(sourceCode);

        if (!loadResult.IsSuccess)
        {
            Console.WriteLine($"[Evolution - Compilation Error] Code failed to compile. Deleting file: {filePath}");
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cleanup error]: {ex.Message}");
            }

            return new EvolveResult
            {
                IsSuccess = false,
                ErrorMessage = $"Compilation error: {loadResult.ErrorMessage}"
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
                Console.WriteLine($"[Evolution - Discard] Deleting feature from disk: {filePath}");
                File.Delete(filePath);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Discard error]: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AcceptAndPushFeatureAsync(string filePath, string viewName)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine("[Git Push Error] Specified file not found on disk.");
            return false;
        }

        Console.WriteLine($"[Evolution - Accept] Feature accepted ('{viewName}'). Starting Git push...");

        try
        {
            return await _gitService.CommitAndPushAsync(
                filePath,
                $"New accepted feature added/modified: {viewName}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Git Push Error]: {ex.Message}");
            return false;
        }
    }
}