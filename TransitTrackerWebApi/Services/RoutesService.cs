using TransitTrackerWebApi.Repositories;
using Route = TransitTrackerWebApi.Models.Route;

namespace TransitTrackerWebApi.Services;

public interface IRoutesService
{
    Task<IEnumerable<Route>> GetAllRoutesAsync();
    Task<Route?> GetRouteByIdAsync(int id);
    Task<Route> AddRouteAsync(Route route);
    Task<bool> UpdateRouteAsync(Route route);
    Task<bool> DeleteRouteAsync(int id);
    Task<string> GetRouteGeoJsonAsync(int routeId);
}


public class RoutesService(RoutesRepository routesRepository) : IRoutesService
{
    public async Task<IEnumerable<Route>> GetAllRoutesAsync()
    {
        return await routesRepository.GetAllRoutesAsync();
    }

    public async Task<Route?> GetRouteByIdAsync(int id)
    {
        return await routesRepository.GetRouteByIdAsync(id);
    }

    public async Task<Route> AddRouteAsync(Route route)
    {
        // Add validation logic here if needed
        if (string.IsNullOrEmpty(route.ShortName))
            throw new ArgumentException("Route name cannot be empty");

        return await routesRepository.AddRouteAsync(route);
    }

    public async Task<bool> UpdateRouteAsync(Route route)
    {
        // Add validation logic here if needed
        if (string.IsNullOrEmpty(route.ShortName))
            throw new ArgumentException("Route name cannot be empty");

        var existingRoute = await routesRepository.GetRouteByIdAsync(route.Id);
        if (existingRoute == null)
            return false;

        return await routesRepository.UpdateRouteAsync(route);
    }

    public async Task<bool> DeleteRouteAsync(int id)
    {
        return await routesRepository.DeleteRouteAsync(id);
    }

    public async Task<string> GetRouteGeoJsonAsync(int routeId)
    {
        return await routesRepository.GetRouteGeoJsonFeatureCollectionAsync(routeId);
    }
}
