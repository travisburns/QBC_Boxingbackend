namespace QBC.Api.Models;

/// <summary>Lifecycle of a one-time day pass (distinct from a recurring membership).</summary>
public enum DayPassStatus
{
    /// <summary>Paid for, not yet used.</summary>
    Paid = 0,
    /// <summary>Checked in at the front desk on the visit day.</summary>
    Redeemed = 1,
    /// <summary>Refunded / voided.</summary>
    Refunded = 2,
}

public static class DayPassStatusExtensions
{
    /// <summary>Wire format expected by the frontend (snake_case-ish lower).</summary>
    public static string ToWire(this DayPassStatus status) => status switch
    {
        DayPassStatus.Paid => "paid",
        DayPassStatus.Redeemed => "redeemed",
        DayPassStatus.Refunded => "refunded",
        _ => "paid",
    };
}
