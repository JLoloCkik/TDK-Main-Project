using System.Collections.Generic;
using System.Linq;
using Kreta.Core;
using Kreta.Services.Database;

namespace Kreta.Contexts;

public class SqliteTeacherContext : ITeacherContext {
    private readonly KretaDbContext _dbContext;

    public SqliteTeacherContext(KretaDbContext dbContext) {
        _dbContext = dbContext;
    }

    public List<User> GetMyClassStudents()
        => _dbContext.Users.Where(u => u.Role == Role.Student).ToList();

    public void AddGrade(int studentId, Grade grade) {
        grade.StudentId = studentId;
        _dbContext.Grades.Add(grade);
        _dbContext.SaveChanges();
    }
}
