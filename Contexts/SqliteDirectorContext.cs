using System.Collections.Generic;
using System.Linq;
using Kreta.Core;
using Kreta.Services.Database;

namespace Kreta.Contexts;

public class SqliteDirectorContext : IDirectorContext
{
    private readonly KretaDbContext _dbContext;

    public SqliteDirectorContext(KretaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<User> GetAllUsers()
    {
        return _dbContext.Users.ToList();
    }

    public void CreateUser(User newUser)
    {
        _dbContext.Users.Add(newUser);
        _dbContext.SaveChanges();
    }

    public void DeleteUser(int userId)
    {
        var user = _dbContext.Users.FirstOrDefault(u => u.Id == userId);
        if (user != null)
        {
            _dbContext.Users.Remove(user);
            _dbContext.SaveChanges();
        }
    }

    public List<string> GetAllClasses()
    {
        return _dbContext.Users
            .Where(u => u.Role == Role.Student && !string.IsNullOrEmpty(u.ClassName))
            .Select(u => u.ClassName)
            .Distinct()
            .ToList();
    }

    public void AssignClassToStudent(int studentId, string className)
    {
        var student = _dbContext.Users.FirstOrDefault(u => u.Id == studentId && u.Role == Role.Student);
        if (student != null)
        {
            student.ClassName = className;
            _dbContext.SaveChanges();
        }
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
                CreatedByRole = Role.Director,
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