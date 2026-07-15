using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Kreta.Core;

namespace Kreta.Services.Evolution;

/// <summary>
/// Egyetlen, elszigetelt (Collectible) AssemblyLoadContext egyetlen generált
/// DLL-hez. FONTOS: egy ALC egyszer tölthető be, majd Unload() után végleg
/// használhatatlanná válik — nem szabad ugyanazt a példányt újra betölteni!
/// A hívónak (MainWindow) minden új generáláskor ÚJ DynamicLoader-t kell
/// létrehoznia, a régit pedig előtte le kell állítania.
/// </summary>
public class DynamicLoader : System.Runtime.Loader.AssemblyLoadContext, IDynamicLoader
{
    private bool _unloaded;

    public DynamicLoader() : base(isCollectible: true) { }

    public Assembly LoadAssembly(string dllPath)
    {
        if (_unloaded)
            throw new InvalidOperationException(
                "Ez az ALC már le lett állítva (Unload). Hozz létre egy új DynamicLoader példányt!");

        using var fs = File.OpenRead(dllPath);
        return LoadFromStream(fs);
    }

    public void UnloadAssembly()
    {
        if (_unloaded) return;
        _unloaded = true;

        Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public List<IEvolView> GetViewsFromAssembly(string dllPath)
    {
        var assembly = LoadAssembly(dllPath);
        var views = new List<IEvolView>();

        foreach (Type type in assembly.GetTypes())
        {
            if (typeof(IEvolView).IsAssignableFrom(type)
                && !type.IsInterface
                && !type.IsAbstract)
            {
                IEvolView? view = Activator.CreateInstance(type) as IEvolView;

                if (view != null)
                {
                    views.Add(view);
                }
            }
        }

        return views;
    }
}
