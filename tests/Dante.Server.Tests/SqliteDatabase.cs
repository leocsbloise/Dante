using Dante.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Dante.Server.Tests;

internal sealed class SqliteDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteDatabase(SqliteConnection connection, DanteDbContext db)
    {
        _connection = connection;
        Db = db;
    }

    public DanteDbContext Db { get; }

    public static async Task<SqliteDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DanteDbContext>().UseSqlite(connection).Options;
        var db = new DanteDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return new SqliteDatabase(connection, db);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
