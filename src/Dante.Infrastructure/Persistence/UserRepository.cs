using Dante.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dante.Infrastructure;

public sealed class UserRepository(DanteDbContext db) : IUserRepository
{
    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.ToLowerInvariant();
        return db.Users.AnyAsync(user => user.Username.ToLower() == normalized, cancellationToken);
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.ToLowerInvariant();
        return db.Users.FirstOrDefaultAsync(user => user.Username.ToLower() == normalized, cancellationToken);
    }

    public void Add(User user) => db.Users.Add(user);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new UsernameAlreadyInUseException();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
                return true;

            if (current.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
