using Microsoft.EntityFrameworkCore;
using TransitTrackerWebApi.Models;
using Route = TransitTrackerWebApi.Models.Route;

namespace TransitTrackerWebApi.Repositories;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<ShapeLine> ShapeLines => Set<ShapeLine>();
    public DbSet<Shape> Shapes => Set<Shape>();
    public DbSet<RouteShape> RouteShapes => Set<RouteShape>();
    public DbSet<UserRouteProgress> UserRouteProgress => Set<UserRouteProgress>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserAuthProvider> UserAuthProviders => Set<UserAuthProvider>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RouteShape>()
            .HasKey(rs => new { rs.RouteId, rs.GtfsShapeId });

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<UserAuthProvider>()
            .HasIndex(provider => new { provider.Provider, provider.ProviderUserId })
            .IsUnique();

        modelBuilder.Entity<UserAuthProvider>()
            .HasIndex(provider => new { provider.UserId, provider.Provider })
            .IsUnique();

        // TODO: Fix Route/UserRouteProgress relationship mapping to remove EF shadow FK RouteId1 warning.
    }
}
