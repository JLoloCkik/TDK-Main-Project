using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Kreta.Core;

namespace Kreta.Services.Database;

public class KretaDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=kreta.db");
    }

    public void Seed()
    {
        Database.EnsureCreated();

        if (!Users.Any())
        {
            var student = new User { Name = "Kovács János", Email = "janos@evolkréta.hu", Role = Role.Student };
            var teacher = new User { Name = "Szabó Mária", Email = "maria@evolkréta.hu", Role = Role.Teacher };
            var director = new User { Name = "Nagy Péter", Email = "peter@evolkréta.hu", Role = Role.Director };

            Users.AddRange(student, teacher, director);
            
            var math = new Subject { Name = "Matematika" };
            var history = new Subject { Name = "Történelem" };
            
            Subjects.AddRange(math, history);

            SaveChanges(); 
            
            var grade1 = new Grade { Value = 5, Weight = 100, Date = DateTime.Now.AddDays(-2), StudentId = student.Id, SubjectId = math.Id };
            var grade2 = new Grade { Value = 4, Weight = 100, Date = DateTime.Now.AddDays(-1), StudentId = student.Id, SubjectId = math.Id };
            var grade3 = new Grade { Value = 3, Weight = 50, Date = DateTime.Now, StudentId = student.Id, SubjectId = history.Id };
            
            Grades.AddRange(grade1, grade2, grade3);

            SaveChanges();
        }
    }
}