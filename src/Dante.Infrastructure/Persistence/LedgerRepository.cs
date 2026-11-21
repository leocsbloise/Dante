using Dante.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Dante.Infrastructure;

public sealed class LedgerRepository(DanteDbContext db) : ILedgerRepository
{
    public void Add(LedgerEntry entry) => db.LedgerEntries.Add(entry);

    public async Task<IReadOnlyList<LedgerEntry>> SearchAsync(LedgerQuery query, CancellationToken cancellationToken)
    {
        var rows = db.LedgerEntries.AsNoTracking()
            .Join(
                db.Accounts.AsNoTracking(),
                entry => entry.DebitAccountId,
                account => account.Id,
                (entry, account) => new { entry, account })
            .Where(row => row.account.UserId == query.UserId)
            .Select(row => row.entry);

        if (query.DebitAccountId is Guid debitId)
            rows = rows.Where(entry => entry.DebitAccountId == debitId);

        if (query.CreditAccountId is Guid creditId)
            rows = rows.Where(entry => entry.CreditAccountId == creditId);

        if (query.FromUtc is DateTimeOffset fromUtc)
            rows = rows.Where(entry => EF.Property<long>(entry, "OccurredUtcTicks") >= fromUtc.UtcTicks);

        if (query.ToUtc is DateTimeOffset toUtc)
            rows = rows.Where(entry => EF.Property<long>(entry, "OccurredUtcTicks") < toUtc.UtcTicks);

        if (query.AfterUtc is DateTimeOffset afterUtc)
            rows = rows.Where(entry => EF.Property<long>(entry, "OccurredUtcTicks") > afterUtc.UtcTicks);

        return await rows
            .OrderBy(entry => EF.Property<long>(entry, "OccurredUtcTicks"))
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
