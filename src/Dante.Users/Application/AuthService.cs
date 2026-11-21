namespace Dante.Users;

public sealed class AuthService(IUserRepository users, IUserPasswordHasher passwords, IAccessTokenIssuer tokens) : IAuthService
{
    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var username = UsernameRules.Parse(request.Username);
        var password = Password.Parse(request.Password);

        if (await users.UsernameExistsAsync(username, cancellationToken))
            throw new UsernameAlreadyInUseException();

        var user = User.Create(username);
        user.SetPasswordHash(passwords.Hash(user, password));
        users.Add(user);
        await users.SaveChangesAsync(cancellationToken);

        return new UserResponse(user.Id, user.Username, user.CreatedAt);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var user = string.IsNullOrEmpty(username)
            ? null
            : await users.FindByUsernameAsync(username, cancellationToken);

        if (user is null || string.IsNullOrEmpty(request.Password) || !passwords.Verify(user, request.Password))
            throw new InvalidCredentialsException();

        var accessToken = tokens.Create(user);
        return new LoginResponse(accessToken.Token, accessToken.ExpiresAt);
    }
}
