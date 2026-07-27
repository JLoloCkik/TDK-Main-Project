using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.Evolution;

public interface IEvolutionService
{
    Task<EvolveResult> EvolveAsync(string prompt, Role currentRole, int maxAttempts = 3);
    Task<EvolveResult> EvolveFeatureAsync(string viewName, string description, string sourceCode, string testCode);
    Task<bool> AcceptAndPushFeatureAsync(string filePath, string viewName);
    Task<bool> DiscardFeatureAsync(string filePath);
}