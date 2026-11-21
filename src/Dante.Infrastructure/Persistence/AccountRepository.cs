using Dante.Accounts;
using Dante.Banks;
using Microsoft.EntityFrameworkCore;

namespace Dante.Infrastructure;

public sealed class AccountRepository(DanteDbContext db) : IAccountRepository
{
    public async Task<IReadOnlyList<OwnedAccount>> ListByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await (
            from account in db.Accounts.AsNoTracking()
            join bank in db.Banks.AsNoTracking() on account.BankId equals bank.Id
            where account.UserId == userId
            orderby account.Name
            select new
            {
                account.Id,
                account.Name,
                BankId = bank.Id,
                bank.Ispb,
                bank.Code,
                bank.FullName,
                bank.ShortName,
                bank.LogoUrl,
            }).ToListAsync(cancellationToken);

        return rows.Select(row => new OwnedAccount(
            row.Id,
            row.Name,
            new Bank
            {
                Id = row.BankId,
                Ispb = row.Ispb,
                Code = row.Code,
                FullName = row.FullName,
                ShortName = row.ShortName,
                LogoUrl = row.LogoUrl,
            })).ToList();
    }

    public void Add(Account account) => db.Accounts.Add(account);

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.UserId == userId,
            cancellationToken);
        if (account is null)
            return false;

        db.Accounts.Remove(account);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
