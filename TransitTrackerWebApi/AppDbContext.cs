using Microsoft.EntityFrameworkCore;
using TransitTrackerWebApi.Models;
using Route = TransitTrackerWebApi.Models.Route;

namespace TransitTrackerWebApi;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Route> Routes => Set<Route>();
    public DbSet<UserRouteProgress> UserRouteProgress => Set<UserRouteProgress>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Route>()
            .HasIndex(r => r.ShapeId)
            .IsUnique();

        modelBuilder.Entity<Route>()
            .HasIndex(r => r.Geom)
            .HasMethod("GIST");
    }
}