using System.ComponentModel.DataAnnotations;

namespace GameAuth.Models;

public record RegisterRequest(
    [Required, StringLength(32,MinimumLength =3)]string Username,
    [Required, EmailAddress, StringLength(254)]string Email,
    [Required, StringLength(72, MinimumLength = 8)]string Password);

public record LoginRequest(
    [Required, EmailAddress, StringLength(254)]string Email,
    [Required, StringLength(72)]string Password);
public record VerifyEmailRequest([Required, StringLength(64,MinimumLength=3)]string RawToken);
public record AuthResponse(string AccessToken, string RefreshToken, string Username, string Email);
public record RefreshRequest([Required] string RefreshToken);
