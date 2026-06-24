using System.Collections.Generic;
using Kreta.Core;

namespace Kreta.Contexts;

public interface IStudentContext
{
    List<Grade> GetMyGrades();
    User? GetMyProfile();
}