using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Kreta.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Kreta.Services.Evolution;

public class EvolutionService : IEvolutionService {
    public async Task<EvolveResult> EvolveFeatureAsync(string featureName, string buttonLabel, string handlerCode,
        string axamlCode) {
        string dllPath = Path.Combine(AppContext.BaseDirectory, "Dynamic", "DynamicFeatures.dll");

        Directory.CreateDirectory(Path.GetDirectoryName(dllPath)!);

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(handlerCode);

        var references = new List<MetadataReference> {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEvolView).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("Avalonia.Controls").Location),
            MetadataReference.CreateFromFile(Assembly.Load("Avalonia.Layout").Location)
        };

        var compilation = CSharpCompilation.Create(
            "DynamicFeatures",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var dllStream = new FileStream(dllPath, FileMode.Create, FileAccess.Write);
        var emitResult = compilation.Emit(dllStream);

        if (emitResult.Success) {
            return new EvolveResult(true, "A háttér-DLL sikeresen lefordítva!");
        }

        var failures = emitResult.Diagnostics.Where(diagnostic =>
            diagnostic.IsWarningAsError ||
            diagnostic.Severity == DiagnosticSeverity.Error);

        string errorMsg = string.Join("\n", failures.Select(f => f.GetMessage()));
        return new EvolveResult(false, $"Fordítási hiba:\n{errorMsg}");
    }
}