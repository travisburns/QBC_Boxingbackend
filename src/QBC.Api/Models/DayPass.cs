using System.ComponentModel.DataAnnotations;

namespace QBC.Api.Models;

/// <summary>
/// A one-time, single-day gym pass bought online and reserved for a specific
/// date. Unlike <see cref="MembershipSubscription"/> this is not recurring: it
/// records a single Square payment. We store only Square identifiers, the
/// reserved date, the (server-side) amount, and non-sensitive card display data
/// (brand + last 4) — never a PAN.
/// </summary>
public class DayPass
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>Our internal product id, e.g. "day-pass".</summary>
    [Required]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>The day the pass is reserved for (gym-local calendar day).</summary>
    public DateOnly VisitDate { get; set; }

    /// <summary>Amount actually charged, in minor units. Sourced server-side, never from the client.</summary>
    public int PriceCents { get; set; }

    public string Currency { get; set; } = "USD";

    public string? SquarePaymentId { get; set; }
    public string? SquareCustomerId { get; set; }

    // Display-only, safe to store.
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }

    public DayPassStatus Status { get; set; } = DayPassStatus.Paid;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RedeemedAtUtc { get; set; }
}
