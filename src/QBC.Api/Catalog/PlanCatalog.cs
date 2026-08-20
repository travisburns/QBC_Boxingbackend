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
        new("membership", "Membership", 12000, "USD", "monthly",
            "Full access. Train on your schedule.",
            ["Unlimited gym & floor access", "All classes included",
             "Open gym hours", "No contract — cancel anytime"], Featured: true),
    };

    public static MembershipPlan? Find(string id) =>
        Plans.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
