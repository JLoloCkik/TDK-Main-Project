using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.Evolution;

public interface IEvolutionService
{
    /// <summary>
    /// Generál és betölt egy új funkciót prompt és szerepkör alapján.
    /// </summary>
    Task<EvolveResult> EvolveAsync(string prompt, Role currentRole);

    /// <summary>
    /// Elment, ellenőriz, fordít és szinkronizál egy meglévő forráskódot.
    /// </summary>
    Task<EvolveResult> EvolveFeatureAsync(string viewName, string description, string sourceCode, string testCode);
}