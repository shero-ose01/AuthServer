using System.Security.Claims;
using GameAuth.Data;
using GameAuth.Models;
using GameAuth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GameAuth.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AppDbContext db, TokenService tokens, IEmailSender email, ILogger<AuthController> logger)
    : ControllerBase
{
    private const string EmailSubject = "Verify your Account";
    private static readonly string HashDummy = BCrypt.Net.BCrypt.HashPassword("dummy");

    [HttpPost("register")][EnableRateLimiting("fixed")][AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req){
        logger.LogInformation("Register attempt from: {Email}", req.Email);

        // check if email or username already taken
        if(await db.Users.AnyAsync(u=>u.Email==req.Email))
            return Conflict(new { message = "Email already in use" });

        if (await db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { message = "Username already taken" });

        // create hash
        string hash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        // set up user
        User user = new User
        {
            Username = req.Username,
            Email = req.Email,
            PasswordHash = hash,
            EmailVerified = false
        };

        // add user to data base
        db.Add(user);

        // create an email verification token
        var verificationTokenUrlEncoded = tokens.GenerateTokenUrl(32);
        var verificationTokenHash = tokens.HashToken(verificationTokenUrlEncoded);

        db.Tokens.Add(new Token {
            User = user,
            Purpose = TokenPurpose.EmailVerification,
            TokenHash = verificationTokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });

        // wait for db update
        await db.SaveChangesAsync();

        // create Email and send it
        // TODO: hardcoded example domain, will change later to prod
        await email.SendAsync(req.Email, EmailSubject, "https://test.domain/verify-email?token="+verificationTokenUrlEncoded);

        return Ok(new {message = "check email"});
    }

    [HttpPost("login")]
    [EnableRateLimiting("fixed")][AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req){
        logger.LogInformation("Login attempt from: {Email}", req.Email);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);

        bool failedAuth = false;

        if(user == null || !user.EmailVerified){
            failedAuth = true;
            BCrypt.Net.BCrypt.Verify("dummy", HashDummy);
        }
        
        if(!failedAuth && !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            failedAuth = true;

        if (failedAuth)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        string token = tokens.GenerateToken(user);
        string refreshToken = tokens.GenerateToken(64);
        string refreshTokenHash = tokens.HashToken(refreshToken);

        Guid familyId = Guid.NewGuid();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserID = user.ID,
            FamilyID = familyId,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });

        await db.SaveChangesAsync();
        return Ok(new AuthResponse(token, refreshToken, user.Username, user.Email));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("fixed")][AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req){
        string hash = tokens.HashToken(req.RefreshToken);
        var stored = await db.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.TokenHash == hash);

        if(stored == null)
            return Unauthorized(new { message = "Invalid refresh token" });

        if(stored.RevokedAt!=null){
            await db.RefreshTokens.Where(r => r.RevokedAt == null && r.FamilyID == stored.FamilyID).ExecuteUpdateAsync(s => s.SetProperty(r=>r.RevokedAt, DateTime.UtcNow));
            logger.LogWarning("Refresh token reused for family {FamilyID}, user {UserID}", stored.FamilyID, stored.UserID);
            return Unauthorized(new { message = "Invalid refresh token" });
        }

        if(stored.ExpiresAt<=DateTime.UtcNow)
            return Unauthorized(new { message = "Invalid refresh token" });

        stored.RevokedAt = DateTime.UtcNow;
        string newRefreshToken = tokens.GenerateToken(64);
        string newRefreshTokenHash = tokens.HashToken(newRefreshToken);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserID = stored.UserID,
            FamilyID = stored.FamilyID,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        await db.SaveChangesAsync();

        string token = tokens.GenerateToken(stored.User);
        return Ok(new AuthResponse(token, newRefreshToken, stored.User.Username, stored.User.Email));
    }

    [HttpPost("logout")]
    [EnableRateLimiting("fixed")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req){
        var hash = tokens.HashToken(req.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (stored != null && stored.IsActive)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(){
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if(!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if(user==null)
            return NotFound();

        return Ok(new { user.ID, user.Username, user.Email, user.CreatedAt });
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req){
        var hash = tokens.HashToken(req.RawToken);
        var stored = await db.Tokens.Include(t=>t.User).FirstOrDefaultAsync(t => t.TokenHash == hash && t.ExpiresAt > DateTime.UtcNow && t.Purpose == TokenPurpose.EmailVerification && t.ConsumedAt == null);

        if(stored == null)
            return Unauthorized();

        stored.User.EmailVerified = true;
        stored.ConsumedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new {message = "Email verified"});
    }
}
