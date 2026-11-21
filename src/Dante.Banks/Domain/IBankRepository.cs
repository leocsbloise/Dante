namespace Dante.Banks;

public interface IBankRepository
{
    Task<IReadOnlyList<Bank>> ListAsync(CancellationToken cancellationToken);

    Task<Bank?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, Bank>> ListByIspbAsync(CancellationToken cancellationToken);

    void Add(Bank bank);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
