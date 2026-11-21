namespace Dante.Users;

public static class Password
{
    public static string Parse(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8 || value.Length > 128)
            throw new InvalidPasswordException("Password must be 8 to 128 characters.");

        return value;
    }
}
