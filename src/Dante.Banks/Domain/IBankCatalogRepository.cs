namespace Dante.Banks;

public interface IBankCatalogRepository
{
    Task<IReadOnlyList<RemoteBank>> GetAllAsync(CancellationToken cancellationToken);
}
