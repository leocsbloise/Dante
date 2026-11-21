using Dante.Accounts;

namespace Dante.Ledger;

public sealed class LedgerService(ILedgerRepository entries, IAccountRepository accounts) : ILedgerService
{
    public async Task<LedgerResponse> CreateAsync(
        Guid userId,
        CreateLedgerRequest request,
        CancellationToken cancellationToken)
    {
        var debitId = request.DebitAccountId ?? Guid.Empty;
        var creditId = request.CreditAccountId ?? Guid.Empty;
        await EnsureOwnedAsync(userId, [debitId, creditId], cancellationToken);

        if (request.OccurredAt is null)
            throw new InvalidLedgerException("A time is required.");

        var entry = LedgerEntry.Create(
            debitId,
            creditId,
            request.Amount ?? 0,
            request.OccurredAt.Value,
            request.TimeZone);
        entries.Add(entry);
        await entries.SaveChangesAsync(cancellationToken);
        return new LedgerResponse(
            entry.Id,
            entry.DebitAccountId,
            entry.CreditAccountId,
            entry.Amount,
            entry.OccurredAt,
            entry.TimeZone);
    }

    public async Task<LedgerPage> SearchAsync(
        Guid userId,
        LedgerSearchRequest request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit ?? 100;
        if (limit is < 1 or > 100)
            throw new InvalidLedgerException("Size must be from 1 to 100.");

        var debitId = request.DebitAccountId;
        var creditId = request.CreditAccountId;
        await EnsureOwnedAsync(userId, Present(debitId, creditId), cancellationToken);
        var (fromUtc, toUtc) = LedgerWindow.ToUtc(request.From, request.To, request.TimeZone);
        var search = new LedgerSearch(debitId, creditId, fromUtc, toUtc);
        if (search.DebitAccountId is null && search.CreditAccountId is null && search.FromUtc is null && search.ToUtc is null)
            throw new InvalidLedgerException("A debit account, a credit account, or a time window is required.");

        DateTimeOffset? after = null;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var cursor = LedgerCursor.Read(request.Cursor);
            if (cursor.Search != search)
                throw new InvalidLedgerException("The cursor belongs to a different search.");

            after = cursor.After;
        }

        var rows = await entries.SearchAsync(
            new LedgerQuery(userId, debitId, creditId, fromUtc, toUtc, after, limit + 1),
            cancellationToken);
        var page = rows.Take(limit).Select(ToResponse).ToList();
        var next = rows.Count > limit ? LedgerCursor.Write(page[^1].OccurredAt, search) : null;
        return new LedgerPage(page, next);
    }

    private async Task EnsureOwnedAsync(
        Guid userId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return;

        var lookup = ids.Where(id => id != Guid.Empty).Distinct().ToArray();
        var owned = lookup.Length == 0
            ? new HashSet<Guid>()
            : await accounts.FindOwnedIdsAsync(userId, lookup, cancellationToken);
        if (ids.Any(id => !owned.Contains(id)))
            throw new AccountNotFoundException();
    }

    private static Guid[] Present(params Guid?[] ids) =>
        ids.Where(id => id is not null).Select(id => id!.Value).ToArray();

    private static LedgerResponse ToResponse(LedgerEntry entry) =>
        new(entry.Id, entry.DebitAccountId, entry.CreditAccountId, entry.Amount, entry.OccurredAt, entry.TimeZone);
}
