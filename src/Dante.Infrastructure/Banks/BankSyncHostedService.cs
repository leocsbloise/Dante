using System.Diagnostics;
using Dante.Banks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dante.Infrastructure;

public sealed class BankSyncHostedService(
    IServiceScopeFactory scopes,
    IBankCatalogRepository catalog,
    IConfiguration configuration,
    ILogger<BankSyncHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Banks:SyncOnStartup", true))
            return;

        using var activity = DanteTelemetry.ActivitySource.StartActivity("Bank catalog sync");
        try
        {
            var remoteBanks = await catalog.GetAllAsync(cancellationToken);
            if (remoteBanks.Count == 0)
            {
                logger.LogWarning("Bank catalog response was empty. The existing catalog was left unchanged.");
                return;
            }

            await using var scope = scopes.CreateAsyncScope();
            var synchronizer = scope.ServiceProvider.GetRequiredService<IBankCatalogSynchronizer>();
            var result = await synchronizer.SyncAsync(remoteBanks, cancellationToken);
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag("bank.catalog.inserted", result.Inserted);
                activity.SetTag("bank.catalog.updated", result.Updated);
            }

            logger.LogInformation(
                "Bank catalog sync inserted {Inserted} and updated {Updated}.",
                result.Inserted,
                result.Updated);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogError(exception, "Bank catalog sync failed. The application will start with the existing catalog.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
