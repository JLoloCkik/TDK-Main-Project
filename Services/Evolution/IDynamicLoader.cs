using System.Reflection;
using Avalonia.Controls;

namespace Kreta.Services.Evolution;

public class DynamicLoadResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Control? ViewControl { get; set; }
    public Assembly? CompiledAssembly { get; set; }
}

public interface IDynamicLoader
{
    DynamicLoadResult LoadViewFromCode(string sourceCode);
}