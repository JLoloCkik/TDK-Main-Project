using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

public interface IAiService
{
    /// <param name="userPrompt">A felhasználó természetes nyelvű kérése.</param>
    /// <param name="userRole">Az aktuális szerepkör (RBAC-ellenőrzéshez).</param>
    /// <param name="previousError">
    /// Ha egy korábbi próbálkozás fordítási hibát vagy biztonsági elutasítást kapott,
    /// ide kerül a hibaüzenet, hogy az AI ki tudja javítani (Self-Healing Loop).
    /// </param>
    Task<AiEvolveResponse> GenerateFeatureAsync(string userPrompt, Role userRole, string? previousError = null);
}
