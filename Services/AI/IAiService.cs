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
    /// <param name="targetViewFilePath">
    /// Opcionális: annak a jelenleg kijelölt/megnyitott nézetnek a fájlútja, amelyet a felhasználó
    /// a felületen épp néz. Ha meg van adva, az AI ennek a TELJES, meglévő forráskódját kapja meg
    /// és azt módosítja közvetlenül, ahelyett hogy a nézet-lista alapján kellene kitalálnia.
    /// </param>
    Task<AiEvolveResponse> GenerateFeatureAsync(string prompt, Role role, string? history = null,
        string? targetViewFilePath = null);
}