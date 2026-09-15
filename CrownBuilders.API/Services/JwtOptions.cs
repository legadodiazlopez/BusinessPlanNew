namespace CrownBuilders.API.Services;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "CrownBuilders";

    public string Audience { get; init; } = "CrownBuilders.Client";

    public string Key { get; init; } = string.Empty;

    public int LifetimeMinutes { get; init; } = 60;
}
