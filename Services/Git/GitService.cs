using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Kreta.Services.Git;

public class GitService : IGitService
{
    private readonly string _workingDirectory;

    public GitService()
    {
        _workingDirectory = PathHelper.GetProjectRootDirectory();
    }

    /// <summary>
    /// Hozzáadja a módosított fájlt, commitolja, és feltölti a GitHub-ra az ai-dev ágra.
    /// </summary>
    public async Task<bool> CommitAndPushAsync(string filePath, string commitMessage, string branchName = "ai-dev")
    {
        try
        {
            Console.WriteLine($"[Git] Automatikus mentés indítása a GitHub '{branchName}' ágra...");

            if (!Directory.Exists(Path.Combine(_workingDirectory, ".git")))
            {
                Console.WriteLine("[Git Hiba] A projekt gyökerében nem található .git tárhely.");
                return false;
            }

            // 1. Átváltás az ai-dev ágra (vagy létrehozás, ha nem létezik)
            await RunGitCommandAsync($"checkout -B {branchName}");

            // 2. Fájl stégelése (git add)
            var relativePath = Path.GetRelativePath(_workingDirectory, filePath);
            await RunGitCommandAsync($"add \"{relativePath}\"");

            // 3. Commit készítése
            var sanitizedMessage = commitMessage.Replace("\"", "'");
            var commitResult = await RunGitCommandAsync($"commit -m \"[AI-Evolúció] {sanitizedMessage}\"");

            if (commitResult.Contains("nothing to commit"))
            {
                Console.WriteLine("[Git] Nincs új változtatás a commit-hoz.");
                return true;
            }

            // 4. Push az ai-dev ágra
            Console.WriteLine($"[Git] Változtatások feltöltése a távoli tárhelyre (origin {branchName})...");
            await RunGitCommandAsync($"push -u origin {branchName}");

            Console.WriteLine($"[Git Siker] A(z) {Path.GetFileName(filePath)} fájl sikeresen feltöltve a GitHub 'ai-dev' ágára!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Git Hiba] Hiba történt a Git szinkronizáció során: {ex.Message}");
            return false;
        }
    }

    private async Task<string> RunGitCommandAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new Exception("Nem sikerült elindítani a Git folyamatot.");
        }

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output + "\n" + error;
    }
}