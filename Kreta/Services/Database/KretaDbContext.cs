using Microsoft.EntityFrameworkCore;
using Kreta.Core;
using System;
using System.Linq;

namespace Kreta.Services.Database;

public class KretaDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Lesson> Lessons => Set<Lesson>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=evol_kreta.db");
    }

    public void SeedData()
    {
        Database.EnsureCreated();

        if (!Users.Any())
        {
            var student1 = new User { Name = "Kovács János", Role = Role.Student, ClassName = "9.A" };
            var student2 = new User { Name = "Németh Alíz", Role = Role.Student, ClassName = "9.A" };
            var student3 = new User { Name = "Kiss Bence", Role = Role.Student, ClassName = "10.B" };
            var teacher = new User { Name = "Szabó Mária", Role = Role.Teacher, ClassName = "" };
            var director = new User { Name = "Nagy Péter", Role = Role.Director, ClassName = "" };

            Users.AddRange(student1, student2, student3, teacher, director);
            SaveChanges();

            var subMat = new Subject { Name = "Matematika" };
            var subLit = new Subject { Name = "Magyar nyelv és irodalom" };
            var subHis = new Subject { Name = "Történelem" };
            Subjects.AddRange(subMat, subLit, subHis);
            SaveChanges();

            Grades.AddRange(
                new Grade { StudentId = student1.Id, SubjectId = subMat.Id, Value = 5, Date = DateTime.Now.AddDays(-3) },
                new Grade { StudentId = student1.Id, SubjectId = subLit.Id, Value = 4, Date = DateTime.Now.AddDays(-1) },
                new Grade { StudentId = student2.Id, SubjectId = subMat.Id, Value = 3, Date = DateTime.Now.AddDays(-2) }
            );

            Lessons.AddRange(
                new Lesson { ClassName = "9.A", SubjectName = "Matematika", Room = "101-es terem", Date = DateTime.Now.AddHours(2) },
                new Lesson { ClassName = "9.A", SubjectName = "Történelem", Room = "204-es terem", Date = DateTime.Now.AddHours(4) }
            );

            SaveChanges();
        }
    }
}