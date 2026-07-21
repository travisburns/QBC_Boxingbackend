using System.ComponentModel.DataAnnotations;

namespace QBC.Api.Domain;

/// <summary>
/// Local mirror of a Square subscription. We store only Square identifiers,
/// status, and non-sensitive card display data (brand + last 4) — never a PAN.
/// </summary>
public class MembershipSubscription
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>Our internal plan id, e.g. "boxing".</summary>
    [Required]
    public string PlanId { get; set; } = string.Empty;

    public string? SquareSubscriptionId { get; set; }
    public string? SquareCustomerId { get; set; }
    public string? SquareCardId { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Pending;

    // Display-only, safe to store.
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }

    public DateTime? CurrentPeriodEndUtc { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
