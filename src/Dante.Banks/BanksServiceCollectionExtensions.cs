using Microsoft.Extensions.DependencyInjection;

namespace Dante.Banks;

public static class BanksServiceCollectionExtensions
{
    public static IServiceCollection AddBanks(this IServiceCollection services)
    {
        services.AddScoped<IBankQueryService, BankQueryService>();
        services.AddScoped<IBankCatalogSynchronizer, BankCatalogSynchronizer>();
        return services;
    }
}
