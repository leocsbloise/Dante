using Dante.Banks;

namespace Dante.Accounts;

public sealed record CreateAccountRequest(Guid? BankId, string? Name);

public sealed record AccountResponse(Guid Id, string Name, BankResponse Bank);

public interface IAccountService
{
    Task<IReadOnlyList<AccountResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<AccountResponse> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid accountId, CancellationToken cancellationToken);
}

public sealed class AccountService(IAccountRepository accounts, IBankRepository banks) : IAccountService
{
    public async Task<IReadOnlyList<AccountResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var owned = await accounts.ListByUserAsync(userId, cancellationToken);
        return owned
            .Select(account => new AccountResponse(account.Id, account.Name, BankResponse.From(account.Bank)))
            .ToList();
    }

    public async Task<AccountResponse> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken)
    {
        if (request.BankId is null || request.BankId == Guid.Empty)
            throw new InvalidAccountException("A bank is required.");

        var bank = await banks.FindAsync(request.BankId.Value, cancellationToken);
        if (bank is null)
            throw new BankNotFoundException("Bank was not found.");

        var account = Account.Create(userId, bank.Id, request.Name ?? string.Empty);
        accounts.Add(account);
        await accounts.SaveChangesAsync(cancellationToken);
        return new AccountResponse(account.Id, account.Name, BankResponse.From(bank));
    }

    public async Task DeleteAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        var deleted = await accounts.DeleteAsync(accountId, userId, cancellationToken);
        if (!deleted)
            throw new AccountNotFoundException();

        await accounts.SaveChangesAsync(cancellationToken);
    }
}
