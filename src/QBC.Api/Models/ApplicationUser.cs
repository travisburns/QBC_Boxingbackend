using Microsoft.AspNetCore.Identity;

namespace QBC.Api.Models;

/// <summary>
/// App user. Extends Identity with profile fields and the Square customer id.
/// No card data is ever stored on the user — Square holds cards on file.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Square customer id (created lazily at first checkout). Not sensitive.</summary>
    public string? SquareCustomerId { get; set; }

    /// <summary>
    /// The user's default card-on-file (Square card id) for one-tap repeat
    /// purchases such as day passes. Not sensitive — it's an opaque Square id.
    /// The card itself lives at Square; we keep only the id plus display data.
    /// </summary>
    public string? DefaultSquareCardId { get; set; }
    public string? DefaultCardBrand { get; set; }
    public string? DefaultCardLast4 { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
