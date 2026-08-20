namespace QBC.Api.Catalog;

/// <summary>A one-time purchasable pass. Mirrors the frontend's day-pass list (lib/dayPasses.ts).</summary>
public sealed record DayPassProduct(
    string Id,
    string Name,
    int PriceCents,
    string Currency,
    string Description);

/// <summary>
/// Server-side source of truth for one-time day-pass prices. The client sends a
/// product <c>id</c> and a date — never an amount — so a tampered request can
/// never change what is charged (same guarantee as <see cref="PlanCatalog"/>).
/// </summary>
public static class DayPassCatalog
{
    /// <summary>
    /// How far ahead a pass may be reserved. A pass may be booked for today
    /// through today + this many days (inclusive).
    /// </summary>
    public const int MaxDaysAhead = 7;

    // TODO(owner): confirm the real drop-in price. $20.00 is a placeholder.
    public static readonly IReadOnlyList<DayPassProduct> Products = new List<DayPassProduct>
    {
        new("day-pass", "Day Pass", 2000, "USD",
            "One full day of open gym + floor access. Reserve any day within the next week."),
    };

    public static DayPassProduct? Find(string id) =>
        Products.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
