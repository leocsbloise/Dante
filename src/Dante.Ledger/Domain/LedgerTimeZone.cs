namespace Dante.Ledger;

public static class LedgerTimeZone
{
    public const int MaxLength = 64;

    public static string Parse(string? value)
    {
        var timeZone = value?.Trim() ?? string.Empty;
        if (timeZone.Length == 0 || timeZone.Length > MaxLength || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _))
            throw new InvalidLedgerException("Time zone was not recognized.");

        return timeZone;
    }

    public static TimeZoneInfo Find(string? value)
    {
        var timeZone = Parse(value);
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone);
        return zone!;
    }
}
