using System.Linq;
using Kreta.Core;
using Kreta.Services.Database;

namespace Kreta.Contexts;

public class SqliteDirectorContext : IDirectorContext {
    private readonly KretaDbContext _dbContext;

    public SqliteDirectorContext(KretaDbContext dbContext) {
        _dbContext = dbContext;
    }

    public void CreateUser(User newUser) {
        _dbContext.Users.Add(newUser);
        _dbContext.SaveChanges();
    }

    public void DeleteUser(int userId) {
        var user = _dbContext.Users.FirstOrDefault(u => u.Id == userId);
        if (user != null) {
            _dbContext.Users.Remove(user);
            _dbContext.SaveChanges();
        }
    }
}
