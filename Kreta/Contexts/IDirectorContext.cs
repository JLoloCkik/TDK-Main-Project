using Kreta.Core;

namespace Kreta.Contexts;

public interface IDirectorContext
{
    void CreateUser(User newUser);
    void DeleteUser(int userId);
}