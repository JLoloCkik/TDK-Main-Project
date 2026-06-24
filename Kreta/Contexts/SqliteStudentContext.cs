using System.Collections.Generic;
using System.Linq;
using Kreta.Core;
using Kreta.Services.Database;
using Microsoft.EntityFrameworkCore;

namespace Kreta.Contexts;

public class SqliteStudentContext : IStudentContext {
    private readonly KretaDbContext _dbContext;
    private readonly int _studentId;

    public SqliteStudentContext(KretaDbContext dbContext, int studentId) {
        _dbContext = dbContext;
        _studentId = studentId;
    }

    public List<Grade> GetMyGrades()
        => _dbContext.Grades
            .Include(g => g.Subject)
            .Where(g => g.StudentId == _studentId)
            .ToList();

    public User GetMyProfile()
        => _dbContext.Users
            .FirstOrDefault(u => u.Id == _studentId);
}