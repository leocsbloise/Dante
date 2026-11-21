using System.Text;
using Dante.Accounts;
using Dante.Banks;
using Dante.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Dante.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDanteInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Dante");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings__Dante is required.");

        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Jwt__SigningKey is required and must be at least 32 bytes.");

        services.AddSingleton<DatabaseCommandInstrumentation>();
        services.AddDbContext<DanteDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(provider.GetRequiredService<DatabaseCommandInstrumentation>());
        });
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IBankRepository, BankRepository>();

        var catalogUrl = configuration["Banks:CatalogUrl"] ?? "https://brasilapi.com.br/api/";
        services.AddHttpClient<IBankCatalogRepository, BrasilApiBankCatalogRepository>(client =>
        {
            client.BaseAddress = new Uri(catalogUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Dante");
        });

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.PostConfigure<JwtOptions>(options => options.SigningKey = signingKey);
        services.AddSingleton<PasswordHasher<User>>();
        services.AddSingleton<IUserPasswordHasher, IdentityUserPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddHostedService<BankSyncHostedService>();
        services.AddExceptionHandler<DomainExceptionHandler>();
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        services.AddProblemDetails();

        var jwtIssuer = configuration["Jwt:Issuer"] ?? "dante";
        var jwtAudience = configuration["Jwt:Audience"] ?? "dante";
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                };
            });
        services.AddAuthorization();

        return services;
    }
}
