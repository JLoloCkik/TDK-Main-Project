using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Kreta.Core;
using Kreta.Contexts;

namespace Kreta.Services.Evolution;

public class DynamicLoader : IDynamicLoader
{
    public DynamicLoadResult LoadViewFromCode(string sourceCode)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var assemblyName = $"KretaDynamic_{Guid.NewGuid():N}";

            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(UserControl).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Control).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IEvolView).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
                MetadataReference.CreateFromFile(Assembly.Load("Avalonia.Base").Location),
                MetadataReference.CreateFromFile(Assembly.Load("Avalonia.Controls").Location),
                MetadataReference.CreateFromFile(Assembly.Load("Avalonia.Layout").Location)
            };

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = string.Join("\n", emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage()));

                return new DynamicLoadResult
                {
                    IsSuccess = false,
                    ErrorMessage = errors
                };
            }

            ms.Seek(0, SeekOrigin.Begin);
            var alc = new AssemblyLoadContext(assemblyName, isCollectible: true);
            var assembly = alc.LoadFromStream(ms);

            var type = assembly.GetTypes().FirstOrDefault(t => typeof(IEvolView).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (type == null)
            {
                return new DynamicLoadResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Nem található IEvolView megvalósítás a kódban."
                };
            }

            var instance = Activator.CreateInstance(type);
            var evolView = instance as IEvolView;
            var control = evolView?.CreateView();

            return new DynamicLoadResult
            {
                IsSuccess = true,
                ViewControl = control,
                CompiledAssembly = assembly
            };
        }
        catch (Exception ex)
        {
            return new DynamicLoadResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
}