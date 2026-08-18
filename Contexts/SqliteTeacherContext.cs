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

    public List<GenericRecord> QueryEntities(string entityType)
    {
        return _dbContext.GenericRecords
            .Where(r => r.EntityType == entityType)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public GenericRecord? GetEntity(string entityType, int id)
    {
        return _dbContext.GenericRecords
            .FirstOrDefault(r => r.EntityType == entityType && r.Id == id);
    }

    public int SaveEntity(string entityType, Dictionary<string, string> data, int? id = null)
    {
        var record = id.HasValue
            ? _dbContext.GenericRecords.FirstOrDefault(r => r.EntityType == entityType && r.Id == id.Value)
            : null;

        if (record == null)
        {
            record = new GenericRecord
            {
                EntityType = entityType,
                CreatedByRole = Role.Teacher,
                Data = data
            };
            _dbContext.GenericRecords.Add(record);
        }
        else
        {
            record.Data = data;
        }

        _dbContext.SaveChanges();
        return record.Id;
    }

    public void DeleteEntity(string entityType, int id)
    {
        var record = _dbContext.GenericRecords.FirstOrDefault(r => r.EntityType == entityType && r.Id == id);
        if (record != null)
        {
            _dbContext.GenericRecords.Remove(record);
            _dbContext.SaveChanges();
        }
    }
}