using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransitTrackerWebApi.Models;
using TransitTrackerWebApi.Models.Dtos;
using TransitTrackerWebApi.Repositories;

namespace TransitTrackerWebApi.Controllers;

[ApiController]
[Route("api/achievements")]
public class AchievementsController(AppDbContext db) : ControllerBase
{
    [Authorize]
    [HttpGet("unlocked")]
    public async Task<ActionResult<IEnumerable<UserAchievementDto>>> GetUnlocked()
    {
        var userId = GetUserId();
        var unlocked = await db.UserAchievements
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.UnlockedAt)
            .Select(item => new UserAchievementDto(item.AchievementId, item.UnlockedAt))
            .ToListAsync();

        return Ok(unlocked);
    }

    [Authorize]
    [HttpPost("unlocked")]
    public async Task<IActionResult> UnlockAchievements(UnlockAchievementsRequest request)
    {
        if (request.AchievementIds.Count == 0)
        {
            return Ok();
        }

        var userId = GetUserId();
        var existing = await db.UserAchievements
            .Where(item => item.UserId == userId && request.AchievementIds.Contains(item.AchievementId))
            .Select(item => item.AchievementId)
            .ToListAsync();

        var toInsert = request.AchievementIds
            .Except(existing)
            .Distinct()
            .Select(id => new UserAchievement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AchievementId = id,
                UnlockedAt = DateTime.UtcNow
            })
            .ToList();

        if (toInsert.Count == 0)
        {
            return Ok();
        }

        db.UserAchievements.AddRange(toInsert);
        await db.SaveChangesAsync();
        return Ok();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
        {
            throw new InvalidOperationException("User id claim is missing.");
        }
        return userId;
    }
}
