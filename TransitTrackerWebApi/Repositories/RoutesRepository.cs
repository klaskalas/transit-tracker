using Microsoft.EntityFrameworkCore;
using Route = TransitTrackerWebApi.Models.Route;
using NetTopologySuite.Features;
using NetTopologySuite.IO;

namespace TransitTrackerWebApi.Repositories;

public class RoutesRepository(AppDbContext context)
{
    public async Task<List<Route>> GetAllRoutesAsync(int offset, int limit)
    {
        var safeOffset = Math.Max(0, offset);
        var safeLimit = Math.Max(0, limit);

        var query = context.Routes
            .Include(a => a.Agency)
            .Include(r => r.Feed)
            .OrderBy(r => r.Id)
            .AsQueryable();

        if (safeOffset > 0)
        {
            query = query.Skip(safeOffset);
        }

        if (safeLimit > 0)
        {
            query = query.Take(safeLimit);
        }

        return await query.ToListAsync();
    }

    public async Task<Route?> GetRouteByIdAsync(int id)
    {
        return await context.Routes.FindAsync(id);
    }

    public async Task<Route> AddRouteAsync(Route route)
    {
        context.Routes.Add(route);
        await context.SaveChangesAsync();
        return route;
    }

    public async Task<bool> UpdateRouteAsync(Route route)
    {
        context.Routes.Update(route);
        var affected = await context.SaveChangesAsync();
        return affected > 0;
    }

    public async Task<bool> DeleteRouteAsync(int id)
    {
        var route = await GetRouteByIdAsync(id);
        if (route == null) return false;
        
        context.Routes.Remove(route);
        var affected = await context.SaveChangesAsync();
        return affected > 0;
    }
    
    public async Task<string> GetRouteGeoJsonFeatureCollectionAsync(int routeId)
    {
        var geometries = await (
            from rs in context.RouteShapes
            join sl in context.ShapeLines on rs.GtfsShapeId equals sl.GtfsShapeId
            where rs.RouteId == routeId
            select sl.Geom
        ).ToListAsync();

        var features = geometries.Select(geom => new Feature(geom, new AttributesTable())).ToList();
        var featureCollection = new FeatureCollection(features);

        var writer = new GeoJsonWriter();
        return writer.Write(featureCollection);
    }
}
