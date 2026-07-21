namespace QBC.Api.Options;

/// <summary>
/// Square configuration bound from "Square". The <see cref="AccessToken"/> and
/// <see cref="WebhookSignatureKey"/> are SECRETS — never commit them; load from
/// user-secrets, environment variables, or a vault.
/// </summary>
public sealed class SquareOptions
{
    public const string SectionName = "Square";

    /// <summary>"sandbox" or "production".</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>Server-side access token for the gym owner's Square account.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Location the memberships are sold under.</summary>
    public string LocationId { get; set; } = string.Empty;

    /// <summary>Signature key from the Square webhook subscription (for verifying callbacks).</summary>
    public string WebhookSignatureKey { get; set; } = string.Empty;

    /// <summary>Public URL Square posts webhooks to; part of the signature payload.</summary>
    public string WebhookNotificationUrl { get; set; } = string.Empty;

    /// <summary>Square API version header sent on every request.</summary>
    public string ApiVersion { get; set; } = "2025-01-23";

    /// <summary>Maps our plan ids (e.g. "boxing") to Square subscription plan-variation ids.</summary>
    public Dictionary<string, string> PlanVariationIds { get; set; } = new();

    public bool IsProduction =>
        string.Equals(Environment, "production", StringComparison.OrdinalIgnoreCase);

    public string ApiBaseUrl =>
        IsProduction ? "https://connect.squareup.com" : "https://connect.squareupsandbox.com";
}
