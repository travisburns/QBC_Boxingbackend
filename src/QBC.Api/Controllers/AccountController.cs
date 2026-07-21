using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QBC.Api.Domain;
using QBC.Api.Dtos;
using QBC.Api.Services;
using QBC.Api.Services.Square;

namespace QBC.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController(
    IMembershipService memberships,
    UserManager<ApplicationUser> users,
    ILogger<AccountController> logger) : ControllerBase
{
    [HttpGet("membership")]
    public async Task<ActionResult<MembershipDto>> GetMembership(CancellationToken ct)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(await memberships.GetMembershipAsync(user.Id, ct));
    }

    [HttpPost("membership/cancel")]
    public async Task<ActionResult<MembershipDto>> Cancel(CancellationToken ct)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        try
        {
            return Ok(await memberships.CancelAsync(user.Id, ct));
        }
        catch (MembershipException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SquareApiException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    [HttpPost("payment-method")]
    public async Task<ActionResult<CheckoutResult>> UpdatePaymentMethod(
        SubscriptionRequest req, CancellationToken ct)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        try
        {
            return Ok(await memberships.UpdatePaymentMethodAsync(user, req, ct));
        }
        catch (MembershipException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SquareApiException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update payment method for {UserId}", user.Id);
            return StatusCode(500, new { message = "Could not update your card. Please try again." });
        }
    }
}
