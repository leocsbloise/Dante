using Microsoft.Extensions.DependencyInjection;

namespace Dante.Accounts;

public static class AccountsServiceCollectionExtensions
{
    public static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        return services;
    }
}
