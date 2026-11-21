namespace Dante.Ledger;

public sealed class InvalidLedgerException(string message) : Exception(message);
