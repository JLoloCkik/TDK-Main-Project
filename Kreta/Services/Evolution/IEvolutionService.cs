using System.Threading.Tasks;

namespace Kreta.Services.Evolution;

public interface IEvolutionService
{
    Task<EvolveResult> EvolveFeatureAsync(string viewName, string description, string sourceCode, string testCode);
}