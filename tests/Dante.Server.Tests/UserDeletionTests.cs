using Dante.Accounts;
using Dante.Banks;
using Dante.Infrastructure;
using Dante.Users;
using Microsoft.EntityFrameworkCore;

namespace Dante.Server.Tests;

public sealed class UserDeletionTests
{
    [Fact]
    public async Task Deleting_a_user_deletes_accounts_and_keeps_the_bank_and_other_users()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var db = database.Db;
        var bank = new Bank
        {
            Id = Guid.NewGuid(),
            Ispb = "00000000",
            Code = 1,
            FullName = "Banco do Brasil S.A.",
            ShortName = "Banco do Brasil",
        };
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Username = "owner",
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var other = new User
        {
            Id = Guid.NewGuid(),
            Username = "other",
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Banks.Add(bank);
        db.Users.AddRange(owner, other);
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            BankId = bank.Id,
            Name = "Salary",
        });
        await db.SaveChangesAsync();

        db.Users.Remove(owner);
        await db.SaveChangesAsync();

        Assert.Empty(await db.Accounts.Where(account => account.UserId == owner.Id).ToListAsync());
        Assert.NotNull(await db.Banks.SingleAsync(candidate => candidate.Id == bank.Id));
        Assert.NotNull(await db.Users.SingleAsync(candidate => candidate.Id == other.Id));

        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = other.Id,
            BankId = bank.Id,
            Name = "Daily",
        });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var persistedBank = await db.Banks.SingleAsync(candidate => candidate.Id == bank.Id);
        db.Banks.Remove(persistedBank);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
