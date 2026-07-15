using System.Collections.Generic;
using System.Linq;
using Kreta.Core;
using Kreta.Services.Database;

namespace Kreta.Contexts;

public class SqliteTeacherContext : ITeacherContext
{
    private readonly KretaDbContext _dbContext;

    public SqliteTeacherContext(KretaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<User> GetMyClassStudents()
    {
        return _dbContext.Users.Where(u => u.Role == Role.Student).ToList();
    }

    public void AddGrade(int studentId, Grade grade)
    {
        grade.StudentId = studentId;
        _dbContext.Grades.Add(grade);
        _dbContext.SaveChanges();
    }

    public List<Subject> GetAllSubjects()
    {
        return _dbContext.Subjects.ToList();
    }

    public List<string> GetAllClasses()
    {
        return _dbContext.Users
            .Where(u => u.Role == Role.Student && !string.IsNullOrEmpty(u.ClassName))
            .Select(u => u.ClassName)
            .Distinct()
            .ToList();
    }

    public List<Lesson> GetLessons()
    {
        return _dbContext.Lessons.ToList();
    }

    public void AddLesson(Lesson lesson)
    {
        _dbContext.Lessons.Add(lesson);
        _dbContext.SaveChanges();
    }
}