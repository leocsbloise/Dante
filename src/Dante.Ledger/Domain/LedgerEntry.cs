namespace Dante.Ledger;

public sealed class LedgerEntry
{
    public Guid Id { get; set; }

    public Guid DebitAccountId { get; set; }

    public Guid CreditAccountId { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public required string TimeZone { get; set; }

    public static LedgerEntry Create(
        Guid debitAccountId,
        Guid creditAccountId,
        decimal amount,
        DateTimeOffset occurredAt,
        string? timeZone)
    {
        if (debitAccountId == Guid.Empty)
            throw new InvalidLedgerException("A debit account is required.");

        if (creditAccountId == Guid.Empty)
            throw new InvalidLedgerException("A credit account is required.");

        if (debitAccountId == creditAccountId)
            throw new InvalidLedgerException("The debit and credit accounts must be different.");

        if (amount <= 0)
            throw new InvalidLedgerException("Amount must be greater than zero.");

        if (decimal.Round(amount, 2) != amount)
            throw new InvalidLedgerException("Amount must have at most two decimal places.");

        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            DebitAccountId = debitAccountId,
            CreditAccountId = creditAccountId,
            Amount = amount,
            OccurredAt = occurredAt.ToUniversalTime(),
            TimeZone = LedgerTimeZone.Parse(timeZone),
        };
    }
}
