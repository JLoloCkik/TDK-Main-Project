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

    // === GENERAL ENTITY STORE (for new features, e.g. notice board, events) ===
    // In Director role, READ and WRITE are allowed.
    List<GenericRecord> QueryEntities(string entityType);
    GenericRecord? GetEntity(string entityType, int id);
    int SaveEntity(string entityType, Dictionary<string, string> data, int? id = null);
    void DeleteEntity(string entityType, int id);
}