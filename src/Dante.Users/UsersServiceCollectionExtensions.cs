using Microsoft.Extensions.DependencyInjection;

namespace Dante.Users;

public static class UsersServiceCollectionExtensions
{
    public static IServiceCollection AddUsers(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
