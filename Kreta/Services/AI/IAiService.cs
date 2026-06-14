using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

public interface IAiService {
    Task<AiEvolveResponse> GenerateFeatureAsync(string userPrompt, Role userRole);
}