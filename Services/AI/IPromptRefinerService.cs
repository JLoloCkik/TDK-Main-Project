using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

/// <summary>
/// A kötetlen természetes nyelvi promptokat formális JSON specifikációvá alakító szolgáltatás interfésze.
/// </summary>
public interface IPromptRefinerService
{
    /// <summary>
    /// Elemzi és normalizálja a nyers felhasználói kérést egy strukturált specifikációvá.
    /// </summary>
    /// <param name="rawPrompt">A felhasználó által beírt nyers szöveg.</param>
    /// <param name="role">A jelenleg aktív szimulált szerepkör.</param>
    /// <param name="apiKey">Gemini API kulcs.</param>
    /// <returns>A formálisan specifikált prompt objektum.</returns>
    Task<FormalPromptSpecification> RefinePromptAsync(string rawPrompt, Role role, string apiKey);
}