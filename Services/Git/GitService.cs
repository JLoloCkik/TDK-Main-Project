using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Kreta.Services.Git;

/// <summary>
/// A Git parancsok automatikus futtatásáért és a GitHub 'ai-dev' ágára történő push-olásért felelős szolgáltatás.
/// </summary>
public class GitService : IGitService
{
    private readonly string _workingDirectory;

    public GitService()
    {
        _workingDirectory = PathHelper.GetProjectRootDirectory();
    }

    /// <summary>
    /// Hozzáadja a módosított vagy új C# fájlt, commitolja és feltölti a GitHub 'ai-dev' ágra.
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

            await RunGitCommandAsync($"checkout -B {branchName}");

            var relativePath = Path.GetRelativePath(_workingDirectory, filePath);
            await RunGitCommandAsync($"add \"{relativePath}\"");

            var sanitizedMessage = commitMessage.Replace("\"", "'");
            var commitResult = await RunGitCommandAsync($"commit -m \"[AI-Evolúció] {sanitizedMessage}\"");

            if (commitResult.Contains("nothing to commit"))
            {
                Console.WriteLine("[Git] Nincs új változtatás a commit-hoz.");
                return true;
            }

            Console.WriteLine($"[Git] Változtatások feltöltése a távoli tárhelyre (origin {branchName})...");
            await RunGitCommandAsync($"push -u origin {branchName}");

            Console.WriteLine($"[Git Siker] A(z) {Path.GetFileName(filePath)} fájl sikeresen feltöltve a GitHub '{branchName}' ágára!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Git Hiba] Hiba történt a Git szinkronizáció során: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Eltávolítja a törölt C# fájlt a Git tárhelyből és elküldi a törlési commitot a remote ágra.
    /// </summary>
    public async Task<bool> RemoveAndPushAsync(string filePath, string commitMessage, string branchName = "ai-dev")
    {
        try
        {
            Console.WriteLine($"[Git] Fájl törlésének szinkronizálása a GitHub '{branchName}' ágra...");

            if (!Directory.Exists(Path.Combine(_workingDirectory, ".git")))
            {
                return false;
            }

            await RunGitCommandAsync($"checkout -B {branchName}");

            var relativePath = Path.GetRelativePath(_workingDirectory, filePath);
            await RunGitCommandAsync($"rm \"{relativePath}\" --ignore-unmatch");

            var sanitizedMessage = commitMessage.Replace("\"", "'");
            await RunGitCommandAsync($"commit -m \"[AI-Evolúció - Törlés] {sanitizedMessage}\"");
            await RunGitCommandAsync($"push -u origin {branchName}");

            Console.WriteLine($"[Git Siker] A(z) {Path.GetFileName(filePath)} fájl törölve a GitHub '{branchName}' ágáról!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Git Törlési Hiba]: {ex.Message}");
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

        var result = output + "\n" + error;
        if (process.ExitCode != 0 && !result.Contains("nothing to commit"))
        {
            Console.WriteLine($"[Git Parancs Hiba ({arguments})]:\n{result}");
        }

        return result;
    }
}