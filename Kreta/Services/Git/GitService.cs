using System;
using System.IO;
using LibGit2Sharp;
using Kreta.Services;

namespace Kreta.Services.Git;

public class GitService : IGitService {
    private readonly string _projectRoot;

    public GitService() {
        _projectRoot = PathHelper.FindProjectRoot()
            ?? throw new Exception("Nem található a Git gyökérkönyvtár (nincs .csproj a szülőkönyvtárakban)!");

        // Ha még nincs Git repó a projektben, automatikusan létrehozzuk —
        // korábban ez hiányzott, és minden Commit() hívás elhasalt volna
        // "repository not found" hibával egy vadonatúj projektben.
        if (!Repository.IsValid(_projectRoot)) {
            Repository.Init(_projectRoot);
        }

        EnsureGitIgnore();
    }

    private void EnsureGitIgnore() {
        string gitignorePath = Path.Combine(_projectRoot, ".gitignore");
        if (!File.Exists(gitignorePath)) {
            File.WriteAllText(gitignorePath,
                "bin/\nobj/\n.env\n*.db\nDynamic/\n.vs/\n.idea/\n");
        }
    }

    public void Commit(string message) {
        using var repo = new Repository(_projectRoot);
        Commands.Stage(repo, "*");

        var author = new Signature("EvolKréta AI", "ai@evolkreta.local", DateTimeOffset.Now);

        try {
            repo.Commit(message, author, author);
        }
        catch (EmptyCommitException) {
            // Nincs tényleges változás a fájlokban — ez nem hiba, egyszerűen
            // nincs mit menteni (pl. ha a felhasználó kétszer nyomja meg a
            // Jóváhagyás gombot).
        }
    }

    public void RevertToLastStable() {
        using var repo = new Repository(_projectRoot);

        if (repo.Head?.Tip == null) {
            // Még egyetlen commit sem történt — nincs mihez visszaállni.
            return;
        }

        repo.Reset(ResetMode.Hard, repo.Head.Tip);
    }
}
