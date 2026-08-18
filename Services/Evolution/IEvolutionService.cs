using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.Evolution;

public interface IEvolutionService
{
    /// <param name="targetViewFilePath">
    ///     Optional: file path of the existing view selected/opened by the user. If provided,
    ///     the request DIRECTLY modifies this view (the AI receives its full existing source code)
    ///     instead of the system having to guess which view it is.
    /// </param>
    Task<EvolveResult> EvolveAsync(string prompt, Role currentRole, int maxAttempts = 3,
        string? targetViewFilePath = null);

    Task<EvolveResult> EvolveFeatureAsync(string viewName, string description, string sourceCode, string testCode);
    Task<bool> AcceptAndPushFeatureAsync(string filePath, string viewName);
    Task<bool> DiscardFeatureAsync(string filePath);
}