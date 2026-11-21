namespace Dante.Accounts;

public static class AccountName
{
    public static string Parse(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 80)
            throw new InvalidAccountException("Account name must be 1 to 80 characters.");

        return name;
    }
}
