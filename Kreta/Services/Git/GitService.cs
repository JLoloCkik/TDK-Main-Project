using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace Kreta.Services.Git;

public class GitService : IGitService {
    private readonly string _projectRoot;
    
    GitService() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.csproj").Any()) {
            dir = dir.Parent;
        }
            
        _projectRoot = dir?.FullName ?? throw new Exception("Nem található a Git gyökérkönyvtár!");
    }
    
    public void Commit(string message) {
        using var repo = new Repository(_projectRoot);
        Commands.Stage(repo, "*");
        
        var author = new Signature("EvolKréta AI", "ai@evolkréta.hu", DateTimeOffset.Now);
        repo.Commit(message, author, author);
    }
    
    public void RevertToLastStable() {
        using var repo = new Repository(_projectRoot);
        repo.Reset(ResetMode.Hard, repo.Head.Tip);
    }
}