using System.Reflection;
using Avalonia.Controls;

namespace Kreta.Services.Evolution;

/// <summary>
/// Az evolúciós kódgenerálás és betöltés eredményét tároló adatmodell.
/// </summary>
public class EvolveResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ViewName { get; set; }
    public string? Description { get; set; }
    public Control? LoadedControl { get; set; }
    public string? FilePath { get; set; }
    public Assembly? CompiledAssembly { get; set; }
}