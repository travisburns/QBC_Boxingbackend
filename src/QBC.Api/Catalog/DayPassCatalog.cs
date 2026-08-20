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

    public static readonly IReadOnlyList<DayPassProduct> Products = new List<DayPassProduct>
    {
        new("drop-in", "Drop-In", 2500, "USD",
            "One visit — open gym + floor access for the day."),
        new("kids-class", "Kids Class", 2500, "USD",
            "One kids class. Reserve the day you're coming in."),
        new("session", "Session", 3500, "USD",
            "One coached session — held Mon, Tue, Thu & Sat."),
    };

    public static DayPassProduct? Find(string id) =>
        Products.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
