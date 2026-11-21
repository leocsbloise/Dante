using Dante.Banks;

namespace Dante.Accounts;

public sealed record OwnedAccount(Guid Id, string Name, Bank Bank);

public interface IAccountRepository
{
    Task<IReadOnlyList<OwnedAccount>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Account account);

    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
