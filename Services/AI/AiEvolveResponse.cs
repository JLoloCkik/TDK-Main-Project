namespace Kreta.Services.AI;

/// <summary>
///     Deserialization model of the AI-generated response.
/// </summary>
public class AiEvolveResponse
{
    /// <summary>
    ///     The C# class name / identifier of the generated or modified view.
    /// </summary>
    public string? ViewName { get; set; }

    /// <summary>
    ///     A short description of the feature in Hungarian.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The generated Avalonia/C# source code.
    /// </summary>
    public string? SourceCode { get; set; }

    /// <summary>
    ///     Optional test code or additional script.
    /// </summary>
    public string? TestCode { get; set; }

    /// <summary>
    ///     The action requested by the AI (e.g. "CREATE", "UPDATE", "DELETE", "REJECT").
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    ///     Optional event handler method name.
    /// </summary>
    public string? HandlerMethod { get; set; }
}