using System;
using System.IO;

namespace Kreta.Services;

public static class PathHelper
{
    public static string GetProjectRootDirectory()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDir != null)
        {
            var csprojFiles = currentDir.GetFiles("*.csproj");
            if (csprojFiles.Length > 0 || Directory.Exists(Path.Combine(currentDir.FullName, ".git")))
            {
                return currentDir.FullName;
            }
            currentDir = currentDir.Parent;
        }

        return AppContext.BaseDirectory;
    }

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