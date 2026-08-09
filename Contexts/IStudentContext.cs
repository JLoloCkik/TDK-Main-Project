using System.Collections.Generic;
using Kreta.Core;

namespace Kreta.Contexts;

public interface IStudentContext
{
    List<Grade> GetMyGrades();
    User? GetMyProfile();
    List<Lesson> GetMyLessons();

    // === ALTALANOS ENTITAS-TAROLO (uj funkciokhoz, pl. hirdetotabla, esemenyek) ===
    // Diak szerepkorben CSAK OLVASAS engedelyezett - iras (SaveEntity/DeleteEntity) NEM elerheto.
    List<GenericRecord> QueryEntities(string entityType);
    GenericRecord? GetEntity(string entityType, int id);
}
