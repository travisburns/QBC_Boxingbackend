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
