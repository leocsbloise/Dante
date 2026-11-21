namespace Dante.Banks;

public sealed class InvalidBankException(string message) : Exception(message);

public sealed class BankNotFoundException(string message) : Exception(message);
