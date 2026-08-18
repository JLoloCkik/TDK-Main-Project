using System.Reflection;
using Avalonia.Controls;

namespace Kreta.Services.Evolution;

/// <summary>
///     Model storing the result of evolutionary code generation, modification, deletion, and loading.
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
    public bool IsDeletedAction { get; set; }
    public bool IsRejectedAction { get; set; }
}