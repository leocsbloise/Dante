namespace Dante.Accounts;

public sealed class InvalidAccountException(string message, params string[] fields) : Exception(message)
{
    public IReadOnlyList<string> Fields { get; } = fields;
}

public sealed class AccountNotFoundException() : Exception("Account was not found.");
