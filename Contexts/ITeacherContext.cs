using System.Collections.Generic;
using Kreta.Core;

namespace Kreta.Contexts;

public interface ITeacherContext
{
    List<User> GetMyClassStudents();
    void AddGrade(int studentId, Grade grade);
    List<Subject> GetAllSubjects();
    List<string> GetAllClasses();
    List<Lesson> GetLessons();
    void AddLesson(Lesson lesson);

    // === ALTALANOS ENTITAS-TAROLO (uj funkciokhoz, pl. hirdetotabla, esemenyek) ===
    // Tanar szerepkorben OLVASAS es IRAS is engedelyezett.
    List<GenericRecord> QueryEntities(string entityType);
    GenericRecord? GetEntity(string entityType, int id);
    int SaveEntity(string entityType, Dictionary<string, string> data, int? id = null);
    void DeleteEntity(string entityType, int id);
}
