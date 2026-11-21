using Dante.Users;
using Microsoft.AspNetCore.Identity;

namespace Dante.Infrastructure;

public sealed class IdentityUserPasswordHasher(PasswordHasher<User> hasher) : IUserPasswordHasher
{
    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
