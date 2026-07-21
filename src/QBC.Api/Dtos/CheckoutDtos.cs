using System.ComponentModel.DataAnnotations;

namespace QBC.Api.Dtos;

/// <summary>
/// Checkout payload. Note what is NOT here: no card number, expiry, or CVV.
/// The client sends only Square's single-use <see cref="SourceId"/> token.
/// </summary>
public sealed class SubscriptionRequest
{
    [Required, MaxLength(64)]
    public string PlanId { get; set; } = string.Empty;

    /// <summary>Single-use payment token produced by the Square Web Payments SDK.</summary>
    [Required, MaxLength(1024)]
    public string SourceId { get; set; } = string.Empty;

    /// <summary>Client-generated idempotency key to make retries safe.</summary>
    [Required, MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed record MembershipDto(
    string Status,
    string? PlanId,
    string? PlanName,
    string? CardBrand,
    string? CardLast4,
    DateTime? CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd);

public sealed record CheckoutResult(string Status, MembershipDto Membership);

public sealed record PlanDto(
    string Id,
    string Name,
    int PriceCents,
    string Currency,
    string Cycle,
    string Tagline,
    string[] Features,
    bool Featured);
