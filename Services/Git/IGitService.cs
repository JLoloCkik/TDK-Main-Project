namespace Kreta.Services.Git;

public interface IGitService {
    void Commit(string message);
    void RevertToLastStable();
}