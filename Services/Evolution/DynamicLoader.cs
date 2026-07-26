using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Controls;
using Avalonia.Layout;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Kreta.Core;
using Kreta.Contexts;
using Kreta.Services.Database;

namespace Kreta.Services.Evolution;

public class DynamicLoader : IDynamicLoader
{
    public DynamicLoadResult LoadViewFromCode(string sourceCode)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var assemblyName = $"KretaDynamic_{Guid.NewGuid():N}";

            var assembliesToRef = new HashSet<Assembly>
            {
                typeof(object).Assembly,
                typeof(Console).Assembly,
                typeof(Enumerable).Assembly,
                typeof(UserControl).Assembly,
                typeof(Control).Assembly,
                typeof(HorizontalAlignment).Assembly,
                typeof(Avalonia.Media.Brushes).Assembly,
                typeof(IEvolView).Assembly,
                typeof(IStudentContext).Assembly,
                typeof(ITeacherContext).Assembly,
                typeof(IDirectorContext).Assembly,
                typeof(KretaDbContext).Assembly
            };

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.IsDynamic && !string.IsNullOrWhiteSpace(asm.Location))
                {
                    assembliesToRef.Add(asm);
                }
            }

            var references = assembliesToRef
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location) && File.Exists(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

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

            object? instance = null;

            // 1. Megpróbáljuk a paraméter nélküli konstruktort
            var defaultCtor = type.GetConstructor(Type.EmptyTypes);
            if (defaultCtor != null)
            {
                instance = Activator.CreateInstance(type);
            }
            else
            {
                // 2. Ha nincs paraméter nélküli, kiszolgáljuk a kontextusfüggő konstruktort
                var ctors = type.GetConstructors();
                if (ctors.Length > 0)
                {
                    var ctor = ctors[0];
                    var parameters = ctor.GetParameters();
                    var args = new object?[parameters.Length];

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        
                        if (paramType == typeof(IStudentContext))
                        {
                            args[i] = new SqliteStudentContext(new KretaDbContext(), 1);
                        }
                        else if (paramType == typeof(ITeacherContext))
                        {
                            args[i] = new SqliteTeacherContext(new KretaDbContext());
                        }
                        else if (paramType == typeof(IDirectorContext))
                        {
                            args[i] = new SqliteDirectorContext(new KretaDbContext());
                        }
                        else
                        {
                            args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                        }
                    }

                    instance = ctor.Invoke(args);
                }
            }

            if (instance == null)
            {
                return new DynamicLoadResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Nem sikerült példányosítani a generált osztályt."
                };
            }

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