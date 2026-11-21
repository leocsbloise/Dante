namespace Dante.Users;

public sealed class User
{
    public Guid Id { get; set; }

    public required string Username { get; set; }

    public required string PasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public static User Create(string username)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = UsernameRules.Parse(username),
            PasswordHash = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new InvalidPasswordException("Password hash is required.");

        PasswordHash = passwordHash;
    }
}
