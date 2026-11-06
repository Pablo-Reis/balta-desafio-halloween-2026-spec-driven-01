using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Productivity.Application.Abstractions;
using Productivity.Application.Persistence;

namespace Productivity.Application.Tests;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;

    public void Advance(TimeSpan span) => UtcNow += span;
}

/// <summary>Banco SQLite em memória compartilhado por todos os contextos criados.</summary>
public sealed class TestDbFactory : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestDbFactory()
    {
        _connection.Open();
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}

