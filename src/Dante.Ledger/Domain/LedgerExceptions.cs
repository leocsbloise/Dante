namespace Dante.Ledger;

public sealed class InvalidLedgerException(string message, params string[] fields) : Exception(message)
{
    public IReadOnlyList<string> Fields { get; } = fields;
}
