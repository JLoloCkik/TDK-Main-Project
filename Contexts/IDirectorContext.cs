using System.Collections.Generic;
using Kreta.Core;

namespace Kreta.Contexts;

public interface IDirectorContext
{
    List<User> GetAllUsers();
    void CreateUser(User newUser);
    void DeleteUser(int userId);
    List<string> GetAllClasses();
    void AssignClassToStudent(int studentId, string className);
}