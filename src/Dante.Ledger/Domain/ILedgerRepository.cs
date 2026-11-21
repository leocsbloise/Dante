namespace Dante.Ledger;

public interface ILedgerRepository
{
    void Add(LedgerEntry entry);

    Task<IReadOnlyList<LedgerEntry>> SearchAsync(LedgerQuery query, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record LedgerQuery(
    Guid UserId,
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    DateTimeOffset? AfterUtc,
    int Limit);
