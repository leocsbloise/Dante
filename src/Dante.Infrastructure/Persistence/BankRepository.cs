using Dante.Banks;
using Microsoft.EntityFrameworkCore;

namespace Dante.Infrastructure;

public sealed class BankRepository(DanteDbContext db) : IBankRepository
{
    public async Task<IReadOnlyList<Bank>> ListAsync(CancellationToken cancellationToken)
    {
        return await db.Banks.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<Bank?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Banks.AsNoTracking().FirstOrDefaultAsync(bank => bank.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, Bank>> ListByIspbAsync(CancellationToken cancellationToken)
    {
        return await db.Banks.ToDictionaryAsync(bank => bank.Ispb, cancellationToken);
    }

    public void Add(Bank bank) => db.Banks.Add(bank);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
