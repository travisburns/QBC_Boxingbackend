using System.ComponentModel.DataAnnotations;

namespace QBC.Api.Dtos;

/// <summary>
/// Day-pass purchase payload. Note what is NOT here: no card number, expiry, or
/// CVV, and no amount. The client sends a product id, a date, and either a
/// single-use Square token (<see cref="SourceId"/>) or a flag to charge the
/// saved card. The price comes from the server-side <c>DayPassCatalog</c>.
/// </summary>
public sealed class DayPassRequest
{
    [Required, MaxLength(64)]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Reserved calendar day, ISO "yyyy-MM-dd".</summary>
    [Required, MaxLength(10)]
    public string VisitDate { get; set; } = string.Empty;

    /// <summary>
    /// Single-use payment token from the Square Web Payments SDK (card / Apple
    /// Pay / Google Pay). Required unless <see cref="UseSavedCard"/> is true.
    /// </summary>
    [MaxLength(1024)]
    public string? SourceId { get; set; }

    /// <summary>Client-generated idempotency key to make retries safe.</summary>
    [Required, MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Save this card on file for faster checkout next time.</summary>
    public bool SaveCard { get; set; }

    /// <summary>Charge the card already on file instead of a new token.</summary>
    public bool UseSavedCard { get; set; }
}

public sealed record DayPassDto(
    int Id,
    string ProductId,
    string ProductName,
    string VisitDate,
    int PriceCents,
    string Currency,
    string Status,
    string? CardBrand,
    string? CardLast4,
    DateTime CreatedAtUtc,
    DateTime? RedeemedAtUtc);

public sealed record DayPassProductDto(
    string Id,
    string Name,
    int PriceCents,
    string Currency,
    string Description,
    int MaxDaysAhead);

/// <summary>The card a member has on file (display-only), for a one-tap repeat purchase.</summary>
public sealed record SavedCardDto(bool HasCard, string? CardBrand, string? CardLast4);
