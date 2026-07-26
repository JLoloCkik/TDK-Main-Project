using System;
using System.IO;

namespace Kreta.Services;

public static class PathHelper
{
    /// <summary>
    /// Intelligensen megkeresi a C# projekt gyökérmappáját (ahol a .csproj vagy a .git mappa található),
    /// megelőzve, hogy a fájlok a bin/Debug/ kimeneti mappába mentődjenek.
    /// </summary>
    public static string GetProjectRootDirectory()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDir != null)
        {
            // Keressük a .csproj fájlt vagy a .git mappát a projekt gyökerének azonosításához
            var csprojFiles = currentDir.GetFiles("*.csproj");
            if (csprojFiles.Length > 0 || Directory.Exists(Path.Combine(currentDir.FullName, ".git")))
            {
                return currentDir.FullName;
            }
            currentDir = currentDir.Parent;
        }

        // Tartalék opció, ha a projektgyökér nem azonosítható
        return AppContext.BaseDirectory;
    }

    /// <summary>
    /// Visszaadja a projekt forráskódján belüli EvolViews mappa elérési útját.
    /// </summary>
    public static string GetEvolViewsDirectory()
    {
        var projectRoot = GetProjectRootDirectory();
        var evolViewsDir = Path.Combine(projectRoot, "EvolViews");

        if (!Directory.Exists(evolViewsDir))
        {
            Directory.CreateDirectory(evolViewsDir);
        }

        return evolViewsDir;
    }
}