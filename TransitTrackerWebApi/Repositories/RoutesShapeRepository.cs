using Microsoft.EntityFrameworkCore;
using TransitTrackerWebApi.Models;
using Route = TransitTrackerWebApi.Models.Route;

namespace TransitTrackerWebApi.Repositories;

public class RoutesShapeRepository
{
    private readonly AppDbContext _context;

    public RoutesShapeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RouteShape?> GetRouteShapeByRouteIdAsync(int id)
    {
        return await _context.RouteShapes.FindAsync(id);
    }

}