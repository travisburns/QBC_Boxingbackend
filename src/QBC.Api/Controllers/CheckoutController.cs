using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QBC.Api.Models;
using QBC.Api.Dtos;
using QBC.Api.Services;
using QBC.Api.Services.Square;

namespace QBC.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/checkout")]
public sealed class CheckoutController(
    IMembershipService memberships,
    UserManager<ApplicationUser> users,
    ILogger<CheckoutController> logger) : ControllerBase
{
    [HttpPost("subscription")]
    public async Task<ActionResult<CheckoutResult>> StartSubscription(
        SubscriptionRequest req, CancellationToken ct)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        try
        {
            var result = await memberships.StartSubscriptionAsync(user, req, ct);
            return Ok(result);
        }
        catch (MembershipException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SquareApiException ex)
        {
            // Card declined / provider rejection — safe, specific message.
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected checkout error for user {UserId}",
                User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, new { message = "Something went wrong. You were not charged." });
        }
    }
}
