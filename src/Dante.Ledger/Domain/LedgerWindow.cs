namespace Dante.Ledger;

public static class LedgerWindow
{
    public static (DateTimeOffset? FromUtc, DateTimeOffset? ToUtc) ToUtc(
        string? from,
        string? to,
        string? timeZone)
    {
        var hasFrom = !string.IsNullOrWhiteSpace(from);
        var hasTo = !string.IsNullOrWhiteSpace(to);
        if (!hasFrom && !hasTo)
            return (null, null);

        var zone = LedgerTimeZone.Find(timeZone);
        var fromUtc = hasFrom ? Convert(from!, zone) : (DateTimeOffset?)null;
        var toUtc = hasTo ? Convert(to!, zone) : (DateTimeOffset?)null;
        if (fromUtc is not null && toUtc is not null && fromUtc >= toUtc)
            throw new InvalidLedgerException("The time window ends before it starts.");

        return (fromUtc, toUtc);
    }

    private static DateTimeOffset Convert(string text, TimeZoneInfo zone)
    {
        var local = ParseLocal(text);
        if (zone.IsInvalidTime(local))
            throw new InvalidLedgerException("The local time does not exist in this time zone.");

        if (zone.IsAmbiguousTime(local))
        {
            var earlier = zone.GetAmbiguousTimeOffsets(local)
                .Select(offset => new DateTimeOffset(local, offset))
                .MinBy(instant => instant.UtcDateTime);
            return earlier.ToUniversalTime();
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    private static DateTime ParseLocal(string text)
    {
        var hyphens = 0;
        foreach (var character in text)
        {
            if (character == 'Z' || character == 'z' || character == '+')
                throw new InvalidLedgerException("A time window bound is a local time without an offset.");

            if (character == '-')
                hyphens++;
        }

        if (hyphens > 2)
            throw new InvalidLedgerException("A time window bound is a local time without an offset.");

        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd'T'HH:mm",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        };
        if (!DateTime.TryParseExact(text, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var local))
            throw new InvalidLedgerException("A time window bound was not recognized.");

        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }
}
