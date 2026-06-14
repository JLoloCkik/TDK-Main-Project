using System.Collections.Generic;
using Kreta.Core;

namespace Kreta.Contexts;

public interface ITeacherContext
{
    List<User> GetMyClassStudents();
    void AddGrade(int studentId, Grade grade);
}