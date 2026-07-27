using System.Threading.Tasks;

namespace Kreta.Services.Git;

public interface IGitService
{
    Task<bool> CommitAndPushAsync(string filePath, string commitMessage, string branchName = "ai-dev");
    Task<bool> RemoveAndPushAsync(string filePath, string commitMessage, string branchName = "ai-dev");
}