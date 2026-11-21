using Dante.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dante.Server.Tests;

public sealed class DanteApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public DanteApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(descriptor =>
            {
                var serviceType = descriptor.ServiceType;
                var genericArguments = serviceType.IsGenericType
                    ? serviceType.GenericTypeArguments
                    : Type.EmptyTypes;

                return serviceType == typeof(DanteDbContext)
                    || serviceType == typeof(DbContextOptions)
                    || serviceType == typeof(DbContextOptions<DanteDbContext>)
                    || genericArguments.Contains(typeof(DanteDbContext));
            }).ToList();

            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<DanteDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DanteDbContext>();
        db.Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
