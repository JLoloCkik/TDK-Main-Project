using System.Threading.Tasks;
using Kreta.Core;

namespace Kreta.Services.AI;

/// <summary>
///     Interface for the AI code generation and context analysis service.
/// </summary>
public interface IAiService
{
    /// <summary>
    ///     Simple feature generation based on a single prompt.
    /// </summary>
    Task<string> GenerateFeatureAsync(string prompt);

    /// <summary>
    ///     Role-based and history-aware feature generation, modification, or deletion.
    /// </summary>
    /// <param name="targetViewFilePath">
    ///     Optional: the file path of the currently selected/opened view that the user
    ///     is looking at on the UI. If provided, the AI receives its ENTIRE, existing source code
    ///     and modifies it directly, instead of having to guess based on the view list.
    /// </param>
    Task<AiEvolveResponse> GenerateFeatureAsync(string prompt, Role role, string? history = null,
        string? targetViewFilePath = null);
}