using Microsoft.EntityFrameworkCore;
using Route = TransitTrackerWebApi.Models.Route;

namespace TransitTrackerWebApi.Repositories;

public class RoutesRepository
{
    private readonly AppDbContext _context;

    public RoutesRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Route>> GetAllRoutesAsync()
    {
        return await _context.Routes.Take(25).ToListAsync();
    }

    public async Task<Route?> GetRouteByIdAsync(int id)
    {
        return await _context.Routes.FindAsync(id);
    }

    public async Task<Route> AddRouteAsync(Route route)
    {
        _context.Routes.Add(route);
        await _context.SaveChangesAsync();
        return route;
    }

    public async Task<bool> UpdateRouteAsync(Route route)
    {
        _context.Routes.Update(route);
        var affected = await _context.SaveChangesAsync();
        return affected > 0;
    }

    public async Task<bool> DeleteRouteAsync(int id)
    {
        var route = await GetRouteByIdAsync(id);
        if (route == null) return false;
        
        _context.Routes.Remove(route);
        var affected = await _context.SaveChangesAsync();
        return affected > 0;
    }
}