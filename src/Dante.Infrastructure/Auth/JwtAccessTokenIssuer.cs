using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dante.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dante.Infrastructure;

public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Create(User user)
    {
        var signingKey = _options.SigningKey
            ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.LifetimeMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            ],
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
