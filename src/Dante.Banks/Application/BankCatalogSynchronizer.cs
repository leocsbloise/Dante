namespace Dante.Banks;

public interface IBankCatalogSynchronizer
{
    Task<BankSyncResult> SyncAsync(IReadOnlyList<RemoteBank> remoteBanks, CancellationToken cancellationToken);
}

public sealed class BankCatalogSynchronizer(IBankRepository banks) : IBankCatalogSynchronizer
{
    public async Task<BankSyncResult> SyncAsync(IReadOnlyList<RemoteBank> remoteBanks, CancellationToken cancellationToken)
    {
        if (remoteBanks.Count == 0)
            return new BankSyncResult(0, 0);

        var localBanks = await banks.ListByIspbAsync(cancellationToken);
        var inserted = 0;
        var updated = 0;

        foreach (var remote in remoteBanks)
        {
            if (!localBanks.TryGetValue(remote.Ispb, out var local))
            {
                banks.Add(Bank.Create(remote));
                inserted++;
                continue;
            }

            if (local.HasSameCatalogData(remote))
                continue;

            local.ApplyCatalog(remote);
            updated++;
        }

        await banks.SaveChangesAsync(cancellationToken);
        return new BankSyncResult(inserted, updated);
    }
}
