using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Kreta.Core;

namespace Kreta.Services.Database;

public class KretaDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Lesson> Lessons => Set<Lesson>();

    public KretaDbContext()
    {
        // 🟢 JAVÍTÁS: Biztosítjuk, hogy az SQLite fájl és a táblák séma szerint mindig létrejöjjenek
        Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=evol_kreta.db");
    }

    public void SeedData()
    {
        Database.EnsureCreated();

        if (!Users.Any())
        {
            Users.AddRange(
                new User { Id = 1, Name = "Kovács János", Role = Role.Student, ClassName = "9.A" },
                new User { Id = 2, Name = "Nagy Anna", Role = Role.Student, ClassName = "9.A" },
                new User { Id = 3, Name = "Szabó Mária", Role = Role.Teacher, ClassName = "9.A" },
                new User { Id = 4, Name = "Nagy Péter", Role = Role.Director, ClassName = "9A" }
            );
            SaveChanges();
        }

        if (!Subjects.Any())
        {
            Subjects.AddRange(
                new Subject { Id = 1, Name = "Matematika" },
                new Subject { Id = 2, Name = "Magyar nyelv és irodalom" },
                new Subject { Id = 3, Name = "Történelem" },
                new Subject { Id = 4, Name = "Angol nyelv" }
            );
            SaveChanges();
        }

        if (!Grades.Any())
        {
            Grades.AddRange(
                new Grade { Id = 1, StudentId = 1, SubjectId = 1, Value = 5, Date = DateTime.Now.AddDays(-5) },
                new Grade { Id = 2, StudentId = 1, SubjectId = 2, Value = 4, Date = DateTime.Now.AddDays(-3) },
                new Grade { Id = 3, StudentId = 1, SubjectId = 3, Value = 5, Date = DateTime.Now.AddDays(-1) }
            );
            SaveChanges();
        }

        if (!Lessons.Any())
        {
            Lessons.AddRange(
                new Lesson { Id = 1, ClassName = "9.A", SubjectName = "Matematika", Room = "101", Date = DateTime.Now.AddHours(1) },
                new Lesson { Id = 2, ClassName = "9.A", SubjectName = "Történelem", Room = "202", Date = DateTime.Now.AddHours(2) }
            );
            SaveChanges();
        }
    }
}