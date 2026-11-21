namespace Dante.Users;

public sealed record RegisterRequest(string? Username, string? Password);

public sealed record LoginRequest(string? Username, string? Password);

public sealed record UserResponse(Guid Id, string Username, DateTimeOffset CreatedAt);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IUserPasswordHasher
{
    string Hash(User user, string password);

    bool Verify(User user, string password);
}

public interface IAccessTokenIssuer
{
    AccessToken Create(User user);
}

public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
