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
        
        string fullCodeWithUsings =
            $"global using Kreta.Core;\nglobal using Kreta.Contexts;\nglobal using Avalonia.Controls;\nglobal using System.Linq;\nglobal using System.Collections.Generic;\n{handlerCode}";

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(fullCodeWithUsings);
        
        var references = new List<MetadataReference> {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEvolView).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq.Expressions").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.ObjectModel").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.ComponentModel").Location),
            MetadataReference.CreateFromFile(Assembly.Load("Avalonia.Base").Location), 
            MetadataReference.CreateFromFile(Assembly.Load("Avalonia.Markup.Xaml").Location), 
            MetadataReference.CreateFromFile(Assembly.Load("Avalonia.Controls").Location),
            MetadataReference.CreateFromFile(typeof(Avalonia.Layout.HorizontalAlignment).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "DynamicFeatures",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Rövid újrapróbálkozás, ha az előző generált DLL fájlzárolása még nem
        // szűnt meg (a Collectible ALC felszabadítása nem feltétlenül azonnali).
        const int maxAttempts = 5;
        Microsoft.CodeAnalysis.Emit.EmitResult? emitResult = null;
        Exception? lastIoException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var dllStream = new FileStream(dllPath, FileMode.Create, FileAccess.Write);
                emitResult = compilation.Emit(dllStream);
                lastIoException = null;
                break;
            }
            catch (IOException ex) when (attempt < maxAttempts)
            {
                lastIoException = ex;
                await Task.Delay(150 * attempt);
            }
        }

        if (emitResult == null)
        {
            return new EvolveResult(false, $"A fájl zárolva volt {maxAttempts} próbálkozás után is: {lastIoException?.Message}");
        }

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