using System.Collections.Generic;
using Kreta.Core;

namespace Kreta.Contexts;

public interface IStudentContext
{
    List<Grade> GetMyGrades();
    User? GetMyProfile();
    List<Lesson> GetMyLessons();

    // === GENERAL ENTITY STORE (for new features, e.g. notice board, events) ===
    // In Student role, ONLY READ is allowed - write (SaveEntity/DeleteEntity) is NOT available.
    List<GenericRecord> QueryEntities(string entityType);
    GenericRecord? GetEntity(string entityType, int id);
}