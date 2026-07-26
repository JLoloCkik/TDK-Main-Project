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
}