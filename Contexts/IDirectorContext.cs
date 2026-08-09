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

    // === ALTALANOS ENTITAS-TAROLO (uj funkciokhoz, pl. hirdetotabla, esemenyek) ===
    // Igazgato szerepkorben OLVASAS es IRAS is engedelyezett.
    List<GenericRecord> QueryEntities(string entityType);
    GenericRecord? GetEntity(string entityType, int id);
    int SaveEntity(string entityType, Dictionary<string, string> data, int? id = null);
    void DeleteEntity(string entityType, int id);
}
