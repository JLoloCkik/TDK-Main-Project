using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.Evolution;

public interface IEvolutionService
{
    /// <param name="targetViewFilePath">
    /// Opcionális: a felhasználó által épp kijelölt/megnyitott meglévő nézet fájlútja. Ha meg van adva,
    /// a kérés KÖZVETLENÜL ezt a nézetet módosítja (az AI a teljes meglévő forráskódját megkapja),
    /// ahelyett hogy a rendszernek ki kellene találnia, melyik nézetről van szó.
    /// </param>
    Task<EvolveResult> EvolveAsync(string prompt, Role currentRole, int maxAttempts = 3, string? targetViewFilePath = null);
    Task<EvolveResult> EvolveFeatureAsync(string viewName, string description, string sourceCode, string testCode);
    Task<bool> AcceptAndPushFeatureAsync(string filePath, string viewName);
    Task<bool> DiscardFeatureAsync(string filePath);
}