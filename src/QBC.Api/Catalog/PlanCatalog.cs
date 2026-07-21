namespace QBC.Api.Catalog;

/// <summary>A membership tier. Mirrors the frontend's plan list (lib/plans.ts).</summary>
public sealed record MembershipPlan(
    string Id,
    string Name,
    int PriceCents,
    string Currency,
    string Cycle,
    string Tagline,
    string[] Features,
    bool Featured = false);

/// <summary>
/// Server-side source of truth for plans. Prices are validated here so the
/// client can never dictate an amount. Square plan-variation ids are supplied
/// separately via configuration (SquareOptions.PlanVariationIds).
/// </summary>
public static class PlanCatalog
{
    public static readonly IReadOnlyList<MembershipPlan> Plans = new List<MembershipPlan>
    {
        new("strength", "Strength", 8900, "USD", "monthly",
            "The iron. The platform. Open gym, all yours.",
            ["Full strength & powerlifting floor", "Open gym, 24/7 member access",
             "Programming templates & PR tracking", "Locker room & recovery area"]),
        new("boxing", "Boxing", 9900, "USD", "monthly",
            "Ring, bags, and coaching that hits back.",
            ["Boxing ring & heavy-bag stations", "All boxing & conditioning classes",
             "Wraps, gloves & technique clinics", "Open gym, 24/7 member access"]),
        new("unlimited", "Unlimited", 14900, "USD", "monthly",
            "Everything we do — no limits, no excuses.",
            ["Everything in Strength + Boxing", "Unlimited group classes",
             "Priority class booking", "Guest passes & recovery suite"], Featured: true),
    };

    public static MembershipPlan? Find(string id) =>
        Plans.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
