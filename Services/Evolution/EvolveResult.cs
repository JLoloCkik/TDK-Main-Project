using System.Reflection;

namespace Kreta.Services.Evolution;

public class EvolveResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Assembly? CompiledAssembly { get; set; }

    // Alapértelmezett paraméter nélküli konstruktor az objektum-inicializáláshoz
    public EvolveResult()
    {
    }

    // Konstruktor a (bool, string) szignatúrához a hibák elkerülésére
    public EvolveResult(bool isSuccess, string errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    // Konstruktor az assembly közvetlen átadásához
    public EvolveResult(bool isSuccess, Assembly? compiledAssembly, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        CompiledAssembly = compiledAssembly;
        ErrorMessage = errorMessage;
    }
}