using System.Collections.Generic;
using System.Linq;
using Kreta.Core;
using Kreta.Services.Database;
using Microsoft.EntityFrameworkCore;

namespace Kreta.Contexts;

public class SqliteStudentContext : IStudentContext
{
    private readonly KretaDbContext _dbContext;
    private readonly int _studentId;

    public SqliteStudentContext(KretaDbContext dbContext, int studentId)
    {
        _dbContext = dbContext;
        _studentId = studentId;
    }

    public List<Grade> GetMyGrades()
    {
        return _dbContext.Grades
            .Include(g => g.Subject)
            .Where(g => g.StudentId == _studentId)
            .ToList();
    }

    public User? GetMyProfile()
    {
        return _dbContext.Users.FirstOrDefault(u => u.Id == _studentId);
    }

    public List<Lesson> GetMyLessons()
    {
        var profile = GetMyProfile();
        if (profile == null || string.IsNullOrEmpty(profile.ClassName))
        {
            return new List<Lesson>();
        }

        return _dbContext.Lessons
            .Where(l => l.ClassName == profile.ClassName)
            .OrderBy(l => l.Date)
            .ToList();
    }
}