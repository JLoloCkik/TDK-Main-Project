using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Kreta.Services.Git;

/// <summary>
///     Service responsible for automatically executing Git commands and pushing to the 'ai-dev' GitHub branch.
/// </summary>
public class GitService : IGitService
{
    private readonly string _workingDirectory;

    public GitService()
    {
        _workingDirectory = PathHelper.GetProjectRootDirectory();
    }

    /// <summary>
    ///     Adds the modified or new C# file, commits it, and pushes it to the GitHub 'ai-dev' branch.
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

            var checkoutResult = await RunGitCommandAsync($"checkout -B {branchName}");
            if (!checkoutResult.Success)
            {
                Console.WriteLine("[Git Hiba] Nem sikerült váltani/létrehozni a célágat.");
                return false;
            }

            var relativePath = Path.GetRelativePath(_workingDirectory, filePath);
            var addResult = await RunGitCommandAsync($"add \"{relativePath}\"");
            if (!addResult.Success)
            {
                Console.WriteLine("[Git Hiba] Nem sikerült hozzáadni a fájlt a stage-hez.");
                return false;
            }

            var sanitizedMessage = commitMessage.Replace("\"", "'");
            var commitResult = await RunGitCommandAsync($"commit -m \"[AI-Evolúció] {sanitizedMessage}\"");

            if (commitResult.Output.Contains("nothing to commit"))
            {
                Console.WriteLine("[Git] Nincs új változtatás a commit-hoz.");
                return true;
            }

            if (!commitResult.Success)
            {
                Console.WriteLine("[Git Hiba] A commit létrehozása sikertelen.");
                return false;
            }

            Console.WriteLine($"[Git] Változtatások feltöltése a távoli tárhelyre (origin {branchName})...");

            // Force push: the 'ai-dev' branch is fully managed by this system
            // (the EvolViews directory is the source of truth), so we don't try to merge/rebase
            // with a potentially differing remote state — we simply overwrite it.
            var pushResult = await RunGitCommandAsync($"push --force -u origin {branchName}");

            if (!pushResult.Success)
            {
                Console.WriteLine($"[Git Hiba] A push sikertelen volt még force móddal is:\n{pushResult.Output}");
                return false;
            }

            Console.WriteLine(
                $"[Git Siker] A(z) {Path.GetFileName(filePath)} fájl sikeresen feltöltve a GitHub '{branchName}' ágára!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Git Hiba] Hiba történt a Git szinkronizáció során: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Removes the deleted C# file from the Git repository and pushes the deletion commit to the remote branch.
    /// </summary>
    public async Task<bool> RemoveAndPushAsync(string filePath, string commitMessage, string branchName = "ai-dev")
    {
        try
        {
            Console.WriteLine($"[Git] Fájl törlésének szinkronizálása a GitHub '{branchName}' ágra...");

            if (!Directory.Exists(Path.Combine(_workingDirectory, ".git")))
            {
                Console.WriteLine("[Git Hiba] A projekt gyökerében nem található .git tárhely.");
                return false;
            }

            var checkoutResult = await RunGitCommandAsync($"checkout -B {branchName}");
            if (!checkoutResult.Success)
            {
                Console.WriteLine("[Git Hiba] Nem sikerült váltani/létrehozni a célágat.");
                return false;
            }

            var relativePath = Path.GetRelativePath(_workingDirectory, filePath);
            await RunGitCommandAsync($"rm \"{relativePath}\" --ignore-unmatch");

            var sanitizedMessage = commitMessage.Replace("\"", "'");
            var commitResult = await RunGitCommandAsync($"commit -m \"[AI-Evolúció - Törlés] {sanitizedMessage}\"");

            if (commitResult.Output.Contains("nothing to commit"))
            {
                Console.WriteLine("[Git] Nincs új változtatás a törlési commit-hoz.");
                return true;
            }

            if (!commitResult.Success)
            {
                Console.WriteLine("[Git Hiba] A törlési commit létrehozása sikertelen.");
                return false;
            }

            var pushResult = await RunGitCommandAsync($"push --force -u origin {branchName}");

            if (!pushResult.Success)
            {
                Console.WriteLine(
                    $"[Git Hiba] A törlés push-olása sikertelen volt még force móddal is:\n{pushResult.Output}");
                return false;
            }

            Console.WriteLine(
                $"[Git Siker] A(z) {Path.GetFileName(filePath)} fájl törölve a GitHub '{branchName}' ágáról!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Git Törlési Hiba]: {ex.Message}");
            return false;
        }
    }

    private async Task<GitCommandResult> RunGitCommandAsync(string arguments)
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
        if (process == null) throw new Exception("Nem sikerült elindítani a Git folyamatot.");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var result = output + "\n" + error;
        var isNothingToCommit = result.Contains("nothing to commit");
        var success = process.ExitCode == 0 || isNothingToCommit;

        if (!success) Console.WriteLine($"[Git Parancs Hiba ({arguments})]:\n{result}");

        return new GitCommandResult { Success = success, Output = result };
    }

    private class GitCommandResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
    }
}