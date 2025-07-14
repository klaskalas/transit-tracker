using Microsoft.EntityFrameworkCore;
using Route = TransitTrackerWebApi.Models.Route;
using NetTopologySuite.Features;
using NetTopologySuite.IO;

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
    
    public async Task<string> GetRouteGeoJsonFeatureCollectionAsync(int routeId)
    {
        var geometries = await (
            from rs in _context.RouteShapes
            join sl in _context.ShapeLines on rs.GtfsShapeId equals sl.GtfsShapeId
            where rs.RouteId == routeId
            select sl.Geom
        ).ToListAsync();

        var features = geometries.Select(geom => new Feature(geom, new AttributesTable())).ToList();
        var featureCollection = new FeatureCollection(features);

        var writer = new GeoJsonWriter();
        return writer.Write(featureCollection);
    }
}