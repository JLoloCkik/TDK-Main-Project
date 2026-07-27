namespace Kreta.Services.AI;

/// <summary>
/// Az AI által generált válasz deszerializációs modellje.
/// </summary>
public class AiEvolveResponse
{
    /// <summary>
    /// A generált vagy módosított nézet C# osztályneve / azonosítója.
    /// </summary>
    public string? ViewName { get; set; }

    /// <summary>
    /// A funkció rövid magyar nyelvű leírása.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// A generált Avalonia/C# forráskód.
    /// </summary>
    public string? SourceCode { get; set; }

    /// <summary>
    /// Opcionális tesztkód vagy kiegészítő szkript.
    /// </summary>
    public string? TestCode { get; set; }

    /// <summary>
    /// Az AI által kért akció (pl. "CREATE", "UPDATE", "DELETE", "REJECT").
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Opcionális eseménykezelő metódus megnevezés.
    /// </summary>
    public string? HandlerMethod { get; set; }
}