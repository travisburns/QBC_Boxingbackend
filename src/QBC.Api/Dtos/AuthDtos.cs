using System.ComponentModel.DataAnnotations;

namespace QBC.Api.Dtos;

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string LastName { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed record UserDto(string Id, string Email, string FirstName, string LastName);

public sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, UserDto User);
