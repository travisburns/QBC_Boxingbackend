using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QBC.Api.Models;
using QBC.Api.Dtos;
using QBC.Api.Services;

namespace QBC.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ITokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var existing = await users.FindByEmailAsync(req.Email);
        if (existing is not null)
            return Conflict(new { message = "An account with that email already exists." });

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
        };

        var result = await users.CreateAsync(user, req.Password);
        if (!result.Succeeded)
        {
            // Identity messages (e.g. weak password) are safe to surface.
            return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
        }

        return Ok(await BuildAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await users.FindByEmailAsync(req.Email);
        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var check = await signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            var message = check.IsLockedOut
                ? "Account locked due to too many attempts. Try again later."
                : "Invalid email or password.";
            return Unauthorized(new { message });
        }

        return Ok(await BuildAuthResponse(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await users.FindByIdAsync(id);
        if (user is null) return Unauthorized();
        var roles = await users.GetRolesAsync(user);
        return Ok(new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList()));
    }

    private async Task<AuthResponse> BuildAuthResponse(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var (token, expires) = tokens.CreateAccessToken(user, roles);
        return new AuthResponse(token, expires,
            new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList()));
    }
}
