using System.Threading.Tasks;

namespace Kreta.Services.Git;

public interface IGitService
{
    /// <summary>
    /// Elvégzi az új/módosított fájl automatikus commit-ját és push-olását a megadott Git ágra.
    /// </summary>
    Task<bool> CommitAndPushAsync(string filePath, string commitMessage, string branchName = "ai-dev");
}