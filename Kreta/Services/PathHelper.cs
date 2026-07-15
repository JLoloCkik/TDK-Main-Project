using System;
using System.IO;
using System.Linq;

namespace Kreta.Services;

/// <summary>
/// Segédosztály, ami megbízhatóan megtalálja a projekt gyökérkönyvtárát
/// (ahol a .csproj van), függetlenül attól, hogy az alkalmazást
/// "dotnet run"-nal, az IDE-ből, vagy a lefordított .exe dupla kattintásával
/// indítjuk. Enélkül a relatív útvonalak (.env, kreta.db) az aktuális
/// munkakönyvtártól (CWD) függtek, ami induláskor eltérő lehet.
/// </summary>
public static class PathHelper
{
    private static string? _cachedRoot;

    public static string? FindProjectRoot()
    {
        if (_cachedRoot != null) return _cachedRoot;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
        {
            dir = dir.Parent;
        }

        _cachedRoot = dir?.FullName;
        return _cachedRoot;
    }
}
