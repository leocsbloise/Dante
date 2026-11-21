namespace Dante.Ledger;

public sealed record CreateLedgerRequest(
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    decimal? Amount,
    DateTimeOffset? OccurredAt,
    string? TimeZone);

public sealed record LedgerResponse(
    Guid Id,
    Guid DebitAccountId,
    Guid CreditAccountId,
    decimal Amount,
    DateTimeOffset OccurredAt,
    string TimeZone);

public sealed record LedgerSearchRequest(
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    string? From,
    string? To,
    string? TimeZone,
    int? Limit,
    string? Cursor);

public sealed record LedgerPage(IReadOnlyList<LedgerResponse> Entries, string? NextCursor);

public interface ILedgerService
{
    Task<LedgerResponse> CreateAsync(Guid userId, CreateLedgerRequest request, CancellationToken cancellationToken);

    Task<LedgerPage> SearchAsync(Guid userId, LedgerSearchRequest request, CancellationToken cancellationToken);
}
