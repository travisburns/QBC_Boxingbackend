using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QBC.Api.Catalog;
using QBC.Api.Data;
using QBC.Api.Dtos;
using QBC.Api.Models;
using QBC.Api.Options;

namespace QBC.Api.Controllers;

/// <summary>
/// Owner-facing customer CRM. Locked to the Admin role — regular members can
/// never reach these endpoints. Read-only: it surfaces who's a member, their
/// plan, status, and membership history.
/// </summary>
[ApiController]
[Authorize(Roles = AdminOptions.RoleName)]
[Route("api/admin")]
public sealed class AdminController(AppDbContext db) : ControllerBase
{
    [HttpGet("customers")]
    public async Task<ActionResult<CustomerListDto>> Customers(
        [FromQuery] string? search, CancellationToken ct)
    {
        var q = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u =>
                (u.Email != null && u.Email.Contains(s)) ||
                u.FirstName.Contains(s) ||
                u.LastName.Contains(s));
        }

        var users = await q
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName, u.CreatedAtUtc })
            .ToListAsync(ct);

        var ids = users.Select(u => u.Id).ToList();
        var latestByUser = (await db.Subscriptions.AsNoTracking()
                .Where(s => ids.Contains(s.UserId))
                .ToListAsync(ct))
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAtUtc).First());

        var customers = users.Select(u =>
        {
            latestByUser.TryGetValue(u.Id, out var sub);
            return ToSummary(u.Id, u.Email!, u.FirstName, u.LastName, u.CreatedAtUtc, sub);
        }).ToList();

        var active = latestByUser.Values.Count(s => s.Status == MembershipStatus.Active);
        return Ok(new CustomerListDto(users.Count, active, customers));
    }

    [HttpGet("customers/{id}")]
    public async Task<ActionResult<CustomerDetailDto>> Customer(string id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return NotFound();

        var subs = await db.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == id)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);

        var summary = ToSummary(u.Id, u.Email!, u.FirstName, u.LastName, u.CreatedAtUtc, subs.FirstOrDefault());
        var history = subs.Select(ToRecord).ToList();

        return Ok(new CustomerDetailDto(
            u.Id, u.Email!, u.FirstName, u.LastName, u.CreatedAtUtc, u.SquareCustomerId, summary, history));
    }

    /// <summary>
    /// Day passes reserved for a given day (default: today), so the front desk
    /// can see who's coming in and check them off.
    /// </summary>
    [HttpGet("day-passes")]
    public async Task<ActionResult<DayPassCheckInListDto>> DayPasses(
        [FromQuery] string? date, CancellationToken ct)
    {
        var day = ParseDateOrToday(date);

        var rows = await (
            from p in db.DayPasses.AsNoTracking()
            join u in db.Users.AsNoTracking() on p.UserId equals u.Id
            where p.VisitDate == day
            orderby p.CreatedAtUtc
            select new { p, u }).ToListAsync(ct);

        var passes = rows.Select(r => new DayPassCheckInDto(
            r.p.Id,
            r.u.Id,
            $"{r.u.FirstName} {r.u.LastName}".Trim(),
            r.u.Email!,
            DayPassCatalog.Find(r.p.ProductId)?.Name ?? r.p.ProductId,
            r.p.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.p.Status.ToWire(),
            r.p.CreatedAtUtc,
            r.p.RedeemedAtUtc)).ToList();

        var redeemed = passes.Count(p => p.Status == DayPassStatus.Redeemed.ToWire());
        return Ok(new DayPassCheckInListDto(
            day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), passes.Count, redeemed, passes));
    }

    /// <summary>Marks a day pass as redeemed (checked in at the desk).</summary>
    [HttpPost("day-passes/{id:int}/redeem")]
    public async Task<ActionResult<DayPassCheckInDto>> RedeemDayPass(int id, CancellationToken ct)
    {
        var pass = await db.DayPasses.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pass is null) return NotFound();

        if (pass.Status == DayPassStatus.Paid)
        {
            pass.Status = DayPassStatus.Redeemed;
            pass.RedeemedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == pass.UserId, ct);
        return Ok(new DayPassCheckInDto(
            pass.Id,
            pass.UserId,
            user is null ? "" : $"{user.FirstName} {user.LastName}".Trim(),
            user?.Email ?? "",
            DayPassCatalog.Find(pass.ProductId)?.Name ?? pass.ProductId,
            pass.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            pass.Status.ToWire(),
            pass.CreatedAtUtc,
            pass.RedeemedAtUtc));
    }

    private static DateOnly ParseDateOrToday(string? raw) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d)
            ? d
            : DateOnly.FromDateTime(DateTime.UtcNow);

    private static CustomerSummaryDto ToSummary(
        string id, string email, string first, string last, DateTime joined, MembershipSubscription? sub)
    {
        var status = sub?.Status ?? MembershipStatus.None;
        var plan = sub is null ? null : PlanCatalog.Find(sub.PlanId);
        return new CustomerSummaryDto(
            id, email, first, last, joined,
            status.ToWire(), sub?.PlanId, plan?.Name, sub?.CurrentPeriodEndUtc);
    }

    private static MembershipRecordDto ToRecord(MembershipSubscription s)
    {
        var plan = PlanCatalog.Find(s.PlanId);
        return new MembershipRecordDto(
            s.PlanId, plan?.Name, s.Status.ToWire(),
            s.CardBrand, s.CardLast4, s.CurrentPeriodEndUtc, s.CancelAtPeriodEnd,
            s.CreatedAtUtc, s.UpdatedAtUtc);
    }
}
