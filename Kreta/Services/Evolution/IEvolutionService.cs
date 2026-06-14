using System.Threading.Tasks;

namespace Kreta.Services.Evolution;

public interface IEvolutionService {
    Task<EvolveResult> EvolveFeatureAsync(string featureName, string buttonLabel, string handlerCode, string axamlCode);
}