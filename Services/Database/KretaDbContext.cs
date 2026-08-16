using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Kreta.Core;

namespace Kreta.Services.Database;

public class KretaDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Lesson> Lessons => Set<Lesson>();

    /// <summary>
    /// Altalanos, tetszoleges uj funkciohoz hasznalhato entitasok tablaja.
    /// Ide kerul MINDEN olyan uj adat, amit a Gemini a fix domain-osztalyokon (Grade, Lesson,
    /// Subject, User) kivul, sajat maga valasztott EntityType cimkevel akar tarolni
    /// (pl. hirdetotabla / "NoticeMessage", esemenyek, szavazasok, stb.).
    /// </summary>
    public DbSet<GenericRecord> GenericRecords => Set<GenericRecord>();

    public KretaDbContext()
    {
        // 🟢 JAVÍTÁS: Biztosítjuk, hogy az SQLite fájl és a táblák séma szerint mindig létrejöjjenek
        Database.EnsureCreated();
        EnsureGenericRecordsTableExists();
    }

    /// <summary>
    /// Tesztelhetőségi konstruktor: lehetővé teszi, hogy tesztek saját (pl. ideiglenes fájl vagy
    /// in-memory) SQLite kapcsolatot injektáljanak ahelyett, hogy a valódi evol_kreta.db-t használnák.
    /// </summary>
    public KretaDbContext(DbContextOptions<KretaDbContext> options) : base(options)
    {
        Database.EnsureCreated();
        EnsureGenericRecordsTableExists();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=evol_kreta.db");
        }
    }

    /// <summary>
    /// A Database.EnsureCreated() CSAK akkor hozza letre a teljes semat, ha az adatbazis-fajl
    /// MEG NEM LETEZETT. Mivel a GenericRecords tabla bevezetese elott mar sok evol_kreta.db
    /// fajl letezett (User/Grade/Lesson/Subject tablakkal), azokba EnsureCreated() utolag NEM
    /// szurja be az uj tablat. Ez a metodus IF NOT EXISTS-szel, fajltol fuggetlenul biztositja,
    /// hogy a GenericRecords tabla mindig letezzen - regi es uj adatbazis-fajlon egyarant,
    /// adatvesztes (evol_kreta.db torlese) nelkul.
    /// </summary>
    private void EnsureGenericRecordsTableExists()
    {
        Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""GenericRecords"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_GenericRecords"" PRIMARY KEY AUTOINCREMENT,
                ""EntityType"" TEXT NOT NULL,
                ""CreatedByRole"" INTEGER NOT NULL,
                ""CreatedByUserId"" INTEGER NULL,
                ""CreatedAt"" TEXT NOT NULL,
                ""Data"" TEXT NOT NULL
            );");

        Database.ExecuteSqlRaw(@"
            CREATE INDEX IF NOT EXISTS ""IX_GenericRecords_EntityType"" ON ""GenericRecords"" (""EntityType"");");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // A GenericRecord.Data (Dictionary<string,string>) mezot egyetlen JSON string oszlopkent
        // taroljuk az SQLite-ban, igy nincs szukseg kulon EAV-tablara uj funkciotipusonkent.
        var dataComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) ==
                      JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            d => d == null ? 0 : JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
            d => new Dictionary<string, string>(d));

        modelBuilder.Entity<GenericRecord>()
            .Property(r => r.Data)
            .HasConversion(
                d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
                s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, (JsonSerializerOptions?)null)
                     ?? new Dictionary<string, string>(),
                dataComparer);

        modelBuilder.Entity<GenericRecord>()
            .HasIndex(r => r.EntityType);
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
                new User { Id = 4, Name = "Nagy Péter", Role = Role.Director, ClassName = "9.A" }
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
