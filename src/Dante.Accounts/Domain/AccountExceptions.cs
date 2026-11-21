namespace Dante.Accounts;

public sealed class InvalidAccountException(string message) : Exception(message);

public sealed class AccountNotFoundException() : Exception("Account was not found.");
