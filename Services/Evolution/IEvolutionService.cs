using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.Evolution;

/// <summary>
/// Az evolúciós ciklust (kódgenerálás, fordítás, tesztelés és felülbírálható Git push) vezérlő felület.
/// </summary>
public interface IEvolutionService
{
    /// <summary>
    /// Generál és betölt egy új funkciót prompt és szerepkör alapján.
    /// </summary>
    Task<EvolveResult> EvolveAsync(string prompt, Role currentRole);

    /// <summary>
    /// Elment, ellenőriz, fordít és betölt egy forráskódot (push nélkül).
    /// </summary>
    Task<EvolveResult> EvolveFeatureAsync(string viewName, string description, string sourceCode, string testCode);

    /// <summary>
    /// Feltölti a kijelölt funkció forráskódját a GitHub tárhelyre (ai-dev ág).
    /// Kizárólag akkor hívandó, ha a felhasználó kifejezetten elfogadta a funkciót!
    /// </summary>
    Task<bool> AcceptAndPushFeatureAsync(string filePath, string viewName);
}