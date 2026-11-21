using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Dante.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task ApplyMigrationsAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        if (!configuration.GetValue("Database:ApplyMigrations", true))
            return;

        const int attempts = 10;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<DanteDbContext>();
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception exception) when (IsConnectionFailure(exception) && attempt < attempts)
            {
                logger.LogWarning(
                    "Database is not ready (attempt {Attempt} of {Attempts}). Retrying in 2 seconds.",
                    attempt,
                    attempts);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    private static bool IsConnectionFailure(Exception exception)
    {
        return exception is NpgsqlException or TimeoutException
            || exception.InnerException is not null && IsConnectionFailure(exception.InnerException);
    }
}
