using Microsoft.EntityFrameworkCore;
using BetaPlatform.Data;

namespace BetaPlatform.Tests;

/// <summary>Creates an isolated in-memory ApplicationDbContext per test.</summary>
public static class TestDb
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated(); // applies HasData seed (machine types 1 & 2)
        return db;
    }
}
