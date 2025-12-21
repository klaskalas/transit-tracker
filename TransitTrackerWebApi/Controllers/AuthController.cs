using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransitTrackerWebApi.Models;
using TransitTrackerWebApi.Models.Dtos;
using TransitTrackerWebApi.Repositories;
using TransitTrackerWebApi.Services;

namespace TransitTrackerWebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, JwtTokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await db.Users.FirstOrDefaultAsync(user => user.Email == normalizedEmail);
        if (existingUser is not null)
        {
            return Conflict("Email already in use.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim()
        };

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, new AuthUserResponse(user.Id, user.Email, user.DisplayName)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == normalizedEmail);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return Unauthorized("Invalid email or password.");
        }

        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized("Invalid email or password.");
        }

        var token = tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, new AuthUserResponse(user.Id, user.Email, user.DisplayName)));
    }
}
