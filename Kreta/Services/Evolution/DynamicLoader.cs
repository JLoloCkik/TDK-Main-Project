using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Kreta.Core;

namespace Kreta.Services.Evolution;

public class DynamicLoader : System.Runtime.Loader.AssemblyLoadContext, IDynamicLoader {
    public DynamicLoader() : base(isCollectible: true) { }

    public Assembly LoadAssembly(string dllPath) {
        using var fs = File.OpenRead(dllPath);
        return LoadFromStream(fs);
    }


    public void UnloadAssembly() {
        Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public List<IEvolView> GetViewsFromAssembly(string dllPath) {
        var assembly = LoadAssembly(dllPath);
        var views = new List<IEvolView>();

        foreach (Type type in assembly.GetTypes()) {
            if (typeof(IEvolView).IsAssignableFrom(type)
                && !type.IsInterface
                && !type.IsAbstract) {
                IEvolView? view = Activator.CreateInstance(type) as IEvolView;

                if (view != null) {
                    views.Add(view);
                }
            }
        }

        return views;
    }
}