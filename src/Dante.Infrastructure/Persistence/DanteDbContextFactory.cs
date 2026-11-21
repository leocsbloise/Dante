using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dante.Infrastructure;

public sealed class DanteDbContextFactory : IDesignTimeDbContextFactory<DanteDbContext>
{
    public DanteDbContext CreateDbContext(string[] args)
    {
        EnvFile.Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Dante");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__Dante is required to create the database context.");
        }

        var options = new DbContextOptionsBuilder<DanteDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DanteDbContext(options);
    }
}
