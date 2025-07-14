using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransitTrackerWebApi.Models;
using TransitTrackerWebApi.Repositories;
using TransitTrackerWebApi.Services;

namespace TransitTrackerWebApi.Controllers;

[ApiController]
[Route("api/routes")]
public class RoutesController(AppDbContext db, IRoutesService routesService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllRoutes() =>
        Ok(await routesService.GetAllRoutesAsync());

    [HttpGet("{id:int}/shape")]
    public async Task<IActionResult> GetShapes(int id)
    {
        var route = await routesService.GetRouteGeoJsonAsync(id);
        return Ok(route);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> MarkCompleted(Guid id, [FromQuery] Guid userId)
    {
        var already = await db.UserRouteProgress
            .AnyAsync(p => p.RouteId == id && p.UserId == userId);

        if (already) return Ok();
        
        db.UserRouteProgress.Add(new UserRouteProgress {
            RouteId = id,
            UserId = userId,
            CompletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return Ok();
    }
}
