using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransitTrackerWebApi.Models;

namespace TransitTrackerWebApi.Controllers;

[ApiController]
[Route("api/routes")]
public class RoutesController : ControllerBase
{
    private readonly AppDbContext _db;

    public RoutesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAllRoutes() =>
        Ok(await _db.Routes.Select(r => new {
            r.Id, r.Name
        }).ToListAsync());

    [HttpGet("{id:guid}/shape")]
    public async Task<IActionResult> GetShape(Guid id)
    {
        var route = await _db.Routes.FindAsync(id);
        return route == null ? NotFound() : Ok(route.GeoJsonShape);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> MarkCompleted(Guid id, [FromQuery] Guid userId)
    {
        var already = await _db.UserRouteProgress
            .AnyAsync(p => p.RouteId == id && p.UserId == userId);

        if (!already)
        {
            _db.UserRouteProgress.Add(new UserRouteProgress {
                RouteId = id,
                UserId = userId,
                CompletedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return Ok();
    }
}
