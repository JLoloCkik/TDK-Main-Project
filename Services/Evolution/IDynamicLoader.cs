using System.Reflection;
using Avalonia.Controls;

namespace Kreta.Services.Evolution;

/// <summary>
/// A dinamikus fordítás és betöltés eredményét összefoglaló osztály.
/// </summary>
public class DynamicLoadResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Control? ViewControl { get; set; }
    public Assembly? CompiledAssembly { get; set; }
}

/// <summary>
/// Dinamikus Roslyn C# kód fordításáért és betöltéséért felelős felület.
/// </summary>
public interface IDynamicLoader
{
    DynamicLoadResult LoadViewFromCode(string sourceCode);
}