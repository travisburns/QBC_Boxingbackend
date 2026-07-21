namespace QBC.Api.Domain;

/// <summary>Local view of a member's subscription state, kept in sync with Square via webhooks.</summary>
public enum MembershipStatus
{
    None = 0,
    Pending = 1,
    Active = 2,
    PastDue = 3,
    Canceled = 4,
    Paused = 5,
}

public static class MembershipStatusExtensions
{
    /// <summary>Wire format expected by the frontend (snake_case).</summary>
    public static string ToWire(this MembershipStatus status) => status switch
    {
        MembershipStatus.Active => "active",
        MembershipStatus.Pending => "pending",
        MembershipStatus.PastDue => "past_due",
        MembershipStatus.Canceled => "canceled",
        MembershipStatus.Paused => "paused",
        _ => "none",
    };

    /// <summary>Maps a Square subscription status string to our local enum.</summary>
    public static MembershipStatus FromSquare(string? squareStatus) => squareStatus?.ToUpperInvariant() switch
    {
        "ACTIVE" => MembershipStatus.Active,
        "PENDING" => MembershipStatus.Pending,
        "PAUSED" => MembershipStatus.Paused,
        "DEACTIVATED" => MembershipStatus.PastDue,
        "CANCELED" => MembershipStatus.Canceled,
        _ => MembershipStatus.Pending,
    };
}
