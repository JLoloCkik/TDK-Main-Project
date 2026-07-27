using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

/// <summary>
/// Az AI kódgeneráló és kontextus-elemző szolgáltatás interfésze.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Egyszerű funkciógenerálás egyetlen prompt alapján.
    /// </summary>
    Task<string> GenerateFeatureAsync(string prompt);

    /// <summary>
    /// Szerepkör-alapú és előzmény-tudatos funkciógenerálás, módosítás vagy törlés.
    /// </summary>
    Task<AiEvolveResponse> GenerateFeatureAsync(string prompt, Role role, string? history = null);
}