namespace QBC.Api.Options;

/// <summary>JWT signing/validation settings bound from configuration ("Jwt").</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "apex-athletic";
    public string Audience { get; set; } = "apex-athletic-client";

    /// <summary>Symmetric signing key. Keep out of source control — use user-secrets or env vars.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Access-token lifetime in minutes.</summary>
    public int AccessTokenMinutes { get; set; } = 120;
}
