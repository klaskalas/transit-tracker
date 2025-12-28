using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransitTrackerWebApi.Models;
using TransitTrackerWebApi.Models.Dtos;
using TransitTrackerWebApi.Repositories;
using TransitTrackerWebApi.Services;

namespace TransitTrackerWebApi.Controllers;

[ApiController]
[Route("api/routes")]
public class RoutesController(AppDbContext db, IRoutesService routesService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllRoutes([FromQuery] int offset = 0, [FromQuery] int limit = 25) =>
        Ok(await routesService.GetAllRoutesAsync(offset, limit));

    [HttpGet("{id:int}/shape")]
    public async Task<IActionResult> GetShapes(int id)
    {
        var route = await routesService.GetRouteGeoJsonAsync(id);
        return Ok(route);
    }

    [Authorize]
    [HttpGet("with-progress")]
    public async Task<ActionResult<IEnumerable<TransitLineProgressDto>>> GetRoutesWithProgress(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 25)
    {
        var userId = GetUserId();
        var routes = await routesService.GetAllRoutesAsync(offset, limit);
        var progress = await db.UserRouteProgress
            .Where(entry => entry.UserId == userId)
            .ToListAsync();

        var progressMap = progress.ToDictionary(entry => entry.RouteId);

        var result = routes.Select(route =>
        {
            progressMap.TryGetValue(route.Id, out var entry);
            return new TransitLineProgressDto
            {
                Id = route.Id,
                Agency = route.Agency,
                FeedId = route.FeedId,
                FeedScope = (int)(route.Feed?.Scope ?? DataScope.Unknown),
                ScopeOverride = route.ScopeOverride is null ? null : (int)route.ScopeOverride,
                GtfsRouteId = route.GtfsRouteId,
                ShortName = route.ShortName,
                LongName = route.LongName,
                RouteType = route.RouteType,
                Color = route.Color,
                TextColor = route.TextColor,
                StopCount = route.StopCount,
                LongestTripLengthMeters = route.LongestTripLengthMeters,
                Completed = entry is not null,
                CompletedDate = entry?.CompletedAt
            };
        });

        return Ok(result);
    }

    [Authorize]
    [HttpPost("{id:int}/visited")]
    public async Task<ActionResult<RouteVisitResponse>> ToggleVisited(int id)
    {
        var userId = GetUserId();
        var existing = await db.UserRouteProgress
            .FirstOrDefaultAsync(entry => entry.RouteId == id && entry.UserId == userId);

        if (existing is null)
        {
            var progress = new UserRouteProgress
            {
                RouteId = id,
                UserId = userId,
                CompletedAt = DateTime.UtcNow
            };
            db.UserRouteProgress.Add(progress);
            await db.SaveChangesAsync();
            return Ok(new RouteVisitResponse(id, true, progress.CompletedAt));
        }

        db.UserRouteProgress.Remove(existing);
        await db.SaveChangesAsync();
        return Ok(new RouteVisitResponse(id, false, null));
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
