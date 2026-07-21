using Microsoft.AspNetCore.Identity;

namespace QBC.Api.Domain;

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

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
