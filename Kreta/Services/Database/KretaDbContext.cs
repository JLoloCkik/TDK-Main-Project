namespace Kreta.Services.Database;
using Microsoft.EntityFrameworkCore; 
using Core;

public class KretaDbContext : DbContext{
    public DbSet<User> Users { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=kreta.db");
    }
}

