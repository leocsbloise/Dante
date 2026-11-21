namespace Dante.Accounts;

public sealed class Account
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid BankId { get; set; }

    public required string Name { get; set; }

    public static Account Create(Guid userId, Guid bankId, string name)
    {
        if (userId == Guid.Empty)
            throw new InvalidAccountException("A user is required.");

        if (bankId == Guid.Empty)
            throw new InvalidAccountException("A bank is required.");

        return new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankId = bankId,
            Name = AccountName.Parse(name),
        };
    }
}
