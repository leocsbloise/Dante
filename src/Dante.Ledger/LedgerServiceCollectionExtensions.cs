using Microsoft.Extensions.DependencyInjection;

namespace Dante.Ledger;

public static class LedgerServiceCollectionExtensions
{
    public static IServiceCollection AddLedger(this IServiceCollection services)
    {
        services.AddScoped<ILedgerService, LedgerService>();
        return services;
    }
}
