using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QBC.Api.Catalog;
using QBC.Api.Data;
using QBC.Api.Dtos;
using QBC.Api.Models;
using QBC.Api.Services.Square;

namespace QBC.Api.Services;

public interface IDayPassService
{
    Task<DayPassDto> PurchaseAsync(ApplicationUser user, DayPassRequest req, CancellationToken ct);
    Task<IReadOnlyList<DayPassDto>> ListForUserAsync(string userId, CancellationToken ct);
    SavedCardDto GetSavedCard(ApplicationUser user);
}

/// <summary>
/// Orchestrates one-time day-pass purchases: validates the product and reserved
/// date server-side, charges Square once, and records the pass. Reuses the same
/// "never touch the raw card" tokenization the membership flow uses.
/// </summary>
public sealed class DayPassService(
    AppDbContext db,
    ISquareGateway square,
    UserManager<ApplicationUser> users) : IDayPassService
{
    public async Task<DayPassDto> PurchaseAsync(ApplicationUser user, DayPassRequest req, CancellationToken ct)
    {
        var product = DayPassCatalog.Find(req.ProductId)
            ?? throw new MembershipException("Unknown day pass.");

        var visitDate = ParseVisitDate(req.VisitDate);

        // Resolve the payment source. Three ways to pay, one charge:
        //   1) saved card on file  2) a new token we also save  3) a one-off token
        string sourceId;
        string? customerId = user.SquareCustomerId;
        string? cardBrand = null, cardLast4 = null;

        if (req.UseSavedCard)
        {
            if (string.IsNullOrWhiteSpace(user.DefaultSquareCardId) ||
                string.IsNullOrWhiteSpace(customerId))
            {
                throw new MembershipException("You don't have a saved card yet.");
            }
            sourceId = user.DefaultSquareCardId;
            cardBrand = user.DefaultCardBrand;
            cardLast4 = user.DefaultCardLast4;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(req.SourceId))
                throw new MembershipException("A payment method is required.");

            if (req.SaveCard)
            {
                // Store the card on file, then charge the stored card so it's
                // reusable for one-tap purchases later.
                customerId = await square.EnsureCustomerAsync(user, user.SquareCustomerId, ct);
                if (user.SquareCustomerId != customerId)
                {
                    user.SquareCustomerId = customerId;
                    await users.UpdateAsync(user);
                }

                var card = await square.CreateCardOnFileAsync(
                    customerId, req.SourceId, req.IdempotencyKey + ":card", ct);

                user.DefaultSquareCardId = card.Id;
                user.DefaultCardBrand = card.Brand;
                user.DefaultCardLast4 = card.Last4;
                await users.UpdateAsync(user);

                sourceId = card.Id;
                cardBrand = card.Brand;
                cardLast4 = card.Last4;
            }
            else
            {
                // One-off charge against the single-use token; nothing stored.
                sourceId = req.SourceId;
            }
        }

        var payment = await square.CreatePaymentAsync(
            sourceId, customerId, product.PriceCents, product.Currency, req.IdempotencyKey, ct);

        var pass = new DayPass
        {
            UserId = user.Id,
            ProductId = product.Id,
            VisitDate = visitDate,
            PriceCents = product.PriceCents,
            Currency = product.Currency,
            SquarePaymentId = payment.Id,
            SquareCustomerId = customerId,
            CardBrand = payment.CardBrand ?? cardBrand,
            CardLast4 = payment.CardLast4 ?? cardLast4,
            Status = DayPassStatus.Paid,
        };
        db.DayPasses.Add(pass);
        await db.SaveChangesAsync(ct);

        return ToDto(pass, product);
    }

    public async Task<IReadOnlyList<DayPassDto>> ListForUserAsync(string userId, CancellationToken ct)
    {
        var passes = await db.DayPasses.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.VisitDate)
            .ThenByDescending(p => p.CreatedAtUtc)
            .ToListAsync(ct);

        return passes.Select(p => ToDto(p, DayPassCatalog.Find(p.ProductId))).ToList();
    }

    public SavedCardDto GetSavedCard(ApplicationUser user) =>
        new(!string.IsNullOrWhiteSpace(user.DefaultSquareCardId),
            user.DefaultCardBrand, user.DefaultCardLast4);

    /// <summary>Parses and range-checks the reserved date: today .. today + MaxDaysAhead.</summary>
    private static DateOnly ParseVisitDate(string raw)
    {
        if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            throw new MembershipException("Choose a valid date.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (date < today)
            throw new MembershipException("That day has already passed — pick an upcoming day.");
        if (date > today.AddDays(DayPassCatalog.MaxDaysAhead))
            throw new MembershipException(
                $"Day passes can only be booked up to {DayPassCatalog.MaxDaysAhead} days ahead.");

        return date;
    }

    private static DayPassDto ToDto(DayPass p, DayPassProduct? product) =>
        new(p.Id,
            p.ProductId,
            product?.Name ?? p.ProductId,
            p.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            p.PriceCents,
            p.Currency,
            p.Status.ToWire(),
            p.CardBrand,
            p.CardLast4,
            p.CreatedAtUtc,
            p.RedeemedAtUtc);
}
