using System.Globalization;
using System.Text;

namespace Dante.Ledger;

public sealed record LedgerSearch(
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc);

public static class LedgerCursor
{
    public static string Write(DateTimeOffset after, LedgerSearch search)
    {
        var raw = string.Join(
            '\n',
            Format(after),
            Format(search.DebitAccountId),
            Format(search.CreditAccountId),
            Format(search.FromUtc),
            Format(search.ToUtc));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static (DateTimeOffset After, LedgerSearch Search) Read(string cursor)
    {
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var lines = Encoding.UTF8.GetString(Convert.FromBase64String(padded)).Split('\n');
            if (lines.Length != 5)
                throw new InvalidLedgerException("The cursor is not valid.", "cursor");

            return (ParseInstant(lines[0]), new LedgerSearch(
                ParseAccount(lines[1]),
                ParseAccount(lines[2]),
                ParseOptionalInstant(lines[3]),
                ParseOptionalInstant(lines[4])));
        }
        catch (InvalidLedgerException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidLedgerException("The cursor is not valid.", "cursor");
        }
    }

    private static string Format(DateTimeOffset instant) =>
        instant.ToUniversalTime().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static string Format(DateTimeOffset? instant) => instant is null ? "" : Format(instant.Value);

    private static string Format(Guid? id) => id?.ToString("D") ?? "";

    private static DateTimeOffset ParseInstant(string text)
    {
        if (!DateTimeOffset.TryParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
            throw new InvalidLedgerException("The cursor is not valid.", "cursor");

        return instant.ToUniversalTime();
    }

    private static DateTimeOffset? ParseOptionalInstant(string text) => text.Length == 0 ? null : ParseInstant(text);

    private static Guid? ParseAccount(string text)
    {
        if (text.Length == 0)
            return null;

        if (!Guid.TryParseExact(text, "D", out var id))
            throw new InvalidLedgerException("The cursor is not valid.", "cursor");

        return id;
    }
}
