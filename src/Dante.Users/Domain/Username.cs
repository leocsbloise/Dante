using System.Text.RegularExpressions;

namespace Dante.Users;

public static partial class UsernameRules
{
    public static string Parse(string? value)
    {
        var username = value?.Trim() ?? string.Empty;
        if (!Pattern().IsMatch(username))
        {
            throw new InvalidUsernameException(
                "Username must be 3 to 32 characters and use letters, digits, dots, underscores, or hyphens.");
        }

        return username;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{3,32}$")]
    private static partial Regex Pattern();
}
