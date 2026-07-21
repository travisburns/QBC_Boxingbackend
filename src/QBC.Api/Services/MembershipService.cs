using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QBC.Api.Catalog;
using QBC.Api.Data;
using QBC.Api.Domain;
using QBC.Api.Dtos;
using QBC.Api.Options;
using QBC.Api.Services.Square;

namespace QBC.Api.Services;

/// <summary>Thrown for expected, user-facing checkout problems (returns 400/409).</summary>
public sealed class MembershipException(string message) : Exception(message);

public interface IMembershipService
{
    Task<MembershipDto> GetMembershipAsync(string userId, CancellationToken ct);
    Task<CheckoutResult> StartSubscriptionAsync(ApplicationUser user, SubscriptionRequest req, CancellationToken ct);
    Task<CheckoutResult> UpdatePaymentMethodAsync(ApplicationUser user, SubscriptionRequest req, CancellationToken ct);
    Task<MembershipDto> CancelAsync(string userId, CancellationToken ct);
    Task SyncFromSquareAsync(string squareSubscriptionId, CancellationToken ct);
}

public sealed class MembershipService(
    AppDbContext db,
    ISquareGateway square,
    UserManager<ApplicationUser> users,
    IOptions<SquareOptions> squareOptions) : IMembershipService
{
    private readonly SquareOptions _square = squareOptions.Value;

    public async Task<MembershipDto> GetMembershipAsync(string userId, CancellationToken ct)
    {
        var sub = await CurrentAsync(userId, ct);
        return ToDto(sub);
    }

    public async Task<CheckoutResult> StartSubscriptionAsync(
        ApplicationUser user, SubscriptionRequest req, CancellationToken ct)
    {
        var plan = PlanCatalog.Find(req.PlanId)
            ?? throw new MembershipException("Unknown membership plan.");

        if (!_square.PlanVariationIds.TryGetValue(plan.Id, out var variationId) ||
            string.IsNullOrWhiteSpace(variationId))
        {
            throw new MembershipException("This plan isn't available for online signup yet.");
        }

        // Block a duplicate active subscription.
        var existing = await CurrentAsync(user.Id, ct);
        if (existing is { Status: MembershipStatus.Active or MembershipStatus.PastDue })
            throw new MembershipException("You already have an active membership.");

        // 1) Ensure a Square customer for this user.
        var customerId = await square.EnsureCustomerAsync(user, user.SquareCustomerId, ct);
        if (user.SquareCustomerId != customerId)
        {
            user.SquareCustomerId = customerId;
            await users.UpdateAsync(user);
        }

        // 2) Store the card on file from the single-use token.
        var card = await square.CreateCardOnFileAsync(customerId, req.SourceId, req.IdempotencyKey, ct);

        // 3) Start the subscription.
        var result = await square.CreateSubscriptionAsync(
            customerId, variationId, card.Id, Guid.NewGuid().ToString(), ct);

        // 4) Persist our local mirror (Square IDs + status + display card only).
        var sub = existing ?? new MembershipSubscription { UserId = user.Id };
        sub.PlanId = plan.Id;
        sub.SquareCustomerId = customerId;
        sub.SquareCardId = card.Id;
        sub.SquareSubscriptionId = result.Id;
        sub.Status = MembershipStatusExtensions.FromSquare(result.Status);
        sub.CardBrand = card.Brand;
        sub.CardLast4 = card.Last4;
        sub.CurrentPeriodEndUtc = result.ChargedThroughUtc;
        sub.CancelAtPeriodEnd = false;
        sub.UpdatedAtUtc = DateTime.UtcNow;

        if (existing is null) db.Subscriptions.Add(sub);
        await db.SaveChangesAsync(ct);

        return new CheckoutResult(sub.Status.ToWire(), ToDto(sub));
    }

    public async Task<CheckoutResult> UpdatePaymentMethodAsync(
        ApplicationUser user, SubscriptionRequest req, CancellationToken ct)
    {
        var sub = await CurrentAsync(user.Id, ct)
            ?? throw new MembershipException("No membership to update.");
        if (string.IsNullOrWhiteSpace(sub.SquareSubscriptionId) ||
            string.IsNullOrWhiteSpace(sub.SquareCustomerId))
        {
            throw new MembershipException("Your membership isn't set up for card updates.");
        }

        var card = await square.CreateCardOnFileAsync(
            sub.SquareCustomerId, req.SourceId, req.IdempotencyKey, ct);

        await square.UpdateSubscriptionCardAsync(sub.SquareSubscriptionId, card.Id, ct);

        sub.SquareCardId = card.Id;
        sub.CardBrand = card.Brand;
        sub.CardLast4 = card.Last4;
        sub.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new CheckoutResult(sub.Status.ToWire(), ToDto(sub));
    }

    public async Task<MembershipDto> CancelAsync(string userId, CancellationToken ct)
    {
        var sub = await CurrentAsync(userId, ct)
            ?? throw new MembershipException("No membership to cancel.");
        if (string.IsNullOrWhiteSpace(sub.SquareSubscriptionId))
            throw new MembershipException("Your membership isn't active.");

        await square.CancelSubscriptionAsync(sub.SquareSubscriptionId, ct);

        // Square cancels at period end; keep access until then.
        sub.CancelAtPeriodEnd = true;
        sub.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(sub);
    }

    /// <summary>Reconciles our local record with Square (called from the webhook).</summary>
    public async Task SyncFromSquareAsync(string squareSubscriptionId, CancellationToken ct)
    {
        var sub = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.SquareSubscriptionId == squareSubscriptionId, ct);
        if (sub is null) return;

        var remote = await square.GetSubscriptionAsync(squareSubscriptionId, ct);
        if (remote is null) return;

        sub.Status = MembershipStatusExtensions.FromSquare(remote.Status);
        if (remote.ChargedThroughUtc is not null)
            sub.CurrentPeriodEndUtc = remote.ChargedThroughUtc;
        sub.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private Task<MembershipSubscription?> CurrentAsync(string userId, CancellationToken ct) =>
        db.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    private static MembershipDto ToDto(MembershipSubscription? sub)
    {
        if (sub is null)
            return new MembershipDto("none", null, null, null, null, null, false);

        var plan = PlanCatalog.Find(sub.PlanId);
        return new MembershipDto(
            sub.Status.ToWire(),
            sub.PlanId,
            plan?.Name,
            sub.CardBrand,
            sub.CardLast4,
            sub.CurrentPeriodEndUtc,
            sub.CancelAtPeriodEnd);
    }
}
