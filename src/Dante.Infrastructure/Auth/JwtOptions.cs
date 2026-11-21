namespace Dante.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "dante";

    public string Audience { get; set; } = "dante";

    public string? SigningKey { get; set; }

    public int LifetimeMinutes { get; set; } = 60;
}
