using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Kreta.Services.Evolution;

public class EvolutionService : IEvolutionService
{
    public async Task<EvolveResult> EvolveFeatureAsync(string viewName, string description, string sourceCode, string testCode)
    {
        return await Task.Run(() =>
        {
            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var assemblyName = $"EvolAssembly_{Guid.NewGuid():N}";

                var references = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => MetadataReference.CreateFromFile(a.Location))
                    .Cast<MetadataReference>()
                    .ToList();

                var compilation = CSharpCompilation.Create(
                    assemblyName,
                    new[] { syntaxTree },
                    references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using var ms = new MemoryStream();
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    var errors = string.Join("\n", failures.Select(f => $"{f.Id}: {f.GetMessage()}"));
                    return new EvolveResult { IsSuccess = false, ErrorMessage = errors };
                }

                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());
                return new EvolveResult { IsSuccess = true, CompiledAssembly = assembly };
            }
            catch (Exception ex)
            {
                return new EvolveResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        });
    }
}