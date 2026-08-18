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

    // === GENERAL ENTITY STORE (for new features, e.g. notice board, events) ===
    // In Teacher role, READ and WRITE are allowed.
    List<GenericRecord> QueryEntities(string entityType);
    GenericRecord? GetEntity(string entityType, int id);
    int SaveEntity(string entityType, Dictionary<string, string> data, int? id = null);
    void DeleteEntity(string entityType, int id);
}