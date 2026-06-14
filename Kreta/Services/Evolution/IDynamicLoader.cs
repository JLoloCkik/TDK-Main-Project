using System.Collections.Generic;
using System.Reflection;
using Kreta.Core;

namespace Kreta.Services.Evolution;

public interface IDynamicLoader {
    Assembly LoadAssembly(string dllPath);
    void UnloadAssembly();
    List<IEvolView> GetViewsFromAssembly(string dllPath);
}