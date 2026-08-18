using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

/// <summary>
///     Interface of the service that transforms unstructured natural language prompts into a formal JSON specification.
/// </summary>
public interface IPromptRefinerService
{
    /// <summary>
    ///     Analyzes and normalizes the raw user request into a structured specification.
    /// </summary>
    /// <param name="rawPrompt">The raw text entered by the user.</param>
    /// <param name="role">The currently active simulated role.</param>
    /// <param name="apiKey">Gemini API key.</param>
    /// <returns>The formally specified prompt object.</returns>
    Task<FormalPromptSpecification> RefinePromptAsync(string rawPrompt, Role role, string apiKey);
}